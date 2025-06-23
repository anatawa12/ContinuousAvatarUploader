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

        public static UploaderProgressAsset? Load()
        {
            var loaded = AssetDatabase.LoadAssetAtPath<UploaderProgressAsset>(AssetPath);
            return loaded ? loaded : null;
        }

        public void Save()
        {
            AssetDatabase.CreateAsset(this, AssetPath);
            AssetDatabase.SaveAssets();
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
        IOS,
        
        // 
        LastIndex,
    }
}