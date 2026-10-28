using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public static class VerdantVpm1543Installer
{
    private const string Version = "15.4.3";
    private const string SharedRootName = "Verdant VPM Shared UI State";

    [MenuItem("Tools/Verdant Board/V15.4.3/Apply Supporters + Clock + Version Fix", priority = 1)]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            EditorUtility.DisplayDialog("Verdant V15.4.3", "Leave Play Mode and wait for compilation to finish.", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("Verdant V15.4.3", "No loaded scene was found.", "OK");
            return;
        }

        CreateBackup(scene);

        int scrolls = ProtectSupporterScrolls(scene);
        TMP_Text[] tmpClocks = FindTmpClocks(scene);
        Text[] legacyClocks = FindLegacyClocks(scene);
        Toggle[] toggles = FindTimeToggles(scene);

        GameObject root = FindOrCreateSharedRoot(scene);
        VerdantVpmSharedClock clock = root.GetComponent<VerdantVpmSharedClock>();
        if (clock == null) clock = Undo.AddComponent<VerdantVpmSharedClock>(root);

        Undo.RecordObject(clock, "Verdant VPM clock setup");
        clock.tmpClockTexts = tmpClocks;
        clock.legacyClockTexts = legacyClocks;
        clock.formatToggles = toggles;
        clock.toggleOnMeans24Hour = toggles.Select(Is24HourToggle).ToArray();
        clock.use24Hour = Infer24Hour(tmpClocks, legacyClocks, toggles);
        EditorUtility.SetDirty(clock);

        int versions = RestoreVersionLabel(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog(
            "Verdant V15.4.3",
            "Fix applied.\n\n" +
            "Supporter ScrollRects: " + scrolls + "\n" +
            "TMP clocks: " + tmpClocks.Length + "\n" +
            "Legacy clocks: " + legacyClocks.Length + "\n" +
            "12/24 toggles: " + toggles.Length + "\n" +
            "Main Board version labels: " + versions + "\n\n" +
            "A scene backup was created first.",
            "OK"
        );
    }

    [MenuItem("Tools/Verdant Board/Diagnostics/Show VPM Status", priority = 100)]
    public static void ShowStatus()
    {
        EditorUtility.DisplayDialog(
            "Verdant VPM Status",
            "Verdant World System V" + Version + " editor tools are compiling and loaded successfully.",
            "OK"
        );
    }

    private static int ProtectSupporterScrolls(Scene scene)
    {
        int count = 0;
        foreach (ScrollRect sr in Resources.FindObjectsOfTypeAll<ScrollRect>())
        {
            if (sr == null || sr.gameObject.scene != scene || !sr.vertical) continue;
            string path = PathOf(sr.transform).ToLowerInvariant();
            if (!path.Contains("support")) continue;
            if (path.Contains("faq") || path.Contains("guide")) continue;

            VerdantVpmScrollKeeper keeper = sr.GetComponent<VerdantVpmScrollKeeper>();
            if (keeper == null)
            {
                keeper = Undo.AddComponent<VerdantVpmScrollKeeper>(sr.gameObject);
                count++;
            }

            Undo.RecordObject(keeper, "Verdant supporter scroll");
            keeper.scrollRect = sr;
            EditorUtility.SetDirty(keeper);
        }
        return count;
    }

    private static TMP_Text[] FindTmpClocks(Scene scene)
    {
        return Resources.FindObjectsOfTypeAll<TMP_Text>()
            .Where(t => t != null && t.gameObject.scene == scene && IsClockCandidate(t.text, t.name, t.transform))
            .Distinct().ToArray();
    }

    private static Text[] FindLegacyClocks(Scene scene)
    {
        return Resources.FindObjectsOfTypeAll<Text>()
            .Where(t => t != null && t.gameObject.scene == scene && IsClockCandidate(t.text, t.name, t.transform))
            .Distinct().ToArray();
    }

    private static Toggle[] FindTimeToggles(Scene scene)
    {
        return Resources.FindObjectsOfTypeAll<Toggle>()
            .Where(t =>
            {
                if (t == null || t.gameObject.scene != scene) return false;
                string s = (PathOf(t.transform) + " " + NearbyText(t.transform)).ToLowerInvariant();
                return (s.Contains("12") || s.Contains("24") || s.Contains("time format") || s.Contains("hour format")) &&
                       (s.Contains("tablet") || s.Contains("ipad") || s.Contains("main board") ||
                        s.Contains("mainboard") || s.Contains("settings") || s.Contains("verdant"));
            })
            .Distinct().ToArray();
    }

    private static bool Is24HourToggle(Toggle toggle)
    {
        string s = (PathOf(toggle.transform) + " " + NearbyText(toggle.transform)).ToLowerInvariant();
        bool has12 = s.Contains("12");
        bool has24 = s.Contains("24");
        return has24 || !has12;
    }

    private static bool Infer24Hour(TMP_Text[] tmp, Text[] legacy, Toggle[] toggles)
    {
        foreach (TMP_Text t in tmp)
            if (t != null && ((t.text ?? "").ToUpperInvariant().Contains("AM") || (t.text ?? "").ToUpperInvariant().Contains("PM")))
                return false;

        foreach (Text t in legacy)
            if (t != null && ((t.text ?? "").ToUpperInvariant().Contains("AM") || (t.text ?? "").ToUpperInvariant().Contains("PM")))
                return false;

        if (toggles.Length > 0)
            return Is24HourToggle(toggles[0]) ? toggles[0].isOn : !toggles[0].isOn;

        return true;
    }

    private static bool IsClockCandidate(string value, string objectName, Transform transform)
    {
        string path = PathOf(transform).ToLowerInvariant();
        string name = (objectName ?? "").ToLowerInvariant();

        if (path.Contains("timer") || path.Contains("stopwatch") || path.Contains("video") ||
            path.Contains("duration") || path.Contains("queue")) return false;

        bool surface = path.Contains("tablet") || path.Contains("ipad") || path.Contains("main board") ||
                       path.Contains("mainboard") || path.Contains("verdant board");
        if (!surface) return false;

        bool named = name == "time" || name.Contains("clock") || name.Contains("time text") ||
                     name.Contains("time label") || path.Contains("/clock") || path.Contains("/time");

        return named || LooksLikeTime(value);
    }

    private static bool LooksLikeTime(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        string s = value.Trim().ToUpperInvariant();
        int colon = s.IndexOf(':');
        if (colon <= 0) return false;

        int hour;
        if (!int.TryParse(s.Substring(0, colon).Trim(), out hour) || hour < 0 || hour > 23) return false;

        string right = s.Substring(colon + 1).Trim();
        int space = right.IndexOf(' ');
        if (space >= 0) right = right.Substring(0, space);

        int minute;
        return int.TryParse(right, out minute) && minute >= 0 && minute <= 59;
    }

    private static int RestoreVersionLabel(Scene scene)
    {
        RectTransform board = FindMainBoard(scene);
        if (board == null) return 0;

        TMP_Text existing = board.GetComponentsInChildren<TMP_Text>(true)
            .FirstOrDefault(t =>
            {
                string n = (t.name ?? "").ToLowerInvariant();
                string v = (t.text ?? "").ToLowerInvariant();
                return n.Contains("version") || n.Contains("build") || v.StartsWith("v15.") || v.StartsWith("v14.");
            });

        if (existing != null)
        {
            Undo.RecordObject(existing, "Verdant version");
            existing.gameObject.SetActive(true);
            existing.text = "V" + Version;
            EditorUtility.SetDirty(existing);
            return 1;
        }

        GameObject go = new GameObject("Verdant Version", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(go, "Create Verdant version");
        go.transform.SetParent(board, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-18f, 12f);
        rt.sizeDelta = new Vector2(180f, 28f);

        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        TMP_Text source = board.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t != null && t.font != null);
        if (source != null) label.font = source.font;
        label.text = "V" + Version;
        label.fontSize = 13f;
        label.alignment = TextAlignmentOptions.BottomRight;
        label.raycastTarget = false;
        label.color = new Color(1f, 1f, 1f, 0.7f);
        return 1;
    }

    private static RectTransform FindMainBoard(Scene scene)
    {
        RectTransform best = null;
        int scoreBest = 0;

        foreach (RectTransform rt in Resources.FindObjectsOfTypeAll<RectTransform>())
        {
            if (rt == null || rt.gameObject.scene != scene) continue;
            string p = PathOf(rt).ToLowerInvariant();
            if (p.Contains("tablet") || p.Contains("ipad") || p.Contains("video player")) continue;

            int score = 0;
            if (rt.name.ToLowerInvariant().Contains("main board") || rt.name.ToLowerInvariant().Contains("mainboard")) score += 100;
            if (rt.name.ToLowerInvariant().Contains("verdant board")) score += 80;
            if (p.Contains("main board") || p.Contains("mainboard")) score += 50;
            if (p.Contains("verdant board")) score += 40;

            if (score > scoreBest) { scoreBest = score; best = rt; }
        }

        return best;
    }

    private static GameObject FindOrCreateSharedRoot(Scene scene)
    {
        foreach (GameObject go in scene.GetRootGameObjects())
            if (go.name == SharedRootName) return go;

        GameObject root = new GameObject(SharedRootName);
        Undo.RegisterCreatedObjectUndo(root, "Create Verdant VPM shared state");
        SceneManager.MoveGameObjectToScene(root, scene);
        return root;
    }

    private static string NearbyText(Transform t)
    {
        string s = "";
        foreach (TMP_Text x in t.GetComponentsInChildren<TMP_Text>(true))
            if (x != null) s += " " + x.text;
        foreach (Text x in t.GetComponentsInChildren<Text>(true))
            if (x != null) s += " " + x.text;
        return s;
    }

    private static string PathOf(Transform t)
    {
        string p = t != null ? t.name : "";
        while (t != null && t.parent != null)
        {
            t = t.parent;
            p = t.name + "/" + p;
        }
        return p;
    }

    private static void CreateBackup(Scene scene)
    {
        if (string.IsNullOrEmpty(scene.path)) return;
        string folder = "Assets/VerdantBoard/Generated/VPM Backups";
        EnsureFolder(folder);
        string file = System.IO.Path.GetFileNameWithoutExtension(scene.path) +
                      " - before V15.4.3 " + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
        EditorSceneManager.SaveScene(scene, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + file), true);
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
