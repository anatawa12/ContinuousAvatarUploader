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
        private const float SelectionToggleWidth = 20f;

        private AvatarUploadSettingGroup _asset;
        private VRCAvatarDescriptor _avatarToAdd;
        private string _searchText = "";
        private readonly HashSet<int> _expandedSettings = new HashSet<int>();
        private readonly HashSet<int> _selectedSettings = new HashSet<int>();
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

            DrawSearchField();

            var hasAvatarWithAllPlatformsDisabled = _asset.avatars.Any(avatar => avatar && !avatar.ios.enabled && !avatar.quest.enabled && !avatar.windows.enabled);
            if (hasAvatarWithAllPlatformsDisabled)
                EditorGUILayout.HelpBox("Some avatars have all platforms disabled. This is fine if intentional.", MessageType.Warning);

            ContinuousAvatarUploader.UploadButtonGui(new[] { _asset }, Repaint);

            EditorGUILayout.Space();

            var visibleIndices = BuildVisibleIndices();
            EditorGUILayout.LabelField(BuildCountLabel(visibleIndices.Count), EditorStyles.miniLabel);
            DrawBatchToolbar(visibleIndices);

            if (visibleIndices.Count == 0 && IsSearching)
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

        private bool IsSearching => !string.IsNullOrWhiteSpace(_searchText);

        private void DrawSelectionToggle(int instanceId)
        {
            var selected = _selectedSettings.Contains(instanceId);
            if (EditorGUILayout.Toggle(selected, GUILayout.Width(SelectionToggleWidth)) == selected) return;

            if (selected)
                _selectedSettings.Remove(instanceId);
            else
                _selectedSettings.Add(instanceId);
        }

        // Batch operations act on the selected rows that are currently shown, never on ones hidden by the search.
        private void DrawBatchToolbar(List<int> visibleIndices)
        {
            var selected = VisibleSettings(visibleIndices)
                .Select(setting => setting.GetInstanceID())
                .Where(id => _selectedSettings.Contains(id))
                .ToList();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(SelectionToggleWidth);
                EditorGUILayout.LabelField($"{selected.Count} selected", EditorStyles.miniLabel, GUILayout.Width(76));

                using (new EditorGUI.DisabledScope(visibleIndices.Count == 0))
                {
                    if (GUILayout.Button("Select Shown", EditorStyles.miniButtonLeft, GUILayout.Width(88)))
                        foreach (var setting in VisibleSettings(visibleIndices))
                            _selectedSettings.Add(setting.GetInstanceID());
                }

                using (new EditorGUI.DisabledScope(selected.Count == 0))
                {
                    if (GUILayout.Button("Deselect", EditorStyles.miniButtonMid, GUILayout.Width(64)))
                        foreach (var id in selected)
                            _selectedSettings.Remove(id);

                    using (new EditorGUI.DisabledScope(IsSearching))
                    {
                        if (GUILayout.Button("Move Up", EditorStyles.miniButtonMid, GUILayout.Width(66)))
                            MoveSelectedAndExit(selected, -1);

                        if (GUILayout.Button("Move Down", EditorStyles.miniButtonMid, GUILayout.Width(78)))
                            MoveSelectedAndExit(selected, 1);
                    }

                    if (GUILayout.Button("Delete", EditorStyles.miniButtonRight, GUILayout.Width(56)))
                        DeleteSelectedAndExit(selected);
                }

                GUILayout.FlexibleSpace();
            }
        }

        private IEnumerable<AvatarUploadSetting> VisibleSettings(List<int> visibleIndices) =>
            visibleIndices.Select(index => _asset.avatars[index]).Where(setting => setting);

        private string BuildCountLabel(int visibleCount) =>
            IsSearching
                ? $"{visibleCount} / {_asset.avatars.Length} avatars"
                : $"{_asset.avatars.Length} avatars";

        private void DrawSearchField()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _searchText = EditorGUILayout.TextField("Search Avatars", _searchText);

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_searchText)))
                {
                    if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(50)))
                    {
                        _searchText = "";
                        // the field keeps rendering the old text otherwise
                        GUIUtility.keyboardControl = 0;
                    }
                }
            }
        }

        private List<int> BuildVisibleIndices()
        {
            var avatars = _asset.avatars;
            var searchText = (_searchText ?? "").Trim();
            var result = new List<int>(avatars.Length);

            for (var index = 0; index < avatars.Length; index++)
            {
                if (searchText.Length != 0)
                {
                    var avatar = avatars[index];
                    if (avatar && !MatchesSearch(avatar, index, searchText)) continue;
                    if (!avatar && !MatchesMissingSearch(index, searchText)) continue;
                }

                result.Add(index);
            }

            return result;
        }

        private static IEnumerable<string> SearchTokens(string searchText) =>
            searchText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        private static bool MatchesSearch(AvatarUploadSetting setting, int index, string searchText) =>
            SearchTokens(searchText).All(token => MatchesSearchToken(setting, index, token));

        private static bool MatchesMissingSearch(int index, string searchText) =>
            SearchTokens(searchText).All(token =>
                ContainsIgnoreCase(index.ToString(), token) || MatchesKeyword(token, "missing"));

        private static bool MatchesSearchToken(AvatarUploadSetting setting, int index, string token)
        {
            if (ContainsIgnoreCase(index.ToString(), token) ||
                ContainsIgnoreCase(setting.avatarName, token) ||
                ContainsIgnoreCase(setting.name, token))
                return true;

            if (setting.windows.enabled && MatchesKeyword(token, "pc", "windows")) return true;
            if (setting.quest.enabled && MatchesKeyword(token, "quest", "android")) return true;
            if (setting.ios.enabled && MatchesKeyword(token, "ios")) return true;
            if (!setting.windows.enabled && !setting.quest.enabled && !setting.ios.enabled &&
                MatchesKeyword(token, "disabled"))
                return true;

            return false;
        }

        // Prefix, not substring: "que" should find Quest avatars but "s" should not find every Windows one.
        private static bool MatchesKeyword(string token, params string[] keywords) =>
            keywords.Any(keyword => keyword.StartsWith(token, StringComparison.OrdinalIgnoreCase));

        private static bool ContainsIgnoreCase(string text, string token) =>
            !string.IsNullOrEmpty(text) &&
            text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;

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
                DrawSelectionToggle(instanceId);

                var nextExpanded = GUILayout.Toggle(expanded, BuildTitle(setting, index), EditorStyles.foldout);
                if (nextExpanded != expanded)
                    SetExpanded(instanceId, nextExpanded);

                GUILayout.FlexibleSpace();

                // Reordering moves within the whole array, so its neighbour may be filtered out of sight.
                using (new EditorGUI.DisabledScope(IsSearching || index == 0))
                    if (GUILayout.Button("▲", EditorStyles.miniButton, GUILayout.Width(30)))
                        SwapAndExit(index, index - 1);

                if (GUILayout.Button("Remove Avatar", EditorStyles.miniButton))
                    RemoveAndExit(setting);

                using (new EditorGUI.DisabledScope(IsSearching || index == _asset.avatars.Length - 1))
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
                GUILayout.Space(SelectionToggleWidth);
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
            Undo.RecordObject(_asset, "Move Avatar Upload Setting");
            Swap(from, to);
            EditorUtility.SetDirty(_asset);

            GUIUtility.ExitGUI();
        }

        private void Swap(int first, int second)
        {
            var temp = _asset.avatars[second];
            _asset.avatars[second] = _asset.avatars[first];
            _asset.avatars[first] = temp;
        }

        // Each selected entry hops over the nearest unselected neighbour, so a selected block moves as a block.
        private void MoveSelectedAndExit(List<int> selectedIds, int direction)
        {
            var selected = new HashSet<int>(selectedIds);
            var avatars = _asset.avatars;

            Undo.RecordObject(_asset, direction < 0 ? "Move Avatar Upload Settings Up" : "Move Avatar Upload Settings Down");

            if (direction < 0)
            {
                for (var index = 1; index < avatars.Length; index++)
                    if (IsSelected(avatars[index], selected) && !IsSelected(avatars[index - 1], selected))
                        Swap(index, index - 1);
            }
            else
            {
                for (var index = avatars.Length - 2; index >= 0; index--)
                    if (IsSelected(avatars[index], selected) && !IsSelected(avatars[index + 1], selected))
                        Swap(index, index + 1);
            }

            EditorUtility.SetDirty(_asset);

            GUIUtility.ExitGUI();
        }

        private void DeleteSelectedAndExit(List<int> selectedIds)
        {
            if (!EditorUtility.DisplayDialog("Delete Avatar Upload Settings",
                    $"Delete {selectedIds.Count} avatar upload setting(s) from this group?\n\nThis cannot be undone.",
                    "Delete", "Cancel"))
                return;

            var selected = new HashSet<int>(selectedIds);
            var kept = new List<AvatarUploadSetting>(_asset.avatars.Length);

            foreach (var setting in _asset.avatars)
            {
                if (!IsSelected(setting, selected))
                {
                    kept.Add(setting);
                    continue;
                }

                ForgetEditorState(setting);
                Object.DestroyImmediate(setting, true);
            }

            _asset.avatars = kept.ToArray();
            EditorUtility.SetDirty(_asset);
            AssetDatabase.SaveAssetIfDirty(_asset);

            GUIUtility.ExitGUI();
        }

        private static bool IsSelected(AvatarUploadSetting setting, HashSet<int> selectedIds) =>
            setting && selectedIds.Contains(setting.GetInstanceID());

        // Not undoable: undo would restore the array with references to sub assets that are already destroyed.
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
            Undo.RecordObject(_asset, "Remove Missing Avatar Upload Setting");
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
            _selectedSettings.Remove(instanceId);
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
