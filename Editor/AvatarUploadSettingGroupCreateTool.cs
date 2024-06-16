using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VRC.SDK3.Avatars.Components;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    internal static class AvatarUploadSettingGroupCreateTool
    {
        private static AvatarUploadSettingGroup avatarUploadSettingGroup;
        internal static AvatarUploadSettingGroup CreateNewUploadGroup()
        {
            string path = EditorUtility.SaveFilePanelInProject("Save Upload Group", "NewAvatarUploadSettingGroup", "asset", "Please enter a file name to save the upload group to:");

            if (string.IsNullOrEmpty(path))
                return null;

            AvatarUploadSettingGroup newGroup = ScriptableObject.CreateInstance<AvatarUploadSettingGroup>();
            AssetDatabase.CreateAsset(newGroup, path);
            AssetDatabase.SaveAssets();

            avatarUploadSettingGroup = AssetDatabase.LoadAssetAtPath<AvatarUploadSettingGroup>(path);
            EditorUtility.FocusProjectWindow();
            return avatarUploadSettingGroup;
        }
    }

    public class UploadSettingGroupCreator : EditorWindow
    {
        private static AvatarUploadSettingGroup avatarUploadSettingGroup;
        private VRCAvatarDescriptor avatarDescriptor;
        private List<VRCAvatarDescriptor> collectedAvatars = new List<VRCAvatarDescriptor>();

        private bool windowsSettingToggle = true;
        private bool versioningSettingToggle = false;
        private bool uploadImageSettingToggle = false;
        private bool gitTaggingSettingToggle = false;
        private bool questSettingToggle = false;
        private string versionNamePrefix = "ver";
        private Vector2 scrollPosition;
        private GUIStyle redBoldLabelStyle;
        private bool canExecute = false;

        public static void UploadSettingGroupCreatorShowWindow(AvatarUploadSettingGroup createdGroup)
        {
            avatarUploadSettingGroup = createdGroup;
            GetWindow<UploadSettingGroupCreator>("Upload Setting Group Creator");
        }

        private void OnEnable()
        {
            // カスタムスタイルを初期化
            redBoldLabelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = Color.red }
            };
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Continuous Avatar Uploader Setting Group Creator", EditorStyles.boldLabel);

            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Avatar Upload Setting Group", EditorStyles.boldLabel, GUILayout.ExpandWidth(true));
            avatarUploadSettingGroup = EditorGUILayout.ObjectField(avatarUploadSettingGroup, typeof(AvatarUploadSettingGroup), true,GUILayout.ExpandWidth(true)) as AvatarUploadSettingGroup;
            if (avatarUploadSettingGroup == null)
            {
                EditorGUILayout.LabelField("Avatar Upload Setting Group is Empty.\nPlease Set or Create New Avatar Upload Setting Group", redBoldLabelStyle, GUILayout.ExpandWidth(true),GUILayout.Height(40));
                EditorGUILayout.EndVertical();
                return;
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);

            if (GUILayout.Button("Set Target Avatars"))
            {
                CollectSelectedAvatars();
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            if (collectedAvatars.Count > 0)
            {
                EditorGUILayout.LabelField($"Collected Avatars : " + collectedAvatars.Count);
                foreach (var avatar in collectedAvatars)
                {
                    EditorGUILayout.ObjectField(avatar, typeof(VRCAvatarDescriptor), true);
                }
                canExecute = true;
            }
            else
            {
                EditorGUILayout.LabelField("No avatars collected.\nPlease select the target avatars in the Hierarchy\nand press the [Set Target Avatars]button. (Multiple selections allowed)", GUILayout.Height(60));
                canExecute = false;
            }

            EditorGUILayout.EndScrollView();

            // スペース
            EditorGUILayout.Space(20);

            // Windows設定トグル
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Set Windows Enable", GUILayout.Width(150));
            windowsSettingToggle = EditorGUILayout.Toggle(windowsSettingToggle);
            EditorGUILayout.EndHorizontal();

            // Quest設定トグル
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Set Quest Enable", GUILayout.Width(150));
            questSettingToggle = EditorGUILayout.Toggle(questSettingToggle);
            EditorGUILayout.EndHorizontal();

            // サムネイル更新トグル
            // 自動で生成する場合は追加操作が難しいので一旦無効化
            /*
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Update Thumbnail Image", GUILayout.Width(150));
            uploadImageSettingToggle = EditorGUILayout.Toggle(uploadImageSettingToggle);
            EditorGUILayout.EndHorizontal();
            */

            // Versioningの設定
            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Versioning Enabled", GUILayout.Width(150));
            versioningSettingToggle = EditorGUILayout.Toggle(versioningSettingToggle);
            EditorGUILayout.EndHorizontal();

            if (versioningSettingToggle)
            {
                // バージョン名のプレフィックス文字を設定する
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Version Name Prefixing", GUILayout.Width(200));
                versionNamePrefix = EditorGUILayout.TextField(versionNamePrefix);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField($"'({versionNamePrefix}<version>)'will be added in avatar description");
            }

            // Git Taggingの設定
            // 自動で生成する場合は追加操作が難しいので一旦無効化
            /*
            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Git Tagging Enabled", GUILayout.Width(150));
            gitTaggingSettingToggle = EditorGUILayout.Toggle(gitTaggingSettingToggle);
            EditorGUILayout.EndHorizontal();
            */

            // BeginDisabledGroupを使って、ボタンを無効化する
            EditorGUI.BeginDisabledGroup(!canExecute || (!windowsSettingToggle && !questSettingToggle) || avatarUploadSettingGroup == null );
            if (GUILayout.Button("Execute"))
            {
                //ダイアログを表示
                if (EditorUtility.DisplayDialog("CAU Supporter", "Are you sure you want to execute?", "OK", "Cancel"))
                {
                    Execute();
                }
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndVertical();
        }

        void Execute()
        {
            if (avatarUploadSettingGroup == null)
            {
                Debug.LogError("Avatar Upload Setting Group is null.");
                return;
            }

            if (collectedAvatars.Count == 0)
            {
                Debug.LogError("No avatars collected.");
                return;
            }

            foreach (var avatar in collectedAvatars)
            {
                var newObj = ScriptableObject.CreateInstance<AvatarUploadSetting>();
                newObj.avatarDescriptor = new MaySceneReference(avatar);
                newObj.name = newObj.avatarName = avatar.gameObject.name;
                newObj.windows.versioningEnabled = versioningSettingToggle;
                if (versioningSettingToggle)
                {
                    newObj.windows.versionNamePrefix = versionNamePrefix;
                }
                else
                {
                    newObj.windows.versionNamePrefix = "";
                }
                newObj.windows.enabled = windowsSettingToggle;
                newObj.quest.enabled = questSettingToggle;
                if (windowsSettingToggle)
                {
                    newObj.windows.updateImage = uploadImageSettingToggle;
                    newObj.windows.gitEnabled = gitTaggingSettingToggle;
                }
                else if (questSettingToggle)
                {
                    newObj.quest.updateImage = uploadImageSettingToggle;
                    newObj.quest.gitEnabled = gitTaggingSettingToggle;
                }

                ArrayUtility.Add(ref avatarUploadSettingGroup.avatars, newObj);
                EditorUtility.SetDirty(avatarUploadSettingGroup);
                AssetDatabase.AddObjectToAsset(newObj, avatarUploadSettingGroup);
            }

            // 保存
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ウインドウを閉じる
            Close();

            //Debug.Log("Done.");
        }

        void CollectSelectedAvatars()
        {
            collectedAvatars.Clear();
            var sortedGameObjects = Selection.gameObjects.OrderBy(go => go.transform.GetSiblingIndex()).ToArray();
            foreach (var go in sortedGameObjects)
            {
                VRCAvatarDescriptor descriptor = go.GetComponent<VRCAvatarDescriptor>();
                if (descriptor != null)
                {
                    collectedAvatars.Add(descriptor);
                }
            }
        }
    }

}