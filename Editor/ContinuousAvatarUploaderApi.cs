namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    public static class ContinuousAvatarUploaderApi
    {
        public static bool IsUploadInProgress => UploadOrchestrator.IsUploadInProgress();
        public static void CancelUpload() => UploadOrchestrator.CancelUpload();
    }
}