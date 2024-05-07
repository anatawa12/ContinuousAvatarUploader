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
                if (GUILayout.Button("Remove Avatar"))
                {
                    container.parent.Remove(container);
                    ArrayUtility.Remove(ref _asset.avatars, descriptor);
                    EditorUtility.SetDirty(_asset);
                    DestroyImmediate(descriptor, true);
                }
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
