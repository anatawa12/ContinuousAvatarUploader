using System;
using UnityEngine;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    [CreateAssetMenu]
    public class AvatarUploadSettingGroup : ScriptableObject
    {
        public AvatarUploadSetting[] avatars = Array.Empty<AvatarUploadSetting>();
    }
}
