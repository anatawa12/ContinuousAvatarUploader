using UnityEditor;
using UnityEngine;
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
            EditorGUILayout.LabelField("Avatar Upload Settings", EditorStyles.boldLabel);
            if (GUILayout.Button("Upload All"))
            {
                var uploader = EditorWindow.GetWindow<ContinuousAvatarUploader>();
                uploader.settingsOrGroups = new AvatarUploadSettingOrGroup[] { (AvatarUploadSettingGroupGroup)target };
                if (!uploader.StartUpload())
                {
                    EditorUtility.DisplayDialog("Failed to start upload",
                        "Failed to start upload.\nPlease refer Uploader window for reason", "OK");
                }
            }
            EditorGUILayout.Space();

            serializedObject.Update();
            EditorGUILayout.PropertyField(_groups, true);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
