using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.Core;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor;
using VRCSDK2;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

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
            process.OnEnable();
            process.Repaint += Repaint;
        }

        private void OnDisable()
        {
            process.Repaint -= Repaint;
            process.OnDisable();
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

    /*
     * Logic to trigger avatar build us based on Avatar Phalanx which is published under MIT license.
     * https://gist.github.com/pimaker/02d0dafe7e424a6ac198e2442bb66ac7
     * Copyright (c) 2022 @pimaker on GitHub
     */
    [Serializable]
    sealed class UploadProcess
    {
        public event Action Repaint;

        public AvatarUploadSetting UploadingAvatar => uploadingAvatar;

        public bool IsInProgress() => state != State.Idle;

        public void Abort()
        {
            state = State.Abort;
        }

        public void StartContinuousUpload(int sleepMilliseconds, AvatarUploadSetting[] avatars)
        {
            if (state != State.Idle) throw new InvalidOperationException("Cannot start upload in non idle state");
            uploadingAvatars = avatars;
            this.sleepMilliseconds = sleepMilliseconds;
            state = State.StartingContinuousUpload;
        }

        public void OnEnable()
        {
            EditorApplication.update += OnUpdate;
        }

        public void OnDisable()
        {
            EditorApplication.update -= OnUpdate;
        }

        private void OnUpdate()
        {
            var oldState = state;
            try
            {
                state = OnUpdateImpl(oldState);
                if (state != oldState)
                    Repaint?.Invoke();
            }
            catch
            {
                Debug.LogError("Aborting the build process because of error.");
                state = State.Abort;
                throw;
            }
        }

        private const string PrefabScenePath = "Assets/com.anatawa12.continuous-avatar-uploader-uploading-prefab.unity";

        enum State
        {
            Idle,
            Abort,
            StartingContinuousUpload,

            StartingUploadAvatar,
            StartingUploadAvatarSleeping,
            StartingUploadAvatarSlept,
            StartingUploadAvatarWaitingLogin = StartingUploadAvatarSlept,

            ContinueToNextAvatar,

            ConfigurationScene,
            ConfigurationSceneWaitingRuntimeBlueprintCreation,
            ConfigurationSceneWaitingVrcSdkInitialization,
            ConfigurationSceneSleeping2500,
            ConfigurationSceneSlept2500,

            WaitingUploadFinish,

            SleepBetweenAvatar,
            SleptBetweenAvatar,

            FinishContinuousUpload,
            FinishContinuousUploadAbort,
        }

        // INPUT VARIABLE
        [SerializeField] State state;
        [SerializeField] AvatarUploadSetting[] uploadingAvatars = Array.Empty<AvatarUploadSetting>();
        [SerializeField] int sleepMilliseconds;

        // LOCAL VARIABLES
        [SerializeField] string[] lastOpenedScenes;
        [SerializeField] int processingIndex = -1;
        [SerializeField] AvatarUploadSetting uploadingAvatar;
        [SerializeField] bool oldEnabled;
        [SerializeField] VRCAvatarDescriptor avatarDescriptor;
        [SerializeField] RuntimeBlueprintCreation blueprintCreation;

        private State OnUpdateImpl(State state)
        {
            switch (state)
            {
                case State.Idle:
                    return State.Idle;
                case State.Abort:
                    if (EditorApplication.isPlaying)
                    {
                        EditorApplication.isPlaying = false;
                        return State.Abort;
                    }
                    else
                    {
                        uploadingAvatars = Array.Empty<AvatarUploadSetting>();
                        return State.FinishContinuousUploadAbort;
                    }

                case State.StartingContinuousUpload:
                {
                    if (EditorApplication.isPlaying) return state;
                    AssetDatabase.SaveAssets();
                    var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
                    if (scenes.Any(x => x.isDirty))
                        EditorSceneManager.SaveOpenScenes();
                    var scenePaths = scenes.Select(x => x.path).ToArray();
                    lastOpenedScenes = scenePaths.Any(string.IsNullOrEmpty) ? Array.Empty<string>() : scenePaths;

                    processingIndex = 0;
                    goto case State.StartingUploadAvatar;
                }

                //////// StartingUploadAvatar

                case State.StartingUploadAvatar:
                {
                    if (EditorApplication.isPlaying) return state;
                    var avatar = uploadingAvatars[processingIndex];
                    Debug.Log($"Upload started for {avatar.name}");

                    if (avatar.avatarDescriptor.IsAssetReference())
                    {
                        avatarDescriptor = avatar.avatarDescriptor.asset as VRCAvatarDescriptor;
                        if (avatarDescriptor)
                        {
                            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);
                            var newGameObject = Object.Instantiate(avatarDescriptor.gameObject);
                            avatarDescriptor = newGameObject.GetComponent<VRCAvatarDescriptor>();
                            EditorSceneManager.SaveScene(scene, PrefabScenePath);
                        }
                    }
                    else
                    {
                        // reference to scene
                        avatarDescriptor = avatar.avatarDescriptor.TryResolve() as VRCAvatarDescriptor;
                        if (!avatarDescriptor)
                        {
                            avatar.avatarDescriptor.OpenScene();
                            avatarDescriptor = avatar.avatarDescriptor.TryResolve() as VRCAvatarDescriptor;
                        }
                    }

                    if (!avatarDescriptor)
                    {
                        Debug.LogError("Upload failed: avatar not found", avatar);
                        goto case State.ContinueToNextAvatar;
                    }

                    Debug.Log($"Actual avatar name: {avatarDescriptor.name}");

                    uploadingAvatar = avatar;
                    oldEnabled = avatarDescriptor.gameObject.activeSelf;
                    avatarDescriptor.gameObject.SetActive(true);

                    return Sleep(100, State.StartingUploadAvatarSleeping);
                }
                case State.StartingUploadAvatarSleeping: return state;
                case State.StartingUploadAvatarWaitingLogin:
                {
                    if (EditorApplication.isPlaying) return state;
                    if (!ContinuousAvatarUploader.VerifyCredentials()) return state;
                    
                    // pipelineManager.AssignId() doesn't mark pipeline manager dirty
                    var pipelineManager = avatarDescriptor.gameObject.GetComponent<PipelineManager>();
                    if (pipelineManager && string.IsNullOrEmpty(pipelineManager.blueprintId))
                    {
                        pipelineManager.AssignId();
                        EditorUtility.SetDirty(pipelineManager);
                        EditorSceneManager.SaveScene(pipelineManager.gameObject.scene);
                    }

                    var successful = VRC_SdkBuilder.ExportAndUploadAvatarBlueprint(avatarDescriptor.gameObject);
                    return successful ? State.ConfigurationScene : State.ContinueToNextAvatar;
                }

                //////// ContinueToNextAvatar

                case State.ContinueToNextAvatar:
                {
                    if (EditorApplication.isPlaying) return state;
                    processingIndex++;
                    if (processingIndex < uploadingAvatars.Length) return State.StartingUploadAvatar;
                    return State.FinishContinuousUpload;
                }

                //////// WaitingConfigurationScene

                case State.ConfigurationScene:
                {
                    if (!EditorApplication.isPlaying) return state;
                    blueprintCreation = null;
                    return State.ConfigurationSceneWaitingRuntimeBlueprintCreation;
                }

                // wait for RuntimeBlueprintCreation
                case State.ConfigurationSceneWaitingRuntimeBlueprintCreation:
                {
                    if (!EditorApplication.isPlaying) return State.WaitingUploadFinish; // Abort current avatar.
                    blueprintCreation = Object.FindObjectOfType<RuntimeBlueprintCreation>();
                    if (!blueprintCreation)
                        return State.ConfigurationSceneWaitingRuntimeBlueprintCreation;
                    return State.ConfigurationSceneWaitingVrcSdkInitialization;
                }
                case State.ConfigurationSceneWaitingVrcSdkInitialization:
                {
                    if (!EditorApplication.isPlaying) return State.WaitingUploadFinish; // Abort current avatar.
                    if (blueprintCreation.titleText.text == "New Avatar Creation")
                        return State.ConfigurationSceneWaitingVrcSdkInitialization;
                    // finished sdk initialization

                    if (blueprintCreation.titleText.text == "New Avatar")
                    {
                        blueprintCreation.titleText.text = "New Avatar with Continuous Avatar Uploader";
                        return State.WaitingUploadFinish; // Waiting user first upload
                    }

                    // New Avatar Creation
                    blueprintCreation.titleText.text = "Upload Avatar using Continuous Avatar Uploader!";

                    var platformInfo = uploadingAvatar.GetCurrentPlatformInfo();
                    blueprintCreation.shouldUpdateImageToggle.isOn = platformInfo.updateImage;

                    return Sleep(2500, State.ConfigurationSceneSleeping2500);
                }
                case State.ConfigurationSceneSleeping2500: return state;
                case State.ConfigurationSceneSlept2500:
                {
                    if (!EditorApplication.isPlaying) return State.WaitingUploadFinish; // Abort current avatar.
                    var platformInfo = uploadingAvatar.GetCurrentPlatformInfo();
                    if (platformInfo.versioningEnabled)
                    {
                        long versionName;
                        (blueprintCreation.blueprintDescription.text, versionName) =
                            UpdateVersionName(blueprintCreation.blueprintDescription.text,
                                platformInfo.versionNamePrefix);
                        if (platformInfo.gitEnabled)
                        {
                            var tagName = platformInfo.tagPrefix + versionName + platformInfo.tagSuffix;
                            AddGitTag(tagName);
                        }
                    }

                    EditorPatcher.Patcher.DisplayDialog += args =>
                    {
                        if (args.Title == "VRChat SDK") args.Result = true;
                    };

                    blueprintCreation.SetupUpload();
                    return State.WaitingUploadFinish;
                }

                //////// WaitingUploadFinish
                case State.WaitingUploadFinish:
                {
                    if (EditorApplication.isPlaying) return state;
                    if (!avatarDescriptor)
                        avatarDescriptor = uploadingAvatar.avatarDescriptor.TryResolve() as VRCAvatarDescriptor;

                    if (uploadingAvatar.avatarDescriptor.IsAssetReference())
                    {
                        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene); // without saving anything
                        AssetDatabase.DeleteAsset(PrefabScenePath);
                    }
                    else if (avatarDescriptor)
                    {
                        avatarDescriptor.gameObject.gameObject.SetActive(oldEnabled);
                    }

                    uploadingAvatar = null;
                    return Sleep(sleepMilliseconds, State.SleepBetweenAvatar);
                }
                //////// SleepBetweenAvatar
                case State.SleepBetweenAvatar:
                    return state;
                case State.SleptBetweenAvatar:
                    if (EditorApplication.isPlaying) return state;
                    goto case State.ContinueToNextAvatar;

                case State.FinishContinuousUploadAbort:
                case State.FinishContinuousUpload:
                    if (EditorApplication.isPlaying) return state;
                    if (lastOpenedScenes.Length == 0)
                    {
                        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                    }
                    else
                    {
                        EditorSceneManager.OpenScene(lastOpenedScenes[0]);
                        foreach (var lastOpenedScene in lastOpenedScenes.Skip(1))
                            EditorSceneManager.OpenScene(lastOpenedScene, OpenSceneMode.Additive);
                    }

                    AssetDatabase.DeleteAsset(PrefabScenePath);
                    if (state == State.FinishContinuousUploadAbort)
                    {
                        EditorUtility.DisplayDialog("Continuous Avatar Uploader",
                            "Aborted the build process",
                            "OK");
                    }
                    return State.Idle;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        private State Sleep(int milliseconds, State nextState)
        {
            async void Waiter()
            {
                await System.Threading.Tasks.Task.Delay(milliseconds);
                if (nextState == state)
                    state++;
            }

            Waiter();
            return nextState;
        }

        private (string, long) UpdateVersionName(string description, string versionPrefix)
        {
            var escapedPrefix = Regex.Escape(versionPrefix);
            var regex = new Regex($@"[(（]{escapedPrefix}(\d+)[)）]");

            var match = regex.Match(description);
            if (match.Success)
            {
                var capture = match.Groups[1].Captures[0];
                if (long.TryParse(capture.Value, out var versionName))
                {
                    versionName += 1;
                    var prefix = description.Substring(0, capture.Index);
                    var suffix = description.Substring(capture.Index + capture.Length);
                    return (prefix + versionName + suffix, versionName);
                }
            }

            return (description + $" ({versionPrefix}1)", 1);
        }

        private void AddGitTag(string tagName)
        {
            try
            {
                // ArgumentList is not implemented in Unity 2019.
                using (var p = Process.Start("git", $"tag -- {EscapeForProcessArgument(tagName)}"))
                {
                    System.Diagnostics.Debug.Assert(p != null, nameof(p) + " != null");
                    p.WaitForExit();
                    System.Diagnostics.Debug.Assert(p.ExitCode == 0,
                        $"git command exit with non zer value: {p.ExitCode}");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(new Exception($"Tagging version {tagName} for {uploadingAvatar.name}", e));
            }

            string EscapeForProcessArgument(string argument)
            {
                if (!argument.Any(c => char.IsWhiteSpace(c) || c == '"'))
                    return argument;
                var builder = new StringBuilder();
                builder.Append('"');

                var idx = 0;
                while (idx < argument.Length)
                {
                    var c = argument[idx++];
                    switch (c)
                    {
                        case '\\':
                        {
                            int numBackSlash = 1;
                            while (idx < argument.Length && argument[idx] == '\\')
                            {
                                idx++;
                                numBackSlash++;
                            }

                            if (idx == argument.Length || argument[idx] == '"')
                                builder.Append('\\', numBackSlash * 2);
                            else
                                builder.Append('\\', numBackSlash);
                            break;
                        }
                        case '"':
                            builder.Append('\\').Append('"');
                            break;
                        default:
                            builder.Append(c);
                            break;
                    }
                }

                builder.Append('"');

                return builder.ToString();
            }
        }
    }
}
