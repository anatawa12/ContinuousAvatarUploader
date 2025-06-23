using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;
using Debug = System.Diagnostics.Debug;
using Object = UnityEngine.Object;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    [CustomEditor(typeof(AvatarUploadSettingGroup))]
    public class AvatarUploadSettingGroupEditor : UnityEditor.Editor
    {
        private AvatarUploadSettingGroup _asset;
        private Dictionary<int, CreateDescriptorContainer> _inspectorsDoctionary = new Dictionary<int, CreateDescriptorContainer>();
        private List<CreateDescriptorContainer> _inspectors = new List<CreateDescriptorContainer>();
        private VisualElement _inspector;
        private const int CreatePerFrame = 5;
        private const int CreateInitial = 20;

        public override VisualElement CreateInspectorGUI()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            _asset = (AvatarUploadSettingGroup)target;

            var root = new VisualElement()
            {
                name = "RootElement"
            };
            _inspector = new VisualElement()
            {
                name = "Inspectors"
            };

            var header = new IMGUIContainer(() =>
            {
                EditorGUILayout.LabelField("Avatar Upload Settings", EditorStyles.boldLabel);
                
                ContinuousAvatarUploader.UploadButtonGui(new[] { _asset }, Repaint);

                EditorGUILayout.Space();
            });

            RecreateInspectors(throttled: true);
            CreateInspectorElementsThrottled();

            VRCAvatarDescriptor avatarDescriptor = null;
            var trailer = new IMGUIContainer(() =>
            {
                avatarDescriptor = EditorGUILayout.ObjectField("Avatar to Add", avatarDescriptor,
                    typeof(VRCAvatarDescriptor), true) as VRCAvatarDescriptor;

                EditorGUI.BeginDisabledGroup(!avatarDescriptor);
                if (GUILayout.Button("Add Avatar"))
                {
                    Debug.Assert(avatarDescriptor != null, nameof(avatarDescriptor) + " != null");
                    var newObj = ScriptableObject.CreateInstance<AvatarUploadSetting>();
                    newObj.avatarDescriptor = new MaySceneReference(avatarDescriptor);
                    newObj.name = newObj.avatarName = avatarDescriptor.gameObject.name;

                    ArrayUtility.Add(ref _asset.avatars, newObj);
                    EditorUtility.SetDirty(_asset);
                    AssetDatabase.AddObjectToAsset(newObj, _asset);
                    AssetDatabase.SaveAssetIfDirty(newObj);
                    avatarDescriptor = null;

                    RecreateInspectors();
                }

                EditorGUI.EndDisabledGroup();
            });

            root.Add(header);
            root.Add(_inspector);
            root.Add(trailer);

            UnityEngine.Debug.Log($"CreateInspectorGUI took {stopwatch.ElapsedMilliseconds}ms");

            return root;
        }

        private void CreateInspectorElementsThrottled()
        {
            var index = 0;

            for (var i = 0; i < CreateInitial; i++)
            {
                if (index >= _inspectors.Count) return;
                _inspectors[index].CreateInspectorElement();
                index++;
            }

            void CreateFrame()
            {
                for (var i = 0; i < CreatePerFrame; i++)
                {
                    if (index >= _inspectors.Count) return;
                    _inspectors[index].CreateInspectorElement();
                    index++;
                }

                EditorApplication.delayCall += CreateFrame;
            }

            EditorApplication.delayCall += CreateFrame;
        }

        void RecreateInspectors() => RecreateInspectors(false);

        void RecreateInspectors(bool throttled)
        {
            _inspector.Clear();
            _inspectors.Clear();
            var instanceIds = new HashSet<int>();
            foreach (var assetAvatar in _asset.avatars)
            {
                var instanceId = assetAvatar.GetInstanceID();
                instanceIds.Add(instanceId);
                if (!_inspectorsDoctionary.TryGetValue(instanceId, out var container))
                {
                    _inspectorsDoctionary.Add(instanceId,
                        container = new CreateDescriptorContainer(_asset, assetAvatar));
                    container.OnReorder += RecreateInspectors;
                    if (!throttled) container.CreateInspectorElement();
                }

                _inspector.Add(container);
                _inspectors.Add(container);
            }

            foreach (var i in _inspectorsDoctionary.Keys.ToArray())
                if (!instanceIds.Contains(i))
                    _inspectorsDoctionary.Remove(i);
        }
    }

    class CreateDescriptorContainer : VisualElement
    {
        public event Action OnReorder;
        private readonly AvatarUploadSetting _setting;
        private readonly VisualElement _inspectorElementContainer;

        public CreateDescriptorContainer(AvatarUploadSettingGroup group, AvatarUploadSetting setting)
        {
            _setting = setting;
            Add(new IMGUIContainer(() =>
            {
                int index = System.Array.IndexOf(group.avatars, setting);
                EditorGUILayout.LabelField($"Avatar #{index}");
            }));
            Add(_inspectorElementContainer = new VisualElement());
            Add(new IMGUIContainer(() =>
            {
                GUILayout.BeginHorizontal();
                EditorGUI.BeginDisabledGroup(group.avatars[0] == setting);
                if (GUILayout.Button("▲", EditorStyles.miniButton, GUILayout.Width(30)))
                {
                    var index = System.Array.IndexOf(group.avatars, setting);
                    Debug.Assert(index != -1, nameof(index) + " != -1");
                    var temp = group.avatars[index - 1];
                    group.avatars[index - 1] = setting;
                    group.avatars[index] = temp;
                    EditorUtility.SetDirty(group);

                    OnReorder?.Invoke();
                }
                EditorGUI.EndDisabledGroup();
                if (GUILayout.Button("Remove Avatar"))
                {
                    ArrayUtility.Remove(ref group.avatars, setting);
                    EditorUtility.SetDirty(group);
                    Object.DestroyImmediate(setting, true);
                    AssetDatabase.SaveAssetIfDirty(group);

                    OnReorder?.Invoke();
                }
                EditorGUI.BeginDisabledGroup(group.avatars[group.avatars.Length - 1] == setting);
                if (GUILayout.Button("▼", EditorStyles.miniButton, GUILayout.Width(30)))
                {
                    var index = System.Array.IndexOf(group.avatars, setting);
                    Debug.Assert(index != -1, nameof(index) + " != -1");
                    var temp = group.avatars[index + 1];
                    group.avatars[index + 1] = setting;
                    group.avatars[index] = temp;
                    EditorUtility.SetDirty(group);

                    OnReorder?.Invoke();
                }
                GUILayout.EndHorizontal();
                HorizontalLine();
            }));
        }

        public void CreateInspectorElement()
        {
            if (_inspectorElementContainer.childCount != 0) return;
            _inspectorElementContainer.Add(new InspectorElement(_setting));
        }

        private static void HorizontalLine(float regionHeight = 18f, float lineHeight = 1f)
        {
            var rect = GUILayoutUtility.GetRect(EditorGUIUtility.fieldWidth, float.MaxValue, regionHeight, regionHeight);
            rect.y += (rect.height - lineHeight) / 2;
            rect.height = lineHeight;
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 1));
        }
    }
}
