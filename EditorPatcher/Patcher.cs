using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEditor;

namespace Anatawa12.ContinuousAvatarUploader.EditorPatcher {
    [InitializeOnLoad]
    public static class Patcher
    {
        public static event DisplayDialogEventHandler DisplayDialog;

        static Patcher()
        {
            Patch();
        }

        private static MethodInfo GetOriginalDisplayDialog(Type[] args)
        {
            //var type = typeof(UnityEditor::UnityEditor.EditorUtility);
            var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.EditorUtility");
            return type.GetMethod("DisplayDialog", BindingFlags.Static | BindingFlags.Public | BindingFlags.Static,
                null, args, null);
        }

        private static MethodInfo GetWrapperDisplayDialog(Type[] args)
        {
            return typeof(Patcher).GetMethod("Wrapper", BindingFlags.Static | BindingFlags.NonPublic,
                null, args, null);
        }

        private static void Patch()
        {
            MonoModArchFixer.FixCurrentPlatform();

            var withCancel = new[] { typeof(string), typeof(string), typeof(string), typeof(string) };
            var noCancel = new[] { typeof(string), typeof(string), typeof(string) };

            Memory.DetourMethod(GetOriginalDisplayDialog(withCancel), GetWrapperDisplayDialog(withCancel));
            Memory.DetourMethod(GetOriginalDisplayDialog(noCancel), GetWrapperDisplayDialog(noCancel));
        }

        [UsedImplicitly]
        private static bool Wrapper(string title, string message, string ok, string cancel)
        {
            var eventArgs = new DisplayDialogEventArgs(title, message, ok, cancel);
            DisplayDialog?.Invoke(eventArgs);

            if (eventArgs.Result != null) return (bool)eventArgs.Result;

            return EditorUtility.DisplayDialog(title, message, ok, cancel);
        }

        [UsedImplicitly]
        private static bool Wrapper(string title, string message, string ok)
        {
            return Wrapper(title, message, ok, "");
        }
    }

    public delegate void DisplayDialogEventHandler(DisplayDialogEventArgs args);

    public class DisplayDialogEventArgs : EventArgs
    {
        public string Title { get; }
        public string Message { get; }
        public string Ok { get; }
        public string Cancel { get; }
        public bool? Result { get; set; }

        internal DisplayDialogEventArgs(string title, string message, string ok, string cancel)
        {
            Title = title;
            Message = message;
            Ok = ok;
            Cancel = cancel;
        }
    }
}

namespace UnityEditor
{ 
    static class EditorUtility
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        public static extern bool DisplayDialog(
            string title,
            string message,
            string ok,
            string cancel);
    }
}
