using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    [CustomPropertyDrawer(typeof(PlatformSpecificInfo))]
    public class PlatformSpecificInfoDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();

            // Use IMGUI for now to preserve exact behavior
            var imguiContainer = new IMGUIContainer(() =>
            {
                DrawPlatformSpecificInfo(property);
            });
            
            container.Add(imguiContainer);
            return container;
        }

        private void DrawPlatformSpecificInfo(SerializedProperty infoProp)
        {
            // props
            var enabled = infoProp.FindPropertyRelative(nameof(PlatformSpecificInfo.enabled));
            var updateImage = infoProp.FindPropertyRelative(nameof(PlatformSpecificInfo.updateImage));
            var imageTakeEditorMode = infoProp.FindPropertyRelative(nameof(PlatformSpecificInfo.imageTakeEditorMode));
            var versioningEnabled = infoProp.FindPropertyRelative(nameof(PlatformSpecificInfo.versioningEnabled));
            var versionNamePrefix = infoProp.FindPropertyRelative(nameof(PlatformSpecificInfo.versionNamePrefix));
            var gitEnabled = infoProp.FindPropertyRelative(nameof(PlatformSpecificInfo.gitEnabled));
            var tagPrefix = infoProp.FindPropertyRelative(nameof(PlatformSpecificInfo.tagPrefix));
            var tagSuffix = infoProp.FindPropertyRelative(nameof(PlatformSpecificInfo.tagSuffix));

            ExEditorGUILayout.ToggleLeft(enabled, new GUIContent(infoProp.displayName));

            ToggleScope(enabled, () =>
            {
                ExEditorGUILayout.ToggleLeft(updateImage, Labels.UpdateImage);
                ToggleScope(updateImage, () =>
                {
                    EditorGUILayout.PropertyField(imageTakeEditorMode, Labels.TakeImageIn);
                });
                ExEditorGUILayout.ToggleLeft(versioningEnabled, Labels.VersioningSystem);
                ToggleScope(versioningEnabled, () =>
                {
                    EditorGUILayout.PropertyField(versionNamePrefix, Labels.VersionNamePrefix);
                    if (!versionNamePrefix.hasMultipleDifferentValues)
                        EditorGUILayout.LabelField($"'({versionNamePrefix.stringValue}<version>)'will be added in avatar description");
                    ExEditorGUILayout.ToggleLeft(gitEnabled, Labels.GitTagging);
                    ToggleScope(gitEnabled, () =>
                    {
                        EditorGUILayout.PropertyField(tagPrefix, Labels.TagPrefix);
                        EditorGUILayout.PropertyField(tagSuffix, Labels.TagSuffix);
                        if (!tagPrefix.hasMultipleDifferentValues && !tagSuffix.hasMultipleDifferentValues)
                            EditorGUILayout.LabelField($"tag name will be '{tagPrefix.stringValue}<version>{tagSuffix.stringValue}'");
                    });
                });
            });
        }

        private void ToggleScope(SerializedProperty prop, Action scope)
        {
            if (prop.boolValue || prop.hasMultipleDifferentValues)
            {
                EditorGUI.BeginDisabledGroup(prop.hasMultipleDifferentValues);
                EditorGUI.indentLevel++;
                scope();
                EditorGUI.indentLevel--;
                EditorGUI.EndDisabledGroup();
            }
        }

        static class Labels
        {
            public static readonly GUIContent UpdateImage = new("Update Image on Upload");
            public static readonly GUIContent TakeImageIn = new("Take Image In");
            public static readonly GUIContent VersioningSystem = new("Versioning System");
            public static readonly GUIContent VersionNamePrefix = new("Version Prefix");
            public static readonly GUIContent GitTagging = new("git tagging");
            public static readonly GUIContent TagPrefix = new("Tag Prefix");
            public static readonly GUIContent TagSuffix = new("Tag Suffix");
        }
    }
}
