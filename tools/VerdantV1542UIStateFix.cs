using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UdonSharp;
using UdonSharpEditor;
using VRC.Udon;

public static class VerdantV1542UIStateFix
{
    private const string RootName = "Verdant Shared UI State";
    private const string BackupFolder = "Assets/VerdantBoard/Generated/V15 Backups";
    private const string Version = "15.4.2";

    [MenuItem("Tools/Verdant Board/V15.4.2/Apply Supporters + Clock + Version Fix")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            EditorUtility.DisplayDialog(
                "Verdant V15.4.2",
                "Leave Play Mode and wait for Unity/UdonSharp to finish compiling, then run this again.",
                "OK"
            );
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("Verdant V15.4.2", "No valid loaded scene was found.", "OK");
            return;
        }

        CreateBackup(scene);

        int scrollFixes = InstallScrollKeepers(scene);

        TMP_Text[] tmpClocks = FindTMPClockTexts(scene);
        Text[] legacyClocks = FindLegacyClockTexts(scene);
        Toggle[] formatToggles = FindFormatToggles(scene);

        GameObject root = FindOrCreateRoot(scene);
        VerdantSharedClock sharedClock = root.GetComponent<VerdantSharedClock>();
        if (sharedClock == null)
            sharedClock = Undo.AddComponent<VerdantSharedClock>(root);

        Undo.RecordObject(sharedClock, "Verdant V15.4.2 shared clock setup");
        sharedClock.clockTexts = tmpClocks;
        sharedClock.legacyClockTexts = legacyClocks;
        sharedClock.formatToggles = formatToggles;
        sharedClock.toggleOnMeans24Hour = BuildToggleMeanings(formatToggles);
        sharedClock.use24Hour = Infer24HourMode(tmpClocks, legacyClocks, formatToggles);
        sharedClock.refreshInterval = 0.15f;
        EditorUtility.SetDirty(sharedClock);

        int formatButtons = WireFormatButtons(scene, sharedClock);
        int versionLabels = InstallOrUpdateMainBoardVersion(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!string.IsNullOrEmpty(scene.path))
            EditorSceneManager.SaveScene(scene);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string summary =
            "Verdant V15.4.2 fix installed.\n\n" +
            "Supporter-page scroll areas protected: " + scrollFixes + "\n" +
            "TMP clock displays linked: " + tmpClocks.Length + "\n" +
            "Legacy clock displays linked: " + legacyClocks.Length + "\n" +
            "12/24 toggles linked: " + formatToggles.Length + "\n" +
            "12/24 buttons wired: " + formatButtons + "\n" +
            "Main Board version labels updated/created: " + versionLabels + "\n\n" +
            "The supporter page is protected against programmatic top resets, and every detected Verdant clock is driven from the same 12/24-hour state.\n" +
            "A small V" + Version + " label is also restored to the Main Board.\n\n" +
            "A scene backup was created first.";

