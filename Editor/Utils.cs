using System;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Anatawa12.ContinuousAvatarUploader.Editor
{
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