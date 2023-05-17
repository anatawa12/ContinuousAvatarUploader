using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    [CreateAssetMenu]
    public class AvatarDescriptor : ScriptableObject
    {
        public string avatarName;
        public SceneReference avatarDescriptor;
        public PlatformSpecificInfo quest = new PlatformSpecificInfo()
        {
            versionNamePrefix = "quest"
        };
        public PlatformSpecificInfo windows = new PlatformSpecificInfo()
        {
            versionNamePrefix = "v"
        };
    }

    [Serializable]
    public class SceneReference
    {
        public SceneAsset scene;
        public ulong objectId;
        public ulong prefabId;

        public SceneReference(Object obj)
        {
            var id = GlobalObjectId.GetGlobalObjectIdSlow(obj);
            if (id.identifierType != 2)
                throw new ArgumentException("Not a object");
            var path = AssetDatabase.GUIDToAssetPath(id.assetGUID.ToString());
            scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            if (!scene)
                throw new ArgumentException("Not a object");
            objectId = id.targetObjectId;
            prefabId = id.targetPrefabId;
        }

        public Object TryResolve()
        {
            var sceneGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(scene));
            System.Diagnostics.Debug.Assert(
                GlobalObjectId.TryParse($"GlobalObjectId_V1-2-{sceneGuid}-{objectId}-{prefabId}", out var oid));
            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(oid);
        }

        public bool IsNull() => objectId == 0;
    }

    [Serializable]
    public class PlatformSpecificInfo
    {
        public bool enabled;
        public bool versionNameEnabled;
        public string versionNamePrefix = "";
        public bool gitEnabled;
        public string tagPrefix = "";
        public string tagSuffix = "";
    }
}
