using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3A.Editor;
using VRC.SDKBase.Editor;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    [InitializeOnLoad]
    public static class UploadOrchestrator
    {
        private const string UploadInProgressSessionKey = "com.anatawa12.continuous-avatar-uploader.upload-in-progress";

        public static event Action<UploaderProgressAsset, AvatarUploadSetting> OnUploadSingleAvatarStarted;
        public static event Action<UploaderProgressAsset, AvatarUploadSetting> OnUploadSingleAvatarFinished;
        public static event Action<UploaderProgressAsset, AvatarUploadSetting, List<Exception>> OnUploadSingleAvatarFailed;
        public static event Action<UploaderProgressAsset, bool> OnUploadFinished;
        public static event Action<Exception> OnLoginFailed;
        public static event Action<Exception> OnRandomException;

        static UploadOrchestrator() => EditorApplication.delayCall += ResumeUpload;

        private static CancellationTokenSource _cancellationTokenSource = new();
        private static CancellationToken CancellationToken => _cancellationTokenSource.Token;

        private static void ResumeUpload()
        {
            if (_uploadInProgress)
            {
                Log("Upload is already in progress, so we are not resuming it.");
                return;
            }
            var asset = UploaderProgressAsset.Load();
            if (asset == null)
            {
                // No progress asset found, so we are not uploading anything.
                SessionState.EraseBool(UploadInProgressSessionKey);
                Log("We are not uploading since no UploaderProgressAsset found.");
                return;
            }

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
                    return;
                }
            }

            Log("Resuming upload from progress asset.");

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

            Log($"Starting upload with {asset.uploadSettings.Length} Avatars");
            _cancellationTokenSource = new CancellationTokenSource();

            // Select the first platform to upload to.
            var currentPlatform = Uploader.GetCurrentTargetPlatform();
            if (asset.targetPlatforms.Length == 0)
            {
                throw new InvalidOperationException("No target platforms specified. Please select at least one target platform to upload to.");
            }
            else if (asset.targetPlatforms.Contains(currentPlatform))
            {
                // If the current platform is one of the target platforms, we can start uploading to it.
                asset.uploadingTargetPlatform = currentPlatform;
            }
            else
            {
                // Otherwise, we select the first target platform.
                asset.uploadingTargetPlatform = asset.targetPlatforms.First();
            }

            // if rollback is enabled, store the current build platform
            if (asset.rollbackPlatform)
            {
                var currentBuildTarget = EditorUserBuildSettings.activeBuildTarget;
                var currentBuildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
                asset.lastBuildPlatform = currentBuildTarget;
                asset.lastBuildPlatformGroup = currentBuildTargetGroup;
            }

            asset.Save();

            SessionState.SetBool(UploadInProgressSessionKey, true);
            // Start the upload process.
            _ = UploadNextAvatar(asset);
        }

        private class ActiveBuildTargetChangedCallback : IActiveBuildTargetChanged
        {
            public int callbackOrder => int.MaxValue;

            public void OnActiveBuildTargetChanged(BuildTarget previousTarget, BuildTarget newTarget)
            {
                Debug.Log($"Active build target changed from {previousTarget} to {newTarget}");
                Debug.Log($"Current target platform is {Uploader.GetCurrentTargetPlatform()} " +
                          $"(group: {EditorUserBuildSettings.selectedBuildTargetGroup}, target: {EditorUserBuildSettings.activeBuildTarget})");
                EditorApplication.delayCall += ResumeUpload;
            }
        }

        public static void CancelUpload() => _cancellationTokenSource.Cancel();

        private static bool _uploadInProgress = false;

        static async Task UploadNextAvatar(UploaderProgressAsset asset)
        {
            if (CancellationToken.IsCancellationRequested)
            {
                Log("Upload cancelled by user. so we are finishing the upload.");
                FinishUpload(asset, true);
                return;
            }

            try
            {
                _uploadInProgress = true;
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

                // sleep for a while to avoid overwhelming the server
                await Task.Delay(asset.sleepMilliseconds);

                var currentPlatform = Uploader.GetCurrentTargetPlatform();
                if (currentPlatform != asset.uploadingTargetPlatform)
                {
                    // The current platform is not the one we're uploading to, so we need to switch to the correct platform.
                    Log(
                        $"Switching target platform to {asset.uploadingTargetPlatform} from {currentPlatform} " +
                        $"(group: {EditorUserBuildSettings.selectedBuildTargetGroup}, target: {EditorUserBuildSettings.activeBuildTarget})");
                    if (!Uploader.StartSwitchTargetPlatformAsync(asset.uploadingTargetPlatform))
                    {
                        // If we failed to switch the platform, we should notify the user and stop the upload.
                        WithTryCatch(() => OnRandomException?.Invoke(new Exception("Failed to switch target platform.")),
                            () => EditorUtility.DisplayDialog("Continuous Avatar Uploader", "Failed to switch target platform.", "OK"));
                        FinishUpload(asset, false);
                        return;
                    }

                    // In most cases, the platform switch is asynchronous, so we need to wait for it to complete.
                    if (currentPlatform != asset.uploadingTargetPlatform) return;
                    Log("Switched target platform to " + asset.uploadingTargetPlatform + " synchronously.");
                }

                // Wait for the builder to be ready.
                Log("Trying to get the IVRCSdkAvatarBuilderApi");
                IVRCSdkAvatarBuilderApi builder;
                while (!VRCSdkControlPanel.TryGetBuilder(out builder))
                    await Task.Delay(100, CancellationToken);

                Log("IVRCSdkAvatarBuilderApi is ready, Logging in if needed.");
                try
                {
                    if (!await Uploader.TryLogin())
                    {
                        // Login failed, we should notify the user and stop the upload.
                        WithTryCatch(() => OnLoginFailed?.Invoke(new Exception("No user logged in.")),
                            () => EditorUtility.DisplayDialog("Continuous Avatar Uploader",
                                "Login Failed: No user logged in.", "OK"));
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
                Log($"Uploading avatar {asset.uploadingAvatarIndex + 1}/{asset.uploadSettings.Length} for platform {asset.uploadingTargetPlatform}: {avatarToUpload.name}");

                WithTryCatch(() => OnUploadSingleAvatarStarted?.Invoke(asset, avatarToUpload));

                var trialIndex = 0;
                var errorsInThisTrial = new List<Exception>();
                do
                {
                    try
                    {
                        await Uploader.UploadSingle(avatarToUpload, builder, CancellationToken);
                        break;
                    }
                    catch (OperationCanceledException exception) when (CancellationToken.IsCancellationRequested)
                    {
                        Debug.LogException(exception);
                        errorsInThisTrial.Add(exception);
                        foreach (var exception1 in errorsInThisTrial)
                        {
                            asset.uploadErrors.Add(new UploadErrorInfo
                            {
                                uploadingAvatar = avatarToUpload,
                                targetPlatform = asset.uploadingTargetPlatform,
                                message = exception1.ToString()
                            });
                        }
                        asset.Save();
                        WithTryCatch(() => OnUploadSingleAvatarFailed?.Invoke(asset, avatarToUpload, errorsInThisTrial));
                        break;
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                        errorsInThisTrial.Add(exception);

                        trialIndex++;

                        if (trialIndex > asset.retryCount || IsUnRetryableException(exception))
                        {
                            foreach (var exception1 in errorsInThisTrial)
                            {
                                asset.uploadErrors.Add(new UploadErrorInfo
                                {
                                    uploadingAvatar = avatarToUpload,
                                    targetPlatform = asset.uploadingTargetPlatform,
                                    message = exception1.ToString()
                                });
                            }
                            asset.Save();
                            WithTryCatch(() => OnUploadSingleAvatarFailed?.Invoke(asset, avatarToUpload, errorsInThisTrial));
                            break;
                        }
                    }
                } while (true);

                Log($"Avatar {asset.uploadingAvatarIndex + 1}/{asset.uploadSettings.Length} uploaded for platform {asset.uploadingTargetPlatform}.");

                WithTryCatch(() => OnUploadSingleAvatarFinished?.Invoke(asset, avatarToUpload));

                // After uploading, we increment the index
                asset.uploadingAvatarIndex++;
                asset.Save();
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
                return;
            }
            finally
            {
                _uploadInProgress = false;
            }

            // Continue uploading the next avatar.
            await UploadNextAvatar(asset);
        }

        private static bool IsUnRetryableException(Exception exception) => exception is OwnershipException or BuilderException;

        private static bool TrySelectNextPlatform(UploaderProgressAsset asset)
        {
            Log($"Uploading finished for {asset.uploadingTargetPlatform} finished, moving to next platform.");
            if (!asset.uploadFinishedPlatforms.Contains(asset.uploadingTargetPlatform))
            {
                asset.uploadFinishedPlatforms = asset.uploadFinishedPlatforms.Append(asset.uploadingTargetPlatform).ToArray();
            }

            var nextPlatformOrNull = asset.targetPlatforms.Where(p => !asset.uploadFinishedPlatforms.Contains(p))
                .Select(x => (TargetPlatform?)x)
                .FirstOrDefault();
            if (nextPlatformOrNull is { } nextPlatform)
            {
                Log($"Next platform to upload to: {nextPlatform}");
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
            // if rollback is enabled, restore the build platform first.
            if (asset.rollbackPlatform && asset.lastBuildPlatform != EditorUserBuildSettings.activeBuildTarget)
            {
                asset.Save();
                Log("Restoring the last build platform to " + asset.lastBuildPlatform);
                if (EditorUserBuildSettings.SwitchActiveBuildTargetAsync(asset.lastBuildPlatformGroup,
                        asset.lastBuildPlatform))
                    return;
                Log("Failed to restore the last build platform, please do it manually.");
            }

            WithTryCatch(() => OnUploadFinished?.Invoke(asset, successfully));
            Log($"[CAU Orchestrator] Upload finished: {successfully}");

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

        private static void Log(string message)
        {
            Debug.Log($"[CAU Orchestrator] {message}");
        }
    }
}