using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VRC.SDK3.Avatars.Components;
using Task = System.Threading.Tasks.Task;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    public class ContinuousAvatarUploader : EditorWindow
    {
        [SerializeField] State state;
        [SerializeField] AvatarDescriptor avatarDescriptor;

        private SerializedObject _serialized;
        private SerializedProperty _avatarDescriptor;

        [MenuItem("Window/Continuous Avatar Uploader")]
        public static void OpenWindow() => GetWindow<ContinuousAvatarUploader>("ContinuousAvatarUploader");

        private void OnEnable()
        {
            _serialized = new SerializedObject(this);
            _avatarDescriptor = _serialized.FindProperty(nameof(avatarDescriptor));
        }

        private void OnGUI()
        {
            _serialized.Update();
            EditorGUILayout.PropertyField(_avatarDescriptor);
            _serialized.ApplyModifiedProperties();

            using (new EditorGUI.DisabledScope(!avatarDescriptor))
            {
                if (GUILayout.Button("Upload"))
                    Upload(avatarDescriptor);
            }
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
            }
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

        public void Upload(AvatarDescriptor avatar)
        {
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveOpenScenes();

            var avatarDescriptor = avatar.avatarDescriptor.TryResolve() as VRCAvatarDescriptor;
            if (!avatarDescriptor)
            {
                EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(avatar.avatarDescriptor.scene));
                avatarDescriptor = avatar.avatarDescriptor.TryResolve() as VRCAvatarDescriptor;
            }
            if (!avatarDescriptor)
            {
                Debug.LogError("Avatar Not found");
                return;
            }

            state = State.WaitingForUpload;

            VRC.SDKBase.Editor.VRC_SdkBuilder.ExportAndUploadAvatarBlueprint(avatarDescriptor.gameObject);
        }

        enum State
        {
            Idle,
            WaitingForUpload,
            Uploading,
        }
    }
}
