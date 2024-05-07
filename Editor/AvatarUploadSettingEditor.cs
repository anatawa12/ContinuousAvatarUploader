using System;
using System.Reflection;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

// ReSharper disable AccessToStaticMemberViaDerivedType

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    [CustomEditor(typeof(AvatarUploadSetting))]
    public class AvatarUploadSettingEditor : UnityEditor.Editor
    {
        private VRCAvatarDescriptor _cachedAvatar;
        private bool _settingAvatar;
        [CanBeNull] private PreviewCameraManager _previewCameraManager;

        public override void OnInspectorGUI()
        {
            var asset = (AvatarUploadSetting)target;
            EditorGUI.BeginChangeCheck();

            AvatarDescriptors(asset);

            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(asset);
        }

        private void OnDisable()
        {
            if (_previewCameraManager != null)
            {
                _previewCameraManager.Finish();
                _previewCameraManager = null;
            }
        }

        private void AvatarDescriptors(AvatarUploadSetting avatar)
        {
            if (avatar.avatarDescriptor.IsNull())
                _settingAvatar = true;
            if (_settingAvatar)
            {
                _cachedAvatar = (VRCAvatarDescriptor)EditorGUILayout.ObjectField("Set Avatar: ", 
                    null, typeof(VRCAvatarDescriptor), true);
                
                if (_cachedAvatar)
                {
                    avatar.avatarDescriptor = new MaySceneReference(_cachedAvatar);
                    // might be reverted if it's individual asset but
                    // this is good for DescriptorSet
                    avatar.name = avatar.avatarName = _cachedAvatar.name;
                    _settingAvatar = false;
                }

                EditorGUI.BeginDisabledGroup(avatar.avatarDescriptor.IsNull());
                if (GUILayout.Button("Chancel Change Avatar"))
                    _settingAvatar = false;
                EditorGUI.EndDisabledGroup();
            }
            else
            {
                if (!_cachedAvatar)
                    _cachedAvatar = avatar.avatarDescriptor.TryResolve() as VRCAvatarDescriptor;
                if (_cachedAvatar)
                {
                    EditorGUILayout.ObjectField("Avatar", _cachedAvatar, typeof(VRCAvatarDescriptor), true);
                    avatar.avatarName = _cachedAvatar.name;
                }
                else
                {
                    EditorGUILayout.LabelField("Avatar", avatar.avatarName);
                    EditorGUILayout.ObjectField("In scene", avatar.avatarDescriptor.asset, typeof(SceneAsset), false);
                }

                if (GUILayout.Button("Change Avatar"))
                    _settingAvatar = true;
            }

            using (new EditorGUI.DisabledGroupScope(!avatar.GetCurrentPlatformInfo().enabled))
                if (GUILayout.Button("Upload This Avatar"))
                    UploadThis(avatar);
            PlatformSpecificInfo("PC Windows", avatar.windows);
            PlatformSpecificInfo("Quest", avatar.quest);
        }

        private static void UploadThis(AvatarUploadSetting avatar)
        {
            var uploader = EditorWindow.GetWindow<ContinuousAvatarUploader>();
            uploader.avatarSettings = new[] { avatar };
            uploader.groups = Array.Empty<AvatarUploadSettingGroup>();
            if (!uploader.StartUpload())
            {
                EditorUtility.DisplayDialog("Failed to start upload",
                    "Failed to start upload.\nPlease refer Uploader window for reason", "OK");
            }
        }

        private void PlatformSpecificInfo(string name, PlatformSpecificInfo info)
        {
            info.enabled = EditorGUILayout.ToggleLeft(name, info.enabled);

            if (info.enabled)
            {
                EditorGUI.indentLevel++;
                info.updateImage = EditorGUILayout.ToggleLeft("update Image", info.updateImage);
                if (info.updateImage)
                {
                    EditorGUI.indentLevel++;
                    info.imageTakeEditorMode = (ImageTakeEditorMode)EditorGUILayout.EnumPopup("Take Image In", info.imageTakeEditorMode);
                    EditorGUI.BeginDisabledGroup(!_cachedAvatar);
                    if (_previewCameraManager != null)
                    {
                        _previewCameraManager.DrawPreview();
                        if (IndentedButton("Finish Setting Camera Position"))
                        {
                            _previewCameraManager?.Finish();
                            _previewCameraManager = null;
                        }
                    }
                    else
                    {
                        if (IndentedButton("Configure Camera Position"))
                        {
                            _previewCameraManager = new PreviewCameraManager(this, _cachedAvatar);
                        }
                    }
                    EditorGUI.EndDisabledGroup();
                    EditorGUI.indentLevel--;
                }
                else
                {
                    _previewCameraManager?.Finish();
                    _previewCameraManager = null;
                }
                info.versioningEnabled = EditorGUILayout.ToggleLeft("Versioning System", info.versioningEnabled);
                if (info.versioningEnabled)
                {
                    EditorGUI.indentLevel++;
                    info.versionNamePrefix = EditorGUILayout.TextField("Version Prefix", info.versionNamePrefix);
                    EditorGUILayout.LabelField($"'({info.versionNamePrefix}<version>)'will be added in avatar description");
                    info.gitEnabled = EditorGUILayout.ToggleLeft("git tagging", info.gitEnabled);
                    if (info.gitEnabled)
                    {
                        EditorGUI.indentLevel++;
                        info.tagPrefix = EditorGUILayout.TextField("Tag Prefix", info.tagPrefix);
                        info.tagSuffix = EditorGUILayout.TextField("Tag Suffix", info.tagSuffix);
                        EditorGUILayout.LabelField($"tag name will be '{info.tagPrefix}<version>{info.tagSuffix}'");
                        EditorGUI.indentLevel--;
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUI.indentLevel--;
            }
        }

        private static void HorizontalLine(float regionHeight = 18f, float lineHeight = 1f)
        {
            var rect = GUILayoutUtility.GetRect(EditorGUIUtility.fieldWidth, float.MaxValue, regionHeight, regionHeight);
            rect.y += (rect.height - lineHeight) / 2;
            rect.height = lineHeight;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
        }

        private static bool IndentedButton(string text, params GUILayoutOption[] options)
        {
            var content = new GUIContent(text);
            var rect = EditorGUI.IndentedRect(GUILayoutUtility.GetRect(content, GUI.skin.button, options));
            return GUI.Button(rect, content);
        }
    }

    sealed class PreviewCameraManager
    {
        private Camera _camera;
        private bool _prevLocked;
        private readonly VRCAvatarDescriptor _cachedAvatar;
        private readonly UnityEditor.Editor _editor;
        private readonly Scene _previewScene;

        public PreviewCameraManager([NotNull] UnityEditor.Editor editor,
            [NotNull] VRCAvatarDescriptor cachedAvatar)
        {
            if (editor == null) throw new ArgumentNullException(nameof(editor));
            if (cachedAvatar == null) throw new ArgumentNullException(nameof(cachedAvatar));
            _editor = editor;
            _cachedAvatar = cachedAvatar;

            if (EditorUtility.IsPersistent(cachedAvatar))
            {
                _previewScene = EditorSceneManager.NewPreviewScene();
                PrefabUtility.LoadPrefabContentsIntoPreviewScene(
                    AssetDatabase.GetAssetPath(cachedAvatar), _previewScene);
            }

            _prevLocked = ActiveEditorTracker.sharedTracker.isLocked;
            ActiveEditorTracker.sharedTracker.isLocked = true;

            _camera = EditorUtility.CreateGameObjectWithHideFlags("VRCCam Shim Camera", HideFlags.DontSave,
                    typeof(Camera))
                .GetComponent<Camera>();
            _camera.enabled = false;
            _camera.cullingMask = unchecked((int)0xFFFFFFDF);
            _camera.nearClipPlane = 0.01f;
            _camera.farClipPlane = 100f;
            _camera.allowHDR = false;
            _camera.scene = _previewScene.IsValid() ? _previewScene : cachedAvatar.gameObject.scene;
            cachedAvatar.PositionPortraitCamera(_camera.transform);
            EditorApplication.update += OnUpdate;
            Selection.objects = new Object[] { _camera.gameObject };
        }

        private Vector3 _cameraPositionOld;
        private Quaternion _cameraRotationOld;
        private void OnUpdate()
        {
            var transform = _camera.transform;
            if (_cameraPositionOld != transform.position || _cameraRotationOld != transform.rotation)
                _editor.Repaint();
            _cameraPositionOld = transform.position;
            _cameraRotationOld = transform.rotation;
        }

        public void DrawPreview(params GUILayoutOption[] options)
        {
            var cameraRect = GUILayoutUtility.GetAspectRect(16.0f / 9f, options);
            if (Event.current.type == EventType.Repaint)
            {
                var previewTexture = GetRenderTexture((int)cameraRect.width, (int)cameraRect.height);
                _camera.targetTexture = previewTexture;
                _camera.pixelRect = new Rect(0, 0, cameraRect.width, cameraRect.height);
                _camera.Render();
                Graphics.DrawTexture(cameraRect, previewTexture, new Rect(0, 0, 1, 1), 
                    0, 0, 0, 0, GUI.color, _guiTextureBlit2SrgbMaterial);
            }
        }

        readonly Material _guiTextureBlit2SrgbMaterial = typeof(EditorGUIUtility)
                .GetProperty("GUITextureBlit2SRGBMaterial", BindingFlags.Static | BindingFlags.NonPublic)
                ?.GetValue(typeof(EditorGUIUtility), null) as Material;

        private RenderTexture _previewTexture;

        private RenderTexture GetRenderTexture(int width, int height)
        {
            int antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing);
            if (_previewTexture == null || _previewTexture.width != width || _previewTexture.height != height || _previewTexture.antiAliasing != antiAliasing)
            {
                _previewTexture = new RenderTexture(width, height, 24, SystemInfo.GetGraphicsFormat(DefaultFormat.LDR));
                _previewTexture.antiAliasing = antiAliasing;
            }
            return _previewTexture;
        }

        public void Finish()
        {
            if (!_camera) return;

            EditorApplication.update -= OnUpdate;
            if (_cachedAvatar)
            {
                var transform = _cachedAvatar.transform;
                _cachedAvatar.portraitCameraPositionOffset =
                    transform.InverseTransformPoint(_camera.transform.position);
                _cachedAvatar.portraitCameraRotationOffset =
                    Quaternion.Inverse(transform.rotation) * _camera.transform.rotation;
                EditorUtility.SetDirty(_cachedAvatar);
                // k = x * y
                // ^x * k = ^x * x * y
                // ^x * k = y
            }

            Object.DestroyImmediate(_camera.gameObject);
            _camera = null;
            Selection.objects = new []{_editor.target};
            ActiveEditorTracker.sharedTracker.isLocked = _prevLocked;

            // ReSharper disable once PossiblyImpureMethodCallOnReadonlyVariable // IsValid doesn't change
            if (_previewScene.IsValid())
                EditorSceneManager.ClosePreviewScene(_previewScene);
        }
    }
}
