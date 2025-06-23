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
        [SerializeField] private List<UploadErrorInfo> uploadErrors;

        private UploaderProgressAsset progressAsset;

        private SerializedObject _serialized;
        private SerializedProperty _settingsOrGroups;

        [MenuItem("Window/Continuous Avatar Uploader")]
        [MenuItem("Tools/Continuous Avatar Uploader")]
        private static void OpenWindowItem() => OpenWindow();
        public static ContinuousAvatarUploader OpenWindow() => GetWindow<ContinuousAvatarUploader>("ContinuousAvatarUploader");

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
            var loaded = UploaderProgressAsset.Load();
            progressAsset = loaded != null ? loaded : progressAsset;
            progressAsset = progressAsset == null ? null : progressAsset;
            uploadErrors = progressAsset?.uploadErrors ?? uploadErrors;
            var uploadInProgress = progressAsset != null;
            if (uploadInProgress)
            {
                var totalCount = progressAsset.uploadSettings.Length;
                var uploadingIndex = progressAsset.uploadingAvatarIndex;
                var totalPlatforms = progressAsset.targetPlatforms.Length;
                var uploadingTargetCount = progressAsset.uploadFinishedPlatforms.Length;
                GUILayout.Label("UPLOAD IN PROGRESS");
                EditorGUI.ProgressBar(GUILayoutUtility.GetRect(100, 20),
                    (uploadingTargetCount + 0.5f) / totalPlatforms,
                    $"Uploading for {progressAsset.uploadingTargetPlatform} ({uploadingTargetCount + 1} / {totalPlatforms} platforms)");
                EditorGUI.ProgressBar(GUILayoutUtility.GetRect(100, 20),
                    (uploadingIndex + 0.5f) / totalCount,
                    $"Uploading {uploadingIndex + 1} / {totalCount} for {progressAsset.uploadingTargetPlatform}");
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
            Preferences.RollbackBuildPlatform = EditorGUILayout.ToggleLeft(
                new GUIContent("Rollback Build Platform", 
                    "If this is enabled, CAU will rollback the build platform to the one before upload after upload finished."),
                Preferences.RollbackBuildPlatform);
            Preferences.RetryCount = EditorGUILayout.IntField(
                new GUIContent("Retry Count", "The number of retries to attempt for each upload. Zero means no retries, so only one attempt will be made."),
                Preferences.RetryCount);

            EditorGUILayout.LabelField("Target Platforms", EditorStyles.boldLabel);
            foreach (var platform in Uploader.GetTargetPlatforms())
            {
                var isEnabled = Preferences.UploadFor(platform);
                var supported = Uploader.IsBuildSupportedInstalled(platform);
                EditorGUI.BeginDisabledGroup(!supported && !isEnabled);
                isEnabled = EditorGUILayout.ToggleLeft(
                    new GUIContent($"Upload for {platform}", 
                        supported ? $"If this is enabled, CAU will upload avatars for {platform} platform."
                            : $"Build support for {platform} is not installed. "),
                    isEnabled);
                EditorGUI.EndDisabledGroup();
                Preferences.SetUploadFor(platform, isEnabled);
            }

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
            if ((checkResult & UploadCheckResult.UnsupportedPlatformSelected) != 0)
                EditorGUILayout.HelpBox(
                    "Some target platforms are selected, but not supported by current build. " +
                    "Please install the build support for those platforms in Unity Hub, or " +
                    "uncheck the target platforms in Continuous Avatar Uploader settings.",
                    MessageType.Error);
            if ((checkResult & UploadCheckResult.NoPlatformsSelected) != 0)
                EditorGUILayout.HelpBox(
                    "No target platforms are selected. " +
                    "Please select at least one target platform in Continuous Avatar Uploader settings.",
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
            if (uploadErrors.Count == 0) GUILayout.Label("No Errors");
            else
            {
                foreach (var previousUploadError in uploadErrors)
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
            UnsupportedPlatformSelected = 1 << 8,
            NoPlatformsSelected = 1 << 9,
        }

        private UploadCheckResult CheckUpload() => CheckUploadStatic(GetUploadingAvatars(), Repaint);

        private static UploadCheckResult CheckUploadStatic(IEnumerable<AvatarUploadSetting> avatars, [CanBeNull] Action repaint = null)
        {
            var result = UploadCheckResult.Ok;
            if (UploadOrchestrator.IsUploadInProgress()) result |= UploadCheckResult.Uploading;
            if (EditorApplication.isPlayingOrWillChangePlaymode) result |= UploadCheckResult.PlayMode;
            if (!Uploader.VerifyCredentials(repaint)) result |= UploadCheckResult.NoCredentials;
            if (!VRCSdkControlPanel.window) result |= UploadCheckResult.ControlPanelClosed;
            if (!VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out _)) result |= UploadCheckResult.NoAvatarBuilder;
            if (!CheckPlaymodeSettings(avatars)) result |= UploadCheckResult.PlayModeSettingsNotGood;

            foreach (var platform in Uploader.GetTargetPlatforms())
            {
                if (!Uploader.IsBuildSupportedInstalled(platform) && Preferences.UploadFor(platform))
                    result |= UploadCheckResult.UnsupportedPlatformSelected;
            }

            if (!Uploader.GetTargetPlatforms().Any(Preferences.UploadFor))
                result |= UploadCheckResult.NoPlatformsSelected;

            return result;
        }

        internal bool StartUpload()
        {
            if (CheckUpload() != UploadCheckResult.Ok) return false;
            DoStartUpload();
            return true;
        }

        private static bool CheckPlaymodeSettings(IEnumerable<AvatarUploadSetting> avatars)
        {
            if (Utils.ReloadDomainDisabled())
                return true;

            if (Preferences.TakeThumbnailInPlaymodeByDefault)
                return false;

            foreach (var avatarUploadSetting in avatars)
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
            progress.targetPlatforms = Uploader.GetTargetPlatforms().Where(Preferences.UploadFor).ToArray();
            progress.sleepMilliseconds = (int)(Preferences.SleepSeconds * 1000);
            progress.rollbackPlatform = Preferences.RollbackBuildPlatform;
            progress.retryCount = Preferences.RetryCount;
            UploadOrchestrator.StartUpload(progress);
        }

        enum State
        {
            Configuring,

            // upload avatar process
            UploadingAvatar,
            UploadedAvatar,
        }

        static class UploadButtonGuiStyles
        {
            public static GUIContent label = new("Upload This Avatar",
                "Upload this avatar to the current target platform. " +
                "If you want to upload multiple avatars, use the Continuous Avatar Uploader window.");
        }

        public static void UploadButtonGui(IEnumerable<AvatarUploadSettingOrGroup> avatarOrGroups, [CanBeNull] Action repaint = null)
        {
            var avatars = avatarOrGroups
                .Where(x => x)
                .SelectMany(x => x.Settings)
                .Where(x => x)
                .ToArray();
            var check = CheckUploadStatic(avatars, repaint);

            // target platform selector
            var flags = FlagsForCurrentBuildPlatforms();
            EditorGUI.BeginChangeCheck();
            flags = (TargetPlatformFlags)EditorGUILayout.EnumFlagsField("Target Platforms", flags);
            if (EditorGUI.EndChangeCheck()) SetBuildPlatforms(flags);

            EditorGUI.BeginDisabledGroup(check != UploadCheckResult.Ok);
            var guiContent = UploadButtonGuiStyles.label;
            guiContent.text = avatars.Length == 1 ? "Upload This Avatar" : $"Upload {avatars.Length} Avatars";
            guiContent.tooltip = check == UploadCheckResult.Ok ? "" : "Cannot upload avatars now. Check the Continuous Avatar Uploader window for details.";
            if (GUILayout.Button(guiContent))
            {
                var uploader = OpenWindow();
                uploader.settingsOrGroups = avatars.ToArray<AvatarUploadSettingOrGroup>();
                if (!uploader.StartUpload())
                {
                    EditorUtility.DisplayDialog("Failed to start upload",
                        "Failed to start upload.\nPlease refer Uploader window for reason", "OK");
                }
            }
            EditorGUI.EndDisabledGroup();
        }

        private static TargetPlatformFlags FlagsForCurrentBuildPlatforms()
        {
            TargetPlatformFlags flags = 0;
            foreach (var platform in Uploader.GetTargetPlatforms())
            {
                if (Preferences.UploadFor(platform))
                {
                    flags |= (TargetPlatformFlags)(1 << (int)platform);
                }
            }
            return flags;
        }

        private static void SetBuildPlatforms(TargetPlatformFlags flags)
        {
            foreach (var platform in Uploader.GetTargetPlatforms())
            {
                var flag = (TargetPlatformFlags)(1 << (int)platform);
                Preferences.SetUploadFor(platform, (flags & flag) != 0);
            }
        }

        [Flags]
        enum TargetPlatformFlags
        {
            Windows = 1 << (int)TargetPlatform.Windows,
            Android = 1 << (int)TargetPlatform.Android,
            iOS = 1 << (int)TargetPlatform.iOS,
        }
    }
}
