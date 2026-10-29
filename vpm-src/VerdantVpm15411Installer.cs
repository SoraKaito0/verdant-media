using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public static class VerdantVpm15411Installer
{
    private const string Version = "15.4.11";
    private const string SharedRootName = "Verdant VPM Shared UI State";

    [MenuItem("Tools/Verdant Board/V15.4.11/Apply Supporters + Clock + Version Fix", priority = 1)]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            EditorUtility.DisplayDialog("Verdant V15.4.11", "Leave Play Mode and wait for compilation to finish.", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("Verdant V15.4.11", "No loaded scene was found.", "OK");
            return;
        }

        Transform preflightBoard = FindMainBoardRoot(scene);
        RectTransform preflightCanvas = FindMainBoardCanvasRect(preflightBoard);
        TMP_Text preflightLabel;
        if (preflightCanvas == null || FindPlayersCard(preflightCanvas, out preflightLabel) == null || FindTabletRoot(scene) == null)
        {
            EditorUtility.DisplayDialog("Verdant 15.4.11", "Main Board PLAYERS card or iPad not found. No changes applied.", "OK");
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

        int distanceFlags = DisableDistanceHiding(mainBoardRoot) + DisableDistanceHiding(tabletRoot);
        int sourceFixes = PatchDistanceSourceScripts();
        Debug.Log("Verdant distance source fixes applied: " + sourceFixes);
        Debug.Log("Verdant 15.4.11 distance-hide flags disabled: " + distanceFlags +
            ". Verify both surfaces in Play Mode; custom/Udon-only fields require inspection in the world project.");

        int versions = UpdateAllVersionLabels(scene, mainBoardRoot, tabletRoot);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        if (sourceFixes > 0) AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Verdant V15.4.11",
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
        const string numericVersion = "15.4.11";
        int changed = 0;

        // iPad/tablet: update the existing visible version label in place.
        changed += UpdateVersionLabelsUnder(tabletRoot, numericVersion, false);

        // Main Board: remember the old floating label position, then replace it
        // with a compact Verdant status card matching nearby HUD cards.
        RectTransform boardCanvas = FindMainBoardCanvasRect(mainBoardRoot);
        RectTransform oldVersionRect = FindVersionRectUnder(mainBoardRoot);

        if (boardCanvas != null)
        {
            changed += CreateOrRestyleVersionCard(boardCanvas, oldVersionRect, numericVersion);

            foreach (TMP_Text text in boardCanvas.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text == null) continue;
                if (text.transform.parent != null && text.transform.parent.name == "Verdant Version Card") continue;
                if (!IsVersionText(text.name, text.text, text.transform)) continue;

                Undo.RecordObject(text.gameObject, "Hide old Verdant version text");
                text.gameObject.SetActive(false);
                changed++;
            }

            foreach (Text text in boardCanvas.GetComponentsInChildren<Text>(true))
            {
                if (text == null) continue;
                if (text.transform.parent != null && text.transform.parent.name == "Verdant Version Card") continue;
                if (!IsVersionText(text.name, text.text, text.transform)) continue;

                Undo.RecordObject(text.gameObject, "Hide old Verdant version text");
                text.gameObject.SetActive(false);
                changed++;
            }
        }

        // Fallback: if the tablet's old V14.5 label could not be found, add a
        // subtle version label to its existing StatusBar rather than a new panel.
        if (tabletRoot != null && CountVersionLabelsUnder(tabletRoot) == 0)
        {
            RectTransform status = FindRectByNames(tabletRoot, "statusbar", "status bar", "header");
            if (status != null)
            {
                TMP_Text sample = status.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault();
                GameObject go = new GameObject("Verdant Tablet Version", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                Undo.RegisterCreatedObjectUndo(go, "Create Verdant tablet version");
                go.transform.SetParent(status, false);

                RectTransform rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.72f, 0f);
                rt.anchorMax = new Vector2(0.98f, 1f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
                if (sample != null)
                {
                    label.font = sample.font;
                    label.fontSize = sample.fontSize;
                    label.color = sample.color;
                }
                else
                {
                    label.font = TMP_Settings.defaultFontAsset;
                    label.fontSize = 14f;
                    label.color = Color.white;
                }

                label.text = "Version " + numericVersion;
                label.alignment = TextAlignmentOptions.MidlineRight;
                label.raycastTarget = false;
                changed++;
            }
        }

        return changed;
    }

    private static int UpdateVersionLabelsUnder(Transform root, string numericVersion, bool hide)
    {
        if (root == null) return 0;

        int changed = 0;

        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text == null || !IsVersionText(text.name, text.text, text.transform)) continue;

            Undo.RecordObject(text, "Update Verdant version");
            Undo.RecordObject(text.gameObject, "Update Verdant version");

            string current = text.text ?? "";
            text.text = current.ToLowerInvariant().Contains("version")
                ? "Version " + numericVersion
                : "V" + numericVersion;

            text.gameObject.SetActive(!hide);
            EditorUtility.SetDirty(text);
            changed++;
        }

        foreach (Text text in root.GetComponentsInChildren<Text>(true))
        {
            if (text == null || !IsVersionText(text.name, text.text, text.transform)) continue;

            Undo.RecordObject(text, "Update Verdant version");
            Undo.RecordObject(text.gameObject, "Update Verdant version");

            string current = text.text ?? "";
            text.text = current.ToLowerInvariant().Contains("version")
                ? "Version " + numericVersion
                : "V" + numericVersion;

            text.gameObject.SetActive(!hide);
            EditorUtility.SetDirty(text);
            changed++;
        }

        return changed;
    }

    private static int CountVersionLabelsUnder(Transform root)
    {
        if (root == null) return 0;

        int count = 0;

        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            if (text != null && IsVersionText(text.name, text.text, text.transform))
                count++;

        foreach (Text text in root.GetComponentsInChildren<Text>(true))
            if (text != null && IsVersionText(text.name, text.text, text.transform))
                count++;

        return count;
    }

    private static RectTransform FindVersionRectUnder(Transform root)
    {
        if (root == null) return null;

        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            if (text != null && IsVersionText(text.name, text.text, text.transform))
                return text.rectTransform;

        foreach (Text text in root.GetComponentsInChildren<Text>(true))
            if (text != null && IsVersionText(text.name, text.text, text.transform))
                return text.rectTransform;

        return null;
    }

    private static int CreateOrRestyleVersionCard(RectTransform boardCanvas, RectTransform oldVersionRect, string numericVersion)
    {
        Transform existing = boardCanvas.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(t => t != null && t.name == "Verdant Version Card");

        GameObject cardObject;

        if (existing != null)
        {
            cardObject = existing.gameObject;
            Undo.RecordObject(cardObject, "Restyle Verdant version card");
            cardObject.SetActive(true);
        }
        else
        {
            cardObject = new GameObject(
                "Verdant Version Card",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

            Undo.RegisterCreatedObjectUndo(cardObject, "Create Verdant version card");
            cardObject.transform.SetParent(boardCanvas, false);
        }

        RectTransform card = cardObject.GetComponent<RectTransform>();
        Image background = cardObject.GetComponent<Image>();

        TMP_Text playersLabel;
        Image playersCard = FindPlayersCard(boardCanvas, out playersLabel);

        if (playersCard != null)
        {
            background.sprite = playersCard.sprite;
            background.type = playersCard.type;
            background.color = playersCard.color;
            background.material = playersCard.material;
            background.preserveAspect = playersCard.preserveAspect;
        }
        else
        {
            background.color = new Color(0.02f, 0.055f, 0.035f, 0.88f);
        }

        Vector2 desiredSize = new Vector2(230f, 78f);

        if (playersCard != null)
        {
            RectTransform reference = playersCard.rectTransform;
            float h = Mathf.Max(54f, reference.rect.height);
            float w = Mathf.Max(180f, reference.rect.width * 0.72f);
            desiredSize = new Vector2(w, h);
        }

        // Share PLAYERS' coordinate space, including nested/scaled headers.
        // Do not use the old floating version label's anchors or position.
        if (playersCard == null)
            throw new InvalidOperationException("PLAYERS card was not found. Undo the migration and inspect the Main Board hierarchy.");

        RectTransform playersRect = playersCard.rectTransform;
        Undo.SetTransformParent(card, playersRect.parent, "Move version beside PLAYERS");
        Undo.RecordObject(card, "Position version beside PLAYERS");
        Canvas.ForceUpdateCanvases();
        // Align to the PLAYERS panel in the same parent transform space.
        // Avoid copying its anchors: that previously placed VERSION in the old status area.
        RectTransform parentRect = playersRect.parent as RectTransform;
        if (parentRect == null) throw new InvalidOperationException("PLAYERS does not have a RectTransform parent.");
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot = new Vector2(0.5f, 0.5f);
        card.localScale = playersRect.localScale;
        card.localRotation = Quaternion.identity;
        card.sizeDelta = desiredSize;
        Canvas.ForceUpdateCanvases();
        Bounds playersBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parentRect, playersRect);
        float offset = 12f + desiredSize.x * 0.5f * card.localScale.x;
        card.localPosition = new Vector3(playersBounds.max.x + offset, playersBounds.center.y, playersRect.localPosition.z);
        // Warn if the header lacks enough space, rather than silently moving the card back.
        Bounds cardBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(parentRect, card);
        Bounds panelBounds = new Bounds(Vector3.zero, new Vector3(parentRect.rect.width, parentRect.rect.height, 0f));
        if (cardBounds.max.x > parentRect.rect.xMax)
            Debug.LogWarning("Verdant VERSION may extend beyond PLAYERS header. Check top-header space.", card);
        desiredSize = card.sizeDelta;

        // Copy all Image settings (including sliced corners and pixels-per-unit),
        // plus graphic effects. Never clone the button's scripts or click events.
        EditorUtility.CopySerialized(playersCard, background);
        foreach (BaseMeshEffect effect in cardObject.GetComponents<BaseMeshEffect>())
            Undo.DestroyObjectImmediate(effect);
        foreach (BaseMeshEffect effect in playersCard.GetComponents<BaseMeshEffect>())
        {
            BaseMeshEffect copy = (BaseMeshEffect)Undo.AddComponent(cardObject, effect.GetType());
            EditorUtility.CopySerialized(effect, copy);
        }
        LayoutElement layout = cardObject.GetComponent<LayoutElement>();
        if (layout == null) layout = Undo.AddComponent<LayoutElement>(cardObject);
        layout.ignoreLayout = true;

        card.sizeDelta = desiredSize;
        card.SetAsLastSibling();

        // Rebuild only the contents of our own version card.
        for (int i = card.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(card.GetChild(i).gameObject);

        Color textColor = playersLabel != null ? playersLabel.color : Color.white;
        TMP_FontAsset font = playersLabel != null && playersLabel.font != null
            ? playersLabel.font
            : TMP_Settings.defaultFontAsset;

        float baseSize = playersLabel != null ? playersLabel.fontSize : 22f;
        TextMeshProUGUI title = CreateCardText(
            card,
            "Version Title",
            "VERSION",
            font,
            Mathf.Max(12f, baseSize * 0.72f),
            textColor,
            new Vector2(0.12f, 0.49f),
            new Vector2(0.94f, 0.90f),
            TextAlignmentOptions.BottomLeft,
            FontStyles.Bold
        );

        TextMeshProUGUI number = CreateCardText(
            card,
            "Version Number",
            numericVersion,
            font,
            Mathf.Max(15f, baseSize * 0.95f),
            textColor,
            new Vector2(0.12f, 0.08f),
            new Vector2(0.94f, 0.52f),
            TextAlignmentOptions.TopLeft,
            FontStyles.Bold
        );

        if (playersLabel != null)
        {
            title.fontSharedMaterial = playersLabel.fontSharedMaterial;
            number.fontSharedMaterial = playersLabel.fontSharedMaterial;
            title.fontStyle = playersLabel.fontStyle;
            number.fontStyle = playersLabel.fontStyle;
        }
        title.raycastTarget = false;
        number.raycastTarget = false;
        background.raycastTarget = false;

        EditorUtility.SetDirty(background);
        EditorUtility.SetDirty(title);
        EditorUtility.SetDirty(number);

        return 1;
    }

    private static Image FindPlayersCard(RectTransform boardCanvas, out TMP_Text playersLabel)
    {
        playersLabel = boardCanvas.GetComponentsInChildren<TMP_Text>(true)
            .FirstOrDefault(t =>
                t != null &&
                string.Equals((t.text ?? "").Trim(), "PLAYERS", StringComparison.OrdinalIgnoreCase));

        if (playersLabel == null)
            return null;

        Button button = playersLabel.GetComponentInParent<Button>();
        if (button != null && button.transform.IsChildOf(boardCanvas) && button.targetGraphic is Image)
            return (Image)button.targetGraphic;

        Transform current = playersLabel.transform.parent;

        while (current != null && current != boardCanvas)
        {
            Image image = current.GetComponent<Image>();

            if (image != null)
                return image;

            current = current.parent;
        }

        return null;
    }

    private static Color FindCardAccent(Image playersCard)
    {
        if (playersCard != null)
        {
            Image[] images = playersCard.GetComponentsInChildren<Image>(true);

            foreach (Image image in images)
            {
                if (image == null || image == playersCard) continue;

                Color c = image.color;

                if (c.g > c.r * 1.15f && c.g > c.b * 1.10f)
                    return c;
            }
        }

        return new Color(0.55f, 0.86f, 0.43f, 1f);
    }

    private static TextMeshProUGUI CreateCardText(
        RectTransform parent,
        string name,
        string value,
        TMP_FontAsset font,
        float fontSize,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        TextAlignmentOptions alignment,
        FontStyles style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        Undo.RegisterCreatedObjectUndo(go, "Create Verdant version text");

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = style;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;

        return text;
    }

    private static RectTransform FindRectByNames(Transform root, params string[] names)
    {
        if (root == null) return null;

        foreach (RectTransform rt in root.GetComponentsInChildren<RectTransform>(true))
        {
            if (rt == null) continue;

            string lower = rt.name.ToLowerInvariant();

            for (int i = 0; i < names.Length; i++)
            {
                if (lower == names[i] || lower.Contains(names[i]))
                    return rt;
            }
        }

        return null;
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

    private static int PatchDistanceSourceScripts()
    {
        // These exact source patterns are from VerdantBoard.zip. Do not create duplicate scripts.
        int changed = 0;
        string boardPath = "Assets/VerdantBoard/Scripts/VerdantBoard.cs";
        string tabletPath = "Assets/VerdantBoard/Scripts/VerdantTablet.cs";
        changed += PatchSourceFile(boardPath,
            "public bool autoMinimise = true;",
            "public bool autoMinimise = false;");
        changed += PatchSourceFile(boardPath,
            "if (Time.unscaledTime >= nextDistanceCheck) { nextDistanceCheck = Time.unscaledTime + 0.25f; CheckDistance(); }",
            "// Distance-based minimisation removed. Manual controls remain available.");
        changed += PatchSourceFile(tabletPath,
            "if(board!=null)board.Initialize();ShowPage(0);Refresh();",
            "if(board!=null)board.Initialize();if(screen!=null)screen.SetActive(true);ShowPage(0);Refresh();");
        const string oldTablet = "bool awake=pickup!=null&&pickup.IsHeld;\n" +
            "        if(!awake&&Utilities.IsValid(Networking.LocalPlayer))awake=Vector3.Distance(Networking.LocalPlayer.GetPosition(),transform.position)<=Mathf.Max(1,screenWakeDistance);\n" +
            "        if(screen!=null&&screen.activeSelf!=awake)screen.SetActive(awake);\n" +
            "        if(awake)Refresh();";
        const string newTablet = "// Distance-based tablet hiding removed.\n" +
            "        if(screen!=null&&!screen.activeSelf)screen.SetActive(true);\n" +
            "        Refresh();";
        changed += PatchSourceFile(tabletPath, oldTablet, newTablet);
        return changed;
    }

    private static int PatchSourceFile(string path, string from, string to)
    {
        if (!System.IO.File.Exists(path))
        {
            Debug.LogWarning("Verdant 15.4.11 missing source: " + path);
            return 0;
        }
        string original = System.IO.File.ReadAllText(path);
        string normalized = original.Replace("\r\n", "\n");
        if (!normalized.Contains(from))
        {
            if (!normalized.Contains(to))
                Debug.LogWarning("Verdant source pattern not found in " + path + ". Inspect distance-hiding source manually.");
            return 0;
        }
        System.IO.File.WriteAllText(path, normalized.Replace(from, to));
        Debug.Log("Verdant patched distance-hiding source: " + path);
        return 1;
    }

    private static int DisableDistanceHiding(Transform root)
    {
        if (root == null) return 0;
        int changed = 0;
        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null) continue;
            SerializedObject serialized = new SerializedObject(behaviour);
            SerializedProperty property = serialized.GetIterator();
            bool modified = false;
            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.Boolean) continue;
                string key = property.name.Replace("_", "").ToLowerInvariant();
                if (key != "autohidebydistance" && key != "hidebydistance" &&
                    key != "enabledistancehiding" && key != "usedistancehiding" &&
                    key != "distanceculling" && key != "enabledistanceculling" &&
                    key != "autohidewhendistant") continue;
                Undo.RecordObject(behaviour, "Disable Verdant distance hiding");
                property.boolValue = false;
                modified = true;
                changed++;
                Debug.Log("Verdant disabled " + PathOf(behaviour.transform) + "." + property.propertyPath, behaviour);
            }
            if (modified)
            {
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(behaviour);
            }
        }
        if (changed == 0)
            Debug.LogWarning("No supported distance-hiding flag found under " + PathOf(root) +
                ". Distance visibility is NOT validated; inspect the controller/Udon program in Unity.", root);
        return changed;
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
                      " - before V15.4.11 " + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
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
