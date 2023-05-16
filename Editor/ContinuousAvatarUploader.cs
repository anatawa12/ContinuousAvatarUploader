using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VRC.Core;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase.Editor;
using Task = System.Threading.Tasks.Task;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    public class ContinuousAvatarUploader : EditorWindow
    {
        [SerializeField] State state;
        [SerializeField] int processingIndex = -1;
        [SerializeField] AvatarDescriptor[] avatarDescriptors;
        [SerializeField] AvatarDescriptor uploadingAvatar;

        private SerializedObject _serialized;
        private SerializedProperty _avatarDescriptor;

        [MenuItem("Window/Continuous Avatar Uploader")]
        public static void OpenWindow() => GetWindow<ContinuousAvatarUploader>("ContinuousAvatarUploader");

        private void OnEnable()
        {
            _serialized = new SerializedObject(this);
            _avatarDescriptor = _serialized.FindProperty(nameof(avatarDescriptors));
        }

        private void OnGUI()
        {
            if (uploadingAvatar)
            {
                GUILayout.Label("UPLOAD IN PROGRESS");
                GUILayout.BeginHorizontal();
                GUILayout.Label("Uploading ");
                EditorGUILayout.ObjectField(uploadingAvatar, typeof(AvatarDescriptor), true);
                GUILayout.EndHorizontal();
            }
            EditorGUI.BeginDisabledGroup(uploadingAvatar);
            _serialized.Update();
            EditorGUILayout.PropertyField(_avatarDescriptor);
            _serialized.ApplyModifiedProperties();

            var noDescriptors = avatarDescriptors.Length == 0;
            var anyNull = avatarDescriptors.Any(x => !x);
            var playMode = !uploadingAvatar && EditorApplication.isPlayingOrWillChangePlaymode;
            var noCredentials = !APIUser.IsLoggedIn;
            if (noDescriptors) EditorGUILayout.HelpBox("No AvatarDescriptor are specified", MessageType.Error);
            if (anyNull) EditorGUILayout.HelpBox("Some AvatarDescriptor is None", MessageType.Error);
            if (playMode) EditorGUILayout.HelpBox("To upload avatars, exit Play mode", MessageType.Error);
            if (noCredentials) EditorGUILayout.HelpBox("Please login in control panel", MessageType.Error);
            using (new EditorGUI.DisabledScope(noDescriptors || anyNull || playMode || noCredentials))
            {
                if (GUILayout.Button("Start Upload"))
                {
                    processingIndex = 0;
                    ContinueUpload();
                    EditorUtility.SetDirty(this);
                }
            }
            EditorGUI.EndDisabledGroup();
        }

        private void Update()
        {
            if (state == State.Idle) return;

            if (EditorApplication.isPlaying && state == State.WaitingForUpload)
            {
                state = State.Uploading;
                StartUpload();
            }
            if (!EditorApplication.isPlaying && state == State.Uploading)
            {
                state = State.Idle;
                uploadingAvatar = null;
                if (processingIndex >= 0)
                {
                    processingIndex++;
                    ContinueUpload();
                }
            }
        }

        private async void ContinueUpload()
        {
            for (; processingIndex < avatarDescriptors.Length; processingIndex++)
            {
                // returns true means start upload successful.
                if (await Upload(avatarDescriptors[processingIndex]))
                    return;
            }

            // done everything.

            processingIndex = -1;
        }

        public async void StartUpload()
        {
            await Task.Delay(1500);

            var titleText = GameObject.Find("VRCSDK/UI/Canvas/AvatarPanel/Title Text")
                                      .GetComponent<Text>();
            titleText.text = "Phalanx Upload!";

            await Task.Delay(2500);

            // we're running baby!
            //Debug.Log("Runtime Phalanx for " + AvatarName + " (" + AvatarId + ")");

            var nameField = GameObject.Find("VRCSDK/UI/Canvas/AvatarPanel/Avatar Info Panel/Settings Section/Name Input Field")
                                      .GetComponent<InputField>();
            var descriptionField = GameObject.Find("VRCSDK/UI/Canvas/AvatarPanel/Avatar Info Panel/Settings Section/DescriptionBackdrop/Description Input Field")
                                             .GetComponent<InputField>();
            var warrantToggle = GameObject.Find("VRCSDK/UI/Canvas/AvatarPanel/Avatar Info Panel/Settings Section/Upload Section/ToggleWarrant")
                                          .GetComponent<Toggle>();
            var uploadButton = GameObject.Find("VRCSDK/UI/Canvas/AvatarPanel/Avatar Info Panel/Settings Section/Upload Section/UploadButton")
                                         .GetComponent<Button>();

            while (nameField.text == "")
                await Task.Delay(2500);

            // TODO: update description
            descriptionField.text = descriptionField.text + " Test Upload using ContinuousAvatarUploader";

            warrantToggle.isOn = true;

            EditorPatcher.Patcher.DisplayDialog += args =>
            {
                if (args.Title == "VRChat SDK") args.Result = true;
            };

            uploadButton.OnPointerClick(new PointerEventData(EventSystem.current));
        }

        private async Task<bool> Upload(AvatarDescriptor avatar)
        {
            if (state != State.Idle)
                throw new InvalidOperationException($"Don't start upload while {state}");
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"Upload started for {avatar.name}");

            var avatarDescriptor = avatar.avatarDescriptor.TryResolve() as VRCAvatarDescriptor;
            if (!avatarDescriptor)
            {
                EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(avatar.avatarDescriptor.scene));
                avatarDescriptor = avatar.avatarDescriptor.TryResolve() as VRCAvatarDescriptor;
            }
            if (!avatarDescriptor)
            {
                Debug.LogError("Upload failed: avatar not found", avatar);
                return false;
            }

            Debug.Log($"Actual avatar name: {avatarDescriptor.name}");

            state = State.Building;
            uploadingAvatar = avatar;
            try
            {
                // wait a while to show updates on the window
                await Task.Delay(100);

                var successful = VRC_SdkBuilder.ExportAndUploadAvatarBlueprint(avatarDescriptor.gameObject);
                if (successful)
                {
                    state = State.WaitingForUpload;
                }

                return successful;
            }
            catch
            {
                uploadingAvatar = null;
                state = State.Idle;
                throw;
            }
        }

        enum State
        {
            Idle,
            Building,
            WaitingForUpload,
            Uploading,
        }
    }
}
