using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UdonSharp;

public static class VerdantV154SupporterClockFix
{
    private const string RootName = "Verdant Shared UI State";
    private const string BackupFolder = "Assets/VerdantBoard/Generated/V15 Backups";

    [MenuItem("Tools/Verdant Board/V15.4/Apply Supporter Scroll + Clock Sync Fix")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            EditorUtility.DisplayDialog(
                "Verdant V15.4",
                "Leave Play Mode and wait for Unity/UdonSharp to finish compiling, then run this again.",
                "OK"
            );
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("Verdant V15.4", "No valid loaded scene was found.", "OK");
            return;
        }

        CreateBackup(scene);

        int scrollFixes = InstallScrollKeepers(scene);

        TMP_Text[] clocks = FindClockTexts(scene);
        Toggle[] formatToggles = FindFormatToggles(scene);

        GameObject root = FindOrCreateRoot(scene);
        VerdantSharedClock sharedClock = root.GetComponent<VerdantSharedClock>();
        if (sharedClock == null)
            sharedClock = Undo.AddComponent<VerdantSharedClock>(root);

        Undo.RecordObject(sharedClock, "Verdant V15.4 shared clock setup");

        sharedClock.clockTexts = clocks;
        sharedClock.formatToggles = formatToggles;
        sharedClock.toggleOnMeans24Hour = BuildToggleMeanings(formatToggles);
        sharedClock.use24Hour = Infer24HourMode(clocks, formatToggles);

