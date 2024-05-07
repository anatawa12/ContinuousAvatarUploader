using System;
using UnityEngine;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    [CreateAssetMenu(menuName = "Continuous Avatar Uploader/Avatar Upload Setting Group")]
    public class AvatarUploadSettingGroup : AvatarUploadSettingOrGroup
    {
        public AvatarUploadSetting[] avatars = Array.Empty<AvatarUploadSetting>();

        internal override AvatarUploadSetting[] Settings => avatars;
    }
}
