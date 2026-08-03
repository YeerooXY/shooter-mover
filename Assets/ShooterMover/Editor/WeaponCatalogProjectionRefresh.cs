using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;

namespace ShooterMover.Editor
{
    [InitializeOnLoad]
    internal static class WeaponCatalogProjectionRefresh
    {
        private const string GeneratedAssetPath =
            "Assets/ShooterMover/Runtime/Application/Guns/Catalog/"
            + "AuthoredGunCatalogue.Generated.cs";

        private static bool running;

        static WeaponCatalogProjectionRefresh()
        {
            EditorApplication.delayCall += RefreshAfterDomainLoad;
        }

        [MenuItem("Shooter Mover/Content/Refresh Weapon Catalogue")]
        private static void RefreshFromMenu()
        {
            Refresh(true);
        }

        private static void RefreshAfterDomainLoad()
        {
            Refresh(false);
        }

        private static void Refresh(bool reportSuccess)
        {
            if (running || EditorApplication.isCompiling)
            {
                return;
            }

            running = true;
            try
            {
                string root = Directory.GetParent(
                    UnityEngine.Application.dataPath).FullName;
                string script = Path.Combine(
                    root,
                    "tools",
                    "item-maker",
                    "runtime-export.js");
                if (!File.Exists(script))
                {
                    UnityEngine.Debug.LogError(
                        "weapon-catalog-runtime-export-script-missing:" + script);
                    return;
                }

                var start = new ProcessStartInfo
                {
                    FileName = "node",
                    Arguments = Quote(script) + " " + Quote(root),
                    WorkingDirectory = root,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                string output;
                string error;
                int exitCode;
                using (Process process = Process.Start(start))
                {
                    output = process.StandardOutput.ReadToEnd();
                    error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    exitCode = process.ExitCode;
                }

                if (exitCode != 0)
                {
                    UnityEngine.Debug.LogError(
                        "weapon-catalog-runtime-export-failed:"
                        + (string.IsNullOrWhiteSpace(error)
                            ? output
                            : error));
                    return;
                }

                bool changed = output.IndexOf(
                    "\"generatedChanged\": true",
                    StringComparison.Ordinal) >= 0;
                if (changed)
                {
                    AssetDatabase.ImportAsset(
                        GeneratedAssetPath,
                        ImportAssetOptions.ForceUpdate);
                }
                if (reportSuccess)
                {
                    UnityEngine.Debug.Log(
                        "Weapon Maker production catalogue refreshed.\n"
                        + output.Trim());
                }
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
            }
            finally
            {
                running = false;
            }
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }
    }
}
