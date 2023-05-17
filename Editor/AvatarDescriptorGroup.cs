using System;
using UnityEngine;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    [CreateAssetMenu]
    public class AvatarDescriptorGroup : ScriptableObject
    {
        public AvatarDescriptor[] avatars = Array.Empty<AvatarDescriptor>();
    }
}
