using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Graphs;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

// ReSharper disable AccessToStaticMemberViaDerivedType

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    [CustomEditor(typeof(AvatarUploadSetting))]
    [CanEditMultipleObjects]
    public class AvatarUploadSettingEditor : UnityEditor.Editor
    {
        private VRCAvatarDescriptor _cachedAvatar;
        private bool _settingAvatar;
        [CanBeNull] private static PreviewCameraManager _previewCameraManager;

        private SerializedProperty _name = null!;
        private SerializedProperty _avatarName = null!;
        private SerializedProperty _avatarDescriptor = null!;
        private SerializedProperty _windows = null!;
        private SerializedProperty _quest = null!;
        private SerializedProperty _ios = null!;

        private void OnEnable()
        {
            _name = serializedObject.FindProperty("m_Name");
            _avatarName = serializedObject.FindProperty(nameof(AvatarUploadSetting.avatarName));
            _avatarDescriptor = serializedObject.FindProperty(nameof(AvatarUploadSetting.avatarDescriptor));

            _windows = serializedObject.FindProperty(nameof(AvatarUploadSetting.windows));
            _quest = serializedObject.FindProperty(nameof(AvatarUploadSetting.quest));
            _ios = serializedObject.FindProperty(nameof(AvatarUploadSetting.ios));
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            // Avatar descriptor and upload button section - keep IMGUI for complex interactions
            var avatarSection = new IMGUIContainer(() =>
            {
                serializedObject.Update();
                var avatars = targets.Cast<AvatarUploadSetting>().ToArray();

                if (serializedObject.isEditingMultipleObjects)
                {
                    MultipleAvatarDescriptor(avatars);
                }
                else
                {
                    SingleAvatarDescriptor(_avatarDescriptor);
                }

                if (!serializedObject.isEditingMultipleObjects)
                {
                    var avatar = avatars.First();
                    if (!avatar.ios.enabled && !avatar.quest.enabled && !avatar.windows.enabled)
                        EditorGUILayout.HelpBox("This avatar has all platforms disabled. This is fine if intentional.", MessageType.Warning);
                }
                
                var enabledAll = avatars.All(x => x.GetCurrentPlatformInfo().enabled);
                using (new EditorGUI.DisabledGroupScope(!enabledAll))
                    ContinuousAvatarUploader.UploadButtonGui(avatars, Repaint);

                serializedObject.ApplyModifiedProperties();
            });
            root.Add(avatarSection);

            // Platform settings - use simple PropertyFields for automatic UIElements rendering
            var windowsField = new PropertyField(_windows);
            windowsField.Bind(serializedObject);
            root.Add(windowsField);

            var questField = new PropertyField(_quest);
            questField.Bind(serializedObject);
            root.Add(questField);

            var iosField = new PropertyField(_ios);
            iosField.Bind(serializedObject);
            root.Add(iosField);

            // Camera configuration - keep IMGUI for preview functionality
            var cameraSection = new IMGUIContainer(() =>
            {
                serializedObject.Update();

                if (serializedObject.isEditingMultipleObjects)
                {
                    EditorGUILayout.LabelField("Editing Camera Position is not supported in multi-editing");
                }
                else
                {
                    EditorGUI.BeginDisabledGroup(
                        !_cachedAvatar
                        || _previewCameraManager != null && _previewCameraManager.Target != _cachedAvatar
                    );
                    if (_previewCameraManager != null && _previewCameraManager.Target == _cachedAvatar)
                    {
                        _previewCameraManager.AddEditor(this);
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
                            _previewCameraManager = new PreviewCameraManager(this, _cachedAvatar!);
                        }
                    }
                    EditorGUI.EndDisabledGroup();
                }

                serializedObject.ApplyModifiedProperties();
            });
            root.Add(cameraSection);

            return root;
        }

        private void MultipleAvatarDescriptor(AvatarUploadSetting[] avatars)
        {
            EditorGUI.BeginDisabledGroup(true);
            if (_avatarDescriptor.hasMultipleDifferentValues)
            {
                var position = EditorGUILayout.GetControlRect(true, 18f);
                position = EditorGUI.PrefixLabel(position, Labels.Avatar);
                GUI.Label(position, Labels.MixedValueContent, EditorStyles.objectField);
                GUIStyle buttonStyle = "ObjectFieldButton";
                Rect position1 = new Rect(position.xMax - 19f, position.y, 19f, position.height);
                GUI.Label(position1, GUIContent.none, buttonStyle);
            }
            else
            {
                var descriptor = avatars[0].avatarDescriptor;
                var avatar = descriptor.TryResolve();
                if (avatar != null || descriptor.IsNull())
                {
                    EditorGUILayout.ObjectField(Labels.Avatar, avatar, typeof(VRCAvatarDescriptor), true);
                }
                else
                {
                    EditorGUILayout.LabelField(Labels.Avatar, new GUIContent(_avatarName.stringValue));
                    EditorGUILayout.ObjectField("In scene", descriptor.asset, typeof(SceneAsset),
                        false);
                }
            }
            EditorGUI.EndDisabledGroup();
        }

        private void SingleAvatarDescriptor(SerializedProperty avatarDescriptor)
        {
            var descriptor = (MaySceneReference)avatarDescriptor.boxedValue;
            if (descriptor.IsNull())
                _settingAvatar = true;
            if (_settingAvatar)
            {
                _cachedAvatar = (VRCAvatarDescriptor)EditorGUILayout.ObjectField("Set Avatar: ",
                    null, typeof(VRCAvatarDescriptor), true);

                if (_cachedAvatar)
                {
                    avatarDescriptor.boxedValue = new MaySceneReference(_cachedAvatar);
                    // might be reverted if it's individual asset but
                    // this is good for DescriptorSet
                    _name.stringValue = _avatarName.stringValue = _cachedAvatar.name;
                    _settingAvatar = false;
                }

                EditorGUI.BeginDisabledGroup(descriptor.IsNull());
                if (GUILayout.Button("Cancel Change Avatar"))
                    _settingAvatar = false;
                EditorGUI.EndDisabledGroup();
            }
            else
            {
                if (!_cachedAvatar)
                    _cachedAvatar = descriptor.TryResolve() as VRCAvatarDescriptor;
                if (_cachedAvatar)
                {
                    EditorGUILayout.ObjectField(Labels.Avatar, _cachedAvatar, typeof(VRCAvatarDescriptor), true);
                    _avatarName.stringValue = _cachedAvatar.name;
                }
                else
                {
                    EditorGUILayout.LabelField(Labels.Avatar,  new GUIContent(_avatarName.stringValue));
                    EditorGUILayout.ObjectField("In scene", descriptor.asset, typeof(SceneAsset),
                        false);
                }

                if (GUILayout.Button("Change Avatar"))
                    _settingAvatar = true;
            }
        }

        private void OnDisable()
        {
            if (_previewCameraManager != null && _previewCameraManager.Target == _cachedAvatar) 
            {
                _previewCameraManager.RemoveEditor(this);
            }
        }

        public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height)
        {
            var overlayTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath("235dc340dbeca4d2d84f557bf52e93b4"));
            if (overlayTexture == null) return null;
            var target = this.target as AvatarUploadSetting;
            if (target == null) return null;
            if (!target.avatarDescriptor.IsAssetReference()) return null;
            var targetAvatar = target.avatarDescriptor.TryResolve() as VRCAvatarDescriptor;
            if (targetAvatar == null) return null;
            var targetGameObject = targetAvatar.gameObject;

            /*
            var renderUtility = new PreviewRenderUtility();
            renderUtility.BeginPreview(new Rect(0, 0, width, height), GUIStyle.none);
            renderUtility.AddSingleGO(Instantiate(targetGameObject));
            renderUtility.Render(true);
            var previewTexture = renderUtility.EndStaticPreview();
            /*/
            var editor = UnityEditor.Editor.CreateEditor(targetGameObject);
            var previewTexture = editor.RenderStaticPreview("", Array.Empty<Object>(), width, height);
            DestroyImmediate(editor);
            // */
            if (previewTexture == null) return null;
 
            // overlay the "CAU Settings" text
            var renderTexture = RenderTexture.GetTemporary(width, height, 0, GraphicsFormat.R8G8B8A8_SRGB);
            try
            {
                // copy the overlay texture
                RenderTexture.active = renderTexture;

                // draw the overlay texture
                GL.PushMatrix();
                GL.LoadPixelMatrix(0, width, height, 0);
                Graphics.DrawTexture(new Rect(0, 0, width, height), previewTexture);
                Graphics.DrawTexture(new Rect(0, 0, width, height), overlayTexture);
                GL.PopMatrix();

                var texture2D = new Texture2D(width, height, TextureFormat.RGB24, false, false);
                texture2D.ReadPixels(new Rect(0.0f, 0.0f, width, height), 0, 0);
                texture2D.Apply();

                return texture2D;
            }
            finally
            {
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }

        static class Labels
        {
            public static readonly GUIContent Avatar = new("Avatar");

            public static readonly GUIContent PCWindows = new("PC Windows");
            public static readonly GUIContent QuestAndroid = new("Quest / Android");
            public static readonly GUIContent IOS = new("iOS");

            public static readonly GUIContent UpdateImage = new("Update Image on Upload");
            public static readonly GUIContent TakeImageIn = new("Take Image In");
            public static readonly GUIContent VersioningSystem = new("Versioning System");
            public static readonly GUIContent VersionNamePrefix = new("Version Prefix");
            public static readonly GUIContent GitTagging = new("git tagging");
            public static readonly GUIContent TagPrefix = new("Tag Prefix");
            public static readonly GUIContent TagSuffix = new("Tag Suffix");
            
            public static readonly GUIContent MixedValueContent = EditorGUIUtility.TrTextContent("—", "Mixed Values");
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
        private Object[] _prevSelection;
        private readonly Object[] _trackerTargets;

        private readonly VRCAvatarDescriptor _cachedAvatar;
        public Object Target => _cachedAvatar;

        private readonly HashSet<UnityEditor.Editor> _editors;
        private readonly Scene _previewScene;

        public PreviewCameraManager([NotNull] UnityEditor.Editor editor,
            [NotNull] VRCAvatarDescriptor cachedAvatar)
        {
            if (editor == null) throw new ArgumentNullException(nameof(editor));
            if (cachedAvatar == null) throw new ArgumentNullException(nameof(cachedAvatar));
            _editors = new HashSet<UnityEditor.Editor> { editor };
            _cachedAvatar = cachedAvatar;

            if (EditorUtility.IsPersistent(cachedAvatar))
            {
                _previewScene = EditorSceneManager.NewPreviewScene();
                PrefabUtility.LoadPrefabContentsIntoPreviewScene(
                    AssetDatabase.GetAssetPath(cachedAvatar), _previewScene);
            }

            _prevLocked = ActiveEditorTracker.sharedTracker.isLocked;
            ActiveEditorTracker.sharedTracker.isLocked = true;
            _trackerTargets = ActiveEditorTracker.sharedTracker.activeEditors.Select(x => x.target).ToArray();

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
            _prevSelection = Selection.objects;
            Selection.objects = new Object[] { _camera.gameObject };
        }

        private Vector3 _cameraPositionOld;
        private Quaternion _cameraRotationOld;
        private void OnUpdate()
        {
            if (_cachedAvatar == null || _editors.All(x => x == null))
            {
                Finish();
                return;
            }
            var transform = _camera.transform;
            if (_cameraPositionOld != transform.position || _cameraRotationOld != transform.rotation)
                foreach (var editor in _editors)
                    editor.Repaint();

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
            Selection.objects = _prevSelection;
            ActiveEditorTracker.sharedTracker.isLocked = _prevLocked;

            // ReSharper disable once PossiblyImpureMethodCallOnReadonlyVariable // IsValid doesn't change
            if (_previewScene.IsValid())
                EditorSceneManager.ClosePreviewScene(_previewScene);
        }

        public void AddEditor(UnityEditor.Editor editor) => _editors.Add(editor);
        public void RemoveEditor(UnityEditor.Editor editor) => _editors.Remove(editor);
    }
}
