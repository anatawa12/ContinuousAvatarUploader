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

        internal bool Uploading => _guiState != State.Configuring;

        // for uploading avatars

        [NonSerialized] private State _guiState;
        [NonSerialized] private AvatarUploadSetting _currentUploadingAvatar;
        [SerializeField] private List<UploadErrorInfo> previousUploadErrors = new List<UploadErrorInfo>();
        [SerializeField] private Vector2 errorsScroll;

        [Serializable]
        private struct UploadErrorInfo
        {
            public string message;
            public AvatarUploadSetting uploadingAvatar;
        }

        private SerializedObject _serialized;
        private SerializedProperty _settingsOrGroups;

        private CancellationTokenSource _cancellationToken;
        private IVRCSdkAvatarBuilderApi _builder = null;
        private int _totalCount;
        private int _uploadingIndex;

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
        }

        private void OnDisable()
        {
            VRCSdkControlPanel.OnSdkPanelEnable -= OnSdkPanelEnableDisable;
            VRCSdkControlPanel.OnSdkPanelDisable -= OnSdkPanelEnableDisable;
        }

        private void OnSdkPanelEnableDisable(object sender, EventArgs e) => Repaint();

        private void OnGUI()
        {
            var uploadInProgress = _guiState != State.Configuring;
            if (uploadInProgress)
            {
                GUILayout.Label("UPLOAD IN PROGRESS");
                EditorGUI.ProgressBar(GUILayoutUtility.GetRect(100, 20),
                    (_uploadingIndex + 0.5f) / _totalCount,
                    $"Uploading {_uploadingIndex + 1} / {_totalCount}");
                if (_currentUploadingAvatar)
                {
                    EditorGUILayout.ObjectField("Uploading", _currentUploadingAvatar, typeof(AvatarUploadSetting), true);
                }
                else
                {
                    GUILayout.Label("Sleeping a little");
                }

                if (GUILayout.Button("ABORT UPLOAD"))
                    _cancellationToken?.Cancel();
            }

            EditorGUI.BeginDisabledGroup(uploadInProgress);
            _serialized.Update();
            EditorGUILayout.PropertyField(_settingsOrGroups);
            if (GUI.Button(EditorGUI.IndentedRect(EditorGUILayout.GetControlRect()), "Clear Groups"))
                _settingsOrGroups.arraySize = 0;
            _serialized.ApplyModifiedProperties();
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
                    StartUpload(_builder);
            }

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
            GUILayout.Label("Errors from Previous Build:", EditorStyles.boldLabel);
            errorsScroll = EditorGUILayout.BeginScrollView(errorsScroll);
            if (previousUploadErrors.Count == 0) GUILayout.Label("No Errors");
            else
            {
                foreach (var previousUploadError in previousUploadErrors)
                {
                    EditorGUILayout.ObjectField("Uploading", previousUploadError.uploadingAvatar,
                        typeof(AvatarUploadSetting), false);
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
            if (!VerifyCredentials(Repaint)) result |= UploadCheckResult.NoCredentials;
            if (!VRCSdkControlPanel.window) result |= UploadCheckResult.ControlPanelClosed;
            if (!VRCSdkControlPanel.TryGetBuilder(out _builder)) result |= UploadCheckResult.NoAvatarBuilder;
            if (!CheckPlaymodeSettings()) result |= UploadCheckResult.PlayModeSettingsNotGood;
            return result;
        }

        internal bool StartUpload()
        {
            if (CheckUpload() != UploadCheckResult.Ok) return false;
            StartUpload(_builder);
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

        private async void StartUpload(IVRCSdkAvatarBuilderApi builder)
        {
            _cancellationToken = new CancellationTokenSource();
            previousUploadErrors.Clear();

            try
            {
                _guiState = State.PreparingAvatar;
                var uploadingAvatars = GetUploadingAvatars().ToArray();
                _totalCount = uploadingAvatars.Length;
                await Uploader.Upload(builder,
                    sleepMilliseconds: (int)(Preferences.SleepSeconds * 1000),
                    uploadingAvatars: uploadingAvatars,
                    onStartUpload: (avatar, index) =>
                    {
                        _guiState = State.UploadingAvatar;
                        _currentUploadingAvatar = avatar;
                        _uploadingIndex = index;
                    },
                    onException: (exception, avatar) =>
                    {
                        previousUploadErrors.Add(new UploadErrorInfo
                        {
                            uploadingAvatar = avatar,
                            message = exception.ToString()
                        });
                    },
                    onFinishUpload: avatar =>
                    {
                        _guiState = State.UploadedAvatar;
                        _currentUploadingAvatar = null;
                    },
                    cancellationToken: _cancellationToken.Token);
            }
            catch (OperationCanceledException c) when (c.CancellationToken == _cancellationToken.Token)
            {
                // cancelled
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                previousUploadErrors.Add(new UploadErrorInfo { message = exception.ToString() });
            }
            finally
            {
                _guiState = State.Configuring;
            }

            if (Preferences.ShowDialogWhenUploadFinished)
                EditorUtility.DisplayDialog("Continuous Avatar Uploader", "Finished Uploading Avatars!", "OK");
        }

        enum State
        {
            Configuring,

            // upload avatar process
            PreparingAvatar,
            UploadingAvatar,
            UploadedAvatar,
        }

        public static bool VerifyCredentials(Action onSuccess = null)
        {
            if (!ConfigManager.RemoteConfig.IsInitialized())
            {
                API.SetOnlineMode(true, "vrchat");
                ConfigManager.RemoteConfig.Init();
            }
            if (!APIUser.IsLoggedIn && ApiCredentials.Load())
                APIUser.InitialFetchCurrentUser(c =>
                {
                    AnalyticsSDK.LoggedInUserChanged(c.Model as APIUser);
                    onSuccess?.Invoke();
                }, null);
            return APIUser.IsLoggedIn;
        }
    }
}
