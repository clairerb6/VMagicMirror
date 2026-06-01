using System.IO;
using NugetForUnity;
using NugetForUnity.Configuration;
using UnityEditor;
using UnityEngine;

public static class NuGetCliRestore
{
    public static void RestoreAndQuit()
    {
        Debug.Log("[NuGetCliRestore] Loading NuGet config...");
        ConfigurationManager.LoadNugetConfigFile();

        // Use full restore in CI/CLI to ensure dependencies are materialized.
        ConfigurationManager.NugetConfigFile.SlimRestore = false;

        Debug.Log("[NuGetCliRestore] Starting PackageRestorer.Restore(false)...");
        PackageRestorer.Restore(false);
        AssetDatabase.Refresh();

        var projectRoot = Directory.GetCurrentDirectory();
        var packagesDir = Path.Combine(projectRoot, "Packages", "nuget-packages", "Packages");
        var hasR3 = Directory.Exists(packagesDir) &&
                    Directory.GetFiles(packagesDir, "R3.dll", SearchOption.AllDirectories).Length > 0;
        var hasTimeProvider = Directory.Exists(packagesDir) &&
                              Directory.GetFiles(packagesDir, "Microsoft.Bcl.TimeProvider.dll", SearchOption.AllDirectories).Length > 0;
        var hasAsyncInterfaces = Directory.Exists(packagesDir) &&
                                 Directory.GetFiles(packagesDir, "Microsoft.Bcl.AsyncInterfaces.dll", SearchOption.AllDirectories).Length > 0;

        Debug.Log($"[NuGetCliRestore] R3.dll={hasR3}, TimeProvider={hasTimeProvider}, AsyncInterfaces={hasAsyncInterfaces}");
        Debug.Log("[NuGetCliRestore] Done.");
        EditorApplication.Exit(0);
    }
}
