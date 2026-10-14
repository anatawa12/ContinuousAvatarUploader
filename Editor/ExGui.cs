#nullable enable

using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
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

    internal static class ExEditor
    {
        private static readonly Type? _iPropertyViewType = 
            typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.IPropertyView", false, false) is {} type
            && type.IsInterface ? type : null;

        private static PropertyInfo? _propertyViewerOfEditorProperty =
            typeof(UnityEditor.Editor)
                .GetProperty("propertyViewer", BindingFlags.NonPublic | BindingFlags.Instance) is { } property
            && property.PropertyType == _iPropertyViewType
                ? property
                : null;

        private static PropertyInfo? _trackerOfIPropertyViewerProperty =
            _iPropertyViewType?.GetProperty("tracker", BindingFlags.Public | BindingFlags.Instance) is { } property
            && property.PropertyType == typeof(ActiveEditorTracker)
                ? property
                : null;

        public static PropertyInfo? _editorOfInspectorElement =
            typeof(InspectorElement).GetProperty("editor", BindingFlags.NonPublic | BindingFlags.Instance) is
                { } property
            && property.PropertyType == typeof(UnityEditor.Editor)
                ? property
                : null;
            

        public static /*IPropertyView*/ object? GetPropertyViewer(this UnityEditor.Editor editor)
        {
            try
            {
                if (_propertyViewerOfEditorProperty == null) throw new Exception("Editor.propertyViewer not found");
                return _propertyViewerOfEditorProperty.GetGetMethod(true).Invoke(editor, new object[] { });
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }

        public static ActiveEditorTracker? GetEditorTracker(this UnityEditor.Editor editor)
        {
            try
            {
                if (_trackerOfIPropertyViewerProperty == null) throw new Exception("PropertyViewer.tracker not found");
                var propertyViewer = editor.GetPropertyViewer();
                if (propertyViewer == null) return null;
                var tracker = _trackerOfIPropertyViewerProperty.GetGetMethod(true)
                    .Invoke(propertyViewer, new object[] { });
                return tracker as ActiveEditorTracker;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }
        
        public static void SetPropertyViewer(this UnityEditor.Editor editor, object? value)
        {
            try
            {
                if (_propertyViewerOfEditorProperty == null) throw new Exception("Editor.propertyViewer not found");
                _propertyViewerOfEditorProperty.GetSetMethod(true).Invoke(editor, new[] { value });
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public static UnityEditor.Editor? GetEditor(this InspectorElement inspectorElement)
        {
            try
            {
                if (_editorOfInspectorElement == null) throw new Exception("Inspector.editor not found");
                return _editorOfInspectorElement.GetGetMethod(true).Invoke(inspectorElement, new object[] { }) as
                    UnityEditor.Editor;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }
    }
}
