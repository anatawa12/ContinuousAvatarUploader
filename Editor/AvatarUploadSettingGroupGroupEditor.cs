using UnityEditor;
using VRC.SDK3.Avatars.Components;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    [CustomEditor(typeof(AvatarUploadSettingGroupGroup))]
    public class AvatarUploadSettingGroupGroupEditor : UnityEditor.Editor
    {
        private SerializedProperty _groups;

        private VRCAvatarDescriptor _cachedAvatar;
        private bool _settingAvatar;

        private void OnEnable()
        {
            _groups = serializedObject.FindProperty(nameof(AvatarUploadSettingGroupGroup.groups));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(_groups, true);
            serializedObject.ApplyModifiedProperties();
        }
    }
}