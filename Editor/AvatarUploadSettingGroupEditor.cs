using System;
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
        private string _searchText = "";
        private readonly HashSet<int> _expandedSettings = new HashSet<int>();
        private readonly HashSet<int> _selectedSettings = new HashSet<int>();
        private readonly Dictionary<int, UnityEditor.Editor> _settingEditors = new Dictionary<int, UnityEditor.Editor>();

        private void OnEnable()
        {
            _asset = (AvatarUploadSettingGroup)target;
            _selectedSettings.Clear();
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
            if (!_asset)
                return;

            var avatars = _asset.avatars ?? Array.Empty<AvatarUploadSetting>();
            var visibleIndices = BuildVisibleIndices(avatars);
            PruneDeadSelection(avatars);

            EditorGUILayout.LabelField("Avatar Upload Settings", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(BuildCountLabel(avatars.Length, visibleIndices.Count), EditorStyles.miniLabel);
            DrawSearchField();
            DrawBatchToolbar(avatars, visibleIndices);

            var hasAvatarWithAllPlatformsDisabled =
                avatars.Any(avatar => avatar && !avatar.ios.enabled && !avatar.quest.enabled && !avatar.windows.enabled);
            if (hasAvatarWithAllPlatformsDisabled)
                EditorGUILayout.HelpBox("Some avatars have all platforms disabled. This is fine if intentional.", MessageType.Warning);

            ContinuousAvatarUploader.UploadButtonGui(new[] { _asset }, Repaint);

            EditorGUILayout.Space();
            if (visibleIndices.Count == 0 && !string.IsNullOrWhiteSpace(_searchText))
            {
                EditorGUILayout.HelpBox("No avatars match the current search.", MessageType.Info);
            }
            else
            {
                foreach (var index in visibleIndices)
                    DrawAvatarRow(index);
            }

            EditorGUILayout.Space();
            DrawAddAvatar();
        }

        private string BuildCountLabel(int totalCount, int visibleCount)
        {
            if (string.IsNullOrWhiteSpace(_searchText))
                return $"{totalCount} avatars";
            return $"{visibleCount} / {totalCount} avatars";
        }

        private List<int> BuildVisibleIndices(AvatarUploadSetting[] avatars)
        {
            var result = new List<int>(avatars.Length);
            var searchText = (_searchText ?? "").Trim();
            var hasSearch = !string.IsNullOrWhiteSpace(searchText);

            for (var index = 0; index < avatars.Length; index++)
            {
                var avatar = avatars[index];
                if (!hasSearch ||
                    avatar && MatchesSearch(avatar, index, searchText) ||
                    !avatar && MatchesMissingSearch(index, searchText))
                    result.Add(index);
            }

            return result;
        }

        private void PruneDeadSelection(AvatarUploadSetting[] avatars)
        {
            var liveIds = new HashSet<int>(avatars.Where(x => x).Select(x => x.GetInstanceID()));
            _selectedSettings.RemoveWhere(id => !liveIds.Contains(id));
            _expandedSettings.RemoveWhere(id => !liveIds.Contains(id));
        }

        private void DrawSearchField()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                var nextSearchText = EditorGUILayout.TextField("Search Avatars", _searchText ?? "");
                var changed = EditorGUI.EndChangeCheck();

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(nextSearchText)))
                {
                    if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(52f)))
                    {
                        nextSearchText = "";
                        changed = true;
                    }
                }

                if (changed && nextSearchText != _searchText)
                {
                    _searchText = nextSearchText;
                    Repaint();
                }
            }
        }

        private void DrawBatchToolbar(AvatarUploadSetting[] avatars, List<int> visibleIndices)
        {
            var visibleSelected = GetVisibleSelectedIds(avatars, visibleIndices);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(28f);
                EditorGUILayout.LabelField($"{visibleSelected.Count} selected", EditorStyles.miniLabel, GUILayout.Width(82f));

                using (new EditorGUI.DisabledScope(visibleIndices.All(index => !avatars[index])))
                {
                    if (GUILayout.Button("Select Visible", EditorStyles.miniButtonLeft, GUILayout.Width(92f)))
                    {
                        foreach (var setting in VisibleSettings(avatars, visibleIndices))
                            _selectedSettings.Add(setting.GetInstanceID());
                    }
                }

                using (new EditorGUI.DisabledScope(visibleSelected.Count == 0))
                {
                    if (GUILayout.Button("Clear Visible", EditorStyles.miniButtonMid, GUILayout.Width(82f)))
                    {
                        foreach (var id in visibleSelected)
                            _selectedSettings.Remove(id);
                    }

                    if (GUILayout.Button("Move Up", EditorStyles.miniButtonMid, GUILayout.Width(66f)))
                        MoveSelectedVisible(visibleSelected, -1);

                    if (GUILayout.Button("Move Down", EditorStyles.miniButtonMid, GUILayout.Width(82f)))
                        MoveSelectedVisible(visibleSelected, 1);

                    if (GUILayout.Button("Delete", EditorStyles.miniButtonRight, GUILayout.Width(58f)))
                        DeleteSelectedVisible(visibleSelected);
                }

                GUILayout.FlexibleSpace();
            }
        }

        private List<int> GetVisibleSelectedIds(AvatarUploadSetting[] avatars, List<int> visibleIndices)
        {
            var selected = new List<int>();
            foreach (var setting in VisibleSettings(avatars, visibleIndices))
            {
                var id = setting.GetInstanceID();
                if (_selectedSettings.Contains(id))
                    selected.Add(id);
            }

            return selected;
        }

        private static IEnumerable<AvatarUploadSetting> VisibleSettings(AvatarUploadSetting[] avatars, List<int> visibleIndices)
        {
            foreach (var index in visibleIndices)
                if (0 <= index && index < avatars.Length && avatars[index])
                    yield return avatars[index];
        }

        private void DrawAvatarRow(int index)
        {
            if (_asset.avatars == null || index < 0 || index >= _asset.avatars.Length)
                return;

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
                DrawSelectionToggle(instanceId);

                if (GUILayout.Button(BuildExpansionTitle(setting, index, expanded), EditorStyles.label))
                    SetExpanded(instanceId, !expanded);

                using (new EditorGUI.DisabledScope(index <= 0))
                {
                    if (GUILayout.Button("^", EditorStyles.miniButtonLeft, GUILayout.Width(28f)))
                        MoveAndExit(index, index - 1);
                }

                if (GUILayout.Button("Select", EditorStyles.miniButtonMid, GUILayout.Width(54f)))
                    Selection.activeObject = setting;

                if (GUILayout.Button("Remove", EditorStyles.miniButtonMid, GUILayout.Width(70f)))
                    RemoveAndExit(setting);

                using (new EditorGUI.DisabledScope(index >= _asset.avatars.Length - 1))
                {
                    if (GUILayout.Button("v", EditorStyles.miniButtonRight, GUILayout.Width(28f)))
                        MoveAndExit(index, index + 1);
                }
            }

            if (expanded)
            {
                EditorGUI.indentLevel++;
                GetSettingEditor(setting).OnInspectorGUI();
                EditorGUI.indentLevel--;
            }

            HorizontalLine();
        }

        private void DrawSelectionToggle(int instanceId)
        {
            var selected = _selectedSettings.Contains(instanceId);

            EditorGUI.BeginChangeCheck();
            selected = EditorGUILayout.Toggle(selected, GUILayout.Width(24f));
            if (EditorGUI.EndChangeCheck())
            {
                if (selected)
                    _selectedSettings.Add(instanceId);
                else
                    _selectedSettings.Remove(instanceId);
            }
        }

        private void SetExpanded(int instanceId, bool expanded)
        {
            if (expanded)
                _expandedSettings.Add(instanceId);
            else
                _expandedSettings.Remove(instanceId);
        }

        private void DrawMissingAvatarRow(int index)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(28f);
                EditorGUILayout.LabelField($"{index + 1}. Missing AvatarUploadSetting", EditorStyles.boldLabel);
                if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(70f)))
                {
                    RemoveMissingAt(index);
                    EditorUtility.SetDirty(_asset);
                    AssetDatabase.SaveAssetIfDirty(_asset);
                    GUIUtility.ExitGUI();
                }
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

                    if (_asset.avatars == null)
                        _asset.avatars = Array.Empty<AvatarUploadSetting>();
                    ArrayUtility.Add(ref _asset.avatars, newObj);
                    EditorUtility.SetDirty(_asset);
                    AssetDatabase.AddObjectToAsset(newObj, _asset);
                    AssetDatabase.SaveAssetIfDirty(_asset);
                    _avatarToAdd = null;
                    GUIUtility.ExitGUI();
                }
            }
        }

        private UnityEditor.Editor GetSettingEditor(AvatarUploadSetting setting)
        {
            var instanceId = setting.GetInstanceID();
            if (!_settingEditors.TryGetValue(instanceId, out var editor) || !editor)
            {
                editor = CreateEditor(setting);
                _settingEditors[instanceId] = editor;
            }

            return editor;
        }

        private void MoveAndExit(int from, int to)
        {
            if (_asset.avatars == null ||
                from < 0 ||
                to < 0 ||
                from >= _asset.avatars.Length ||
                to >= _asset.avatars.Length)
                return;

            Undo.RecordObject(_asset, "Move Avatar Upload Setting");
            var temp = _asset.avatars[to];
            _asset.avatars[to] = _asset.avatars[from];
            _asset.avatars[from] = temp;
            EditorUtility.SetDirty(_asset);
            GUIUtility.ExitGUI();
        }

        private void MoveSelectedVisible(List<int> selectedVisibleIds, int direction)
        {
            if (_asset.avatars == null || selectedVisibleIds.Count == 0)
                return;

            var selected = new HashSet<int>(selectedVisibleIds);
            Undo.RecordObject(_asset, direction < 0 ? "Move Selected Avatars Up" : "Move Selected Avatars Down");

            if (direction < 0)
            {
                for (var index = 1; index < _asset.avatars.Length; index++)
                {
                    if (IsSelected(_asset.avatars[index], selected) && !IsSelected(_asset.avatars[index - 1], selected))
                        Swap(index, index - 1);
                }
            }
            else
            {
                for (var index = _asset.avatars.Length - 2; index >= 0; index--)
                {
                    if (IsSelected(_asset.avatars[index], selected) && !IsSelected(_asset.avatars[index + 1], selected))
                        Swap(index, index + 1);
                }
            }

            EditorUtility.SetDirty(_asset);
            GUIUtility.ExitGUI();
        }

        private void DeleteSelectedVisible(List<int> selectedVisibleIds)
        {
            if (_asset.avatars == null || selectedVisibleIds.Count == 0)
                return;

            var selected = new HashSet<int>(selectedVisibleIds);
            Undo.RecordObject(_asset, "Delete Selected Avatars");
            var kept = new List<AvatarUploadSetting>(_asset.avatars.Length);

            for (var index = _asset.avatars.Length - 1; index >= 0; index--)
            {
                var setting = _asset.avatars[index];
                if (!IsSelected(setting, selected))
                    kept.Add(setting);
                else
                {
                    RemoveEditorState(setting);
                    Object.DestroyImmediate(setting, true);
                }
            }

            kept.Reverse();
            _asset.avatars = kept.ToArray();

            foreach (var id in selected)
                _selectedSettings.Remove(id);

            EditorUtility.SetDirty(_asset);
            AssetDatabase.SaveAssetIfDirty(_asset);
            GUIUtility.ExitGUI();
        }

        private static bool IsSelected(AvatarUploadSetting setting, HashSet<int> selectedIds) =>
            setting && selectedIds.Contains(setting.GetInstanceID());

        private void Swap(int first, int second)
        {
            var temp = _asset.avatars[second];
            _asset.avatars[second] = _asset.avatars[first];
            _asset.avatars[first] = temp;
        }

        private void RemoveAndExit(AvatarUploadSetting setting)
        {
            RemoveEditorState(setting);

            Undo.RecordObject(_asset, "Remove Avatar Upload Setting");
            ArrayUtility.Remove(ref _asset.avatars, setting);
            EditorUtility.SetDirty(_asset);
            Object.DestroyImmediate(setting, true);
            AssetDatabase.SaveAssetIfDirty(_asset);
            GUIUtility.ExitGUI();
        }

        private void RemoveEditorState(AvatarUploadSetting setting)
        {
            var instanceId = setting.GetInstanceID();
            if (_settingEditors.TryGetValue(instanceId, out var editor) && editor)
                Object.DestroyImmediate(editor);
            _settingEditors.Remove(instanceId);
            _expandedSettings.Remove(instanceId);
            _selectedSettings.Remove(instanceId);
        }

        private static string BuildTitle(AvatarUploadSetting setting, int index)
        {
            var avatarName = string.IsNullOrEmpty(setting.avatarName) ? setting.name : setting.avatarName;
            return $"{index + 1}. {avatarName} [{BuildPlatformSummary(setting)}]";
        }

        private static string BuildExpansionTitle(AvatarUploadSetting setting, int index, bool expanded) =>
            (expanded ? "[-] " : "[+] ") + BuildTitle(setting, index);

        private void RemoveMissingAt(int index)
        {
            if (_asset.avatars == null || index < 0 || index >= _asset.avatars.Length)
                return;

            Undo.RecordObject(_asset, "Remove Missing Avatar Upload Setting");
            var avatars = _asset.avatars.ToList();
            avatars.RemoveAt(index);
            _asset.avatars = avatars.ToArray();
        }

        private static string BuildPlatformSummary(AvatarUploadSetting setting)
        {
            var platforms = new List<string>(3);
            if (setting.windows.enabled) platforms.Add("PC");
            if (setting.quest.enabled) platforms.Add("Quest");
            if (setting.ios.enabled) platforms.Add("iOS");
            return platforms.Count == 0 ? "disabled" : string.Join(", ", platforms);
        }

        private static bool MatchesSearch(AvatarUploadSetting setting, int index, string searchText)
        {
            foreach (var token in SearchTokens(searchText))
                if (!MatchesSearchToken(setting, index, token))
                    return false;

            return true;
        }

        private static bool MatchesMissingSearch(int index, string searchText)
        {
            foreach (var token in SearchTokens(searchText))
            {
                if (ContainsIgnoreCase((index + 1).ToString(), token) ||
                    ContainsIgnoreCase("missing null none", token))
                    continue;

                return false;
            }

            return true;
        }

        private static IEnumerable<string> SearchTokens(string searchText) =>
            (searchText ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        private static bool MatchesSearchToken(AvatarUploadSetting setting, int index, string token)
        {
            if (ContainsIgnoreCase((index + 1).ToString(), token) ||
                ContainsIgnoreCase(setting.avatarName, token) ||
                ContainsIgnoreCase(setting.name, token))
                return true;

            if (setting.windows.enabled && ContainsIgnoreCase("pc windows win", token))
                return true;
            if (setting.quest.enabled && ContainsIgnoreCase("quest android", token))
                return true;
            if (setting.ios.enabled && ContainsIgnoreCase("ios iphone ipad", token))
                return true;
            if (!setting.windows.enabled && !setting.quest.enabled && !setting.ios.enabled &&
                ContainsIgnoreCase("disabled off", token))
                return true;

            return false;
        }

        private static bool ContainsIgnoreCase(string text, string token) =>
            !string.IsNullOrEmpty(text) &&
            !string.IsNullOrEmpty(token) &&
            text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;

        private static void HorizontalLine(float regionHeight = 18f, float lineHeight = 1f)
        {
            var rect = GUILayoutUtility.GetRect(EditorGUIUtility.fieldWidth, float.MaxValue, regionHeight, regionHeight);
            rect.y += (rect.height - lineHeight) / 2;
            rect.height = lineHeight;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
        }
    }
}
