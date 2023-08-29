using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
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

        public static async Task Upload(
            IVRCSdkAvatarBuilderApi builder,
            int sleepMilliseconds,
            IEnumerable<AvatarUploadSetting> uploadingAvatars,
            Action<AvatarUploadSetting> onStartUpload = null,
            Action<AvatarUploadSetting> onFinishUpload = null,
            Action<Exception, AvatarUploadSetting> onException = null,
            CancellationToken cancellationToken = default
        )
        {
            if (EditorApplication.isPlaying) throw new Exception("Playmode"); // TODO
            if (builder.UploadState != SdkUploadState.Idle) throw new Exception("Invalid State"); // TODO

            var gotLock = false;
            try
            {
                // ReSharper disable once MethodHasAsyncOverloadWithCancellation
                // Since timeout 0 means will not block the thread.
                gotLock = GlobalSemaphore.Wait(0);
                if (!gotLock) throw new Exception("Upload in progress");

                await UploadImpl(builder, sleepMilliseconds, uploadingAvatars, onStartUpload, onFinishUpload,
                    onException, cancellationToken);
            }
            finally
            {
                if (gotLock)
                {
                    GlobalSemaphore.Release();
                }
            }
        }

        private static async Task UploadImpl(
            IVRCSdkAvatarBuilderApi builder,
            int sleepMilliseconds,
            IEnumerable<AvatarUploadSetting> uploadingAvatars,
            Action<AvatarUploadSetting> onStartUpload,
            Action<AvatarUploadSetting> onFinishUpload,
            Action<Exception, AvatarUploadSetting> onException,
            CancellationToken cancellationToken
        )
        {
            if (onException == null) onException = Debug.LogException;

            AssetDatabase.SaveAssets();
            using (var playmodeScope = new PreventEnteringPlayModeScope())
            using (new OpeningSceneRestoreScope())
            {
                foreach (var avatar in uploadingAvatars)
                {
                    Debug.Log($"Upload started for {avatar.name}");

                    onStartUpload?.Invoke(avatar);

                    using (var scope = LoadAvatar(avatar))
                    {
                        try
                        {
                            await UploadAvatar(avatar, scope.AvatarDescriptor, playmodeScope, builder, cancellationToken);
                        }
                        catch (Exception e)
                        {
                            onException(e, avatar);
                        }
                    }

                    onFinishUpload?.Invoke(avatar);

                    await Task.Delay(sleepMilliseconds, cancellationToken);
                }
            }

            AssetDatabase.DeleteAsset(PrefabScenePath);
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
            await builder.BuildAndUpload(avatarDescriptor.gameObject, vrcAvatar,
                thumbnailPath: picturePath,
                cancellationToken: cancellationToken);

            // get uploaded avatar info
            vrcAvatar = await VRCApi.GetAvatar(pipelineManager.blueprintId, forceRefresh: true,
                cancellationToken: cancellationToken);

            // upload avatar image if not uploaded.
            // When https://feedback.vrchat.com/open-beta/p/beta-sdk-330-beta1-lack-of-ability-to-update-description-from-code
            // is fixed, this process may not required
            if (platformInfo.updateImage && !uploadingNewAvatar)
            {
                await VRCApi.UpdateAvatarImage(vrcAvatar.ID, vrcAvatar, picturePath,
                    cancellationToken: cancellationToken);
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

                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);
                var newGameObject = Object.Instantiate(avatarDescriptor.gameObject);
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
            private readonly bool _oldEnabled;
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
                    _oldEnabled = AvatarDescriptor.gameObject.activeSelf;
                    AvatarDescriptor.gameObject.SetActive(true);
                }
            }

            public void Dispose()
            {
                if (AvatarDescriptor != null)
                    AvatarDescriptor.gameObject.gameObject.SetActive(_oldEnabled);
            }
        }

        private static IUploadAvatarScope LoadAvatar(AvatarUploadSetting avatar) =>
            avatar.avatarDescriptor.IsAssetReference()
                ? (IUploadAvatarScope) new AssetUploadAvatarScope(avatar)
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
    }
}