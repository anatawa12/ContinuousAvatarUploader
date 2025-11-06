#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    internal class UploaderProgressAsset : ScriptableObject
    {
        public const string AssetPath = "Assets/com.anatawa12.continuous-avatar-uploader.uploader-progress.asset";

        // Semi-readonly fields that describe the requested uploads

        /// <summary>
        /// The list of opened scenes that the user has requested to upload.
        /// </summary>
        public OpenedSceneInformation[] openedScenes = Array.Empty<OpenedSceneInformation>();

        /// <summary>
        /// The list of upload settings that the user has requested to upload.
        /// </summary>
        public AvatarUploadSetting[] uploadSettings = Array.Empty<AvatarUploadSetting>();
        /// <summary>
        /// The list of target platforms that the user has requested to upload.
        /// </summary>
        public TargetPlatform[] targetPlatforms = Array.Empty<TargetPlatform>();
        /// <summary>
        /// The number of milliseconds to wait between each upload attempt.
        /// </summary>
        public int sleepMilliseconds;
        /// <summary>
        /// Whether to rollback the build platform after the upload is finished.
        /// </summary>
        public bool rollbackPlatform;
        public BuildTarget lastBuildPlatform;
        public BuildTargetGroup lastBuildPlatformGroup;
        /// <summary>
        /// The number of retries to attempt for each upload.
        /// Zero means no retries, so only one attempt will be made.
        /// </summary>
        // Note: for simplicity in the implementation, we only preserve the retry count in the local variable,
        // not on the asset so restarting the editor while uploading a avatar will reset the retry count.
        public int retryCount;

        /// <summary>
        /// Strict mode will stop uploading when any error occurs. It will ignore the retry count.
        /// </summary>
        public bool strictMode;

        // Mutable fields that describe the current progress of the upload
        /// <summary>
        /// The index of the current upload is in progress.
        /// If unity editor crashed or was closed, we should resume with uploading this index.
        /// If this is out of 0..&lt;uploadSettings.Length, upload has finished.
        /// We should resume with uploading 0 with next target platform.
        /// </summary>
        public int uploadingAvatarIndex;
        /// <summary>
        /// The target platform that is currently being uploaded.
        /// If this platform is included in `uploadFinishedPlatforms`, it means that the upload has finished for this platform.
        /// We should resume with uploading the next platform in `targetPlatforms` starting from first avatar in `uploadSettings`.
        /// </summary>
        public TargetPlatform uploadingTargetPlatform;
        /// <summary>
        /// The list of platforms that have finished uploading.
        /// </summary>
        public TargetPlatform[] uploadFinishedPlatforms = Array.Empty<TargetPlatform>();
        /// <summary>
        /// The list of errors that occurred during the upload.
        /// </summary>
        public List<UploadErrorInfo> uploadErrors = new();

        private bool isDeleting = false;
        private static bool isReloading = false;

        static UploaderProgressAsset()
        {
            AssemblyReloadEvents.beforeAssemblyReload += () => isReloading = true;
        }

        private void OnEnable()
        {
            hideFlags = HideFlags.DontUnloadUnusedAsset;
        }

        private void OnDisable()
        {
            if (!isDeleting && !isReloading)
                Debug.LogError("UploaderProgressAsset is unloaded unexpectedly. You should not unload this asset manually, it should use 'Delete' method instead.", this);
        }

        private void OnDestroy()
        {
            if (!isDeleting)
                Debug.Log("UploaderProgressAsset is destroyed unexpectedly. You should not destroy this asset manually, it should use 'Delete' method instead.", this);
        }

        public static UploaderProgressAsset? Load()
        {
            var loaded = AssetDatabase.LoadAssetAtPath<UploaderProgressAsset>(AssetPath);
            return loaded ? loaded : null;
        }

        public void Save()
        {
            if (this == null)
            {
                throw new NullReferenceException("this UploaderProgressAsset is destroyed. You cannot save it.");
            }

            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(this)))
            {
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssetIfDirty(this);
            }
            else if (System.IO.File.Exists(AssetPath))
            {
                throw new Exception($"{AssetPath} already exists");
            }
            else
            {
                AssetDatabase.CreateAsset(this, AssetPath);
            }
        }

        public void Delete()
        {
            if (AssetDatabase.LoadAssetAtPath<UploaderProgressAsset>(AssetPath) == this)
            {
                isDeleting = true;
                AssetDatabase.DeleteAsset(AssetPath);
            }
            else
            {
                Debug.Log("Deleting UploaderProgressAsset not at desired location. Can be a bug.", this);
                isDeleting = true;
                DestroyImmediate(this);
            }
        }
    }
    
    [Serializable]
    struct OpenedSceneInformation
    {
        public string scenePath;
        public bool isLoaded;
    }

    [Serializable]
    struct UploadErrorInfo
    {
        public TargetPlatform targetPlatform;
        public AvatarUploadSetting uploadingAvatar;
        public string message;
    }

    enum TargetPlatform
    {
        Windows,
        Android,
        iOS,
        
        // 
        LastIndex,
    }
}