        EditorUtility.SetDirty(sharedClock);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!string.IsNullOrEmpty(scene.path))
            EditorSceneManager.SaveScene(scene);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string summary =
            "Verdant V15.4 UI state fix installed.\n\n" +
            "Supporter scroll areas protected: " + scrollFixes + "\n" +
            "Clock displays linked: " + clocks.Length + "\n" +
            "12/24 controls linked: " + formatToggles.Length + "\n\n" +
            "The shared clock now writes every linked clock from the local machine time and keeps the 12/24-hour state consistent across Verdant panels.\n" +
            "Supporter/member scroll lists preserve their current position during runtime list/layout refreshes.\n\n" +
            "A scene backup was created first.";

        Debug.Log("[Verdant V15.4] " + summary.Replace("\n", " | "));
        EditorUtility.DisplayDialog("Verdant V15.4", summary, "OK");
    }

    private static int InstallScrollKeepers(Scene scene)
    {
        int changed = 0;

        foreach (ScrollRect scroll in Resources.FindObjectsOfTypeAll<ScrollRect>())
        {
            if (scroll == null || scroll.gameObject.scene != scene) continue;
            if (!IsSupporterNameList(scroll.transform)) continue;

            VerdantScrollPositionKeeper keeper = scroll.GetComponent<VerdantScrollPositionKeeper>();
            if (keeper == null)
            {
                keeper = Undo.AddComponent<VerdantScrollPositionKeeper>(scroll.gameObject);
                changed++;
            }

            Undo.RecordObject(keeper, "Verdant supporter scroll keeper");
            keeper.scrollRect = scroll;
            keeper.preserveVertical = true;
            keeper.protectUnexpectedTopReset = true;
            keeper.minimumUnexpectedJump = 0.12f;
            keeper.topResetThreshold = 0.995f;
            keeper.layoutHeightTolerance = 0.5f;
            EditorUtility.SetDirty(keeper);

            Undo.RecordObject(scroll, "Verdant supporter scroll setup");
            scroll.vertical = true;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            EditorUtility.SetDirty(scroll);
        }

        return changed;
    }

    private static bool IsSupporterNameList(Transform transform)
    {
        string path = HierarchyPath(transform).ToLowerInvariant();

        if (!path.Contains("support"))
            return false;

        if (path.Contains("benefit") || path.Contains("faq") || path.Contains("guide"))
            return false;

        bool memberContext =
            path.Contains("member") ||
            path.Contains("player") ||
            path.Contains("name") ||
            path.Contains("recognition") ||
            path.Contains("wall") ||
            path.Contains("supporter list") ||
            path.Contains("supporters");

        if (!memberContext)
            return false;

        return true;
    }

    private static TMP_Text[] FindClockTexts(Scene scene)
    {
        return Resources.FindObjectsOfTypeAll<TMP_Text>()
            .Where(text =>
                text != null &&
                text.gameObject.scene == scene &&
                IsClockText(text))
            .Distinct()
            .OrderBy(text => HierarchyPath(text.transform))
            .ToArray();
    }

    private static bool IsClockText(TMP_Text text)
    {
        string name = text.name.ToLowerInvariant();
        string path = HierarchyPath(text.transform).ToLowerInvariant();

        if (path.Contains("timer") || path.Contains("stopwatch") || path.Contains("duration") || path.Contains("video"))
            return false;

        bool timeNamed =
            name == "time" ||
            name.Contains("clock") ||
            name.Contains("time text") ||
            name.Contains("time label") ||
            path.Contains("/clock") ||
            path.Contains("clock/") ||
            path.Contains("top bar/time") ||
            path.Contains("topbar/time");

        if (!timeNamed)
            return false;

        return path.Contains("tablet") ||
               path.Contains("ipad") ||
               path.Contains("main board") ||
               path.Contains("mainboard") ||
               path.Contains("verdant board");
    }

    private static Toggle[] FindFormatToggles(Scene scene)
    {
        return Resources.FindObjectsOfTypeAll<Toggle>()
            .Where(toggle =>
            {
                if (toggle == null || toggle.gameObject.scene != scene) return false;

                string path = HierarchyPath(toggle.transform).ToLowerInvariant();
                string nearby = NearbyText(toggle.transform).ToLowerInvariant();
                string combined = path + " " + nearby;

                bool clockContext = combined.Contains("clock") || combined.Contains("time");
                bool formatContext =
                    combined.Contains("12") ||
                    combined.Contains("24") ||
                    combined.Contains("hour format") ||
                    combined.Contains("time format");

                return clockContext && formatContext;
            })
            .Distinct()
            .OrderBy(toggle => HierarchyPath(toggle.transform))
            .ToArray();
    }

    private static bool[] BuildToggleMeanings(Toggle[] toggles)
    {
        bool[] values = new bool[toggles.Length];

        for (int i = 0; i < toggles.Length; i++)
        {
            Toggle toggle = toggles[i];
            string combined =
                (HierarchyPath(toggle.transform) + " " + NearbyText(toggle.transform)).ToLowerInvariant();

            bool mentions12 = combined.Contains("12");
            bool mentions24 = combined.Contains("24");

            values[i] = mentions24 || !mentions12;
        }

        return values;
    }

    private static bool Infer24HourMode(TMP_Text[] clocks, Toggle[] toggles)
    {
        foreach (TMP_Text clock in clocks)
        {
            if (clock == null || string.IsNullOrWhiteSpace(clock.text)) continue;

            string value = clock.text.ToUpperInvariant();
            if (value.Contains("AM") || value.Contains("PM"))
                return false;
        }

        if (toggles != null && toggles.Length > 0)
        {
            bool[] meanings = BuildToggleMeanings(toggles);
            return meanings[0] ? toggles[0].isOn : !toggles[0].isOn;
        }

        return true;
    }

    private static GameObject FindOrCreateRoot(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = root.transform.Find(RootName);
            if (found != null) return found.gameObject;

            if (root.name == RootName)
                return root;
        }

        GameObject go = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(go, "Create Verdant shared UI state");
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }

    private static string NearbyText(Transform transform)
    {
        string result = "";

        TMP_Text own = transform.GetComponentInChildren<TMP_Text>(true);
        if (own != null && !string.IsNullOrWhiteSpace(own.text))
            result += " " + own.text;

        Transform parent = transform.parent;
        if (parent != null)
        {
            foreach (TMP_Text text in parent.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text != null && !string.IsNullOrWhiteSpace(text.text))
                    result += " " + text.text;
            }
        }

        return result;
    }

    private static string HierarchyPath(Transform transform)
    {
        if (transform == null) return "";

        string path = transform.name;
        Transform current = transform.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private static void CreateBackup(Scene scene)
    {
        if (string.IsNullOrEmpty(scene.path)) return;

        EnsureFolder(BackupFolder);

        string file =
            System.IO.Path.GetFileNameWithoutExtension(scene.path) +
            " - before V15.4 UI state fix " +
            DateTime.Now.ToString("yyyyMMdd-HHmmss") +
            ".unity";

        string backup = AssetDatabase.GenerateUniqueAssetPath(BackupFolder + "/" + file);
        EditorSceneManager.SaveScene(scene, backup, true);
        Debug.Log("[Verdant V15.4] Backup created: " + backup);
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
}
