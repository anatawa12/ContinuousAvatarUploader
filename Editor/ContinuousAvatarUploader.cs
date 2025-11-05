using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using VRC.Core;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3A.Editor;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    public class ContinuousAvatarUploader : EditorWindow
    {
        [SerializeField] [ItemCanBeNull] [NotNull] internal AvatarUploadSettingOrGroup[] settingsOrGroups = Array.Empty<AvatarUploadSettingOrGroup>();
        [SerializeField] private List<MaySceneReference> temporarySettings = new List<MaySceneReference>();

        [CanBeNull]
        internal static ContinuousAvatarUploader Instance
        {
            get
            {
                var objects = Resources.FindObjectsOfTypeAll(typeof(ContinuousAvatarUploader));
                return objects.Length == 0 ? null : (ContinuousAvatarUploader)objects[0];
            }
        }

        // for uploading avatars

        [NonSerialized] private State _guiState;
        [NonSerialized] private AvatarUploadSetting _currentUploadingAvatar;
        [SerializeField] private Vector2 uploadsScroll;
        [SerializeField] private Vector2 errorsScroll;
        [SerializeField] private Vector2 temporaryAvatarsScroll;
        [SerializeField] private List<UploadErrorInfo> uploadErrors = new List<UploadErrorInfo>();
        [SerializeField] private bool dragDropFoldout = false;

        private UploaderProgressAsset progressAsset;

        private SerializedObject _serialized;
        private SerializedProperty _settingsOrGroups;

        [MenuItem("Window/Continuous Avatar Uploader")]
        [MenuItem("Tools/Continuous Avatar Uploader")]
        private static void OpenWindowItem() => OpenWindow();
        public static ContinuousAvatarUploader OpenWindow() => GetWindow<ContinuousAvatarUploader>("ContinuousAvatarUploader");

        private void OnEnable()
        {
            _serialized = new SerializedObject(this);
            _settingsOrGroups = _serialized.FindProperty(nameof(settingsOrGroups));
            _settingsOrGroups.isExpanded = true;

            CleanupTempGroupAsset();

            VRCSdkControlPanel.OnSdkPanelEnable += OnSdkPanelEnableDisable;
            VRCSdkControlPanel.OnSdkPanelDisable += OnSdkPanelEnableDisable;
            UploadOrchestrator.OnUploadSingleAvatarStarted += OnUploadSingleAvatarStarted;
            UploadOrchestrator.OnUploadSingleAvatarFinished += OnUploadSingleAvatarFinished;
            UploadOrchestrator.OnUploadFinished += OnUploadFinished;
            UploadOrchestrator.OnLoginFailed += OnLoginFailed;
            UploadOrchestrator.OnRandomException += OnRandomException;
        }

        private void OnDisable()
        {
            VRCSdkControlPanel.OnSdkPanelEnable -= OnSdkPanelEnableDisable;
            VRCSdkControlPanel.OnSdkPanelDisable -= OnSdkPanelEnableDisable;
            UploadOrchestrator.OnUploadSingleAvatarStarted -= OnUploadSingleAvatarStarted;
            UploadOrchestrator.OnUploadSingleAvatarFinished -= OnUploadSingleAvatarFinished;
            UploadOrchestrator.OnUploadFinished -= OnUploadFinished;
            UploadOrchestrator.OnLoginFailed -= OnLoginFailed;
            UploadOrchestrator.OnRandomException -= OnRandomException;
            CleanupTempGroupAsset();
        }

        private void OnSdkPanelEnableDisable(object sender, EventArgs e) => Repaint();

        private void OnUploadSingleAvatarStarted(UploaderProgressAsset progress, AvatarUploadSetting avatar)
        {
            _guiState = State.UploadingAvatar;
            _currentUploadingAvatar = avatar;
            Repaint();
        }

        private void OnUploadSingleAvatarFinished(UploaderProgressAsset progress, AvatarUploadSetting avatar)
        {
            _guiState = State.UploadedAvatar;
            _currentUploadingAvatar = null;
            Repaint();
        }

        private void OnUploadFinished(UploaderProgressAsset obj, bool successfully)
        {
            _guiState = State.Configuring;
            _currentUploadingAvatar = null;
            // if finished unsuccessfully, we should have shown error dialog already
            if (Preferences.ShowDialogWhenUploadFinished && successfully)
                EditorUtility.DisplayDialog("Continuous Avatar Uploader", "Finished Uploading Avatars!", "OK");

            CleanupTempGroupAsset();

            Repaint();
        }

        private void OnLoginFailed(Exception obj)
        {
            _guiState = State.Configuring;
            _currentUploadingAvatar = null;
            EditorUtility.DisplayDialog("Continuous Avatar Uploader", "Login Failed: " + obj.Message, "OK");
        }

        private void OnRandomException(Exception obj)
        {
            _guiState = State.Configuring;
            _currentUploadingAvatar = null;
            EditorUtility.DisplayDialog("Continuous Avatar Uploader", "An error occurred: " + obj.Message, "OK");
        }

        private void OnGUI()
        {
            var loaded = UploaderProgressAsset.Load();
            progressAsset = loaded != null ? loaded : progressAsset;
            progressAsset = progressAsset == null ? null : progressAsset;
            uploadErrors = progressAsset?.uploadErrors ?? uploadErrors;
            var uploadInProgress = progressAsset != null;
            if (uploadInProgress)
            {
                var totalCount = progressAsset.uploadSettings.Length;
                var uploadingIndex = progressAsset.uploadingAvatarIndex;
                var totalPlatforms = progressAsset.targetPlatforms.Length;
                var uploadingTargetCount = progressAsset.uploadFinishedPlatforms.Length;
                GUILayout.Label("UPLOAD IN PROGRESS");
                EditorGUI.ProgressBar(GUILayoutUtility.GetRect(100, 20),
                    (uploadingTargetCount + 0.5f) / totalPlatforms,
                    $"Uploading for {progressAsset.uploadingTargetPlatform} ({uploadingTargetCount + 1} / {totalPlatforms} platforms)");
                EditorGUI.ProgressBar(GUILayoutUtility.GetRect(100, 20),
                    (uploadingIndex + 0.5f) / totalCount,
                    $"Uploading {uploadingIndex + 1} / {totalCount} for {progressAsset.uploadingTargetPlatform}");
                if (_currentUploadingAvatar)
                {
                    EditorGUILayout.ObjectField("Uploading", _currentUploadingAvatar, typeof(AvatarUploadSetting), true);
                }
                else
                {
                    GUILayout.Label("Sleeping a little");
                }

                if (GUILayout.Button("ABORT UPLOAD"))
                    UploadOrchestrator.CancelUpload();
            }

            EditorGUI.BeginDisabledGroup(uploadInProgress);
            _serialized.Update();
            Preferences.SleepSeconds = EditorGUILayout.FloatField(
                new GUIContent("Sleep Seconds", "The time sleeps between upload"),
                Preferences.SleepSeconds);
            Preferences.TakeThumbnailInPlaymodeByDefault = EditorGUILayout.ToggleLeft(
                new GUIContent("Take Thumbnail In PlayMode by Default", 
                    "If this is enabled, CAU will take Thumbnail after entering PlayMode."),
                Preferences.TakeThumbnailInPlaymodeByDefault);
            Preferences.ShowDialogWhenUploadFinished = EditorGUILayout.ToggleLeft(
                new GUIContent("Show Dialog when Finished", 
                    "If this is enabled, CAU will tell you upload finished."),
                Preferences.ShowDialogWhenUploadFinished);
            Preferences.RollbackBuildPlatform = EditorGUILayout.ToggleLeft(
                new GUIContent("Rollback Build Platform", 
                    "If this is enabled, CAU will rollback the build platform to the one before upload after upload finished."),
                Preferences.RollbackBuildPlatform);
            Preferences.RetryCount = EditorGUILayout.IntField(
                new GUIContent("Retry Count", "The number of retries to attempt for each upload. Zero means no retries, so only one attempt will be made."),
                Preferences.RetryCount);

            EditorGUILayout.Space();
            dragDropFoldout = EditorGUILayout.Foldout(dragDropFoldout, new GUIContent("Drag & Drop Upload"), true, EditorStyles.foldoutHeader);
            if (dragDropFoldout)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.Space();
                HandleDragAndDrop();

                if (temporarySettings.Count > 0)
                {
                    EditorGUILayout.Space();
                    DrawTemporaryAvatarList();
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Target Platforms", EditorStyles.boldLabel);
            foreach (var platform in Uploader.GetTargetPlatforms())
            {
                var isEnabled = Preferences.UploadFor(platform);
                var supported = Uploader.IsBuildSupportedInstalled(platform);
                EditorGUI.BeginDisabledGroup(!supported && !isEnabled);
                isEnabled = EditorGUILayout.ToggleLeft(
                    new GUIContent($"Upload for {platform}", 
                        supported ? $"If this is enabled, CAU will upload avatars for {platform} platform."
                            : $"Build support for {platform} is not installed. "),
                    isEnabled);
                EditorGUI.EndDisabledGroup();
                Preferences.SetUploadFor(platform, isEnabled);
            }

            var checkResult = CheckUpload();
            if ((checkResult & UploadCheckResult.Uploading) != 0)
                EditorGUILayout.HelpBox("Uploading", MessageType.Info);
            if ((checkResult & UploadCheckResult.NoDescriptors) != 0)
                EditorGUILayout.HelpBox("No AvatarUploadSettings are specified", MessageType.Error);
            if ((checkResult & UploadCheckResult.AnyNull) != 0)
                EditorGUILayout.HelpBox("Some AvatarUploadSetting or Group are None", MessageType.Error);
            if ((checkResult & UploadCheckResult.PlayMode) != 0)
                EditorGUILayout.HelpBox("To upload avatars, exit Play mode", MessageType.Error);
            if ((checkResult & UploadCheckResult.NoCredentials) != 0)
                EditorGUILayout.HelpBox("Please login in control panel", MessageType.Error);
            if ((checkResult & UploadCheckResult.ControlPanelClosed) != 0)
                EditorGUILayout.HelpBox("Please open Control panel", MessageType.Error);
            if ((checkResult & UploadCheckResult.NoAvatarBuilder) != 0)
                EditorGUILayout.HelpBox("No Valid VRCSDK Avatars Found", MessageType.Error);
            if ((checkResult & UploadCheckResult.PlayModeSettingsNotGood) != 0)
                EditorGUILayout.HelpBox(
                    "Some avatars are going or taking thumbnail in PlayMode. " +
                    "To take thumbnail in PlayMode, Please Disable 'Reload Domain' Option in " +
                    "Enter Play Mode Settings in Editor in Project Settings",
                    MessageType.Error);
            if ((checkResult & UploadCheckResult.UnsupportedPlatformSelected) != 0)
                EditorGUILayout.HelpBox(
                    "Some target platforms are selected, but not supported by current build. " +
                    "Please install the build support for those platforms in Unity Hub, or " +
                    "uncheck the target platforms in Continuous Avatar Uploader settings.",
                    MessageType.Error);
            if ((checkResult & UploadCheckResult.NoPlatformsSelected) != 0)
                EditorGUILayout.HelpBox(
                    "No target platforms are selected. " +
                    "Please select at least one target platform in Continuous Avatar Uploader settings.",
                    MessageType.Error);
            using (new EditorGUI.DisabledScope(checkResult != UploadCheckResult.Ok))
            {
                if (GUILayout.Button("Start Upload"))
                {
                    DoStartUpload();
                }
            }

            uploadsScroll = EditorGUILayout.BeginScrollView(uploadsScroll);
            EditorGUILayout.PropertyField(_settingsOrGroups);
            if (GUI.Button(EditorGUI.IndentedRect(EditorGUILayout.GetControlRect()), "Clear Settings"))
                _settingsOrGroups.arraySize = 0;
            _serialized.ApplyModifiedProperties();
            EditorGUILayout.EndScrollView();

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();
            GUILayout.Label("Errors from Previous Build:", EditorStyles.boldLabel);
            errorsScroll = EditorGUILayout.BeginScrollView(errorsScroll);
            if (uploadErrors.Count == 0) GUILayout.Label("No Errors");
            else
            {
                foreach (var previousUploadError in uploadErrors)
                {
                    if (previousUploadError.uploadingAvatar != null)
                    {
                        EditorGUILayout.ObjectField("Uploading", previousUploadError.uploadingAvatar,
                            typeof(AvatarUploadSetting), false);
                    }
                    else if (previousUploadError.avatarDescriptor.asset != null
                                && previousUploadError.avatarDescriptor.GetCachedResolve() is VRCAvatarDescriptor descriptor)
                    {
                        EditorGUILayout.ObjectField("Uploading", descriptor, typeof(VRCAvatarDescriptor), false);
                    }
                    else
                    {
                        EditorGUILayout.LabelField("Avatar", previousUploadError.avatarName);
                    }
                    EditorGUILayout.EnumPopup("For", previousUploadError.targetPlatform);
                    EditorGUILayout.TextArea(previousUploadError.message);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        [Flags]
        internal enum UploadCheckResult
        {
            Ok = 0,
            Uploading = 1 << 0,
            NoDescriptors = 1 << 1,
            AnyNull = 1 << 2,
            PlayMode = 1 << 3,
            NoCredentials = 1 << 4,
            ControlPanelClosed = 1 << 5,
            NoAvatarBuilder = 1 << 6,
            PlayModeSettingsNotGood = 1 << 7,
            UnsupportedPlatformSelected = 1 << 8,
            NoPlatformsSelected = 1 << 9,
        }

        // We do omit temp since play mode settings is always default
        private UploadCheckResult CheckUpload() => CheckUploadStatic(GetUploadingAvatars(includeTemp: false), Repaint);

        private static UploadCheckResult CheckUploadStatic(IEnumerable<AvatarUploadSetting> avatars, [CanBeNull] Action repaint = null)
        {
            var result = UploadCheckResult.Ok;
            if (UploadOrchestrator.IsUploadInProgress()) result |= UploadCheckResult.Uploading;
            if (EditorApplication.isPlayingOrWillChangePlaymode) result |= UploadCheckResult.PlayMode;
            if (!Uploader.VerifyCredentials(repaint)) result |= UploadCheckResult.NoCredentials;
            if (!VRCSdkControlPanel.window) result |= UploadCheckResult.ControlPanelClosed;
            if (!VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out _)) result |= UploadCheckResult.NoAvatarBuilder;
            if (!CheckPlaymodeSettings(avatars)) result |= UploadCheckResult.PlayModeSettingsNotGood;

            foreach (var platform in Uploader.GetTargetPlatforms())
            {
                if (!Uploader.IsBuildSupportedInstalled(platform) && Preferences.UploadFor(platform))
                    result |= UploadCheckResult.UnsupportedPlatformSelected;
            }

            if (!Uploader.GetTargetPlatforms().Any(Preferences.UploadFor))
                result |= UploadCheckResult.NoPlatformsSelected;

            return result;
        }

        internal bool StartUpload()
        {
            if (CheckUpload() != UploadCheckResult.Ok) return false;
            DoStartUpload();
            return true;
        }

        private static bool CheckPlaymodeSettings(IEnumerable<AvatarUploadSetting> avatars)
        {
            if (Utils.ReloadDomainDisabled())
                return true;

            if (Preferences.TakeThumbnailInPlaymodeByDefault)
                return false;

            foreach (var avatarUploadSetting in avatars)
            {
                var currentInfo = avatarUploadSetting.GetCurrentPlatformInfo();
                if (currentInfo.updateImage)
                {
                    bool enterPlaymode;
                    switch (currentInfo.imageTakeEditorMode)
                    {
                        case ImageTakeEditorMode.UseUploadGuiSetting:
                            enterPlaymode = Preferences.TakeThumbnailInPlaymodeByDefault;
                            break;
                        case ImageTakeEditorMode.InEditMode:
                            enterPlaymode = false;
                            break;
                        case ImageTakeEditorMode.InPlayMode:
                            enterPlaymode = true;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    if (enterPlaymode)
                        return false;
                }
            }

            // there are need to disable reload domain
            return true;
        }

        private IEnumerable<AvatarUploadSetting> GetUploadingAvatars(bool includeTemp = true) =>
            settingsOrGroups
                .Where(x => x)
                .SelectMany(x => x.Settings)
                .Where(x => x)
                .Concat(includeTemp ? CreateTemporarySettings() : Array.Empty<AvatarUploadSetting>());

        private List<AvatarUploadSetting> CreateTemporarySettings()
        {
            var tempSettings = new List<AvatarUploadSetting>();
            foreach (var maySceneRef in temporarySettings)
            {
                if (maySceneRef.asset == null) continue;

                var tempSetting = CreateTemporarySetting(maySceneRef);
                if (tempSetting != null)
                {
                    tempSettings.Add(tempSetting);
                }
            }

            return tempSettings;
        }

        private void CleanupTempGroupAsset()
        {
        }

        private void DoStartUpload()
        {

            var progress = ScriptableObject.CreateInstance<UploaderProgressAsset>();
            progress.openedScenes = UploadOrchestrator.GetLastOpenedScenes();
            progress.uploadSettings = GetUploadingAvatars().ToArray();
            progress.targetPlatforms = Uploader.GetTargetPlatforms().Where(Preferences.UploadFor).ToArray();
            progress.sleepMilliseconds = (int)(Preferences.SleepSeconds * 1000);
            progress.rollbackPlatform = Preferences.RollbackBuildPlatform;
            progress.retryCount = Preferences.RetryCount;
            UploadOrchestrator.StartUpload(progress);
        }

        enum State
        {
            Configuring,

            // upload avatar process
            UploadingAvatar,
            UploadedAvatar,
        }

        static class UploadButtonGuiStyles
        {
            public static GUIContent label = new("Upload This Avatar",
                "Upload this avatar to the current target platform. " +
                "If you want to upload multiple avatars, use the Continuous Avatar Uploader window.");
        }

        public static void UploadButtonGui(IEnumerable<AvatarUploadSettingOrGroup> avatarOrGroups, [CanBeNull] Action repaint = null)
        {
            var avatars = avatarOrGroups
                .Where(x => x)
                .SelectMany(x => x.Settings)
                .Where(x => x)
                .ToArray();
            var check = CheckUploadStatic(avatars, repaint);

            // target platform selector
            var flags = FlagsForCurrentBuildPlatforms();
            EditorGUI.BeginChangeCheck();
            flags = (TargetPlatformFlags)EditorGUILayout.EnumFlagsField("Target Platforms", flags);
            if (EditorGUI.EndChangeCheck()) SetBuildPlatforms(flags);

            EditorGUI.BeginDisabledGroup(check != UploadCheckResult.Ok);
            var guiContent = UploadButtonGuiStyles.label;
            guiContent.text = avatars.Length == 1 ? "Upload This Avatar" : $"Upload {avatars.Length} Avatars";
            guiContent.tooltip = check == UploadCheckResult.Ok ? "" : "Cannot upload avatars now. Check the Continuous Avatar Uploader window for details.";
            if (GUILayout.Button(guiContent))
            {
                var uploader = OpenWindow();
                uploader.settingsOrGroups = avatars.ToArray<AvatarUploadSettingOrGroup>();
                if (!uploader.StartUpload())
                {
                    EditorUtility.DisplayDialog("Failed to start upload",
                        "Failed to start upload.\nPlease refer Uploader window for reason", "OK");
                }
            }
            EditorGUI.EndDisabledGroup();
        }

        private static TargetPlatformFlags FlagsForCurrentBuildPlatforms()
        {
            TargetPlatformFlags flags = 0;
            foreach (var platform in Uploader.GetTargetPlatforms())
            {
                if (Preferences.UploadFor(platform))
                {
                    flags |= (TargetPlatformFlags)(1 << (int)platform);
                }
            }
            return flags;
        }

        private static void SetBuildPlatforms(TargetPlatformFlags flags)
        {
            foreach (var platform in Uploader.GetTargetPlatforms())
            {
                var flag = (TargetPlatformFlags)(1 << (int)platform);
                Preferences.SetUploadFor(platform, (flags & flag) != 0);
            }
        }

        [Flags]
        enum TargetPlatformFlags
        {
            Windows = 1 << (int)TargetPlatform.Windows,
            Android = 1 << (int)TargetPlatform.Android,
            iOS = 1 << (int)TargetPlatform.iOS,
        }


        private void HandleDragAndDrop()
        {
            var dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
            var style = new GUIStyle(GUI.skin.box);
            style.alignment = TextAnchor.MiddleCenter;
            style.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.7f, 0.7f, 0.7f) : new Color(0.3f, 0.3f, 0.3f);
            GUI.Box(dropArea, "Drag Avatar Prefabs or GameObjects Here", style);

            var currentEvent = Event.current;

            switch (currentEvent.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropArea.Contains(currentEvent.mousePosition))
                        return;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                    if (currentEvent.type != EventType.DragPerform)
                        break;

                    DragAndDrop.AcceptDrag();

                    foreach (var draggedObject in DragAndDrop.objectReferences)
                    {
                        if (!(draggedObject is GameObject go))
                            continue;

                        var descriptor = go.GetComponent<VRCAvatarDescriptor>();
                        if (descriptor == null)
                            continue;

                        var maySceneRef = new MaySceneReference(descriptor);
                        temporarySettings.Add(maySceneRef);
                    }
                    currentEvent.Use();
                    Repaint();
                    break;
            }
        }

        private AvatarUploadSetting CreateTemporarySetting(MaySceneReference maySceneRef)
        {
            var descriptor = maySceneRef.GetCachedResolve() as VRCAvatarDescriptor;
            if (descriptor == null) return null;

            var tempSetting = ScriptableObject.CreateInstance<AvatarUploadSetting>();
            tempSetting.name = descriptor.gameObject.name;
            tempSetting.avatarDescriptor = maySceneRef;
            tempSetting.avatarName = descriptor.gameObject.name;

            tempSetting.windows.enabled = true;
            tempSetting.quest.enabled = true;
            tempSetting.ios.enabled = true;

            tempSetting.hideFlags = HideFlags.DontUnloadUnusedAsset;

            return tempSetting;
        }


        private void DrawTemporaryAvatarList()
        {
            EditorGUILayout.LabelField("Avatar List:");

            const float itemHeight = 20f;
            const int maxVisibleItems = 8;

            if (temporarySettings.Count > maxVisibleItems)
            {
                temporaryAvatarsScroll = EditorGUILayout.BeginScrollView(
                    temporaryAvatarsScroll,
                    GUILayout.MaxHeight(maxVisibleItems * itemHeight)
                );
            }

            for (int i = temporarySettings.Count - 1; i >= 0; i--)
            {
                var maySceneRef = temporarySettings[i];
                if (maySceneRef.asset == null)
                {
                    temporarySettings.RemoveAt(i);
                    continue;
                }

                var descriptor = maySceneRef.GetCachedResolve() as VRCAvatarDescriptor;
                var avatarName = descriptor?.gameObject.name ?? "Missing Avatar";

                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                var newDescriptor = EditorGUILayout.ObjectField(avatarName, descriptor, typeof(VRCAvatarDescriptor), true) as VRCAvatarDescriptor;
                if (EditorGUI.EndChangeCheck() && newDescriptor != null && newDescriptor != descriptor)
                {
                    temporarySettings[i] = new MaySceneReference(newDescriptor);
                }

                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    temporarySettings.RemoveAt(i);
                }
                EditorGUILayout.EndHorizontal();
            }

            if (temporarySettings.Count > maxVisibleItems)
            {
                EditorGUILayout.EndScrollView();
            }

            if (GUILayout.Button("Clear All D&D Avatars"))
            {
                temporarySettings.Clear();
            }
        }
    }
}
