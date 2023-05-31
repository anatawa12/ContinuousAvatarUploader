using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VRC.Core;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor;
using VRCSDK2;
using Debug = UnityEngine.Debug;
using Task = System.Threading.Tasks.Task;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    /*
     * Logic to trigger avatar build us based on Avatar Phalanx which is published under MIT license.
     * https://gist.github.com/pimaker/02d0dafe7e424a6ac198e2442bb66ac7
     * Copyright (c) 2022 @pimaker on GitHub
     */
    public class ContinuousAvatarUploader : EditorWindow
    {
        [SerializeField] State state;
        [SerializeField] AvatarDescriptor[] avatarDescriptors = Array.Empty<AvatarDescriptor>();
        [SerializeField] AvatarDescriptorGroup[] groups = Array.Empty<AvatarDescriptorGroup>();
        [Tooltip("The time sleeps between upload")]
        [SerializeField] float sleepSeconds = 3;

        // for uploading avatars
        [SerializeField] int processingIndex = -1;
        [SerializeField] AvatarDescriptor[] uploadingAvatars = Array.Empty<AvatarDescriptor>();
        [SerializeField] AvatarDescriptor uploadingAvatar;
        [SerializeField] bool oldEnabled;
        [SerializeField] bool sleeping;

        private SerializedObject _serialized;
        private SerializedProperty _avatarDescriptor;
        private SerializedProperty _groups;
        private SerializedProperty _sleepSeconds;

        [MenuItem("Window/Continuous Avatar Uploader")]
        public static void OpenWindow() => GetWindow<ContinuousAvatarUploader>("ContinuousAvatarUploader");

        private void OnEnable()
        {
            _serialized = new SerializedObject(this);
            _avatarDescriptor = _serialized.FindProperty(nameof(avatarDescriptors));
            _groups = _serialized.FindProperty(nameof(groups));
            _sleepSeconds = _serialized.FindProperty(nameof(sleepSeconds));
            EditorApplication.update += OnUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnUpdate;
        }

        private void OnGUI()
        {
            var uploadInProgress = uploadingAvatar || sleeping;
            if (uploadInProgress)
            {
                GUILayout.Label("UPLOAD IN PROGRESS");
                if (uploadingAvatar)
                {
                    EditorGUILayout.ObjectField("Uploading", uploadingAvatar, typeof(AvatarDescriptor), true);
                }
                else
                {
                    GUILayout.Label("Sleeping a little");
                }

                if (GUILayout.Button("ABORT UPLOAD"))
                    state = State.Aborting;
            }
            EditorGUI.BeginDisabledGroup(uploadInProgress);
            _serialized.Update();
            EditorGUILayout.PropertyField(_avatarDescriptor);
            EditorGUILayout.PropertyField(_groups);
            EditorGUILayout.PropertyField(_sleepSeconds);
            _serialized.ApplyModifiedProperties();

            var noDescriptors = avatarDescriptors.Length == 0 && groups.Length == 0;
            var anyNull = avatarDescriptors.Any(x => !x);
            var anyGroupNull = groups.Any(x => !x);
            var playMode = !uploadingAvatar && EditorApplication.isPlayingOrWillChangePlaymode;
            var noCredentials = !APIUser.IsLoggedIn;
            if (noDescriptors) EditorGUILayout.HelpBox("No AvatarDescriptors are specified", MessageType.Error);
            if (anyNull) EditorGUILayout.HelpBox("Some AvatarDescriptor is None", MessageType.Error);
            if (anyGroupNull) EditorGUILayout.HelpBox("Some AvatarDescriptor Group is None", MessageType.Error);
            if (playMode) EditorGUILayout.HelpBox("To upload avatars, exit Play mode", MessageType.Error);
            if (noCredentials) EditorGUILayout.HelpBox("Please login in control panel", MessageType.Error);
            using (new EditorGUI.DisabledScope(noDescriptors || anyNull || anyGroupNull || playMode || noCredentials))
            {
                if (GUILayout.Button("Start Upload"))
                {
                    if (groups.Length == 0)
                        uploadingAvatars = avatarDescriptors;
                    else
                        uploadingAvatars = avatarDescriptors.Concat(groups.SelectMany(x => x.avatars)).ToArray();
                    StartContinuousUpload();
                    EditorUtility.SetDirty(this);
                }
            }
            EditorGUI.EndDisabledGroup();
        }

        private void OnUpdate()
        {
            if (state == State.Idle) return;

            if (EditorApplication.isPlaying && state == State.WaitingForUpload)
            {
                state = State.Uploading;
                ConfigureSdkDisplayAndUpload();
            }
            if (!EditorApplication.isPlaying && state == State.Uploading)
            {
                ResetUploadingAvatar();
                if (processingIndex >= 0)
                {
                    ContinueUpload();
                }
            }

            if (state == State.Aborting)
            {
                if (EditorApplication.isPlaying)
                {
                    EditorApplication.isPlaying = false;
                }
                else
                {
                    uploadingAvatars = Array.Empty<AvatarDescriptor>();
                    processingIndex = -1;
                    sleeping = false;
                    if (uploadingAvatar)
                        ResetUploadingAvatar();
                    else
                        state = State.Idle;
                }
            }
        }

        private async void StartContinuousUpload()
        {
            processingIndex = 0;
            await UploadNext();
        }

        private async void ContinueUpload()
        {
            sleeping = true;
            await Task.Delay((int)(sleepSeconds * 1000));
            if (!sleeping) return; // aborted.
            sleeping = false;
            processingIndex++;
            await UploadNext();
        }

        private async Task UploadNext()
        {
            for (; processingIndex < uploadingAvatars.Length; processingIndex++)
            {
                // returns true means start upload successful.
                if (await Upload(uploadingAvatars[processingIndex]))
                    return;
                if (state == State.Aborting) return;
            }

            // done everything.

            uploadingAvatars = Array.Empty<AvatarDescriptor>();
            processingIndex = -1;
        }

        private async Task<bool> Upload(AvatarDescriptor avatar)
        {
            if (state != State.Idle)
                throw new InvalidOperationException($"Don't start upload while {state}");
            if (avatar == null) return false;
            if (!avatar.GetCurrentPlatformInfo().enabled)
            {
                Debug.LogWarning($"Skipping uploading {avatar} because it's disabled for current platform");
                return false;
            }
            AssetDatabase.SaveAssets();
            if (Enumerable.Range(0, EditorSceneManager.sceneCount).Any(x => EditorSceneManager.GetSceneAt(x).isDirty))
                EditorSceneManager.SaveOpenScenes();
            Debug.Log($"Upload started for {avatar.name}");

            VRCAvatarDescriptor avatarDescriptor;
            if (avatar.avatarDescriptor.IsAssetReference())
            {
                avatarDescriptor = avatar.avatarDescriptor.asset as VRCAvatarDescriptor;
                if (avatarDescriptor)
                {
                    var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                    var newGameObject = Instantiate(avatarDescriptor.gameObject);
                    avatarDescriptor = newGameObject.GetComponent<VRCAvatarDescriptor>();
                    EditorSceneManager.SaveScene(scene,
                        "Assets/com.anatawa12.continuous-avatar-uploader-uploading-prefab.unity");
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
                return false;
            }

            Debug.Log($"Actual avatar name: {avatarDescriptor.name}");

            state = State.Building;
            uploadingAvatar = avatar;
            oldEnabled = avatarDescriptor.gameObject.activeSelf;
            avatarDescriptor.gameObject.SetActive(true);

            try
            {
                // wait a while to show updates on the window
                await Task.Delay(100);
                if (state != State.Building) return false; // aborted
                
                // pipelineManager.AssignId() doesn't mark pipeline manager dirty
                var pipelineManager = avatarDescriptor.gameObject.GetComponent<PipelineManager>();
                if (pipelineManager && string.IsNullOrEmpty(pipelineManager.blueprintId))
                {
                    pipelineManager.AssignId();
                    EditorUtility.SetDirty(pipelineManager);
                }

                var successful = VRC_SdkBuilder.ExportAndUploadAvatarBlueprint(avatarDescriptor.gameObject);
                state = State.WaitingForUpload;
                if (!successful)
                    ResetUploadingAvatar(avatarDescriptor);

                return successful;
            }
            catch
            {
                ResetUploadingAvatar(avatarDescriptor);
                throw;
            }
        }

        private void ResetUploadingAvatar(VRCAvatarDescriptor avatarDescriptor = null)
        {
            if (uploadingAvatar == null) return;
            if (uploadingAvatar.avatarDescriptor.IsAssetReference())
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene); // without saving anything
            }
            else
            {
                if (!avatarDescriptor)
                    avatarDescriptor = uploadingAvatar.avatarDescriptor.TryResolve() as VRCAvatarDescriptor;

                if (avatarDescriptor)
                    avatarDescriptor.gameObject.gameObject.SetActive(oldEnabled);
            }

            uploadingAvatar = null;
            state = State.Idle;
        }

        private class AbortedException : Exception
        {
        }
        private async void ConfigureSdkDisplayAndUpload()
        {
            try
            {
                await ConfigureSdkDisplayAndUploadImpl();
            }
            catch (AbortedException)
            {
                Debug.LogError("[ContinuousAvatarUploader] Aborted upload process");
            }
        }
        async Task ConfigureSdkDisplayAndUploadImpl()
        {
            async Task Delay(int millisecondsDelay)
            {
                await Task.Delay(millisecondsDelay);
                if (state != State.Uploading || !EditorApplication.isPlaying) throw new AbortedException();
            }

            RuntimeBlueprintCreation creation = null;
            while (!creation)
            {
                creation = FindObjectOfType<RuntimeBlueprintCreation>();
                await Delay(10);
            }

            var titleText = creation.titleText;
            var descriptionField = creation.blueprintDescription;
            var uploadButton = creation.uploadButton;
            
            // this is not referenced by RuntimeBlueprintCreation so use path-based
            var warrantToggle = GameObject.Find("VRCSDK/UI/Canvas/AvatarPanel/Avatar Info Panel/Settings Section/Upload Section/ToggleWarrant")
                .GetComponent<Toggle>();

            while (titleText.text == "New Avatar Creation")
                await Delay(100);

            if (titleText.text == "New Avatar")
            {
                titleText.text = "New Avatar with Continuous Avatar Uploader";
                return;
            }

            // New Avatar Creation
            titleText.text = "Upload Avatar using Continuous Avatar Uploader!";

            await Delay(2500);

            var platformInfo = uploadingAvatar.GetCurrentPlatformInfo();

            if (platformInfo.versioningEnabled)
            {
                long versionName;
                (descriptionField.text, versionName) =
                    UpdateVersionName(descriptionField.text, platformInfo.versionNamePrefix);
                if (platformInfo.gitEnabled)
                {
                    var tagName = platformInfo.tagPrefix + versionName + platformInfo.tagSuffix;
                    AddGitTag(tagName);
                }
            }

            warrantToggle.isOn = true;

            EditorPatcher.Patcher.DisplayDialog += args =>
            {
                if (args.Title == "VRChat SDK") args.Result = true;
            };

            uploadButton.OnPointerClick(new PointerEventData(EventSystem.current));
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

        enum State
        {
            Idle,
            Building,
            WaitingForUpload,
            Uploading,
            Aborting,
        }
    }
}
