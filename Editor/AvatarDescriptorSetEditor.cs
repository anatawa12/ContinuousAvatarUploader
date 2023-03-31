using System;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    [CustomEditor(typeof(AvatarDescriptorSet))]
    public class AvatarDescriptorSetEditor : UnityEditor.Editor
    {
        private bool tagsDefinedOpened = true;
        private Vector2 positoon = new Vector2();

        public override void OnInspectorGUI()
        {
            var asset = (AvatarDescriptorSet)target;
            EditorGUI.BeginChangeCheck();

            tagsDefinedOpened = EditorGUILayout.Foldout(tagsDefinedOpened, "Defined Tags");
            if (tagsDefinedOpened)
            {
                using (new EditorGUI.IndentLevelScope())
                    DefinedTagsList(asset);
            }

            GUILayout.Label("Avatars");

            HorizontalLine();

            positoon = EditorGUILayout.BeginScrollView(positoon);
            AvatarDescriptors(asset);
            EditorGUILayout.EndScrollView();

            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(asset);
        }

        private string tagNamePrompt;

        private void DefinedTagsList(AvatarDescriptorSet avatarDescriptorSet)
        {
            var removeButton = new GUIContent("x", "remove tag");
            for (var i = 0; i < avatarDescriptorSet.definedTags.Length; i++)
            {
                var rect = EditorGUILayout.GetControlRect(true);
                rect.width -= EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                EditorGUI.LabelField(rect, $"Element {i}", avatarDescriptorSet.definedTags[i]);
                rect.x += rect.width + EditorGUIUtility.standardVerticalSpacing;
                rect.width = EditorGUIUtility.singleLineHeight;

                if (GUI.Button(rect, removeButton))
                {
                    ArrayUtility.RemoveAt(ref avatarDescriptorSet.definedTags, i);
                    return;
                }
            }

            tagNamePrompt = EditorGUILayout.TextField($"Tag to Add", tagNamePrompt);

            if (GUI.Button(EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(false)), "Add Tag"))
            {
                ArrayUtility.Add(ref avatarDescriptorSet.definedTags, tagNamePrompt);
                tagNamePrompt = null;
                return;
            }
        }

        private AvatarToAddToastElement avatarToAddToastElement;
        
        private void AvatarDescriptors(AvatarDescriptorSet asset)
        {
            for (var i = 0; i < asset.avatars.Length; i++)
            {
                var avatar = asset.avatars[i];
                EditorGUILayout.LabelField("Avatar", avatar.name);
                EditorGUILayout.ObjectField("In scene", avatar.avatarDescriptor.scene, typeof(SceneAsset), false);
                PlatformSpecificInfo("PC Windows", avatar.windows);
                PlatformSpecificInfo("Quest", avatar.quest);
                HorizontalLine();
            }
            // TODO: list first


            var toAdd = EditorGUILayout.ObjectField("Avatar to Add", null, typeof(VRCAvatarDescriptor), true);
            if (toAdd)
            {
                avatarToAddToastElement = AvatarToAddToastElement.None;

                var id = GlobalObjectId.GetGlobalObjectIdSlow(toAdd);

                if (id.identifierType != 2)
                {
                    avatarToAddToastElement = AvatarToAddToastElement.NonSceneElement;
                    return;
                }

                ArrayUtility.Add(ref asset.avatars, new AvatarDescriptor()
                {
                    avatarDescriptor = new SceneReference(toAdd),
                    name = ((VRCAvatarDescriptor)toAdd).gameObject.name,
                    quest =
                    {
                        enabled = true,
                        branch = "",
                        tagPrefix = "",
                        tagSuffix = "",
                        versionNamePrefix = "q",
                    },
                    windows =
                    {
                        enabled = true,
                        branch = "",
                        tagPrefix = "",
                        tagSuffix = "",
                        versionNamePrefix = "v",
                    },
                });
            }

            switch (avatarToAddToastElement)
            {
                case AvatarToAddToastElement.None:
                    break;
                case AvatarToAddToastElement.NonSceneElement:
                    EditorGUILayout.HelpBox("The avatar is not on the scene", MessageType.Error);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        enum AvatarToAddToastElement
        {
            None,
            NonSceneElement,
        }

        private void PlatformSpecificInfo(string name, PlatformSpecificInfo info)
        {
            info.enabled = EditorGUILayout.ToggleLeft(name, info.enabled);

            if (info.enabled)
            {
                info.branch = EditorGUILayout.TextField("Branch", info.branch);
                info.tagPrefix = EditorGUILayout.TextField("Tag Prefix", info.tagPrefix);
                info.tagSuffix = EditorGUILayout.TextField("Tag Suffix", info.tagSuffix);
                info.versionNamePrefix = EditorGUILayout.TextField("Version Prefix", info.versionNamePrefix);
            }
        }

        private static void HorizontalLine(float regionHeight = 18f, float lineHeight = 1f)
        {
            var rect = GUILayoutUtility.GetRect(EditorGUIUtility.fieldWidth, float.MaxValue, regionHeight, regionHeight);
            rect.y += (rect.height - lineHeight) / 2;
            rect.height = lineHeight;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
        }
    }
}
