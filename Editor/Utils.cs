using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    class PreventEnteringPlayModeScope : IDisposable
    {
        public PreventEnteringPlayModeScope()
        {
            EditorApplication.playModeStateChanged += PlayModeChanged;
        }

        private void PlayModeChanged(PlayModeStateChange change)
        {
            // if EditorApplication.isPlayingOrWillChangePlaymode is false, entering play mode is already cancelled
            if (change == PlayModeStateChange.ExitingEditMode && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying = false;
                ShowNotification(
                    "entering play mode is not allowed while uploading avatars with the Continuous Avatar Uploader.");
            }
        }

        ~PreventEnteringPlayModeScope()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            EditorApplication.playModeStateChanged -= PlayModeChanged;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private static void ShowNotification(string notificationText)
        {
            var notificationViews = Resources.FindObjectsOfTypeAll<SceneView>();

            if (notificationViews.Length > 0)
            {
                var content = new GUIContent(notificationText);
                foreach (var notificationView in notificationViews)
                    notificationView.ShowNotification(content);
            }
            else
            {
                Debug.LogError(notificationText);
            }
        }
    }

    class OpeningSceneRestoreScope : IDisposable
    {
        private readonly string[] _lastOpenedScenes;

        public OpeningSceneRestoreScope()
        {
            var scenes = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .ToArray();
            if (scenes.Any(x => x.isDirty))
                EditorSceneManager.SaveOpenScenes();
            var scenePaths = scenes.Select(x => x.path).ToArray();
            _lastOpenedScenes = scenePaths.Any(string.IsNullOrEmpty) ? Array.Empty<string>() : scenePaths;
        }

        public void Dispose()
        {
            if (_lastOpenedScenes.Length == 0)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            }
            else
            {
                EditorSceneManager.OpenScene(_lastOpenedScenes[0]);
                foreach (var lastOpenedScene in _lastOpenedScenes.Skip(1))
                    EditorSceneManager.OpenScene(lastOpenedScene, OpenSceneMode.Additive);
            }
        }
    }

    struct DestroyLater<T> : IDisposable where T : Object
    {
        public T Value;

        public DestroyLater(T value)
        {
            Value = value;
        }

        public void Dispose()
        {
            if (Value) Object.DestroyImmediate(Value);
            Value = null;
        }
    }

    struct ActiveRenderTextureScope : IDisposable
    {
        private RenderTexture _old;
        public RenderTexture Texture;

        public ActiveRenderTextureScope(RenderTexture texture)
        {
            _old = RenderTexture.active;
            Texture = texture;
            RenderTexture.active = texture;
        }

        public void Dispose()
        {
            if (Texture) RenderTexture.active = _old;
            Texture = null;
        }
    }

    struct PreviewSceneScope : IDisposable
    {
        public Scene Scene;

        public PreviewSceneScope(Scene scene)
        {
            Scene = scene;
        }

        public void Dispose()
        {
            if (Scene.IsValid())
                EditorSceneManager.ClosePreviewScene(Scene);
            Scene = default;
        }

        public static PreviewSceneScope Create(bool create = true) =>
            create ? new PreviewSceneScope(EditorSceneManager.NewPreviewScene()) : default;
    }
}