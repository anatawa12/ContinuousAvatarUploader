using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    public abstract class AvatarUploadSettingOrGroup : ScriptableObject
    {
        internal AvatarUploadSettingOrGroup()
        {
        }

        internal abstract AvatarUploadSetting[] Settings { get; }
    }

    [CreateAssetMenu(menuName = "Continuous Avatar Uploader/Avatar Upload Setting")]
    public class AvatarUploadSetting : AvatarUploadSettingOrGroup
    {
        public string avatarName;
        public MaySceneReference avatarDescriptor;
        public PlatformSpecificInfo ios = new PlatformSpecificInfo()
        {
            versionNamePrefix = "ios"
        };
        public PlatformSpecificInfo quest = new PlatformSpecificInfo()
        {
            versionNamePrefix = "quest"
        };
        public PlatformSpecificInfo windows = new PlatformSpecificInfo()
        {
            versionNamePrefix = "v"
        };

        internal PlatformSpecificInfo GetPlatformInfo(TargetPlatform targetPlatform) =>
            targetPlatform switch
            {
                TargetPlatform.Windows => windows,
                TargetPlatform.Android => quest,
                TargetPlatform.iOS => ios,
                _ => throw new ArgumentOutOfRangeException(nameof(targetPlatform), targetPlatform, null)
            };

        public PlatformSpecificInfo GetCurrentPlatformInfo() => GetPlatformInfo(Uploader.GetCurrentTargetPlatform());

        internal override AvatarUploadSetting[] Settings => new[] { this };

        private void Reset()
        {
            GetCurrentPlatformInfo().enabled = true;
        }
    }

    [Serializable]
    public class MaySceneReference
    {
        // might be reference to scene or asset itself
        [FormerlySerializedAs("scene")]
        public Object asset;
        public ulong objectId;
        public ulong prefabId;

        public MaySceneReference(Object obj)
        {
            if (obj is SceneAsset) throw new Exception("SceneAsset cannot be saved");
            var id = GlobalObjectId.GetGlobalObjectIdSlow(obj);
            switch (id.identifierType)
            {
                case 2:
                {
                    var path = AssetDatabase.GUIDToAssetPath(id.assetGUID.ToString());
                    asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                    if (!asset)
                        throw new ArgumentException("Not a object");
                    objectId = id.targetObjectId;
                    prefabId = id.targetPrefabId;
                    break;
                }
                case 1:
                case 3:
                {
                    asset = obj;
                    break;
                }
                default:
                    throw new ArgumentException("Not a object");
            }
        }

        public Object TryResolve()
        {
            if (!(asset is SceneAsset)) return asset;

            var sceneGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            System.Diagnostics.Debug.Assert(
                GlobalObjectId.TryParse($"GlobalObjectId_V1-2-{sceneGuid}-{objectId}-{prefabId}", out var oid));
            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(oid);
        }

        public bool IsNull() => asset == null || asset is SceneAsset && objectId == 0;

        public bool IsAssetReference() => !(asset is SceneAsset);

        public void OpenScene()
        {
            if (IsAssetReference()) throw new InvalidOperationException("It's asset reference");
            EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(asset));
        }
    }

    [Serializable]
    public class PlatformSpecificInfo
    {
        public bool enabled;
        public bool updateImage;
        public ImageTakeEditorMode imageTakeEditorMode;
        [FormerlySerializedAs("versionNameEnabled")] public bool versioningEnabled;
        public string versionNamePrefix = "";
        public bool gitEnabled;
        public string tagPrefix = "";
        public string tagSuffix = "";
    }

    public enum ImageTakeEditorMode
    {
        UseUploadGuiSetting,
        InEditMode,
        InPlayMode,
    }
}
