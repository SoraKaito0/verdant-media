using UnityEditor;
using UnityEngine;

public static class VerdantVpmBootstrapMenu
{
    private const string Version = "15.4.10";

    [MenuItem("Tools/Verdant Board/Diagnostics/Package Status", priority = 0)]
    public static void PackageStatus()
    {
        EditorUtility.DisplayDialog(
            "Verdant VPM",
            "Verdant World System V" + Version + " package menu is loaded.\n\n" +
            "This bootstrap menu is compiled independently from Verdant Runtime so it can remain available even if a runtime script fails.",
            "OK"
        );
    }
}
