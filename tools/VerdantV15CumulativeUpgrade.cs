using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public static class VerdantV15CumulativeUpgrade
{
    private const string TargetVersion = "15.2.0";
    private const string VersionPath = "Assets/VerdantBoard/VERSION.txt";
    private const string BackupFolder = "Assets/VerdantBoard/Generated/V15 Backups";

    [MenuItem("Tools/Verdant Board/Updates/Run V15.2 Cumulative Upgrade", priority = 1)]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            EditorUtility.DisplayDialog(
                "Verdant V15.2 Cumulative Upgrade",
                "Leave Play Mode and wait for Unity/UdonSharp to finish compiling, then run this again.",
                "OK"
            );
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog(
                "Verdant V15.2 Cumulative Upgrade",
                "No valid loaded scene was found.",
                "OK"
            );
            return;
        }

        CreateBackup(scene);

        int rc1Changes = 0;
        int rc2Changes = 0;
        int rc3Changes = 0;
        string warning = "";
        bool migrationFailed = false;

        try
        {
            rc1Changes = RunRC1(scene);
        }
        catch (Exception ex)
        {
            warning += "RC1 stage failed: " + RootMessage(ex) + "\n";
            migrationFailed = true;
            Debug.LogException(ex);
        }

        try
        {
            rc2Changes = RunRC2(scene);
        }
        catch (Exception ex)
        {
            warning += "RC2 stage failed: " + RootMessage(ex) + "\n";
            migrationFailed = true;
            Debug.LogException(ex);
        }

        try
        {
            rc3Changes = RunRC3(scene);
        }
        catch (Exception ex)
        {
            warning += "RC3 stage failed: " + RootMessage(ex) + "\n";
            migrationFailed = true;
            Debug.LogException(ex);
        }

        if (!migrationFailed)
            StampVersion(scene, TargetVersion);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!string.IsNullOrEmpty(scene.path))
            EditorSceneManager.SaveScene(scene);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string summary =
            "Verdant cumulative upgrade finished.\n\n" +
            "Target version: " + TargetVersion + "\n" +
            "RC1 media/stability actions: " + rc1Changes + "\n" +
            "RC2 UI/media actions: " + rc2Changes + "\n" +
            "RC3 YouTube cleanup actions: " + rc3Changes + "\n" +
            "Version marker: " + (!migrationFailed ? VersionPath : "NOT UPDATED because a migration stage failed") + "\n";

        if (!string.IsNullOrEmpty(warning))
            summary += "\nWarnings:\n" + warning;

        summary += migrationFailed
            ? "\nA scene backup was created. Fix the reported migration error and run the cumulative upgrade again; the installed version was intentionally not changed."
            : "\nA scene backup was created before any migration changes.\nNow use Tools > Verdant Board > Updates > Update Manager to verify the installed/latest version.";

        Debug.Log("[Verdant V15.2 Cumulative] " + summary.Replace("\n", " | "));
        EditorUtility.DisplayDialog("Verdant V15.2 Cumulative Upgrade", summary, "OK");
    }

    private static int RunRC1(Scene scene)
    {
        Type type = typeof(VerdantV15ReleaseFinalizer);
        int changed = 0;

        Component shared = Call(type, "FindBestComponent", scene, "VerdantSharedVideo") as Component;
        Component player = Call(type, "GetMember", shared, "player") as Component;
        Component speaker = Call(type, "GetMember", shared, "speaker") as Component;

        if (player == null && shared != null)
            player = Call(type, "FindLikelyVideoPlayer", shared.transform) as Component;

        RenderTexture target = Call(type, "EnsureSharedRenderTexture", player) as RenderTexture;

        changed += IntResult(Call(type, "RewireMediaAdapters", scene, shared, player, speaker));
        changed += IntResult(Call(type, "RepairVideoSurfaces", scene, target));
        changed += IntResult(Call(type, "RepairYoutubeThumbnails", scene));
        changed += IntResult(Call(type, "ProtectMediaFromThemeFollowers", scene));
        changed += IntResult(Call(type, "DisableKnownLegacyVideoWrappers", scene));
        changed += IntResult(Call(type, "RetireKnownDeadPosterStrings", scene));
        changed += IntResult(Call(type, "StabilizeArcadeRoots", scene));
        Call(type, "UpdateVersionLabels", scene);

        return changed;
    }

    private static int RunRC2(Scene scene)
    {
        Type type = typeof(VerdantV15RC2UIRepair);
        int changed = 0;

        changed += IntResult(Call(type, "RepairYoutubeSearchVisuals", scene));
        changed += IntResult(Call(type, "RepairYoutubeSearchButtons", scene));
        changed += IntResult(Call(type, "RepairSupporterTierScrolling", scene));
        changed += IntResult(Call(type, "RepairTabletBackgroundCards", scene));
        changed += IntResult(Call(type, "RepairCommunityAdBars", scene));
        changed += IntResult(Call(type, "UpdateVersionLabels", scene));

        return changed;
    }

    private static int RunRC3(Scene scene)
    {
        Type type = typeof(VerdantV15RC3YoutubeLayerCleanup);
        int changed = 0;

        Transform youtubeRoot = Call(type, "FindYoutubeRoot", scene) as Transform;
        if (youtubeRoot == null)
        {
            Debug.LogWarning("[Verdant V15.2 Cumulative] YouTube root not found. RC3 cleanup was skipped; this is okay for scenes without the Verdant YouTube page.");
            return 0;
        }

        changed += IntResult(Call(type, "RemoveUnnecessaryYoutubeLayers", youtubeRoot));
        changed += IntResult(Call(type, "ClearBlockingRaycasts", youtubeRoot));
        changed += IntResult(Call(type, "RestoreThumbnailVisibility", youtubeRoot));
        changed += IntResult(Call(type, "RemoveThumbnailThemeReferences", scene, youtubeRoot));
        changed += IntResult(Call(type, "RepairCanvasGroups", youtubeRoot));
        changed += IntResult(Call(type, "UpdateVersionLabels", scene));

        return changed;
    }

    private static object Call(Type type, string name, params object[] args)
    {
        MethodInfo[] candidates = type
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.Name == name && m.GetParameters().Length == args.Length)
            .ToArray();

        foreach (MethodInfo method in candidates)
        {
            ParameterInfo[] parameters = method.GetParameters();
            bool compatible = true;

            for (int i = 0; i < parameters.Length; i++)
            {
                if (args[i] == null) continue;
                if (!parameters[i].ParameterType.IsAssignableFrom(args[i].GetType()))
                {
                    compatible = false;
                    break;
                }
            }

            if (!compatible) continue;

            try
            {
                return method.Invoke(null, args);
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException ?? ex;
            }
        }

        throw new MissingMethodException(type.FullName, name);
    }

    private static int IntResult(object value)
    {
        return value is int ? (int)value : 0;
    }

    private static void StampVersion(Scene scene, string version)
    {
        EnsureFolder("Assets/VerdantBoard");

        File.WriteAllText(VersionPath, version + Environment.NewLine);
        AssetDatabase.ImportAsset(VersionPath, ImportAssetOptions.ForceUpdate);

        foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (text == null || text.gameObject.scene != scene || string.IsNullOrWhiteSpace(text.text))
                continue;

            string current = text.text;
            if (!current.Contains("V14.") && !current.Contains("V15."))
                continue;

            Undo.RecordObject(text, "Verdant V15.2 version label");
            text.text = "V" + version;
            EditorUtility.SetDirty(text);
        }
    }

    private static void CreateBackup(Scene scene)
    {
        if (string.IsNullOrEmpty(scene.path)) return;

        EnsureFolder(BackupFolder);

        string file =
            Path.GetFileNameWithoutExtension(scene.path) +
            " - before V15.2 cumulative " +
            DateTime.Now.ToString("yyyyMMdd-HHmmss") +
            ".unity";

        string backup = AssetDatabase.GenerateUniqueAssetPath(BackupFolder + "/" + file);
        EditorSceneManager.SaveScene(scene, backup, true);
        Debug.Log("[Verdant V15.2 Cumulative] Scene backup created: " + backup);
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static string RootMessage(Exception ex)
    {
        Exception current = ex;
        while (current.InnerException != null)
            current = current.InnerException;
        return current.Message;
    }
}