        Debug.Log("[Verdant V15.4.2] " + summary.Replace("\n", " | "));
        EditorUtility.DisplayDialog("Verdant V15.4.2", summary, "OK");
    }

    private static int InstallScrollKeepers(Scene scene)
    {
        int changed = 0;

        foreach (ScrollRect scroll in Resources.FindObjectsOfTypeAll<ScrollRect>())
        {
            if (scroll == null || scroll.gameObject.scene != scene) continue;
            if (!IsSupporterPageScroll(scroll)) continue;

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
            keeper.minimumUnexpectedJump = 0.08f;
            keeper.topResetThreshold = 0.992f;
            keeper.layoutHeightTolerance = 0.25f;
            EditorUtility.SetDirty(keeper);

            Undo.RecordObject(scroll, "Verdant supporter scroll setup");
            scroll.vertical = true;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            EditorUtility.SetDirty(scroll);
        }

        return changed;
    }

    private static bool IsSupporterPageScroll(ScrollRect scroll)
    {
        string path = HierarchyPath(scroll.transform).ToLowerInvariant();

        if (!path.Contains("support"))
            return false;

        if (path.Contains("faq") || path.Contains("guide"))
            return false;

        if (!scroll.vertical)
            return false;

        // V15.4 was too strict here. Protect all vertical scrolling within the
        // Supporters/Support board, including the dynamically rebuilt name wall.
        return true;
    }

    private static TMP_Text[] FindTMPClockTexts(Scene scene)
    {
        return Resources.FindObjectsOfTypeAll<TMP_Text>()
            .Where(text => text != null && text.gameObject.scene == scene && IsClockCandidate(text.text, text.name, text.transform))
            .Distinct()
            .OrderBy(text => HierarchyPath(text.transform))
            .ToArray();
    }

    private static Text[] FindLegacyClockTexts(Scene scene)
    {
        return Resources.FindObjectsOfTypeAll<Text>()
            .Where(text => text != null && text.gameObject.scene == scene && IsClockCandidate(text.text, text.name, text.transform))
            .Distinct()
            .OrderBy(text => HierarchyPath(text.transform))
            .ToArray();
    }

    private static bool IsClockCandidate(string currentText, string objectName, Transform transform)
    {
        string name = (objectName ?? "").ToLowerInvariant();
        string path = HierarchyPath(transform).ToLowerInvariant();

        if (path.Contains("timer") || path.Contains("stopwatch") || path.Contains("duration") ||
            path.Contains("video") || path.Contains("queue") || path.Contains("youtube"))
            return false;

        bool verdantSurface =
            path.Contains("tablet") ||
            path.Contains("ipad") ||
            path.Contains("main board") ||
            path.Contains("mainboard") ||
            path.Contains("verdant board") ||
            path.Contains("world controls");

        if (!verdantSurface)
            return false;

        bool named =
            name == "time" ||
            name.Contains("clock") ||
            name.Contains("time text") ||
            name.Contains("time label") ||
            path.Contains("/clock") ||
            path.Contains("clock/") ||
            path.Contains("/time");

        return named || LooksLikeClockText(currentText);
    }

    private static bool LooksLikeClockText(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        string text = value.Trim().ToUpperInvariant();
        int colon = text.IndexOf(':');
        if (colon <= 0 || colon >= text.Length - 2) return false;

        string left = text.Substring(0, colon).Trim();
        string right = text.Substring(colon + 1).Trim();

        int hour;
        if (!int.TryParse(left, out hour)) return false;
        if (hour < 0 || hour > 23) return false;

        string minuteText = right;
        int space = minuteText.IndexOf(' ');
        if (space >= 0) minuteText = minuteText.Substring(0, space);

        int minute;
        if (!int.TryParse(minuteText, out minute)) return false;
        if (minute < 0 || minute > 59) return false;

        return true;
    }

    private static Toggle[] FindFormatToggles(Scene scene)
    {
        return Resources.FindObjectsOfTypeAll<Toggle>()
            .Where(toggle =>
            {
                if (toggle == null || toggle.gameObject.scene != scene) return false;

                string path = HierarchyPath(toggle.transform).ToLowerInvariant();
                if (!(path.Contains("tablet") || path.Contains("ipad") || path.Contains("main board") ||
                      path.Contains("mainboard") || path.Contains("verdant board") || path.Contains("settings")))
                    return false;

                string combined = (path + " " + NearbyText(toggle.transform)).ToLowerInvariant();

                return combined.Contains("12") || combined.Contains("24") ||
                       combined.Contains("hour format") || combined.Contains("time format");
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

    private static int WireFormatButtons(Scene scene, VerdantSharedClock sharedClock)
    {
        if (sharedClock == null) return 0;

        UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(sharedClock);
        if (backing == null) return 0;

        int changed = 0;

        foreach (Button button in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (button == null || button.gameObject.scene != scene) continue;

            string path = HierarchyPath(button.transform).ToLowerInvariant();
            if (!(path.Contains("tablet") || path.Contains("ipad") || path.Contains("main board") ||
                  path.Contains("mainboard") || path.Contains("verdant board") || path.Contains("settings")))
                continue;

            string combined = (path + " " + NearbyText(button.transform)).ToLowerInvariant();

            string eventName = null;

            if (combined.Contains("24h") || combined.Contains("24 h") || combined.Contains("24 hour"))
                eventName = "Use24Hour";
            else if (combined.Contains("12h") || combined.Contains("12 h") || combined.Contains("12 hour"))
                eventName = "Use12Hour";

            if (string.IsNullOrEmpty(eventName)) continue;
            if (HasPersistentUdonEvent(button, backing, eventName)) continue;

            Undo.RecordObject(button, "Verdant clock-format button");
            UnityEventTools.AddStringPersistentListener(button.onClick, backing.SendCustomEvent, eventName);
            EditorUtility.SetDirty(button);
            changed++;
        }

        return changed;
    }

    private static bool HasPersistentUdonEvent(Button button, UdonBehaviour target, string eventName)
    {
        try
        {
            SerializedObject so = new SerializedObject(button);
            SerializedProperty calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            if (calls == null || !calls.isArray) return false;

            for (int i = 0; i < calls.arraySize; i++)
            {
                SerializedProperty call = calls.GetArrayElementAtIndex(i);
                UnityEngine.Object existingTarget = call.FindPropertyRelative("m_Target").objectReferenceValue;
                string method = call.FindPropertyRelative("m_MethodName").stringValue;
                string argument = call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue;

                if (existingTarget == target && method == "SendCustomEvent" && argument == eventName)
                    return true;
            }
        }
        catch { }

        return false;
    }

    private static bool Infer24HourMode(TMP_Text[] tmpClocks, Text[] legacyClocks, Toggle[] toggles)
    {
        foreach (TMP_Text clock in tmpClocks)
        {
            if (clock == null || string.IsNullOrWhiteSpace(clock.text)) continue;
            string value = clock.text.ToUpperInvariant();
            if (value.Contains("AM") || value.Contains("PM")) return false;
        }

        foreach (Text clock in legacyClocks)
        {
            if (clock == null || string.IsNullOrWhiteSpace(clock.text)) continue;
            string value = clock.text.ToUpperInvariant();
            if (value.Contains("AM") || value.Contains("PM")) return false;
        }

        if (toggles != null && toggles.Length > 0)
        {
            bool[] meanings = BuildToggleMeanings(toggles);
            return meanings[0] ? toggles[0].isOn : !toggles[0].isOn;
        }

        return true;
    }

    private static int InstallOrUpdateMainBoardVersion(Scene scene)
    {
        RectTransform board = FindMainBoardRoot(scene);
        if (board == null) return 0;

        List<TMP_Text> existing = board.GetComponentsInChildren<TMP_Text>(true)
            .Where(t =>
            {
                if (t == null) return false;
                string n = t.name.ToLowerInvariant();
                string value = (t.text ?? "").ToLowerInvariant();
                return n.Contains("version") || n.Contains("build") ||
                       value.StartsWith("v14.") || value.StartsWith("v15.");
            })
            .ToList();

        if (existing.Count > 0)
        {
            foreach (TMP_Text text in existing)
            {
                Undo.RecordObject(text.gameObject, "Verdant version label");
                Undo.RecordObject(text, "Verdant version label");
                text.gameObject.SetActive(true);
                text.text = "V" + Version;
                EditorUtility.SetDirty(text);
            }

            return existing.Count;
        }

        GameObject go = new GameObject("Verdant Version", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(go, "Create Verdant version label");
        go.transform.SetParent(board, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-18f, 12f);
        rt.sizeDelta = new Vector2(220f, 34f);

        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        label.text = "V" + Version;
        label.fontSize = 14f;
        label.alignment = TextAlignmentOptions.BottomRight;
        label.raycastTarget = false;
        label.color = new Color(1f, 1f, 1f, 0.72f);

        TMP_Text styleSource = board.GetComponentsInChildren<TMP_Text>(true)
            .FirstOrDefault(t => t != null && t.font != null);

        if (styleSource != null)
            label.font = styleSource.font;

        EditorUtility.SetDirty(label);
        return 1;
    }

    private static RectTransform FindMainBoardRoot(Scene scene)
    {
        RectTransform best = null;
        int bestScore = int.MinValue;

        foreach (RectTransform rt in Resources.FindObjectsOfTypeAll<RectTransform>())
        {
            if (rt == null || rt.gameObject.scene != scene) continue;

            string name = rt.name.ToLowerInvariant();
            string path = HierarchyPath(rt).ToLowerInvariant();

            if (path.Contains("tablet") || path.Contains("ipad") || path.Contains("video player"))
                continue;

            int score = 0;
            if (name.Contains("main board") || name.Contains("mainboard")) score += 100;
            if (name.Contains("verdant board")) score += 80;
            if (path.Contains("main board") || path.Contains("mainboard")) score += 50;
            if (path.Contains("verdant board")) score += 40;

            TMP_Text[] texts = rt.GetComponentsInChildren<TMP_Text>(true);
            if (texts.Any(t => t != null && (t.text ?? "").ToUpperInvariant().Contains("VERDANT"))) score += 25;
            if (texts.Any(t => t != null && (t.text ?? "").ToUpperInvariant().Contains("WORLD"))) score += 10;

            if (score > bestScore)
            {
                bestScore = score;
                best = rt;
            }
        }

        return bestScore > 0 ? best : null;
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

        foreach (TMP_Text text in transform.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text != null && !string.IsNullOrWhiteSpace(text.text))
                result += " " + text.text;
        }

        foreach (Text text in transform.GetComponentsInChildren<Text>(true))
        {
            if (text != null && !string.IsNullOrWhiteSpace(text.text))
                result += " " + text.text;
        }

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
            " - before V15.4.2 UI fix " +
            DateTime.Now.ToString("yyyyMMdd-HHmmss") +
            ".unity";

        string backup = AssetDatabase.GenerateUniqueAssetPath(BackupFolder + "/" + file);
        EditorSceneManager.SaveScene(scene, backup, true);
        Debug.Log("[Verdant V15.4.2] Backup created: " + backup);
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
