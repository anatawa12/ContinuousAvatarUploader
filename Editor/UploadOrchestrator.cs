using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3A.Editor;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    [InitializeOnLoad]
    static class UploadOrchestrator
    {
        private const string UploadInProgressSessionKey = "com.anatawa12.continuous-avatar-uploader.upload-in-progress";

        public static event Action<UploaderProgressAsset, AvatarUploadSetting> OnUploadSingleAvatarStarted;
        public static event Action<UploaderProgressAsset, AvatarUploadSetting> OnUploadSingleAvatarFinished;
        public static event Action<UploaderProgressAsset, AvatarUploadSetting, Exception> OnUploadSingleAvatarFailed;
        public static event Action<UploaderProgressAsset, bool> OnUploadFinished;
        public static event Action<Exception> OnLoginFailed;
        public static event Action<Exception> OnRandomException;

        static UploadOrchestrator() => EditorApplication.delayCall += AssemblyLoaded;

        private static CancellationTokenSource _cancellationTokenSource = new();
        private static CancellationToken CancellationToken => _cancellationTokenSource.Token;

        private static void AssemblyLoaded()
        {
            var asset = UploaderProgressAsset.Load();
            if (asset == null) return;

            var isUploading = SessionState.GetBool(UploadInProgressSessionKey, false);
            if (!isUploading)
            {
                // This no session state means that the last upload was not finished properly.
                // (e.g. Unity Editor crashed or was closed while uploading)
                // We should ask the user if they want to resume the upload.
                if (EditorUtility.DisplayDialog(
                        "Continuous Avatar Uploader",
                        "It seems that the last upload was not finished properly. Do you want to resume the upload?",
                        "Yes", "No"))
                {
                    // Set the session state to true to indicate that we are resuming the upload.
                    SessionState.SetBool(UploadInProgressSessionKey, true);
                }
                else
                {
                    // User chose not to resume the upload, so we clear the session state and remove the asset.
                    SessionState.EraseBool(UploadInProgressSessionKey);
                    asset.Delete();
                }
            }

            // show window if it is not already open
            ContinuousAvatarUploader.OpenWindow();

            _ = UploadNextAvatar(asset);
        }

        public static bool IsUploadInProgress()
        {
            // Check if we are already uploading.
            return SessionState.GetBool(UploadInProgressSessionKey, false) || UploaderProgressAsset.Load() != null;
        }

        public static void StartUpload(UploaderProgressAsset asset)
        {
            // Check if we are already uploading.
            if (IsUploadInProgress())
                throw new InvalidOperationException("An upload is already in progress. Please wait for it to finish or cancel it before starting a new one.");

            _cancellationTokenSource = new CancellationTokenSource();

            SessionState.SetBool(UploadInProgressSessionKey, true);
            asset.Save();

            // Start the upload process.
            _ = UploadNextAvatar(asset);
        }

        public static void CancelUpload() => _cancellationTokenSource.Cancel();

        static async Task UploadNextAvatar(UploaderProgressAsset asset)
        {
            try
            {
                if (asset.uploadSettings.Length == 0)
                {
                    // Nothing to upload, so we can finish the upload.
                    FinishUpload(asset, true);
                    return;
                }

                if (asset.uploadFinishedPlatforms.Contains(asset.uploadingTargetPlatform))
                {
                    if (!TrySelectNextPlatform(asset)) return;
                }
                else if (asset.uploadingAvatarIndex < 0 || asset.uploadingAvatarIndex >= asset.uploadSettings.Length)
                {
                    // The index is out of bounds, we should start with the next platform.
                    if (!TrySelectNextPlatform(asset)) return;
                }

                var currentPlatform = Uploader.GetCurrentTargetPlatform();
                if (currentPlatform != asset.uploadingTargetPlatform)
                {
                    // The current platform is not the one we're uploading to, so we need to switch to the correct platform.
                    Uploader.StartSwitchTargetPlatform(currentPlatform);
                    return;
                }

                // sleep for a while to avoid overwhelming the server
                await Task.Delay(asset.sleepMilliseconds);

                // Wait for the builder to be ready.
                IVRCSdkAvatarBuilderApi builder;
                while (!VRCSdkControlPanel.TryGetBuilder(out builder))
                    await Task.Delay(100, CancellationToken);

                try
                {
                    if (!await Uploader.TryLogin())
                    {
                        // Login failed, we should notify the user and stop the upload.
                        WithTryCatch(() => OnLoginFailed?.Invoke(new Exception("No user logged in.")),
                            () => EditorUtility.DisplayDialog("Continuous Avatar Uploader", "Login Failed: No user logged in.", "OK"));
                        FinishUpload(asset, false);
                        return;
                    }
                }
                catch (Exception e)
                {
                    // An exception occurred during login, we should notify the user and stop the upload.
                    WithTryCatch(() => OnLoginFailed?.Invoke(e), 
                        () => EditorUtility.DisplayDialog("Continuous Avatar Uploader", "Login Failed: " + e.Message, "OK"));
                    FinishUpload(asset, false);
                    return;
                }

                // We are ready to upload the next avatar.
                var avatarToUpload = asset.uploadSettings[asset.uploadingAvatarIndex];

                WithTryCatch(() => OnUploadSingleAvatarStarted?.Invoke(asset, avatarToUpload));

                try
                {
                    await Uploader.UploadSingle(avatarToUpload, builder, CancellationToken);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                    asset.uploadErrors.Add(new UploadErrorInfo
                    {
                        uploadingAvatar = avatarToUpload,
                        targetPlatform = Uploader.GetCurrentTargetPlatform(),
                        message = exception.ToString()
                    });
                    asset.Save();
                    WithTryCatch(() => OnUploadSingleAvatarFailed?.Invoke(asset, avatarToUpload, exception));
                }

                WithTryCatch(() => OnUploadSingleAvatarFinished?.Invoke(asset, avatarToUpload));

                // After uploading, we increment the index
                asset.uploadingAvatarIndex++;
                asset.Save();

                // Continue uploading the next avatar.
                await UploadNextAvatar(asset);
            }
            catch (Exception e)
            {
                // If something went wrong during the upload, we should finish the upload and clean up.
                WithTryCatch(() => OnRandomException?.Invoke(e),
                    () => EditorUtility.DisplayDialog("Continuous Avatar Uploader", "An error occurred: " + e.Message, "OK"));
                Debug.LogException(e);
                asset.uploadErrors.Add(new UploadErrorInfo
                {
                    targetPlatform = asset.uploadingTargetPlatform,
                    message = e.ToString()
                });
                FinishUpload(asset, false);
            }
        }

        private static bool TrySelectNextPlatform(UploaderProgressAsset asset)
        {
            if (!asset.uploadFinishedPlatforms.Contains(asset.uploadingTargetPlatform))
            {
                asset.uploadFinishedPlatforms = asset.uploadFinishedPlatforms.Append(asset.uploadingTargetPlatform).ToArray();
            }

            var nextPlatformOrNull = asset.targetPlatforms.Where(p => !asset.uploadFinishedPlatforms.Contains(p))
                .Select(x => (TargetPlatform?)x)
                .FirstOrDefault();
            if (nextPlatformOrNull is { } nextPlatform)
            {
                // We have a next platform to upload to, so we reset the avatar index and set the new platform.
                asset.uploadingTargetPlatform = nextPlatform;
                asset.uploadingAvatarIndex = 0;
                asset.Save();
                return true;
            }
            else
            {
                // All platforms have been uploaded, we can clear the session state and delete the asset.
                FinishUpload(asset, true);
                return false;
            }
        }

        private static void FinishUpload(UploaderProgressAsset asset, bool successfully)
        {
            WithTryCatch(() => OnUploadFinished?.Invoke(asset, successfully));

            SessionState.EraseBool(UploadInProgressSessionKey);
            asset.Delete();
            _cancellationTokenSource = new CancellationTokenSource();

            // restore the scene state
            if (asset.openedScenes.Length == 0)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            }
            else
            {
                var tmp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                foreach (var lastOpenedScene in asset.openedScenes)
                {
                    var mode = lastOpenedScene.isLoaded
                        ? OpenSceneMode.Additive 
                        : OpenSceneMode.AdditiveWithoutLoading;
                    EditorSceneManager.OpenScene(lastOpenedScene.scenePath, mode);
                }
                EditorSceneManager.CloseScene(tmp, true);
            }
        }

        public static OpenedSceneInformation[] GetLastOpenedScenes()
        {
            var scenes = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .ToArray();
            if (scenes.Any(x => x.isDirty))
                EditorSceneManager.SaveOpenScenes();
            return scenes.Any(x => string.IsNullOrEmpty(x.path))
                ? Array.Empty<OpenedSceneInformation>()
                : scenes.Select(x => new OpenedSceneInformation { scenePath = x.path, isLoaded = x.isLoaded })
                    .ToArray();
        }

        private static void WithTryCatch(Action action, [CanBeNull] Action fallback = null)
        {
            action ??= fallback;
            try
            {
                action?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}