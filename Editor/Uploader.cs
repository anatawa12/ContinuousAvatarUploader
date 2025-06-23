using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using VRC.Core;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3A.Editor;
using VRC.SDKBase;
using VRC.SDKBase.Editor;
using VRC.SDKBase.Editor.Api;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    internal static class Uploader
    {
        private const string PrefabScenePath = "Assets/com.anatawa12.continuous-avatar-uploader-uploading-prefab.unity";

        private static readonly SemaphoreSlim GlobalSemaphore = new SemaphoreSlim(1, 1);

        private static void CheckForPreconditions(IVRCSdkAvatarBuilderApi builder)
        {
            if (EditorApplication.isPlaying) throw new Exception("Playmode"); // TODO
            switch (builder.UploadState)
            {
                case SdkUploadState.Idle:
                case SdkUploadState.Failure:
                case SdkUploadState.Success:
                    break;
                case SdkUploadState.Uploading:
                default:
                    throw new Exception($"Previous Upload is in progress (state: {builder.UploadState}");
            }
        }

        public static bool AskForCopyrightAgreement()
        {
            if (Application.isBatchMode) return true;

            return EditorUtility.DisplayDialog("Continuous Avatar Uploader: VRCSDK Agreement",
                AgreementText,
                "OK", "NO");
        }

        public static IEnumerable<TargetPlatform> GetTargetPlatforms()
        {
            for (var platform = TargetPlatform.Windows; platform < TargetPlatform.LastIndex; platform++)
                yield return platform;
        }

        public static TargetPlatform GetCurrentTargetPlatform() =>
            EditorUserBuildSettings.selectedBuildTargetGroup switch
            {
                BuildTargetGroup.Standalone => TargetPlatform.Windows,
                BuildTargetGroup.Android => TargetPlatform.Android,
                BuildTargetGroup.iOS => TargetPlatform.iOS,
                _ => TargetPlatform.LastIndex,
            };

        private static BuildTarget GetBuildTarget(TargetPlatform platform) => platform switch
        {
            TargetPlatform.Windows => BuildTarget.StandaloneWindows64,
            TargetPlatform.Android => BuildTarget.Android,
            TargetPlatform.iOS => BuildTarget.iOS,
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null)
        };

        private static BuildTargetGroup GetBuildTargetGroup(TargetPlatform platform) => platform switch
        {
            TargetPlatform.Windows => BuildTargetGroup.Standalone,
            TargetPlatform.Android => BuildTargetGroup.Android,
            TargetPlatform.iOS => BuildTargetGroup.iOS,
            _ => throw new ArgumentOutOfRangeException(nameof(platform), platform, null)
        };

        public static async Task<bool> TryLogin()
        {
            if (!ConfigManager.RemoteConfig.IsInitialized())
            {
#if CAU_VRCSDK_BASE_3_6_0
                API.SetOnlineMode(true);
#else
                API.SetOnlineMode(true, "vrchat");
#endif
                ConfigManager.RemoteConfig.Init();
            }
            if (!APIUser.IsLoggedIn && ApiCredentials.Load())
            {
                var task = new TaskCompletionSource<bool>();
                APIUser.InitialFetchCurrentUser(c =>
                {
                    AnalyticsSDK.LoggedInUserChanged(c.Model as APIUser);
                    task.TrySetResult(true);
                }, e =>
                {
                    task.TrySetException(new Exception(e.Error));   
                });
                await task.Task;
            }
            return APIUser.IsLoggedIn;
        }

        public static bool VerifyCredentials([CanBeNull] Action onUpdate = null)
        {
            var task = TryLogin();
            if (task.IsCompleted) return true;
            task.ContinueWith(_ => onUpdate, TaskScheduler.FromCurrentSynchronizationContext());
            return false;
        }

        public static bool StartSwitchTargetPlatformAsync(TargetPlatform platform) => 
            EditorUserBuildSettings.SwitchActiveBuildTargetAsync(GetBuildTargetGroup(platform), GetBuildTarget(platform));

        [CanBeNull] private static Func<BuildTarget, bool> _isPlatformSupportLoadedByBuildTargetMethod = null;

        public static bool IsBuildSupportedInstalled(TargetPlatform platform)
        {
            if (_isPlatformSupportLoadedByBuildTargetMethod == null)
            {
                var moduleManagerType =
                    typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Modules.ModuleManager");
                var isPlatformSupportLoadedByBuildTargetMethod =
                    moduleManagerType.GetMethod("IsPlatformSupportLoadedByBuildTarget",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null,
                        new[] { typeof(BuildTarget) }, null);
                if (isPlatformSupportLoadedByBuildTargetMethod == null)
                    throw new Exception("IsPlatformSupportLoadedByBuildTargetMethod not found");
                _isPlatformSupportLoadedByBuildTargetMethod =
                    (Func<BuildTarget, bool>)Delegate.CreateDelegate(typeof(Func<BuildTarget, bool>),
                        isPlatformSupportLoadedByBuildTargetMethod);
            }

            return _isPlatformSupportLoadedByBuildTargetMethod(GetBuildTarget(platform));
        }

        public delegate void StartUpload(AvatarUploadSetting avatr, int index);

        public static async Task Upload(
            IVRCSdkAvatarBuilderApi builder,
            int sleepMilliseconds,
            AvatarUploadSetting[] uploadingAvatars,
            StartUpload onStartUpload = null,
            Action<AvatarUploadSetting> onFinishUpload = null,
            Action<Exception, AvatarUploadSetting> onException = null,
            CancellationToken cancellationToken = default
        )
        {
            // assign default actions
            onException ??= Debug.LogException;

            // pre-upload checks
            CheckForPreconditions(builder);
            if (!AskForCopyrightAgreement())
                throw new Exception("No Agreement");

            using var semaphoreScope = new NoWaitSemaphoreScope(GlobalSemaphore);
            if (!semaphoreScope.Acquired) throw new Exception("Another CAU Upload in progress");

            using (new OpeningSceneRestoreScope())
            {
                for (var index = 0; index < uploadingAvatars.Length; index++)
                {
                    var avatar = uploadingAvatars[index];

                    onStartUpload?.Invoke(avatar, index);
                    try
                    {
                        await UploadSingle(avatar, builder, cancellationToken);
                    }
                    catch (Exception e)
                    {
                        onException(e, avatar);
                    }

                    onFinishUpload?.Invoke(avatar);

                    await Task.Delay(sleepMilliseconds, cancellationToken);
                }
            }

            AssetDatabase.DeleteAsset(PrefabScenePath);
        }

        public static async Task UploadSingle(
            AvatarUploadSetting avatar,
            IVRCSdkAvatarBuilderApi builder,
            CancellationToken cancellationToken = default)
        {
            CheckForPreconditions(builder);

            if (!avatar.GetCurrentPlatformInfo().enabled)
            {
                Debug.Log($"Uploading avatar for {avatar.name} is disabled for current platform");
                return;
            }

            using (var playmodeScope = new PreventEnteringPlayModeScope())
            {
                Debug.Log($"Upload started for {avatar.name}");

                using (var scope = LoadAvatar(avatar))
                {
                    await UploadAvatar(avatar, scope.AvatarDescriptor, playmodeScope, builder,
                        cancellationToken);
                }
            }
        }

        private static async Task UploadAvatar(AvatarUploadSetting avatar,
            VRCAvatarDescriptor avatarDescriptor,
            PreventEnteringPlayModeScope playmodeScope,
            IVRCSdkAvatarBuilderApi builder,
            CancellationToken cancellationToken = default)
        {
            if (!avatarDescriptor)
            {
                Debug.LogError("Upload failed: avatar not found", avatar);
                throw new Exception("Avatar Not Found");
            }

            Debug.Log($"Actual avatar name: {avatarDescriptor.name}");

            var platformInfo = avatar.GetCurrentPlatformInfo();

            // prepare data for upload
            var pipelineManager = PreparePipelineManager(avatarDescriptor.gameObject);
            var (vrcAvatar, uploadingNewAvatar) = await PrepareVRCAvatar(pipelineManager, cancellationToken);

            // if picture is needed, take and use for upload
            string picturePath = null;
            if (platformInfo.updateImage || uploadingNewAvatar)
            {
                bool inPlayMode;
                if (platformInfo.updateImage)
                {
                    switch (platformInfo.imageTakeEditorMode)
                    {
                        case ImageTakeEditorMode.UseUploadGuiSetting:
                            inPlayMode = Preferences.TakeThumbnailInPlaymodeByDefault;
                            break;
                        case ImageTakeEditorMode.InEditMode:
                            inPlayMode = false;
                            break;
                        case ImageTakeEditorMode.InPlayMode:
                            inPlayMode = true;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                else
                {
                    inPlayMode = Preferences.TakeThumbnailInPlaymodeByDefault;
                }


                if (inPlayMode)
                {
                    // Entering playmode OR exiting playmode may destroy AvatarDescriptor
                    // so we may re-get AvatarDescriptor Instance with GlobalObjectId
                    var globalObjectId = GlobalObjectId.GetGlobalObjectIdSlow(avatarDescriptor);
                    using (playmodeScope.AllowScope())
                    {
                        try
                        {
                            await Utils.EnterPlayMode();
                            if (!avatarDescriptor)
                                avatarDescriptor = ResolveAvatar("entered play mode");

                            picturePath = TakePicture(avatarDescriptor, 1200, 900);
                        }
                        finally
                        {
                            await Utils.ExitPlayMode();
                        }

                        if (!avatarDescriptor) avatarDescriptor = ResolveAvatar("exited play mode");
                    }

                    VRCAvatarDescriptor ResolveAvatar(string when)
                    {
                        var newlyResolved =
                            GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId) as VRCAvatarDescriptor;
                        if (!newlyResolved)
                            throw new Exception("We cannot re-resolve VRCAvatarDescriptor");
                        Debug.Log($"Re-resolving avatar when {when}");
                        return newlyResolved;
                    }
                }
                else
                {
                    picturePath = TakePicture(avatarDescriptor, 1200, 900);
                }
            }

            // try to reset errors
            var controlPanel = VRCSdkControlPanel.window;
            if (controlPanel)
            {
                controlPanel.ResetIssues();
                controlPanel.CheckedForIssues = true;
            }

            // build avatar main process
            using (new SetBlueprintIdEveryFrame(pipelineManager, pipelineManager.blueprintId))
            {
                await AddCopyrightAgreement(pipelineManager.blueprintId);
                await builder.BuildAndUpload(avatarDescriptor.gameObject, vrcAvatar,
                    thumbnailPath: picturePath,
                    cancellationToken: cancellationToken);
            }

            // get uploaded avatar info
            vrcAvatar = await VRCApi.GetAvatar(pipelineManager.blueprintId, forceRefresh: true,
                cancellationToken: cancellationToken);

            // upload avatar image if not uploaded.
            // When https://feedback.vrchat.com/open-beta/p/beta-sdk-330-beta1-lack-of-ability-to-update-description-from-code
            // is fixed, this process may not required
            if (platformInfo.updateImage && !uploadingNewAvatar)
            {
                try
                {
                    await VRCApi.UpdateAvatarImage(vrcAvatar.ID, vrcAvatar, picturePath,
                        cancellationToken: cancellationToken);
                }
                catch (UploadException e)
                {
                    if (e.Message.Contains("This file was already uploaded"))
                    {
                        Debug.Log("Uploading image skipped: image already uploaded");
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            // update description
            // This process may also not required with 
            // https://feedback.vrchat.com/open-beta/p/beta-sdk-330-beta1-lack-of-ability-to-update-description-from-code
            if (platformInfo.versioningEnabled)
            {
                long versionName;
                (vrcAvatar.Description, versionName) =
                    UpdateVersionName(vrcAvatar.Description, platformInfo.versionNamePrefix);

                await VRCApi.UpdateAvatarInfo(vrcAvatar.ID, vrcAvatar, cancellationToken: cancellationToken);

                if (platformInfo.gitEnabled)
                {
                    var tagName = platformInfo.tagPrefix + versionName + platformInfo.tagSuffix;
                    AddGitTag(tagName, avatarDescriptor.name);
                }
            }
        }

        private static async Task<(VRCAvatar, bool isNewAvatar)> PrepareVRCAvatar(PipelineManager pipelineManager,
            CancellationToken cancellationToken = default)
        {
            VRCAvatar vrcAvatar = default;
            bool isNewAvatar;

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
            {
                isNewAvatar = true;
                vrcAvatar = new VRCAvatar
                {
                    Name = pipelineManager.gameObject.name,
                    Description = "",
                    Tags = new List<string>(),
                    ReleaseStatus = "private",
                };
            }
            else
            {
                if (APIUser.CurrentUser == null || vrcAvatar.AuthorId != APIUser.CurrentUser?.id)
                    throw new Exception("Uploading other user avatar.");
                isNewAvatar = false;
            }

            return (vrcAvatar, isNewAvatar);
        }

        interface IUploadAvatarScope : IDisposable
        {
            [CanBeNull] VRCAvatarDescriptor AvatarDescriptor { get; }
        }

        class AssetUploadAvatarScope : IUploadAvatarScope
        {
            public VRCAvatarDescriptor AvatarDescriptor { get; }

            public AssetUploadAvatarScope(AvatarUploadSetting avatar)
            {
                var avatarDescriptor = avatar.avatarDescriptor.asset as VRCAvatarDescriptor;
                if (!avatarDescriptor) return;

                // PreparePipelineManager here since we have to save changes to prefab file
                PreparePipelineManager(avatarDescriptor.gameObject);

                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);
                var newGameObject = Object.Instantiate(avatarDescriptor.gameObject);
                newGameObject.name = avatarDescriptor.gameObject.name;
                newGameObject.SetActive(true);
                AvatarDescriptor = newGameObject.GetComponent<VRCAvatarDescriptor>();
                EditorSceneManager.SaveScene(scene, PrefabScenePath);
            }

            public void Dispose()
            {
                // without saving anything
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                AssetDatabase.DeleteAsset(PrefabScenePath);
            }
        }

        class InSceneAvatarScope : IUploadAvatarScope
        {
            private readonly List<bool> _oldActive;
            public VRCAvatarDescriptor AvatarDescriptor { get; }

            public InSceneAvatarScope(AvatarUploadSetting avatar)
            {
                // reference to scene
                AvatarDescriptor = avatar.avatarDescriptor.TryResolve() as VRCAvatarDescriptor;
                if (!AvatarDescriptor)
                {
                    avatar.avatarDescriptor.OpenScene();
                    AvatarDescriptor = avatar.avatarDescriptor.TryResolve() as VRCAvatarDescriptor;
                }

                if (AvatarDescriptor != null)
                {
                    _oldActive = new List<bool>();

                    for (var transform = AvatarDescriptor.transform; transform != null; transform = transform.parent)
                    {
                        _oldActive.Add(transform.gameObject.activeSelf);
                        transform.gameObject.SetActive(true);
                    }
                }
            }

            public void Dispose()
            {
                if (AvatarDescriptor != null)
                {
                    var index = 0;
                    for (var transform = AvatarDescriptor.transform;
                         transform != null;
                         transform = transform.parent, index++)
                    {
                        transform.gameObject.SetActive(_oldActive[index]);
                    }
                }
            }
        }

        private static IUploadAvatarScope LoadAvatar(AvatarUploadSetting avatar) =>
            avatar.avatarDescriptor.IsAssetReference()
                ? (IUploadAvatarScope)new AssetUploadAvatarScope(avatar)
                : new InSceneAvatarScope(avatar);

        private static PipelineManager PreparePipelineManager(GameObject gameObject)
        {
            var pipelineManager = gameObject.GetComponent<PipelineManager>();
            if (!pipelineManager)
                pipelineManager = gameObject.AddComponent<PipelineManager>();

            if (string.IsNullOrEmpty(pipelineManager.blueprintId))
            {
                // pipelineManager.AssignId() doesn't mark pipeline manager dirty
                pipelineManager.AssignId();
                EditorUtility.SetDirty(pipelineManager);
                PrefabUtility.RecordPrefabInstancePropertyModifications(pipelineManager);
                if (pipelineManager.gameObject.scene.IsValid())
                    EditorSceneManager.SaveScene(pipelineManager.gameObject.scene);
            }

            return pipelineManager;
        }

        private static string TakePicture(VRC_AvatarDescriptor cachedAvatar, int width, int height)
        {
            using (var previewSceneScope = PreviewSceneScope.Create(EditorUtility.IsPersistent(cachedAvatar)))
            {
                if (EditorUtility.IsPersistent(cachedAvatar))
                {
                    PrefabUtility.LoadPrefabContentsIntoPreviewScene(
                        AssetDatabase.GetAssetPath(cachedAvatar), previewSceneScope.Scene);
                }

                using (var cameraGameObject = new DestroyLater<GameObject>(EditorUtility.CreateGameObjectWithHideFlags(
                           "Take Picture Camera", HideFlags.DontSave,
                           typeof(Camera))))
                {
                    var camera = cameraGameObject.Value.GetComponent<Camera>();
                    camera.enabled = false;
                    camera.cullingMask = unchecked((int)0xFFFFFFDF);
                    camera.nearClipPlane = 0.01f;
                    camera.farClipPlane = 100f;
                    camera.allowHDR = false;
                    camera.scene = previewSceneScope.Scene.IsValid()
                        ? previewSceneScope.Scene
                        : cachedAvatar.gameObject.scene;
                    cachedAvatar.PositionPortraitCamera(camera.transform);
                    Selection.objects = new Object[] { camera.gameObject };


                    using (var previewTexture = new DestroyLater<RenderTexture>(
                               new RenderTexture(width, height, 24, GraphicsFormat.R8G8B8A8_SRGB)
                                   { antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing) }))
                    {
                        camera.targetTexture = previewTexture.Value;
                        camera.pixelRect = new Rect(0, 0, width, height);
                        camera.Render();

                        using (var tex = new DestroyLater<Texture2D>(new Texture2D(width, height, TextureFormat.RGBA32,
                                   0,
                                   false)))
                        {
                            using (new ActiveRenderTextureScope(previewTexture.Value))
                            {
                                RenderTexture.active = previewTexture.Value;
                                tex.Value.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                                tex.Value.Apply(false);
                            }

                            var path = Path.Combine(Path.GetTempPath(), "picture-" + Guid.NewGuid() + ".png");
                            System.IO.File.WriteAllBytes(path, tex.Value.EncodeToPNG());
                            return path;
                        }
                    }
                }
            }
        }

        private static (string, long) UpdateVersionName(string description, string versionPrefix)
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

        private static void AddGitTag(string tagName, string avatarName)
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
                Debug.LogException(new Exception($"Tagging version {tagName} for {avatarName}", e));
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

#if CAU_VRCSDK_BASE_3_8_1
        public static string AgreementText = VRCCopyrightAgreement.AgreementText;
#else
        public static string AgreementText =
            "By clicking OK, I certify that I have the necessary rights to upload this content and that it will not infringe on any third-party legal or intellectual property rights.";
#endif
#if CAU_VRCSDK_BASE_3_8_0
        private static async Task AddCopyrightAgreement(string blueprint)
        {
            const string key = "VRCSdkControlPanel.CopyrightAgreement.ContentList";
            var keyText = SessionState.GetString(key, "");
            var list = string.IsNullOrEmpty(keyText)
                ? new List<string>()
                : SessionState.GetString(key, "").Split(';').ToList();
            if (list.Contains(blueprint)) return;
            list.Add(blueprint);
            SessionState.SetString(key, string.Join(";", list));

            await VRCApi.ContentUploadConsent(new VRCAgreement
            {
                AgreementCode = "content.copyright.owned",
                AgreementFulltext = AgreementText,
                ContentId = blueprint,
                Version = 1,
            });
        }
#else
        private static Task AddCopyrightAgreement(string blueprint) => Task.CompletedTask;
#endif

        /// <summary>
        /// This class sets the blueprintId every frame until disposed.
        ///
        /// This is the workaround for VRCSDK control panel bug
        /// https://feedback.vrchat.com/sdk-bug-reports/p/calling-ivrcsdkavatarbuilderapibuildandupload-just-after-changing-scene-may-resu
        /// </summary>
        class SetBlueprintIdEveryFrame : IDisposable
        {
            private readonly PipelineManager _pipelineManager;
            private readonly string _blueprintId;

            public SetBlueprintIdEveryFrame(PipelineManager pipelineManager, string blueprintId)
            {
                _pipelineManager = pipelineManager;
                _blueprintId = blueprintId;
                EditorApplication.update += Update;
            }

            private void Update()
            {
                if (_pipelineManager == null) return;
                if (_pipelineManager.blueprintId != _blueprintId)
                {
                    _pipelineManager.blueprintId = _blueprintId;
                    EditorUtility.SetDirty(_pipelineManager);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(_pipelineManager);
                }
            }

            public void Dispose()
            {
                EditorApplication.update -= Update;
            }
        }

        class NoWaitSemaphoreScope : IDisposable
        {
            private readonly SemaphoreSlim _semaphore;
            private bool _acquired;

            public NoWaitSemaphoreScope(SemaphoreSlim semaphore)
            {
                _semaphore = semaphore;
                _acquired = _semaphore.Wait(0);
            }

            public bool Acquired => _acquired;

            public void Dispose()
            {
                if (_acquired) _semaphore.Release();
                _acquired = false;
            }
        }
    }
}