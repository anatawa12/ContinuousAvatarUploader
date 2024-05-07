using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using Debug = System.Diagnostics.Debug;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    [CustomEditor(typeof(AvatarUploadSettingGroup))]
    public class AvatarUploadSettingGroupEditor : UnityEditor.Editor
    {
        private AvatarUploadSettingGroup _asset;

        public override VisualElement CreateInspectorGUI()
        {
            _asset = (AvatarUploadSettingGroup)target;

            var root = new VisualElement()
            {
                name = "RootElement"
            };
            var inspectors = new VisualElement()
            {
                name = "Inspectors"
            };

            foreach (var assetAvatar in _asset.avatars)
                inspectors.Add(CreateDescriptorInspector(assetAvatar));

            VRCAvatarDescriptor avatarDescriptor = null;
            var trailer = new IMGUIContainer(() =>
            {
                avatarDescriptor = EditorGUILayout.ObjectField("Avatar to Add", avatarDescriptor,
                    typeof(VRCAvatarDescriptor), true) as VRCAvatarDescriptor;

                EditorGUI.BeginDisabledGroup(!avatarDescriptor);
                if (GUILayout.Button("Add Avatar"))
                {
                    Debug.Assert(avatarDescriptor != null, nameof(avatarDescriptor) + " != null");
                    var newObj = ScriptableObject.CreateInstance<AvatarUploadSetting>();
                    newObj.avatarDescriptor = new MaySceneReference(avatarDescriptor);
                    newObj.name = newObj.avatarName = avatarDescriptor.gameObject.name;

                    ArrayUtility.Add(ref _asset.avatars, newObj);
                    EditorUtility.SetDirty(_asset);
                    AssetDatabase.AddObjectToAsset(newObj, _asset);
                    AssetDatabase.SaveAssetIfDirty(newObj);
                    inspectors.Add(CreateDescriptorInspector(newObj));
                    avatarDescriptor = null;
                }

                EditorGUI.EndDisabledGroup();
            });

            root.Add(inspectors);
            root.Add(trailer);

            return root;
        }

        private VisualElement CreateDescriptorInspector(AvatarUploadSetting descriptor)
        {
            var container = new VisualElement();
            container.Add(new InspectorElement(descriptor));
            container.Add(new IMGUIContainer(() =>
            {
                GUILayout.BeginHorizontal();
                EditorGUI.BeginDisabledGroup(_asset.avatars[0] == descriptor);
                if (GUILayout.Button("▲", EditorStyles.miniButton, GUILayout.Width(30)))
                {
                    var index = System.Array.IndexOf(_asset.avatars, descriptor);
                    Debug.Assert(index != -1, nameof(index) + " != -1");
                    var temp = _asset.avatars[index - 1];
                    _asset.avatars[index - 1] = descriptor;
                    _asset.avatars[index] = temp;
                    var parent = container.parent;
                    parent.RemoveAt(index);
                    parent.Insert(index - 1, container);
                    EditorUtility.SetDirty(_asset);
                }
                EditorGUI.EndDisabledGroup();
                if (GUILayout.Button("Remove Avatar"))
                {
                    container.parent.Remove(container);
                    ArrayUtility.Remove(ref _asset.avatars, descriptor);
                    EditorUtility.SetDirty(_asset);
                    DestroyImmediate(descriptor, true);
                    AssetDatabase.SaveAssetIfDirty(_asset);
                }
                EditorGUI.BeginDisabledGroup(_asset.avatars[_asset.avatars.Length - 1] == descriptor);
                if (GUILayout.Button("▼", EditorStyles.miniButton, GUILayout.Width(30)))
                {
                    var index = System.Array.IndexOf(_asset.avatars, descriptor);
                    Debug.Assert(index != -1, nameof(index) + " != -1");
                    var temp = _asset.avatars[index + 1];
                    _asset.avatars[index + 1] = descriptor;
                    _asset.avatars[index] = temp;
                    var parent = container.parent;
                    parent.RemoveAt(index);
                    parent.Insert(index + 1, container);
                    EditorUtility.SetDirty(_asset);
                }
                GUILayout.EndHorizontal();
                HorizontalLine();
            }));
            return container;
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
