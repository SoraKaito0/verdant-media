using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public static class VerdantVpm1548Installer
{
    private const string Version = "15.4.8";
    private const string SharedRootName = "Verdant VPM Shared UI State";

    [MenuItem("Tools/Verdant Board/V15.4.8/Apply Supporters + Clock + Version Fix", priority = 1)]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            EditorUtility.DisplayDialog("Verdant V15.4.8", "Leave Play Mode and wait for compilation to finish.", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("Verdant V15.4.8", "No loaded scene was found.", "OK");
            return;
        }

        CreateBackup(scene);

        SupporterOwnershipResult supporterResult = InstallSupporterOwnership(scene);

        Transform tabletRoot = FindTabletRoot(scene);
        Transform mainBoardRoot = FindMainBoardRoot(scene);

        TMP_Text[] tmpClocks = FindTmpClocks(scene, tabletRoot, mainBoardRoot);
        Text[] legacyClocks = FindLegacyClocks(scene, tabletRoot, mainBoardRoot);
        Toggle[] toggles = FindTimeToggles(scene);
        TMP_Dropdown[] tmpDropdowns = FindTmpTimeDropdowns(scene);
        Dropdown[] legacyDropdowns = FindLegacyTimeDropdowns(scene);

        int detachedLegacyWriters = DetachLegacyClockWriters(scene, tmpClocks, legacyClocks);

        GameObject root = FindOrCreateSharedRoot(scene);
        VerdantClockController1547 clock = root.GetComponent<VerdantClockController1547>();
        if (clock == null) clock = Undo.AddComponent<VerdantClockController1547>(root);

        Undo.RecordObject(clock, "Verdant VPM clock setup");
        clock.tmpClockTexts = tmpClocks;
        clock.legacyClockTexts = legacyClocks;
        clock.formatToggles = toggles;
        clock.toggleOnMeans24Hour = toggles.Select(Is24HourToggle).ToArray();
        clock.tmpFormatDropdowns = tmpDropdowns;
        clock.tmpDropdown12Values = GetTmpDropdownValues(tmpDropdowns, false);
        clock.tmpDropdown24Values = GetTmpDropdownValues(tmpDropdowns, true);
        clock.legacyFormatDropdowns = legacyDropdowns;
        clock.legacyDropdown12Values = GetLegacyDropdownValues(legacyDropdowns, false);
        clock.legacyDropdown24Values = GetLegacyDropdownValues(legacyDropdowns, true);
        clock.use24Hour = Infer24Hour(tmpClocks, legacyClocks, toggles, tmpDropdowns, legacyDropdowns);
        EditorUtility.SetDirty(clock);

        int versions = UpdateAllVersionLabels(scene, mainBoardRoot, tabletRoot);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog(
            "Verdant V15.4.8",
            "Fix applied.\n\n" +
            "Primary supporter ScrollRect: " + (supporterResult.primary != null ? PathOf(supporterResult.primary.transform) : "NOT FOUND") + "\n" +
            "Legacy supporter roots disabled: " + supporterResult.legacyRoots.Length + "\n" +
            "Old scroll keepers removed: " + supporterResult.removedKeepers + "\n" +
            "TMP clocks: " + tmpClocks.Length + "\n" +
            "Legacy clocks: " + legacyClocks.Length + "\n" +
            "12/24 toggles: " + toggles.Length + "\n" +
            "TMP 12/24 dropdowns: " + tmpDropdowns.Length + "\n" +
            "Legacy 12/24 dropdowns: " + legacyDropdowns.Length + "\n" +
            "Legacy clock writers detached: " + detachedLegacyWriters + "\n" +
            "Version labels updated/created: " + versions + "\n\n" +
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

    private sealed class SupporterOwnershipResult
    {
        public ScrollRect primary;
        public GameObject[] legacyRoots = new GameObject[0];
        public int removedKeepers;
    }

    private static SupporterOwnershipResult InstallSupporterOwnership(Scene scene)
    {
        SupporterOwnershipResult result = new SupporterOwnershipResult();

        ScrollRect[] candidates = Resources.FindObjectsOfTypeAll<ScrollRect>()
            .Where(sr => sr != null &&
                         sr.gameObject.scene == scene &&
                         sr.vertical &&
                         IsSupporterListCandidate(sr))
            .Distinct()
            .ToArray();

        if (candidates.Length == 0)
            return result;

        ScrollRect primary = candidates
            .OrderByDescending(ScoreSupporterScroll)
            .ThenByDescending(sr => sr.gameObject.activeInHierarchy)
            .FirstOrDefault();

        result.primary = primary;

        Transform pageRoot = FindSupporterPageRoot(primary != null ? primary.transform : null);
        System.Collections.Generic.List<GameObject> legacy = new System.Collections.Generic.List<GameObject>();

        foreach (ScrollRect sr in candidates)
        {
            VerdantVpmScrollKeeper oldKeeper = sr.GetComponent<VerdantVpmScrollKeeper>();
            if (oldKeeper != null)
            {
                Undo.DestroyObjectImmediate(oldKeeper);
                result.removedKeepers++;
            }

            if (sr == primary) continue;
            if (pageRoot == null || !sr.transform.IsChildOf(pageRoot)) continue;

            GameObject branch = FindBranchUnder(pageRoot, sr.transform);
            GameObject primaryBranch = FindBranchUnder(pageRoot, primary.transform);

            if (branch == null || branch == primaryBranch) continue;

            // A duplicate branch that is inactive during setup is the strongest signal:
            // it is commonly the older layout that a legacy controller re-enables later.
            bool likelyLegacy =
                !branch.activeSelf ||
                !branch.activeInHierarchy ||
                NameLooksLegacy(branch.name) ||
                NameLooksLegacy(sr.name);

            if (!likelyLegacy)
            {
                int branchScore = ScoreSupporterScroll(sr);
                int primaryScore = ScoreSupporterScroll(primary);
                likelyLegacy = branchScore + 20 < primaryScore;
            }

            if (likelyLegacy && !legacy.Contains(branch))
            {
                Undo.RecordObject(branch, "Disable legacy Verdant supporter UI");
                branch.SetActive(false);
                legacy.Add(branch);
            }
        }

        GameObject stateRoot = FindOrCreateSharedRoot(scene);
        VerdantSupporterOwnership1548 owner = stateRoot.GetComponent<VerdantSupporterOwnership1548>();
        if (owner == null)
            owner = Undo.AddComponent<VerdantSupporterOwnership1548>(stateRoot);

        Undo.RecordObject(owner, "Verdant supporter UI ownership");
        owner.primaryScroll = primary;
        owner.legacyRoots = legacy.ToArray();
        owner.checkInterval = 0.15f;
        owner.topThreshold = 0.995f;
        EditorUtility.SetDirty(owner);

        result.legacyRoots = legacy.ToArray();
        return result;
    }

    private static bool IsSupporterListCandidate(ScrollRect sr)
    {
        string path = PathOf(sr.transform).ToLowerInvariant();

        if (!path.Contains("support"))
            return false;

        if (path.Contains("faq") || path.Contains("guide") ||
            path.Contains("benefit") || path.Contains("tier") ||
            path.Contains("background"))
            return false;

        return path.Contains("name") ||
               path.Contains("member") ||
               path.Contains("player") ||
               path.Contains("wall") ||
               path.Contains("recognition") ||
               path.Contains("supporter") ||
               path.Contains("list");
    }

    private static int ScoreSupporterScroll(ScrollRect sr)
    {
        if (sr == null) return int.MinValue;

        string path = PathOf(sr.transform).ToLowerInvariant();
        int score = 0;

        if (sr.gameObject.activeInHierarchy) score += 100;
        if (sr.gameObject.activeSelf) score += 40;
        if (path.Contains("supporters")) score += 25;
        if (path.Contains("name")) score += 20;
        if (path.Contains("member")) score += 15;
        if (path.Contains("recognition")) score += 15;
        if (path.Contains("wall")) score += 12;
        if (path.Contains("list")) score += 10;

        if (NameLooksLegacy(path)) score -= 150;

        if (sr.content != null)
        {
            score += Mathf.Min(sr.content.childCount, 25);
            if (sr.content.gameObject.activeInHierarchy) score += 15;
        }

        return score;
    }

    private static bool NameLooksLegacy(string value)
    {
        string s = (value ?? "").ToLowerInvariant();
        return s.Contains("old") ||
               s.Contains("legacy") ||
               s.Contains("classic") ||
               s.Contains("previous") ||
               s.Contains("backup") ||
               s.Contains("v14");
    }

    private static Transform FindSupporterPageRoot(Transform start)
    {
        Transform current = start;

        while (current != null)
        {
            string name = current.name.ToLowerInvariant();

            if (name.Contains("support") &&
                (name.Contains("page") || name.Contains("panel") ||
                 name.Contains("board") || name.Contains("section") ||
                 name == "supporters" || name == "supporter"))
                return current;

            current = current.parent;
        }

        return start != null ? start.parent : null;
    }

    private static GameObject FindBranchUnder(Transform ancestor, Transform leaf)
    {
        if (ancestor == null || leaf == null || !leaf.IsChildOf(ancestor))
            return null;

        Transform current = leaf;

        while (current.parent != null && current.parent != ancestor)
            current = current.parent;

        return current.gameObject;
    }

    private static TMP_Text[] FindTmpClocks(Scene scene, Transform tabletRoot, Transform mainBoardRoot)
    {
        return Resources.FindObjectsOfTypeAll<TMP_Text>()
            .Where(t => t != null && t.gameObject.scene == scene &&
                        IsClockCandidate(t.text, t.name, t.transform, tabletRoot, mainBoardRoot))
            .Distinct().ToArray();
    }

    private static Text[] FindLegacyClocks(Scene scene, Transform tabletRoot, Transform mainBoardRoot)
    {
        return Resources.FindObjectsOfTypeAll<Text>()
            .Where(t => t != null && t.gameObject.scene == scene &&
                        IsClockCandidate(t.text, t.name, t.transform, tabletRoot, mainBoardRoot))
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

    private static bool Infer24Hour(
        TMP_Text[] tmp,
        Text[] legacy,
        Toggle[] toggles,
        TMP_Dropdown[] tmpDropdowns,
        Dropdown[] legacyDropdowns)
    {
        if (tmpDropdowns != null)
        {
            foreach (TMP_Dropdown dropdown in tmpDropdowns)
            {
                if (dropdown == null || dropdown.options == null || dropdown.value < 0 || dropdown.value >= dropdown.options.Count) continue;
                string label = dropdown.options[dropdown.value].text.ToLowerInvariant();
                if (label.Contains("24")) return true;
                if (label.Contains("12")) return false;
            }
        }

        if (legacyDropdowns != null)
        {
            foreach (Dropdown dropdown in legacyDropdowns)
            {
                if (dropdown == null || dropdown.options == null || dropdown.value < 0 || dropdown.value >= dropdown.options.Count) continue;
                string label = dropdown.options[dropdown.value].text.ToLowerInvariant();
                if (label.Contains("24")) return true;
                if (label.Contains("12")) return false;
            }
        }

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

    private static bool IsClockCandidate(
        string value,
        string objectName,
        Transform transform,
        Transform tabletRoot,
        Transform mainBoardRoot)
    {
        string path = PathOf(transform).ToLowerInvariant();
        string name = (objectName ?? "").ToLowerInvariant();

        if (path.Contains("timer") || path.Contains("stopwatch") || path.Contains("video") ||
            path.Contains("duration") || path.Contains("queue")) return false;

        bool onTablet = tabletRoot != null && transform.IsChildOf(tabletRoot);
        bool onBoard = mainBoardRoot != null && transform.IsChildOf(mainBoardRoot);
        bool namedSurface = path.Contains("tablet") || path.Contains("ipad") ||
                            path.Contains("main board") || path.Contains("mainboard") ||
                            path.Contains("verdant board");

        if (!onTablet && !onBoard && !namedSurface) return false;

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

    private static int UpdateAllVersionLabels(Scene scene, Transform mainBoardRoot, Transform tabletRoot)
    {
        int changed = 0;
        string versionText = "V15.4.8";

        foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (text == null || text.gameObject.scene != scene) continue;
            if (!IsVersionText(text.name, text.text, text.transform)) continue;

            Undo.RecordObject(text, "Verdant version label");
            text.gameObject.SetActive(true);

            string current = text.text ?? "";
            text.text = current.ToLowerInvariant().Contains("version")
                ? "Version 15.4.8"
                : versionText;

            EditorUtility.SetDirty(text);
            changed++;
        }

        foreach (Text text in Resources.FindObjectsOfTypeAll<Text>())
        {
            if (text == null || text.gameObject.scene != scene) continue;
            if (!IsVersionText(text.name, text.text, text.transform)) continue;

            Undo.RecordObject(text, "Verdant version label");
            text.gameObject.SetActive(true);

            string current = text.text ?? "";
            text.text = current.ToLowerInvariant().Contains("version")
                ? "Version 15.4.8"
                : versionText;

            EditorUtility.SetDirty(text);
            changed++;
        }

        RectTransform boardCanvasRect = FindMainBoardCanvasRect(mainBoardRoot);

        if (boardCanvasRect != null && !HasVersionLabel(boardCanvasRect))
        {
            GameObject go = new GameObject(
                "Verdant Version",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI)
            );

            Undo.RegisterCreatedObjectUndo(go, "Create Verdant Main Board version");
            go.transform.SetParent(boardCanvasRect, false);
            go.transform.SetAsLastSibling();

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-26f, 18f);
            rt.sizeDelta = new Vector2(360f, 54f);

            TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();

            TMP_Text source = boardCanvasRect.GetComponentsInChildren<TMP_Text>(true)
                .Where(t => t != null && t.font != null)
                .OrderByDescending(t => t.fontSize)
                .FirstOrDefault();

            if (source != null)
            {
                label.font = source.font;
                label.fontSize = Mathf.Clamp(source.fontSize * 0.55f, 18f, 30f);
            }
            else
            {
                label.font = TMP_Settings.defaultFontAsset;
                label.fontSize = 22f;
            }

            label.text = versionText;
            label.alignment = TextAlignmentOptions.BottomRight;
            label.raycastTarget = false;
            label.color = new Color(1f, 1f, 1f, 0.9f);
            label.overflowMode = TextOverflowModes.Overflow;

            EditorUtility.SetDirty(label);
            changed++;
        }

        return changed;
    }

    private static RectTransform FindMainBoardCanvasRect(Transform mainBoardRoot)
    {
        if (mainBoardRoot == null)
            return null;

        Canvas[] canvases = mainBoardRoot.GetComponentsInChildren<Canvas>(true);

        Canvas best = canvases
            .OrderByDescending(c => c != null && c.gameObject.activeInHierarchy)
            .ThenByDescending(c =>
            {
                RectTransform rt = c != null ? c.transform as RectTransform : null;
                return rt != null ? Mathf.Abs(rt.rect.width * rt.rect.height) : 0f;
            })
            .FirstOrDefault();

        if (best != null)
            return best.transform as RectTransform;

        RectTransform direct = mainBoardRoot as RectTransform;
        if (direct != null)
            return direct;

        return mainBoardRoot.GetComponentInChildren<RectTransform>(true);
    }

    private static bool IsVersionText(string objectName, string value, Transform transform)
    {
        string name = (objectName ?? "").ToLowerInvariant();
        string text = (value ?? "").Trim().ToLowerInvariant();
        string path = PathOf(transform).ToLowerInvariant();

        bool verdantSurface =
            path.Contains("tablet") || path.Contains("ipad") ||
            path.Contains("main board") || path.Contains("mainboard") ||
            path.Contains("verdant board") || path.Contains("verdant");

        if (!verdantSurface) return false;

        if (name.Contains("version") || name.Contains("build")) return true;
        if (text.Contains("version 14.") || text.Contains("version 15.")) return true;
        if (text.StartsWith("v14.") || text.StartsWith("v15.")) return true;
        if (text == "14.5" || text == "14.5.0" || text == "v14.5" || text == "v14.5.0") return true;

        return false;
    }

    private static bool HasVersionLabel(Transform root)
    {
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            if (text != null && IsVersionText(text.name, text.text, text.transform)) return true;

        foreach (Text text in root.GetComponentsInChildren<Text>(true))
            if (text != null && IsVersionText(text.name, text.text, text.transform)) return true;

        return false;
    }

    private static Transform FindTabletRoot(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n == "tabletsystem" || n.Contains("tablet system") || n == "ipad")
                    return t;
            }
        }

        return FindComponentTransformByTypeName(scene, "VerdantTablet");
    }

    private static Transform FindMainBoardRoot(Scene scene)
    {
        Transform byType = FindComponentTransformByTypeName(scene, "VerdantBoard");
        if (byType != null) return byType;

        RectTransform best = null;
        int bestScore = 0;

        foreach (RectTransform rt in Resources.FindObjectsOfTypeAll<RectTransform>())
        {
            if (rt == null || rt.gameObject.scene != scene) continue;

            string path = PathOf(rt).ToLowerInvariant();
            if (path.Contains("tablet") || path.Contains("ipad") || path.Contains("video player")) continue;

            int score = 0;
            string name = rt.name.ToLowerInvariant();

            if (name.Contains("main board") || name.Contains("mainboard")) score += 100;
            if (name.Contains("verdant board")) score += 80;
            if (path.Contains("main board") || path.Contains("mainboard")) score += 50;
            if (path.Contains("verdant board")) score += 40;

            TMP_Text[] texts = rt.GetComponentsInChildren<TMP_Text>(true);
            if (texts.Any(t => t != null && (t.text ?? "").ToUpperInvariant().Contains("VERDANT"))) score += 25;

            if (score > bestScore)
            {
                bestScore = score;
                best = rt;
            }
        }

        return best;
    }

    private static Transform FindComponentTransformByTypeName(Scene scene, string simpleName)
    {
        foreach (MonoBehaviour behaviour in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
        {
            if (behaviour == null || behaviour.gameObject.scene != scene) continue;
            Type type = behaviour.GetType();
            if (type != null && type.Name == simpleName)
                return behaviour.transform;
        }

        return null;
    }

    private static TMP_Dropdown[] FindTmpTimeDropdowns(Scene scene)
    {
        return Resources.FindObjectsOfTypeAll<TMP_Dropdown>()
            .Where(d => d != null && d.gameObject.scene == scene && DropdownHasTimeOptions(d.options.Select(o => o.text).ToArray(), d.transform))
            .Distinct().ToArray();
    }

    private static Dropdown[] FindLegacyTimeDropdowns(Scene scene)
    {
        return Resources.FindObjectsOfTypeAll<Dropdown>()
            .Where(d => d != null && d.gameObject.scene == scene && DropdownHasTimeOptions(d.options.Select(o => o.text).ToArray(), d.transform))
            .Distinct().ToArray();
    }

    private static int[] GetTmpDropdownValues(TMP_Dropdown[] dropdowns, bool want24)
    {
        int[] values = new int[dropdowns != null ? dropdowns.Length : 0];

        for (int i = 0; i < values.Length; i++)
        {
            values[i] = -1;
            TMP_Dropdown dropdown = dropdowns[i];
            if (dropdown == null || dropdown.options == null) continue;

            string token = want24 ? "24" : "12";

            for (int option = 0; option < dropdown.options.Count; option++)
            {
                string text = dropdown.options[option].text;
                if (!string.IsNullOrEmpty(text) && text.Contains(token))
                {
                    values[i] = option;
                    break;
                }
            }
        }

        return values;
    }

    private static int[] GetLegacyDropdownValues(Dropdown[] dropdowns, bool want24)
    {
        int[] values = new int[dropdowns != null ? dropdowns.Length : 0];

        for (int i = 0; i < values.Length; i++)
        {
            values[i] = -1;
            Dropdown dropdown = dropdowns[i];
            if (dropdown == null || dropdown.options == null) continue;

            string token = want24 ? "24" : "12";

            for (int option = 0; option < dropdown.options.Count; option++)
            {
                string text = dropdown.options[option].text;
                if (!string.IsNullOrEmpty(text) && text.Contains(token))
                {
                    values[i] = option;
                    break;
                }
            }
        }

        return values;
    }

    private static bool DropdownHasTimeOptions(string[] options, Transform transform)
    {
        if (options == null || options.Length == 0) return false;

        string path = PathOf(transform).ToLowerInvariant();
        bool settingsContext =
            path.Contains("settings") || path.Contains("time") ||
            path.Contains("clock") || path.Contains("tablet") || path.Contains("ipad");

        if (!settingsContext) return false;

        bool has12 = options.Any(o => !string.IsNullOrEmpty(o) && o.Contains("12"));
        bool has24 = options.Any(o => !string.IsNullOrEmpty(o) && o.Contains("24"));
        return has12 && has24;
    }

    private static int DetachLegacyClockWriters(Scene scene, TMP_Text[] tmpClocks, Text[] legacyClocks)
    {
        int changed = 0;
        UnityEngine.Object[] targets = tmpClocks.Cast<UnityEngine.Object>()
            .Concat(legacyClocks.Cast<UnityEngine.Object>())
            .ToArray();

        foreach (MonoBehaviour behaviour in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
        {
            if (behaviour == null || behaviour.gameObject.scene != scene) continue;
            if (behaviour is VerdantClockController1547) continue;

            SerializedObject so;
            try { so = new SerializedObject(behaviour); }
            catch { continue; }

            SerializedProperty iterator = so.GetIterator();
            bool modified = false;

            while (iterator.NextVisible(true))
            {
                if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;

                string propertyName = iterator.name.ToLowerInvariant();
                if (!propertyName.Contains("time") && !propertyName.Contains("clock")) continue;

                UnityEngine.Object reference = iterator.objectReferenceValue;
                if (reference == null) continue;

                if (targets.Contains(reference))
                {
                    Undo.RecordObject(behaviour, "Detach legacy Verdant clock writer");
                    iterator.objectReferenceValue = null;
                    modified = true;
                    changed++;
                }
            }

            if (modified)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(behaviour);
            }
        }

        return changed;
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
                      " - before V15.4.8 " + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
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
