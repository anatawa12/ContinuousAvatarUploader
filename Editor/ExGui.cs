#nullable enable

using UnityEditor;
using UnityEngine;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    internal static class ExEditorGUILayout
    {
        public static void ToggleLeft(SerializedProperty property,
            params GUILayoutOption[] options)
            => ToggleLeft(property, (GUIContent?)null, options);

        public static void ToggleLeft(
            SerializedProperty property,
            GUIContent? label,
            params GUILayoutOption[] options)
            => ToggleLeft(property, label, EditorStyles.label, options);

        public static void ToggleLeft(
            SerializedProperty property,
            GUIContent? label,
            GUIStyle style,
            params GUILayoutOption[] options)
            => ExEditorGUI.ToggleLeft(EditorGUILayout.GetControlRect(true, options), property, label, style);
    }

    internal static class ExEditorGUI
    {
        public static void ToggleLeft(
            Rect position,
            SerializedProperty property)
            => ToggleLeft(position, property, null);

        public static void ToggleLeft(
            Rect position,
            SerializedProperty property,
            GUIContent? label)
            => ToggleLeft(position, property, label, EditorStyles.label);

        public static void ToggleLeft(
            Rect position,
            SerializedProperty property,
            GUIContent? label,
            GUIStyle style)
        {
            label = EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            var flag = EditorGUI.ToggleLeft(position, label, property.boolValue, style);
            if (EditorGUI.EndChangeCheck())
                property.boolValue = flag;
            EditorGUI.EndProperty();
        }
    }
}
