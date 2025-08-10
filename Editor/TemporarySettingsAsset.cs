#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    internal class TemporarySettingsAsset : ScriptableObject
    {
        public const string AssetPath = "Assets/com.anatawa12.continuous-avatar-uploader.temporary-settings.asset";

        /// <summary>
        /// The list of temporary avatar upload settings created via drag & drop.
        /// </summary>
        public List<AvatarUploadSetting> temporarySettings = new List<AvatarUploadSetting>();


        private bool isDeleting = false;
        private static bool isReloading = false;

        static TemporarySettingsAsset()
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
                Debug.LogError("TemporarySettingsAsset is unloaded unexpectedly. You should not unload this asset manually, it should use 'Delete' method instead.", this);
        }

        private void OnDestroy()
        {
            if (!isDeleting)
                Debug.Log("TemporarySettingsAsset is destroyed unexpectedly. You should not destroy this asset manually, it should use 'Delete' method instead.", this);
        }

        public static TemporarySettingsAsset? Load()
        {
            var loaded = AssetDatabase.LoadAssetAtPath<TemporarySettingsAsset>(AssetPath);
            return loaded ? loaded : null;
        }

        public static TemporarySettingsAsset LoadOrCreateInMemory()
        {
            var loaded = Load();
            if (loaded == null)
            {
                // Create instance in memory only, don't create asset file yet
                loaded = CreateInstance<TemporarySettingsAsset>();
            }
            return loaded;
        }

        public void Save()
        {
            if (this == null)
            {
                throw new NullReferenceException("this TemporarySettingsAsset is destroyed. You cannot save it.");
            }

            // Only save if this is already an asset in the AssetDatabase
            if (AssetDatabase.Contains(this))
            {
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssetIfDirty(this);
            }
        }

        public void AddTemporarySetting(AvatarUploadSetting setting)
        {
            if (setting == null) return;

            if (AssetDatabase.Contains(this))
            {
                var settingPath = AssetDatabase.GetAssetPath(setting);
                if (string.IsNullOrEmpty(settingPath))
                {
                    AssetDatabase.AddObjectToAsset(setting, this);
                }
            }

            if (!temporarySettings.Contains(setting))
            {
                temporarySettings.Add(setting);
            }

            if (AssetDatabase.Contains(this))
            {
                Save();
            }
        }

        public void RemoveTemporarySetting(AvatarUploadSetting setting)
        {
            if (setting == null) return;

            temporarySettings.Remove(setting);

            if (AssetDatabase.Contains(this) && AssetDatabase.Contains(setting))
            {
                var assetPath = AssetDatabase.GetAssetPath(setting);
                if (!string.IsNullOrEmpty(assetPath) && assetPath == AssetDatabase.GetAssetPath(this))
                {
                    DestroyImmediate(setting, true);
                }
            }
            else if (!AssetDatabase.Contains(this) && !AssetDatabase.Contains(setting))
            {
                DestroyImmediate(setting);
            }

            if (AssetDatabase.Contains(this))
            {
                Save();
            }
        }

        public void SaveAsAsset()
        {
            if (this == null)
            {
                throw new NullReferenceException("this TemporarySettingsAsset is destroyed. You cannot save it.");
            }

            if (!AssetDatabase.Contains(this))
            {
                AssetDatabase.CreateAsset(this, AssetPath);

                foreach (var setting in temporarySettings)
                {
                    if (setting != null && !AssetDatabase.Contains(setting))
                    {
                        AssetDatabase.AddObjectToAsset(setting, this);
                    }
                }

                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssetIfDirty(this);
            }
            else
            {
                Save();
            }
        }

        public void ClearAllTemporarySettings()
        {
            // Remove all sub-assets
            foreach (var setting in temporarySettings)
            {
                if (setting != null)
                {
                    var assetPath = AssetDatabase.GetAssetPath(setting);
                    if (!string.IsNullOrEmpty(assetPath) && assetPath == AssetDatabase.GetAssetPath(this))
                    {
                        DestroyImmediate(setting, true);
                    }
                }
            }

            temporarySettings.Clear();
            Save();
        }

        public void Delete()
        {
            if (AssetDatabase.LoadAssetAtPath<TemporarySettingsAsset>(AssetPath) == this)
            {
                isDeleting = true;
                // Clear all sub-assets first
                ClearAllTemporarySettings();
                AssetDatabase.DeleteAsset(AssetPath);
            }
            else
            {
                Debug.Log("Deleting TemporarySettingsAsset not at desired location. Can be a bug.", this);
                isDeleting = true;
                DestroyImmediate(this);
            }
        }
    }
}