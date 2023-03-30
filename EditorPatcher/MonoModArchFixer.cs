using System;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using UnityEngine;

namespace Anatawa12.ContinuousAvatarUploader.EditorPatcher
{
    static class MonoModArchFixer
    {
        public static void FixCurrentPlatform()
        {
            var asm = typeof(Harmony).Assembly;
            var platformHelperType = asm.GetType("MonoMod.Utils.PlatformHelper");
            var platformType = asm.GetType("MonoMod.Utils.Platform");
            var currentField = platformHelperType.GetField("_current", BindingFlags.Static | BindingFlags.NonPublic);
            var currentLockedField =
                platformHelperType.GetField("_currentLocked", BindingFlags.Static | BindingFlags.NonPublic);
            var determinePlatformMethod =
                platformHelperType.GetMethod("DeterminePlatform", BindingFlags.Static | BindingFlags.NonPublic);

            Debug.Assert(determinePlatformMethod != null, nameof(determinePlatformMethod) + " != null");
            Debug.Assert(currentField != null, nameof(currentField) + " != null");
            Debug.Assert(currentLockedField != null, nameof(currentLockedField) + " != null");

            var locked = (bool)currentLockedField.GetValue(null);

            if (locked)
            {
                // verification only
                var currentPlatform = (Platform)Convert.ToInt32(currentField.GetValue(null));
                if (currentPlatform == Platform.Unknown)
                    return;
                var isCurrentArm = (currentPlatform & Platform.ARM) == Platform.ARM;
                Debug.Assert(isCurrentArm == IsARM,
                    "locked detected platform for MonoMod and actual platform mismatch about ARM. " +
                    $"locked detected platform: isArm = {isCurrentArm}, actual: isArm = {IsARM}");
            }
            else
            {
                var currentPlatform = (Platform)Convert.ToInt32(currentField.GetValue(null));
                if (currentPlatform == Platform.Unknown)
                {
                    determinePlatformMethod.Invoke(null, Array.Empty<object>());
                    currentPlatform = (Platform)Convert.ToInt32(currentField.GetValue(null));
                }

                var isCurrentArm = (currentPlatform & Platform.ARM) == Platform.ARM;

                if (isCurrentArm != IsARM)
                {
                    if (IsARM)
                        currentPlatform |= Platform.ARM;
                    else
                        currentPlatform &= ~Platform.ARM;

                    currentField.SetValue(null, Enum.ToObject(platformType, currentPlatform));
                }
            }
        }

        private static bool IsARM => RuntimeInformation.ProcessArchitecture == Architecture.Arm ||
                                     RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

        // ReSharper disable InconsistentNaming
        // ReSharper disable UnusedMember.Local
        [Flags]
        enum Platform
        {
            OS = 1,
            Bits64 = 2,
            NT = 4,
            Unix = 8,
            ARM = 65536, // 0x00010000
            Wine = 131072, // 0x00020000
            Unknown = 17, // 0x00000011
            Windows = 37, // 0x00000025
            MacOS = 73, // 0x00000049
            Linux = 137, // 0x00000089
            Android = 393, // 0x00000189
            iOS = 585, // 0x00000249
        }
        // ReSharper restore UnusedMember.Local
        // ReSharper restore InconsistentNaming
    }
}
