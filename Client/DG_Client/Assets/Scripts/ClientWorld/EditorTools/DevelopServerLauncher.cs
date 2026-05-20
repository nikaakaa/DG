#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DG.EditorTools
{
    public static class DevelopServerLauncher
    {
        private const string ServerProjectRelativePath = "Server/Main/Main.csproj";
        private const string Command = "dotnet run --project Server\\Main\\Main.csproj -- --m Develop";

        [MenuItem("DG/Server/Start Develop Server")]
        private static void StartDevelopServer()
        {
            if (!TryFindRepoRoot(out string repoRoot, out string error))
            {
                EditorUtility.DisplayDialog("Start Develop Server", error, "OK");
                return;
            }

            string script = $"Set-Location -LiteralPath '{EscapePowerShellSingleQuoted(repoRoot)}'; {Command}";

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoExit -ExecutionPolicy Bypass -Command \"{script}\"",
                WorkingDirectory = repoRoot,
                UseShellExecute = true
            };

            Process.Start(startInfo);
            Debug.Log($"[DevelopServerLauncher] {Command}");
        }

        [MenuItem("DG/Server/Start Develop Server", true)]
        private static bool CanStartDevelopServer()
        {
            return !EditorApplication.isCompiling && !EditorApplication.isUpdating;
        }

        private static bool TryFindRepoRoot(out string repoRoot, out string error)
        {
            DirectoryInfo current = new DirectoryInfo(Application.dataPath);

            while (current != null)
            {
                string projectPath = Path.Combine(current.FullName, ServerProjectRelativePath);
                if (File.Exists(projectPath))
                {
                    repoRoot = current.FullName;
                    error = "";
                    return true;
                }

                current = current.Parent;
            }

            repoRoot = "";
            error = $"找不到 {ServerProjectRelativePath}，请确认当前 Unity 工程在 DG 仓库内。";
            return false;
        }

        private static string EscapePowerShellSingleQuoted(string value)
        {
            return value.Replace("'", "''");
        }
    }
}
#endif
