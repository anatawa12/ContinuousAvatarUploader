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
        [SerializeField] [ItemCanBeNull] [NotNull] AvatarUploadSetting[] avatarSettings = Array.Empty<AvatarUploadSetting>();
        [SerializeField] [ItemCanBeNull] [NotNull] AvatarUploadSettingGroup[] groups = Array.Empty<AvatarUploadSettingGroup>();

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
        private SerializedProperty _avatarDescriptor;
        private SerializedProperty _groups;

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
            _avatarDescriptor = _serialized.FindProperty(nameof(avatarSettings));
            _groups = _serialized.FindProperty(nameof(groups));
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
            EditorGUILayout.PropertyField(_avatarDescriptor);
            if (GUI.Button(EditorGUI.IndentedRect(EditorGUILayout.GetControlRect()), "Clear Avatars"))
                _avatarDescriptor.arraySize = 0;
            EditorGUILayout.PropertyField(_groups);
            if (GUI.Button(EditorGUI.IndentedRect(EditorGUILayout.GetControlRect()), "Clear Groups"))
                _groups.arraySize = 0;
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

            var noDescriptors = avatarSettings.Length == 0 && groups.Length == 0;
            var anyNull = avatarSettings.Any(x => !x);
            var anyGroupNull = groups.Any(x => !x);
            var playMode = !uploadInProgress && EditorApplication.isPlayingOrWillChangePlaymode;
            var noCredentials = !VerifyCredentials(Repaint);
            var openControlPanel = !VRCSdkControlPanel.window;
            var noAvatarBuilder = !openControlPanel && _builder == null && !VRCSdkControlPanel.TryGetBuilder(out _builder);
            var playModeSettingsNotGood = !CheckPlaymodeSettings();
            if (noDescriptors) EditorGUILayout.HelpBox("No AvatarUploadSettings are specified", MessageType.Error);
            if (anyNull) EditorGUILayout.HelpBox("Some AvatarUploadSetting is None", MessageType.Error);
            if (anyGroupNull) EditorGUILayout.HelpBox("Some AvatarUploadSettingGroup is None", MessageType.Error);
            if (playMode) EditorGUILayout.HelpBox("To upload avatars, exit Play mode", MessageType.Error);
            if (noCredentials) EditorGUILayout.HelpBox("Please login in control panel", MessageType.Error);
            if (openControlPanel) EditorGUILayout.HelpBox("Please open Control panel", MessageType.Error);
            if (noAvatarBuilder) EditorGUILayout.HelpBox("No Valid VRCSDK Avatars Found", MessageType.Error);
            if (playModeSettingsNotGood)
                EditorGUILayout.HelpBox(
                    "Some avatars are going or take thumbnail in PlayMode. " +
                    "To take thumbnail in PlayMode, Please Disable 'Reload Domain' Option in " +
                    "Enter Play Mode Settings in Editor in Project Settings",
                    MessageType.Error);
            using (new EditorGUI.DisabledScope(noDescriptors || anyNull || anyGroupNull || playMode || noCredentials || openControlPanel || noAvatarBuilder || playModeSettingsNotGood))
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
            avatarSettings.Concat(groups.SelectMany(x =>
                x != null ? x.avatars : Array.Empty<AvatarUploadSetting>()));

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
