using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.Core;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3A.Editor;
using VRC.SDKBase.Editor;
using VRC.SDKBase.Editor.Api;
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

        public async void StartContinuousUpload(int sleepMilliseconds, AvatarUploadSetting[] avatars)
        {
            if (state != State.Idle) throw new InvalidOperationException("Cannot start upload in non idle state");
            uploadingAvatars = avatars;
            this.sleepMilliseconds = sleepMilliseconds;
            await Upload(default);
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
        }

        private const string PrefabScenePath = "Assets/com.anatawa12.continuous-avatar-uploader-uploading-prefab.unity";

        enum State
        {
            Idle,
            Abort,
        }

        // Not workings:
        // - new avatar upload
        // - update thumbnail
        // Workings:
        // - simple upload
        // - update description

        // INPUT VARIABLE
        [SerializeField] State state;
        [SerializeField] AvatarUploadSetting[] uploadingAvatars = Array.Empty<AvatarUploadSetting>();
        [SerializeField] int sleepMilliseconds;

        // LOCAL VARIABLES
        [SerializeField] AvatarUploadSetting uploadingAvatar;

        private async Task Upload(CancellationToken cancellationToken)
        {
            VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out var builder);
            if (EditorApplication.isPlaying) throw new Exception("Playmode"); // TODO
            if (builder.UploadState != SdkUploadState.Idle) throw new Exception("Invalid State"); // TODO

            AssetDatabase.SaveAssets();
            var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
                .ToArray();
            if (scenes.Any(x => x.isDirty))
                EditorSceneManager.SaveOpenScenes();
            var scenePaths = scenes.Select(x => x.path).ToArray();
            var lastOpenedScenes = scenePaths.Any(string.IsNullOrEmpty) ? Array.Empty<string>() : scenePaths;

            for (var processingIndex = 0;processingIndex < uploadingAvatars.Length; processingIndex++)
            {
                var avatar = uploadingAvatars[processingIndex];
                Debug.Log($"Upload started for {avatar.name}");

                var avatarDescriptor = LoadAvatar(avatar);

                if (!avatarDescriptor)
                {
                    Debug.LogError("Upload failed: avatar not found", avatar);
                    continue;
                }

                Debug.Log($"Actual avatar name: {avatarDescriptor.name}");

                uploadingAvatar = avatar;
                var oldEnabled = avatarDescriptor.gameObject.activeSelf;
                avatarDescriptor.gameObject.SetActive(true);

                await Task.Delay(100, cancellationToken);

                var pipelineManager = PreparePipelineManager(avatarDescriptor.gameObject);

                VRCAvatar vrcAvatar = default;
                try
                {
                    vrcAvatar = await VRCApi.GetAvatar(pipelineManager.blueprintId, true, cancellationToken);
                }
                catch (ApiErrorException ex)
                {
                    if (ex.StatusCode != HttpStatusCode.NotFound)
                        throw new Exception("Unknown error");
                }

                if (string.IsNullOrEmpty(vrcAvatar.ID))
                    throw new Exception("Uploading new avatar");

                if (APIUser.CurrentUser == null || vrcAvatar.AuthorId != APIUser.CurrentUser?.id)
                    throw new Exception("Uploading other user avatar.");

                await builder.BuildAndUpload(avatarDescriptor.gameObject, vrcAvatar, cancellationToken: cancellationToken);

                var platformInfo = avatar.GetCurrentPlatformInfo();

                if (platformInfo.versioningEnabled)
                {
                    // get uploaded avatar info
                    vrcAvatar = await VRCApi.GetAvatar(pipelineManager.blueprintId, forceRefresh: true,
                        cancellationToken);

                    // update description
                    long versionName;
                    (vrcAvatar.Description, versionName) =
                        UpdateVersionName(vrcAvatar.Description, platformInfo.versionNamePrefix);

                    await VRCApi.UpdateAvatarInfo(vrcAvatar.ID, vrcAvatar, cancellationToken: cancellationToken);

                    if (platformInfo.gitEnabled)
                    {
                        var tagName = platformInfo.tagPrefix + versionName + platformInfo.tagSuffix;
                        AddGitTag(tagName);
                    }
                }

                if (uploadingAvatar.avatarDescriptor.IsAssetReference())
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene); // without saving anything
                    AssetDatabase.DeleteAsset(PrefabScenePath);
                }
                else
                {
                    avatarDescriptor.gameObject.gameObject.SetActive(oldEnabled);
                }

                uploadingAvatar = null;
                await Task.Delay(sleepMilliseconds, cancellationToken);
            }

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
        }

        private VRCAvatarDescriptor LoadAvatar(AvatarUploadSetting avatar)
        {
            VRCAvatarDescriptor avatarDescriptor;

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

            return avatarDescriptor;
        }

        private PipelineManager PreparePipelineManager(GameObject gameObject)
        {
            var pipelineManager = gameObject.GetComponent<PipelineManager>();
            if (!pipelineManager)
                pipelineManager = gameObject.AddComponent<PipelineManager>();

            if (string.IsNullOrEmpty(pipelineManager.blueprintId))
            {
                // pipelineManager.AssignId() doesn't mark pipeline manager dirty
                pipelineManager.AssignId();
                EditorUtility.SetDirty(pipelineManager);
                EditorSceneManager.SaveScene(pipelineManager.gameObject.scene);
            }

            return pipelineManager;
        }

        private State Sleep(int milliseconds, State nextState)
        {
            async void Waiter()
            {
                await Task.Delay(milliseconds);
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
