using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
    internal static class Utils
    {
        public static bool ReloadDomainDisabled() =>
            EditorSettings.enterPlayModeOptionsEnabled &&
            (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0;

        public static Task EnterPlayMode()
        {
            if (EditorApplication.isPlaying)
                return Task.CompletedTask;
            if (!ReloadDomainDisabled())
                throw new Exception("Reload Domain is not disabled so we cannot wait for entering playmode with Task");
            var taskCompletionSource = new TaskCompletionSource<bool>();

            EditorApplication.playModeStateChanged += PlayModeStateChangeImpl;
            EditorApplication.isPlaying = true;

            return taskCompletionSource.Task;

            void PlayModeStateChangeImpl(PlayModeStateChange state)
            {
                switch (state)
                {
                    case PlayModeStateChange.ExitingEditMode:
                        break;
                    case PlayModeStateChange.EnteredPlayMode:
                        taskCompletionSource.SetResult(true);
                        EditorApplication.playModeStateChanged -= PlayModeStateChangeImpl;
                        break;
                    case PlayModeStateChange.ExitingPlayMode:
                        taskCompletionSource.SetException(new Exception("Entering Play mode Is Aborted"));
                        EditorApplication.playModeStateChanged -= PlayModeStateChangeImpl;
                        break;
                    case PlayModeStateChange.EnteredEditMode:
                    default:
                        taskCompletionSource.SetException(new Exception($"Unexpected state: {state}"));
                        EditorApplication.playModeStateChanged -= PlayModeStateChangeImpl;
                        break;
                }
            }
        }
        
        public static Task ExitPlayMode()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                return Task.CompletedTask;
            var taskCompletionSource = new TaskCompletionSource<bool>();

            EditorApplication.playModeStateChanged += PlayModeStateChangeImpl;
            EditorApplication.isPlaying = false;

            return taskCompletionSource.Task;

            void PlayModeStateChangeImpl(PlayModeStateChange state)
            {
                switch (state)
                {
                    case PlayModeStateChange.ExitingPlayMode:
                        break;
                    case PlayModeStateChange.EnteredEditMode:
                        taskCompletionSource.SetResult(true);
                        EditorApplication.playModeStateChanged -= PlayModeStateChangeImpl;
                        break;
                    case PlayModeStateChange.ExitingEditMode:
                    case PlayModeStateChange.EnteredPlayMode:
                    default:
                        taskCompletionSource.SetException(new Exception($"Unexpected state: {state}"));
                        EditorApplication.playModeStateChanged -= PlayModeStateChangeImpl;
                        break;
                }
            }
        }

        public static void RestartEditor()
        {
            EditorApplication.OpenProject(Environment.CurrentDirectory);
        }
    }

    class PreventEnteringPlayModeScope : IDisposable
    {
        private bool _disposed;
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
            if (_disposed) return;
            _disposed = true;
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

        public AllowPlaymodeScope AllowScope() => new AllowPlaymodeScope(this);

        public class AllowPlaymodeScope : IDisposable
        {
            private bool _disposed;
            private readonly PreventEnteringPlayModeScope _outer;

            public AllowPlaymodeScope(PreventEnteringPlayModeScope outer)
            {
                _outer = outer;
                if (_outer._disposed) throw new Exception("PreventEnteringPlayModeScope has been disposed");
                EditorApplication.playModeStateChanged -= _outer.PlayModeChanged;
            }

            ~AllowPlaymodeScope()
            {
                Dispose(false);
            }

            private void Dispose(bool disposing)
            {
                if (_disposed) return;
                _disposed = true;
                if (!disposing) return;
                if (_outer._disposed) throw new Exception("outer PreventEnteringPlayModeScope has been disposed");
                if (EditorApplication.isPlaying)
                    throw new Exception("Exiting AllowPlaymodeScope in playMode");
                EditorApplication.playModeStateChanged += _outer.PlayModeChanged;
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
        }
    }

    class OpeningSceneRestoreScope : IDisposable
    {
        private readonly (string path, bool isLoaded)[] _lastOpenedScenes;

        public OpeningSceneRestoreScope()
        {
            var scenes = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .ToArray();
            if (scenes.Any(x => x.isDirty))
                EditorSceneManager.SaveOpenScenes();
            _lastOpenedScenes = scenes.Any(x => string.IsNullOrEmpty(x.path))
                ? Array.Empty<(string path, bool isLoaded)>()
                : scenes.Select(x => (x.path, x.isLoaded)).ToArray();
        }

        public void Dispose()
        {
            if (_lastOpenedScenes.Length == 0)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            }
            else
            {
                var tmp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
                foreach (var lastOpenedScene in _lastOpenedScenes)
                {
                    var mode = lastOpenedScene.isLoaded
                        ? OpenSceneMode.Additive 
                        : OpenSceneMode.AdditiveWithoutLoading;
                    EditorSceneManager.OpenScene(lastOpenedScene.path, mode);
                }
                EditorSceneManager.CloseScene(tmp, true);
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