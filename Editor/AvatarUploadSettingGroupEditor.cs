using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    [CustomEditor(typeof(AvatarUploadSettingGroup))]
    public class AvatarUploadSettingGroupEditor : UnityEditor.Editor
    {
        private AvatarUploadSettingGroup _asset;
        private VRCAvatarDescriptor _avatarToAdd;
        private readonly HashSet<int> _expandedSettings = new HashSet<int>();
        private readonly Dictionary<int, UnityEditor.Editor> _settingEditors = new Dictionary<int, UnityEditor.Editor>();

        private void OnEnable()
        {
            _asset = (AvatarUploadSettingGroup)target;
        }

        private void OnDisable()
        {
            foreach (var editor in _settingEditors.Values)
                if (editor)
                    Object.DestroyImmediate(editor);
            _settingEditors.Clear();
        }

        public override void OnInspectorGUI()
        {
            if (!_asset) return;

            EditorGUILayout.LabelField("Avatar Upload Settings", EditorStyles.boldLabel);

            var hasAvatarWithAllPlatformsDisabled = _asset.avatars.Any(avatar => avatar && !avatar.ios.enabled && !avatar.quest.enabled && !avatar.windows.enabled);
            if (hasAvatarWithAllPlatformsDisabled)
                EditorGUILayout.HelpBox("Some avatars have all platforms disabled. This is fine if intentional.", MessageType.Warning);

            ContinuousAvatarUploader.UploadButtonGui(new[] { _asset }, Repaint);

            EditorGUILayout.Space();

            for (var index = 0; index < _asset.avatars.Length; index++)
                DrawAvatarRow(index);

            EditorGUILayout.Space();

            DrawAddAvatar();
        }

        private void DrawAvatarRow(int index)
        {
            var setting = _asset.avatars[index];
            if (!setting)
            {
                DrawMissingAvatarRow(index);
                return;
            }

            var instanceId = setting.GetInstanceID();
            var expanded = _expandedSettings.Contains(instanceId);

            using (new EditorGUILayout.HorizontalScope())
            {
                var nextExpanded = GUILayout.Toggle(expanded, BuildTitle(setting, index), EditorStyles.foldout);
                if (nextExpanded != expanded)
                    SetExpanded(instanceId, nextExpanded);

                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(index == 0))
                    if (GUILayout.Button("▲", EditorStyles.miniButton, GUILayout.Width(30)))
                        SwapAndExit(index, index - 1);

                if (GUILayout.Button("Remove Avatar", EditorStyles.miniButton))
                    RemoveAndExit(setting);

                using (new EditorGUI.DisabledScope(index == _asset.avatars.Length - 1))
                    if (GUILayout.Button("▼", EditorStyles.miniButton, GUILayout.Width(30)))
                        SwapAndExit(index, index + 1);
            }

            // The child editor is the expensive part to construct, so it's created on first expansion only.
            if (expanded)
            {
                EditorGUI.indentLevel++;
                GetSettingEditor(setting).OnInspectorGUI();
                EditorGUI.indentLevel--;
            }

            HorizontalLine();
        }

        // An entry can be null when its sub asset went missing, and every other row helper assumes it isn't.
        private void DrawMissingAvatarRow(int index)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"Avatar #{index}: (missing)");

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Remove Avatar", EditorStyles.miniButton))
                    RemoveMissingAndExit(index);
            }

            HorizontalLine();
        }

        private void DrawAddAvatar()
        {
            _avatarToAdd = EditorGUILayout.ObjectField("Avatar to Add", _avatarToAdd,
                typeof(VRCAvatarDescriptor), true) as VRCAvatarDescriptor;

            using (new EditorGUI.DisabledScope(!_avatarToAdd))
            {
                if (GUILayout.Button("Add Avatar"))
                {
                    var newObj = ScriptableObject.CreateInstance<AvatarUploadSetting>();
                    newObj.avatarDescriptor = new MaySceneReference(_avatarToAdd);
                    newObj.name = newObj.avatarName = _avatarToAdd.gameObject.name;

                    ArrayUtility.Add(ref _asset.avatars, newObj);
                    EditorUtility.SetDirty(_asset);
                    AssetDatabase.AddObjectToAsset(newObj, _asset);
                    AssetDatabase.SaveAssetIfDirty(newObj);
                    _avatarToAdd = null;

                    GUIUtility.ExitGUI();
                }
            }
        }

        private UnityEditor.Editor GetSettingEditor(AvatarUploadSetting setting)
        {
            var instanceId = setting.GetInstanceID();
            if (!_settingEditors.TryGetValue(instanceId, out var editor) || !editor)
                _settingEditors[instanceId] = editor = CreateEditor(setting);
            return editor;
        }

        private void SetExpanded(int instanceId, bool expanded)
        {
            if (expanded)
                _expandedSettings.Add(instanceId);
            else
                _expandedSettings.Remove(instanceId);
        }

        private void SwapAndExit(int from, int to)
        {
            var temp = _asset.avatars[to];
            _asset.avatars[to] = _asset.avatars[from];
            _asset.avatars[from] = temp;
            EditorUtility.SetDirty(_asset);

            GUIUtility.ExitGUI();
        }

        private void RemoveAndExit(AvatarUploadSetting setting)
        {
            ForgetEditorState(setting);

            ArrayUtility.Remove(ref _asset.avatars, setting);
            EditorUtility.SetDirty(_asset);
            Object.DestroyImmediate(setting, true);
            AssetDatabase.SaveAssetIfDirty(_asset);

            GUIUtility.ExitGUI();
        }

        private void RemoveMissingAndExit(int index)
        {
            var avatars = _asset.avatars.ToList();
            avatars.RemoveAt(index);
            _asset.avatars = avatars.ToArray();
            EditorUtility.SetDirty(_asset);
            AssetDatabase.SaveAssetIfDirty(_asset);

            GUIUtility.ExitGUI();
        }

        private void ForgetEditorState(AvatarUploadSetting setting)
        {
            var instanceId = setting.GetInstanceID();
            if (_settingEditors.TryGetValue(instanceId, out var editor) && editor)
                Object.DestroyImmediate(editor);
            _settingEditors.Remove(instanceId);
            _expandedSettings.Remove(instanceId);
        }

        // A collapsed row shows nothing but its title, so it has to carry enough to tell the avatars apart.
        private static string BuildTitle(AvatarUploadSetting setting, int index)
        {
            var avatarName = string.IsNullOrEmpty(setting.avatarName) ? setting.name : setting.avatarName;
            return $"Avatar #{index}: {avatarName} [{BuildPlatformSummary(setting)}]";
        }

        private static string BuildPlatformSummary(AvatarUploadSetting setting)
        {
            var platforms = new List<string>(3);
            if (setting.windows.enabled) platforms.Add("PC");
            if (setting.quest.enabled) platforms.Add("Quest");
            if (setting.ios.enabled) platforms.Add("iOS");
            return platforms.Count == 0 ? "disabled" : string.Join(", ", platforms);
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
