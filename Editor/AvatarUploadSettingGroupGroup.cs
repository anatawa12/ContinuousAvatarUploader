using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    [CreateAssetMenu(menuName = "Continuous Avatar Uploader/Avatar Upload Setting Group Group")]
    public class AvatarUploadSettingGroupGroup : AvatarUploadSettingOrGroup
    {
        [FormerlySerializedAs("avatars")] public AvatarUploadSettingOrGroup[] groups = Array.Empty<AvatarUploadSettingOrGroup>();

        // note: possible infinite loop
        internal override AvatarUploadSetting[] Settings =>
            groups.SelectMany(x => x.Settings).ToArray();
    }
}
