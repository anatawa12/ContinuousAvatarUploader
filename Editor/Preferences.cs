using UnityEditor;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    public static class Preferences
    {
        private const string EditorPrefsPrefix = "com.anatawa12.continuous-avatar-uploader.";

        public static float SleepSeconds
        {
            get => EditorPrefs.GetFloat(EditorPrefsPrefix, 3);
            set => EditorPrefs.SetFloat(EditorPrefsPrefix, value);
        }
    }
}