using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRC.SDK3A.Editor;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    public class ContinuousAvatarUploader : EditorWindow
    {
        [SerializeField] [ItemCanBeNull] [NotNull] internal AvatarUploadSettingOrGroup[] settingsOrGroups = Array.Empty<AvatarUploadSettingOrGroup>();

        [CanBeNull]
        internal static ContinuousAvatarUploader Instance
        {
            get
            {
                var objects = Resources.FindObjectsOfTypeAll(typeof(ContinuousAvatarUploader));
                return objects.Length == 0 ? null : (ContinuousAvatarUploader)objects[0];
            }
        }

        // for uploading avatars

        [NonSerialized] private State _guiState;
        [NonSerialized] private AvatarUploadSetting _currentUploadingAvatar;
        [SerializeField] private Vector2 uploadsScroll;
        [SerializeField] private Vector2 errorsScroll;

        private UploaderProgressAsset progressAsset;

        private SerializedObject _serialized;
        private SerializedProperty _settingsOrGroups;

        [MenuItem("Window/Continuous Avatar Uploader")]
        [MenuItem("Tools/Continuous Avatar Uploader")]
        public static void OpenWindow() => GetWindow<ContinuousAvatarUploader>("ContinuousAvatarUploader");

        private void OnEnable()
        {
            _serialized = new SerializedObject(this);
            _settingsOrGroups = _serialized.FindProperty(nameof(settingsOrGroups));
            _settingsOrGroups.isExpanded = true;
            VRCSdkControlPanel.OnSdkPanelEnable += OnSdkPanelEnableDisable;
            VRCSdkControlPanel.OnSdkPanelDisable += OnSdkPanelEnableDisable;
            UploadOrchestrator.OnUploadSingleAvatarStarted += OnUploadSingleAvatarStarted;
            UploadOrchestrator.OnUploadSingleAvatarFinished += OnUploadSingleAvatarFinished;
            UploadOrchestrator.OnUploadFinished += OnUploadFinished;
            UploadOrchestrator.OnLoginFailed += OnLoginFailed;
            UploadOrchestrator.OnRandomException += OnRandomException;
        }

        private void OnDisable()
        {
            VRCSdkControlPanel.OnSdkPanelEnable -= OnSdkPanelEnableDisable;
            VRCSdkControlPanel.OnSdkPanelDisable -= OnSdkPanelEnableDisable;
            UploadOrchestrator.OnUploadSingleAvatarStarted -= OnUploadSingleAvatarStarted;
            UploadOrchestrator.OnUploadSingleAvatarFinished -= OnUploadSingleAvatarFinished;
            UploadOrchestrator.OnUploadFinished -= OnUploadFinished;
            UploadOrchestrator.OnLoginFailed -= OnLoginFailed;
            UploadOrchestrator.OnRandomException -= OnRandomException;
        }

        private void OnSdkPanelEnableDisable(object sender, EventArgs e) => Repaint();

        private void OnUploadSingleAvatarStarted(UploaderProgressAsset progress, AvatarUploadSetting avatar)
        {
            _guiState = State.UploadingAvatar;
            _currentUploadingAvatar = avatar;
            Repaint();
        }

        private void OnUploadSingleAvatarFinished(UploaderProgressAsset progress, AvatarUploadSetting avatar)
        {
            _guiState = State.UploadedAvatar;
            _currentUploadingAvatar = null;
            Repaint();
        }

        private void OnUploadFinished(UploaderProgressAsset obj, bool successfully)
        {
            _guiState = State.Configuring;
            _currentUploadingAvatar = null;
            // if finished unsuccessfully, we should have shown error dialog already
            if (Preferences.ShowDialogWhenUploadFinished && successfully)
                EditorUtility.DisplayDialog("Continuous Avatar Uploader", "Finished Uploading Avatars!", "OK");
            Repaint();
        }

        private void OnLoginFailed(Exception obj)
        {
            _guiState = State.Configuring;
            _currentUploadingAvatar = null;
            EditorUtility.DisplayDialog("Continuous Avatar Uploader", "Login Failed: " + obj.Message, "OK");
        }

        private void OnRandomException(Exception obj)
        {
            _guiState = State.Configuring;
            _currentUploadingAvatar = null;
            EditorUtility.DisplayDialog("Continuous Avatar Uploader", "An error occurred: " + obj.Message, "OK");
        }

        private void OnGUI()
        {
            var uploadInProgress = _guiState != State.Configuring;
            if (uploadInProgress)
            {
                progressAsset = UploaderProgressAsset.Load()!;
                var totalCount = progressAsset.uploadSettings.Length;
                var uploadingIndex = progressAsset.uploadingAvatarIndex;
                GUILayout.Label("UPLOAD IN PROGRESS");
                EditorGUI.ProgressBar(GUILayoutUtility.GetRect(100, 20),
                    (uploadingIndex + 0.5f) / totalCount,
                    $"Uploading {uploadingIndex + 1} / {totalCount}");
                if (_currentUploadingAvatar)
                {
                    EditorGUILayout.ObjectField("Uploading", _currentUploadingAvatar, typeof(AvatarUploadSetting), true);
                }
                else
                {
                    GUILayout.Label("Sleeping a little");
                }

                if (GUILayout.Button("ABORT UPLOAD"))
                    UploadOrchestrator.CancelUpload();
            }

            EditorGUI.BeginDisabledGroup(uploadInProgress);
            _serialized.Update();
            Preferences.SleepSeconds = EditorGUILayout.FloatField(
                new GUIContent("Sleep Seconds", "The time sleeps between upload"),
                Preferences.SleepSeconds);
            Preferences.TakeThumbnailInPlaymodeByDefault = EditorGUILayout.ToggleLeft(
                new GUIContent("Take Thumbnail In PlayMode by Default", 
                    "If this is enabled, CAU will take Thumbnail after entering PlayMode."),
                Preferences.TakeThumbnailInPlaymodeByDefault);
            Preferences.ShowDialogWhenUploadFinished = EditorGUILayout.ToggleLeft(
                new GUIContent("Show Dialog when Finished", 
                    "If this is enabled, CAU will tell you upload finished."),
                Preferences.ShowDialogWhenUploadFinished);

            var checkResult = CheckUpload();
            if ((checkResult & UploadCheckResult.Uploading) != 0)
                EditorGUILayout.HelpBox("Uploading", MessageType.Info);
            if ((checkResult & UploadCheckResult.NoDescriptors) != 0)
                EditorGUILayout.HelpBox("No AvatarUploadSettings are specified", MessageType.Error);
            if ((checkResult & UploadCheckResult.AnyNull) != 0)
                EditorGUILayout.HelpBox("Some AvatarUploadSetting or Group are None", MessageType.Error);
            if ((checkResult & UploadCheckResult.PlayMode) != 0)
                EditorGUILayout.HelpBox("To upload avatars, exit Play mode", MessageType.Error);
            if ((checkResult & UploadCheckResult.NoCredentials) != 0)
                EditorGUILayout.HelpBox("Please login in control panel", MessageType.Error);
            if ((checkResult & UploadCheckResult.ControlPanelClosed) != 0)
                EditorGUILayout.HelpBox("Please open Control panel", MessageType.Error);
            if ((checkResult & UploadCheckResult.NoAvatarBuilder) != 0)
                EditorGUILayout.HelpBox("No Valid VRCSDK Avatars Found", MessageType.Error);
            if ((checkResult & UploadCheckResult.PlayModeSettingsNotGood) != 0)
                EditorGUILayout.HelpBox(
                    "Some avatars are going or taking thumbnail in PlayMode. " +
                    "To take thumbnail in PlayMode, Please Disable 'Reload Domain' Option in " +
                    "Enter Play Mode Settings in Editor in Project Settings",
                    MessageType.Error);
            using (new EditorGUI.DisabledScope(checkResult != UploadCheckResult.Ok))
            {
                if (GUILayout.Button("Start Upload"))
                {
                    DoStartUpload();
                }
            }

            uploadsScroll = EditorGUILayout.BeginScrollView(uploadsScroll);
            EditorGUILayout.PropertyField(_settingsOrGroups);
            if (GUI.Button(EditorGUI.IndentedRect(EditorGUILayout.GetControlRect()), "Clear Settings"))
                _settingsOrGroups.arraySize = 0;
            _serialized.ApplyModifiedProperties();
            EditorGUILayout.EndScrollView();

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
            GUILayout.Label("Errors from Previous Build:", EditorStyles.boldLabel);
            errorsScroll = EditorGUILayout.BeginScrollView(errorsScroll);
            if (progressAsset == null || progressAsset.uploadErrors.Count == 0) GUILayout.Label("No Errors");
            else
            {
                foreach (var previousUploadError in progressAsset.uploadErrors)
                {
                    EditorGUILayout.ObjectField("Uploading", previousUploadError.uploadingAvatar,
                        typeof(AvatarUploadSetting), false);
                    EditorGUILayout.EnumPopup("For", previousUploadError.targetPlatform);
                    EditorGUILayout.TextArea(previousUploadError.message);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        [Flags]
        internal enum UploadCheckResult
        {
            Ok = 0,
            Uploading = 1 << 0,
            NoDescriptors = 1 << 1,
            AnyNull = 1 << 2,
            PlayMode = 1 << 3,
            NoCredentials = 1 << 4,
            ControlPanelClosed = 1 << 5,
            NoAvatarBuilder = 1 << 6,
            PlayModeSettingsNotGood = 1 << 7,
        }

        private UploadCheckResult CheckUpload()
        {
            var result = UploadCheckResult.Ok;
            if (_guiState != State.Configuring) result |= UploadCheckResult.Uploading;
            if (settingsOrGroups.Length == 0) result |= UploadCheckResult.NoDescriptors;
            if (settingsOrGroups.Any(x => !x)) result |= UploadCheckResult.AnyNull;
            if (EditorApplication.isPlayingOrWillChangePlaymode) result |= UploadCheckResult.PlayMode;
            if (!Uploader.VerifyCredentials(Repaint)) result |= UploadCheckResult.NoCredentials;
            if (!VRCSdkControlPanel.window) result |= UploadCheckResult.ControlPanelClosed;
            if (!VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out _)) result |= UploadCheckResult.NoAvatarBuilder;
            if (!CheckPlaymodeSettings()) result |= UploadCheckResult.PlayModeSettingsNotGood;
            return result;
        }

        internal bool StartUpload()
        {
            if (CheckUpload() != UploadCheckResult.Ok) return false;
            DoStartUpload();
            return true;
        }

        private bool CheckPlaymodeSettings()
        {
            if (Utils.ReloadDomainDisabled())
                return true;

            if (Preferences.TakeThumbnailInPlaymodeByDefault)
                return false;

            foreach (var avatarUploadSetting in GetUploadingAvatars())
            {
                var currentInfo = avatarUploadSetting.GetCurrentPlatformInfo();
                if (currentInfo.updateImage)
                {
                    bool enterPlaymode;
                    switch (currentInfo.imageTakeEditorMode)
                    {
                        case ImageTakeEditorMode.UseUploadGuiSetting:
                            enterPlaymode = Preferences.TakeThumbnailInPlaymodeByDefault;
                            break;
                        case ImageTakeEditorMode.InEditMode:
                            enterPlaymode = false;
                            break;
                        case ImageTakeEditorMode.InPlayMode:
                            enterPlaymode = true;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    if (enterPlaymode)
                        return false;
                }
            }

            // there are need to disable reload domain
            return true;
        }

        private IEnumerable<AvatarUploadSetting> GetUploadingAvatars() =>
            settingsOrGroups
                .Where(x => x)
                .SelectMany(x => x.Settings)
                .Where(x => x);

        private void DoStartUpload()
        {
            var progress = ScriptableObject.CreateInstance<UploaderProgressAsset>();
            progress.openedScenes = UploadOrchestrator.GetLastOpenedScenes();
            progress.uploadSettings = GetUploadingAvatars().ToArray();
            progress.targetPlatforms = new[] { Uploader.GetCurrentTargetPlatform() };
            progress.sleepMilliseconds = (int)(Preferences.SleepSeconds * 1000);
            UploadOrchestrator.StartUpload(progress);
        }

        enum State
        {
            Configuring,

            // upload avatar process
            UploadingAvatar,
            UploadedAvatar,
        }
    }
}
