using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    internal static class ContextMenus
    {
        const string CreateMenuBasePath = "Assets/Create/Continuous Avatar Uploader/";
        const string GroupFromVariantsMenuPath = CreateMenuBasePath + "Group from Prefab Variants of the Prefab";

        [MenuItem(GroupFromVariantsMenuPath)]
        private static void CreateAvatarUploadSettingGroupFromPrefabVariants() =>
            CreateFromDescriptors(() =>
            {
                var roots = new HashSet<VRCAvatarDescriptor>(GetSelectedAvatarDescriptors());

                return AssetDatabase.FindAssets("t:Prefab")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                    .Where(go => go)
                    .Select(go => go.GetComponent<VRCAvatarDescriptor>())
                    .Where(descriptor => descriptor)
                    .Where(descriptor => GetCorrespondingObjects(descriptor).Any(roots.Contains))
                    .ToArray();
            });

        private static IEnumerable<VRCAvatarDescriptor> GetCorrespondingObjects(VRCAvatarDescriptor descriptor)
        {
            while (true)
            {
                var prefab = PrefabUtility.GetCorrespondingObjectFromSource(descriptor);
                if (prefab == null || prefab == descriptor) yield break;
                yield return prefab;
                descriptor = prefab;
            }
        }

        [MenuItem(GroupFromVariantsMenuPath, true)]
        private static bool ValidateCreateAvatarUploadSettingGroupFromPrefabVariants() => SelectionAreAvatarPrefabs();

        const string CreateFromSelection = CreateMenuBasePath + "Group from Selection";
        const string CreateFromSelectionInGameObject = "GameObject/Continuous Avatar Uploader/Group from Selection";

        [MenuItem(CreateFromSelection)]
        [MenuItem(CreateFromSelectionInGameObject)]
        private static void CreateAvatarUploadSettingGroupFromSelection() =>
            OnceFilter(() => CreateFromDescriptors(() => GetSelectedAvatarDescriptors().ToArray()));

        [MenuItem(CreateFromSelection, true)]
        [MenuItem(CreateFromSelectionInGameObject, true)]
        private static bool ValidateCreateAvatarUploadSettingGroupFromSelection()
        {
            return SelectionAreAvatarDescriptors();
        }

        // for GameObject/ menu, the handler will be called multiple times (selection count times)
        //   so we need to filter it
        // https://issuetracker.unity3d.com/issues/menuitem-is-executed-more-than-once-when-multiple-objects-are-selected
        private static bool _called;
        private static void OnceFilter(Action action)
        {
            if (_called) return;
            action();
            _called = true;
            EditorApplication.delayCall += () => _called = false;
        }

        private static void CreateFromDescriptors(
            Func<VRCAvatarDescriptor[]> descriptors
        )
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Save Avatar Upload Setting Group",
                "New Avatar Upload Setting Group",
                "asset",
                "Save Avatar Upload Setting Group");
            if (string.IsNullOrEmpty(path)) return;

            var roots = descriptors();

            var group = ScriptableObject.CreateInstance<AvatarUploadSettingGroup>();
            group.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(group, path);
            group.avatars = roots
                .Select(descriptor =>
                {
                    var newObj = ScriptableObject.CreateInstance<AvatarUploadSetting>();
                    newObj.avatarDescriptor = new MaySceneReference(descriptor);
                    newObj.name = newObj.avatarName = descriptor.gameObject.name;
                    return newObj;
                })
                .ToArray();
            EditorUtility.SetDirty(group);
            foreach (var avatarUploadSetting in group.avatars)
                AssetDatabase.AddObjectToAsset(avatarUploadSetting, group);
            AssetDatabase.SaveAssetIfDirty(group);
            EditorGUIUtility.PingObject(group);
        }

        private static bool SelectionAreAvatarPrefabs() =>
            Selection.objects.All(activeObject =>
                activeObject is GameObject asGameObject
                && asGameObject.GetComponent<VRCAvatarDescriptor>()
                && EditorUtility.IsPersistent(activeObject));
        
        private static bool SelectionAreAvatarDescriptors() =>
            Selection.objects.All(activeObject =>
                activeObject is GameObject asGameObject
                && asGameObject.GetComponent<VRCAvatarDescriptor>());

        private static IEnumerable<VRCAvatarDescriptor> GetSelectedAvatarDescriptors() =>
            Selection.objects
                .Select(obj => obj as GameObject)
                .Where(go => go)
                .Select(go => go.GetComponent<VRCAvatarDescriptor>())
                .Where(descriptor => descriptor);
    }
}
