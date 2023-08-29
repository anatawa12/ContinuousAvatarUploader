using System;
using System.Linq;
using System.Threading;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRC.SDK3A.Editor;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    public class ContinuousAvatarUploader : EditorWindow
    {
        [SerializeField] AvatarUploadSetting[] avatarSettings = Array.Empty<AvatarUploadSetting>();
        [SerializeField] AvatarUploadSettingGroup[] groups = Array.Empty<AvatarUploadSettingGroup>();

        [Tooltip("The time sleeps between upload")] [SerializeField]
        public float sleepSeconds = 3;

        // for uploading avatars
        [SerializeField] private UploadProcess process = new UploadProcess();

        private SerializedObject _serialized;
        private SerializedProperty _avatarDescriptor;
        private SerializedProperty _groups;
        private SerializedProperty _sleepSeconds;

        [MenuItem("Window/Continuous Avatar Uploader")]
        public static void OpenWindow() => GetWindow<ContinuousAvatarUploader>("ContinuousAvatarUploader");

        private void OnEnable()
        {
            _serialized = new SerializedObject(this);
            _avatarDescriptor = _serialized.FindProperty(nameof(avatarSettings));
            _groups = _serialized.FindProperty(nameof(groups));
            _sleepSeconds = _serialized.FindProperty(nameof(sleepSeconds));
        }

        private void OnGUI()
        {
            var uploadInProgress = process.IsInProgress();
            if (uploadInProgress)
            {
                GUILayout.Label("UPLOAD IN PROGRESS");
                if (process.UploadingAvatar)
                {
                    EditorGUILayout.ObjectField("Uploading", process.UploadingAvatar, typeof(AvatarUploadSetting),
                        true);
                }
                else
                {
                    GUILayout.Label("Sleeping a little");
                }

                if (GUILayout.Button("ABORT UPLOAD"))
                    process.Abort();
            }

            EditorGUI.BeginDisabledGroup(uploadInProgress);
            _serialized.Update();
            EditorGUILayout.PropertyField(_avatarDescriptor);
            EditorGUILayout.PropertyField(_groups);
            EditorGUILayout.PropertyField(_sleepSeconds);
            _serialized.ApplyModifiedProperties();

            var noDescriptors = avatarSettings.Length == 0 && groups.Length == 0;
            var anyNull = avatarSettings.Any(x => !x);
            var anyGroupNull = groups.Any(x => !x);
            var playMode = !uploadInProgress && EditorApplication.isPlayingOrWillChangePlaymode;
            var noCredentials = !VerifyCredentials(Repaint);
            if (noDescriptors) EditorGUILayout.HelpBox("No AvatarDescriptors are specified", MessageType.Error);
            if (anyNull) EditorGUILayout.HelpBox("Some AvatarDescriptor is None", MessageType.Error);
            if (anyGroupNull) EditorGUILayout.HelpBox("Some AvatarDescriptor Group is None", MessageType.Error);
            if (playMode) EditorGUILayout.HelpBox("To upload avatars, exit Play mode", MessageType.Error);
            if (noCredentials) EditorGUILayout.HelpBox("Please login in control panel", MessageType.Error);
            using (new EditorGUI.DisabledScope(noDescriptors || anyNull || anyGroupNull || playMode || noCredentials))
            {
                if (GUILayout.Button("Start Upload"))
                {
                    var uploadingAvatars = groups.Length == 0
                        ? avatarSettings
                        : avatarSettings.Concat(groups.SelectMany(x => x.avatars)).ToArray();
                    process.StartContinuousUpload((int)(sleepSeconds * 1000), uploadingAvatars);
                    EditorUtility.SetDirty(this);
                }
            }

            EditorGUI.EndDisabledGroup();
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

    [Serializable]
    sealed class UploadProcess
    {
        public AvatarUploadSetting UploadingAvatar => uploadingAvatar;

        public bool IsInProgress() => state != State.Idle;

        public void Abort()
        {
            _cancellationToken.Cancel();
        }

        public async void StartContinuousUpload(int sleepMilliseconds, AvatarUploadSetting[] avatars)
        {
            if (state != State.Idle) throw new InvalidOperationException("Cannot start upload in non idle state");

            if (!VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out var builder))
                throw new Exception("BuilderPanel not found");

            _cancellationToken = new CancellationTokenSource();

            try
            {
                await Uploader.Upload(builder, sleepMilliseconds, avatars,
                    onStartUpload: avatar => uploadingAvatar = avatar,
                    onFinishUpload: avatar => uploadingAvatar = null,
                    cancellationToken: _cancellationToken.Token);
            }
            catch (OperationCanceledException c) when (c.CancellationToken == _cancellationToken.Token)
            {
                // cancelled
            }
        }

        enum State
        {
            Idle,
            Abort,
        }

        private CancellationTokenSource _cancellationToken;

        // INPUT VARIABLE
        [SerializeField] State state;

        // LOCAL VARIABLES
        [SerializeField] AvatarUploadSetting uploadingAvatar;
    }
}
