using UnityEditor;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    public static class Preferences
    {
        private const string EditorPrefsPrefix = "com.anatawa12.continuous-avatar-uploader.";

        public static float SleepSeconds
        {
            get => EditorPrefs.GetFloat(EditorPrefsPrefix + "sleep-seconds", 3);
            set => EditorPrefs.SetFloat(EditorPrefsPrefix + "sleep-seconds", value);
        }

        public static bool TakeThumbnailInPlaymodeByDefault
        {
            get => EditorPrefs.GetBool(EditorPrefsPrefix + "take-in-play", false);
            set => EditorPrefs.SetBool(EditorPrefsPrefix + "take-in-play", value);
        }

        public static bool ShowDialogWhenUploadFinished
        {
            get => EditorPrefs.GetBool(EditorPrefsPrefix + "dialog-when-finish", true);
            set => EditorPrefs.SetBool(EditorPrefsPrefix + "dialog-when-finish", value);
        }

        internal static bool UploadFor(TargetPlatform targetPlatform) => 
            EditorPrefs.GetBool(EditorPrefsPrefix + "upload-for." + targetPlatform, Uploader.GetCurrentTargetPlatform() == targetPlatform);

        internal static void SetUploadFor(TargetPlatform targetPlatform, bool value)
        {
            EditorPrefs.SetBool(EditorPrefsPrefix + "upload-for." + targetPlatform, value);
        }

        public static bool RollbackBuildPlatform
        {
            get => EditorPrefs.GetBool(EditorPrefsPrefix + "rollback-build-platform", true);
            set => EditorPrefs.SetBool(EditorPrefsPrefix + "rollback-build-platform", value);
        }

        public static int RetryCount
        {
            get => EditorPrefs.GetInt(EditorPrefsPrefix + "retry-count", 1);
            set => EditorPrefs.SetInt(EditorPrefsPrefix + "retry-count", value);
        }
    }
}