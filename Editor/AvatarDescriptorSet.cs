using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    [CreateAssetMenu]
    public class AvatarDescriptorSet : ScriptableObject
    {
        public string[] definedTags;
        public AvatarDescriptor[] avatars;
    }

    [Serializable]
    public class AvatarDescriptor
    {
        public string[] tags = Array.Empty<string>();
        public string name;
        public SceneReference avatarDescriptor;
        public PlatformSpecificInfo quest = new PlatformSpecificInfo();
        public PlatformSpecificInfo windows = new PlatformSpecificInfo();
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
    }

    [Serializable]
    public class PlatformSpecificInfo
    {
        public bool enabled;
        public string tagPrefix;
        public string tagSuffix;
        [Tooltip("prefix of version name on the description. for (v10), 'v' is the prefix.")]
        public string versionNamePrefix;
    }
}
