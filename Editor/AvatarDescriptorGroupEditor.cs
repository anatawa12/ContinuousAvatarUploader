using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using Debug = System.Diagnostics.Debug;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    [CustomEditor(typeof(AvatarDescriptorGroup))]
    public class AvatarDescriptorGroupEditor : UnityEditor.Editor
    {
        private AvatarDescriptorGroup _asset;

        public override VisualElement CreateInspectorGUI()
        {
            _asset = (AvatarDescriptorGroup)target;

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
            bool enabled = true;
            var trailer = new IMGUIContainer(() =>
            {
                EditorGUI.BeginChangeCheck();
                avatarDescriptor = EditorGUILayout.ObjectField("Avatar to Add", avatarDescriptor,
                    typeof(VRCAvatarDescriptor), true) as VRCAvatarDescriptor;
                if (EditorGUI.EndChangeCheck())
                {
                    var id = GlobalObjectId.GetGlobalObjectIdSlow(avatarDescriptor);
                    enabled = id.identifierType == 2;
                }

                EditorGUI.BeginDisabledGroup(!avatarDescriptor || !enabled);
                if (GUILayout.Button("Add Avatar"))
                {
                    Debug.Assert(avatarDescriptor != null, nameof(avatarDescriptor) + " != null");
                    var newObj = ScriptableObject.CreateInstance<AvatarDescriptor>();
                    newObj.avatarDescriptor = new SceneReference(avatarDescriptor);
                    newObj.name = newObj.avatarName = avatarDescriptor.gameObject.name;

                    ArrayUtility.Add(ref _asset.avatars, newObj);
                    EditorUtility.SetDirty(_asset);
                    AssetDatabase.AddObjectToAsset(newObj, _asset);
                    inspectors.Add(CreateDescriptorInspector(newObj));
                }

                EditorGUI.EndDisabledGroup();
            });

            root.Add(inspectors);
            root.Add(trailer);

            return root;
        }

        private VisualElement CreateDescriptorInspector(AvatarDescriptor descriptor)
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
