using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    [CustomEditor(typeof(AvatarDescriptor))]
    public class AvatarDescriptorEditor : UnityEditor.Editor
    {
        private VRCAvatarDescriptor _cachedAvatar;
        private bool _settingAvatar;

        public override void OnInspectorGUI()
        {
            var asset = (AvatarDescriptor)target;
            EditorGUI.BeginChangeCheck();

            AvatarDescriptors(asset);

            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(asset);
        }

        private void AvatarDescriptors(AvatarDescriptor avatar)
        {
            if (avatar.avatarDescriptor.IsNull())
                _settingAvatar = true;
            if (_settingAvatar)
            {
                _cachedAvatar = (VRCAvatarDescriptor)EditorGUILayout.ObjectField("Set Avatar: ", 
                    null, typeof(VRCAvatarDescriptor), true);
                
                if (_cachedAvatar && GlobalObjectId.GetGlobalObjectIdSlow(_cachedAvatar).identifierType == 2)
                {
                    avatar.avatarDescriptor = new SceneReference(_cachedAvatar);
                    // might be reverted if it's individual asset but
                    // this is good for DescriptorSet
                    avatar.name = avatar.avatarName = _cachedAvatar.name;
                    _settingAvatar = false;
                }

                EditorGUI.BeginDisabledGroup(avatar.avatarDescriptor.IsNull());
                if (GUILayout.Button("Chancel Change Avatar"))
                    _settingAvatar = false;
                EditorGUI.EndDisabledGroup();
            }
            else
            {
                if (!_cachedAvatar)
                    _cachedAvatar = avatar.avatarDescriptor.TryResolve() as VRCAvatarDescriptor;
                if (_cachedAvatar)
                {
                    EditorGUILayout.ObjectField("Avatar", _cachedAvatar, typeof(VRCAvatarDescriptor), true);
                    avatar.avatarName = _cachedAvatar.name;
                }
                else
                {
                    EditorGUILayout.LabelField("Avatar", avatar.avatarName);
                    EditorGUILayout.ObjectField("In scene", avatar.avatarDescriptor.scene, typeof(SceneAsset), false);
                }

                if (GUILayout.Button("Change Avatar"))
                    _settingAvatar = true;
            }
            PlatformSpecificInfo("PC Windows", avatar.windows);
            PlatformSpecificInfo("Quest", avatar.quest);
        }

        private void PlatformSpecificInfo(string name, PlatformSpecificInfo info)
        {
            info.enabled = EditorGUILayout.ToggleLeft(name, info.enabled);

            if (info.enabled)
            {
                EditorGUI.indentLevel++;
                info.versioningEnabled = EditorGUILayout.ToggleLeft("Versioning System", info.versioningEnabled);
                if (info.versioningEnabled)
                {
                    EditorGUI.indentLevel++;
                    info.versionNamePrefix = EditorGUILayout.TextField("Version Prefix", info.versionNamePrefix);
                    EditorGUILayout.LabelField($"'({info.versionNamePrefix}<version>)'will be added in avatar description");
                    info.gitEnabled = EditorGUILayout.ToggleLeft("git tagging", info.gitEnabled);
                    if (info.gitEnabled)
                    {
                        EditorGUI.indentLevel++;
                        info.tagPrefix = EditorGUILayout.TextField("Tag Prefix", info.tagPrefix);
                        info.tagSuffix = EditorGUILayout.TextField("Tag Suffix", info.tagSuffix);
                        EditorGUILayout.LabelField($"tag name will be '{info.tagPrefix}<version>{info.tagSuffix}'");
                        EditorGUI.indentLevel--;
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUI.indentLevel--;
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
