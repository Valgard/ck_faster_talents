using System;
using System.IO;
using PugMod;
using UnityEditor;
using UnityEngine;

namespace FasterTalents.Editor
{
    /// <summary>
    /// Editor-only helper invoked via
    /// <c>unity -batchmode -executeMethod FasterTalents.Editor.CLIBuildHelper.Build</c>.
    /// Wraps <see cref="ModBuilder.BuildMod"/> and surfaces success/failure
    /// as the Unity process exit code (0 on success, 1/2 on failure).
    /// </summary>
    public static class CLIBuildHelper
    {
        private const string ModName = "FasterTalents";
        // The "Create New Mod" wizard places ModBuilderSettings at the root
        // of Assets/, named after the mod.
        private const string SettingsPath = "Assets/" + ModName + ".asset";

        public static void Build()
        {
            try
            {
                var settings = AssetDatabase.LoadAssetAtPath<ModBuilderSettings>(SettingsPath);
                if (settings == null)
                {
                    Debug.LogError(
                        $"[CLIBuildHelper] Could not load ModBuilderSettings at {SettingsPath}");
                    EditorApplication.Exit(1);
                    return;
                }

                var exportPath = Environment.GetEnvironmentVariable("MOD_INSTALL_PATH");
                if (string.IsNullOrEmpty(exportPath))
                {
                    Debug.LogError("[CLIBuildHelper] MOD_INSTALL_PATH not set");
                    EditorApplication.Exit(1);
                    return;
                }

                Directory.CreateDirectory(exportPath);

                Debug.Log($"[CLIBuildHelper] Building {ModName} → {exportPath}");
                ModBuilder.BuildMod(settings, exportPath, ok =>
                {
                    Debug.Log($"[CLIBuildHelper] Build {(ok ? "succeeded" : "FAILED")}");
                    EditorApplication.Exit(ok ? 0 : 1);
                });
            }
            catch (Exception e)
            {
                Debug.LogError($"[CLIBuildHelper] Exception: {e}");
                EditorApplication.Exit(2);
            }
        }
    }
}
