using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

public static class VerdantV15RC2UIRepair
{
    private const string Root = "Assets/VerdantBoard";
    private const string Version = "15.0 RC2";

    [MenuItem("Tools/Verdant Board/V15 Release Candidate/Run RC2 UI + Media Fixes")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            EditorUtility.DisplayDialog("Verdant V15 RC2", "Leave Play Mode and wait for Unity/UdonSharp to finish compiling, then run this again.", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("Verdant V15 RC2", "No valid loaded scene was found.", "OK");
            return;
        }

        CreateBackup(scene);

        int ytGraphics = RepairYoutubeSearchVisuals(scene);
        int ytButtons = RepairYoutubeSearchButtons(scene);
        int scrolls = RepairSupporterTierScrolling(scene);
        int backgrounds = RepairTabletBackgroundCards(scene);
        int adBars = RepairCommunityAdBars(scene);
        int versions = UpdateVersionLabels(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!string.IsNullOrEmpty(scene.path))
            EditorSceneManager.SaveScene(scene);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string summary =
            "Verdant V15 RC2 UI/media repair complete.\n\n" +
            "YouTube graphics repaired: " + ytGraphics + "\n" +
            "YouTube buttons checked/repaired: " + ytButtons + "\n" +
            "Supporter ScrollRects repaired: " + scrolls + "\n" +
            "Tablet background cards synchronized: " + backgrounds + "\n" +
            "Community ad bars repositioned: " + adBars + "\n" +
            "Version labels updated: " + versions + "\n\n" +
            "Test YouTube Play/Queue, supporter scrolling, every tablet background card, and the sponsor board in ClientSim/Build & Test.";

        Debug.Log("[Verdant V15 RC2] " + summary.Replace("\n", " | "));
        EditorUtility.DisplayDialog("Verdant V15 RC2", summary, "OK");
    }

    private static int RepairYoutubeSearchVisuals(Scene scene)
    {
        int changed = 0;

        foreach (CanvasGroup cg in Resources.FindObjectsOfTypeAll<CanvasGroup>())
        {
            if (cg == null || cg.gameObject.scene != scene || !IsYoutubeContext(cg.transform)) continue;

            Undo.RecordObject(cg, "Verdant RC2 YouTube CanvasGroup");
            if (cg.alpha < 0.999f) { cg.alpha = 1f; changed++; }
            if (!cg.interactable) { cg.interactable = true; changed++; }
            if (!cg.blocksRaycasts) { cg.blocksRaycasts = true; changed++; }
            EditorUtility.SetDirty(cg);
        }

        foreach (RawImage raw in Resources.FindObjectsOfTypeAll<RawImage>())
        {
            if (raw == null || raw.gameObject.scene != scene || !IsYoutubeContext(raw.transform)) continue;
            string n = raw.name.ToLowerInvariant();
            string path = HierarchyPath(raw.transform).ToLowerInvariant();
            bool thumb = n.Contains("thumb") || n.Contains("preview") || n.Contains("result image") ||
                         path.Contains("thumbnail") || path.Contains("search result");
            if (!thumb) continue;

            Undo.RecordObject(raw, "Verdant RC2 YouTube RawImage");
            raw.color = Color.white;
            raw.material = null;
            raw.raycastTarget = false;
            EditorUtility.SetDirty(raw);
            changed++;
        }

        foreach (Image img in Resources.FindObjectsOfTypeAll<Image>())
        {
            if (img == null || img.gameObject.scene != scene || !IsYoutubeContext(img.transform)) continue;

            string n = img.name.ToLowerInvariant();
            string path = HierarchyPath(img.transform).ToLowerInvariant();
            Button ownButton = img.GetComponent<Button>();
            bool thumb = n.Contains("thumb") || n.Contains("preview") || n.Contains("result image") ||
                         path.Contains("thumbnail");
            bool overlay = n.Contains("overlay") || n.Contains("shade") || n.Contains("dim") || n.Contains("tint");

            if (thumb && img.sprite != null)
            {
                Undo.RecordObject(img, "Verdant RC2 YouTube Image");
                img.color = Color.white;
                img.material = null;
                if (ownButton == null) img.raycastTarget = false;
                EditorUtility.SetDirty(img);
                changed++;
            }
            else if (overlay && ownButton == null)
            {
                Undo.RecordObject(img, "Verdant RC2 YouTube overlay");
                Color c = img.color;
                c.a = Mathf.Min(c.a, 0.06f);
                img.color = c;
                img.raycastTarget = false;
                EditorUtility.SetDirty(img);
                changed++;
            }
        }

        foreach (TMP_Text text in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (text == null || text.gameObject.scene != scene || !IsYoutubeContext(text.transform)) continue;
            if (text.raycastTarget)
            {
                Undo.RecordObject(text, "Verdant RC2 YouTube text raycast");
                text.raycastTarget = false;
                EditorUtility.SetDirty(text);
                changed++;
            }
        }

        foreach (Button button in Resources.FindObjectsOfTypeAll<Button>())
        {
            if (button == null || button.gameObject.scene != scene || !IsYoutubeContext(button.transform)) continue;
            Undo.RecordObject(button, "Verdant RC2 YouTube button");
            button.interactable = true;
            Graphic target = button.targetGraphic;
            if (target != null) target.raycastTarget = true;
            EditorUtility.SetDirty(button);
            changed++;
        }

        return changed;
    }

    private static int RepairYoutubeSearchButtons(Scene scene)
    {
        UdonSharpBehaviour bridge = null;
        foreach (UdonSharpBehaviour u in Resources.FindObjectsOfTypeAll<UdonSharpBehaviour>())
        {
            if (u == null || u.gameObject.scene != scene) continue;
            if (u.GetType().Name == "VerdantYoutubeBridge")
            {
                bridge = u;
                break;
            }
        }

        if (bridge == null) return 0;

        UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(bridge);
        if (backing == null) return 0;

        int repaired = 0;
        Button[] buttons = Resources.FindObjectsOfTypeAll<Button>()
            .Where(b => b != null && b.gameObject.scene == scene && IsYoutubeContext(b.transform))
            .OrderBy(b => HierarchyPath(b.transform))
            .ToArray();

        foreach (Button button in buttons)
        {
            string lower = (button.name + " " + HierarchyPath(button.transform)).ToLowerInvariant();
            bool play = lower.Contains("play");
            bool queue = lower.Contains("queue") || lower.Contains("add");
            if (!play && !queue) continue;

            SerializedObject so = new SerializedObject(button);
            SerializedProperty calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            int count = calls != null && calls.isArray ? calls.arraySize : 0;
            if (count > 0) continue;

            int index = ResultIndexFromHierarchy(button.transform);
            string method = FindBridgeMethod(bridge.GetType(), play ? "play" : "queue", index);
            if (string.IsNullOrEmpty(method)) continue;

            Undo.RecordObject(button, "Verdant RC2 rewire YouTube button");
            UnityEventTools.AddStringPersistentListener(button.onClick, backing.SendCustomEvent, method);
            EditorUtility.SetDirty(button);
            repaired++;
        }

        return repaired;
    }

    private static string FindBridgeMethod(Type type, string action, int index)
    {
        MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        string idx = index >= 0 ? index.ToString() : "";

        string[] preferred = action == "play"
            ? new[] { "PlayResult" + idx, "PlaySearchResult" + idx, "SearchResult" + idx + "Play", "Play" + idx, "PlaySelected", "PlaySelection" }
            : new[] { "QueueResult" + idx, "QueueSearchResult" + idx, "SearchResult" + idx + "Queue", "AddResult" + idx, "AddToQueue" + idx, "QueueSelected", "AddSelectedToQueue" };

        foreach (string candidate in preferred)
        {
            MethodInfo m = methods.FirstOrDefault(x => x.Name == candidate && x.GetParameters().Length == 0);
            if (m != null) return m.Name;
        }

        foreach (MethodInfo m in methods)
        {
            if (m.GetParameters().Length != 0) continue;
            string n = m.Name.ToLowerInvariant();
            if (!n.Contains(action)) continue;
            if (index >= 0 && n.Contains(index.ToString())) return m.Name;
            if (index < 0 && (n.Contains("selected") || n.Contains("selection"))) return m.Name;
        }

        return null;
    }

    private static int ResultIndexFromHierarchy(Transform t)
    {
        Transform p = t;
        while (p != null)
        {
            string n = p.name.ToLowerInvariant();
            if (n.Contains("result") || n.Contains("card") || n.Contains("item"))
            {
                int parsed;
                if (TryParseTrailingNumber(n, out parsed)) return parsed;
                return p.GetSiblingIndex();
            }
            p = p.parent;
        }
        return -1;
    }

    private static int RepairSupporterTierScrolling(Scene scene)
    {
        int count = 0;

        foreach (ScrollRect sr in Resources.FindObjectsOfTypeAll<ScrollRect>())
        {
            if (sr == null || sr.gameObject.scene != scene) continue;
            string path = HierarchyPath(sr.transform).ToLowerInvariant();
            if (!path.Contains("support")) continue;
            if (!(path.Contains("tier") || path.Contains("rank") || path.Contains("member"))) continue;

            Undo.RecordObject(sr, "Verdant RC2 supporter scroll");
            sr.horizontal = false;
            sr.vertical = true;
            sr.inertia = false;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = Mathf.Max(24f, sr.scrollSensitivity);
            sr.elasticity = 0f;

            if (sr.viewport != null)
            {
                Mask mask = sr.viewport.GetComponent<Mask>();
                if (mask != null) mask.showMaskGraphic = false;
            }

            RectTransform content = sr.content;
            if (content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);

                float preferred = LayoutUtility.GetPreferredHeight(content);
                float viewportHeight = sr.viewport != null ? sr.viewport.rect.height : 0f;
                float bakedHeight = Mathf.Max(preferred, viewportHeight + 10f, content.rect.height);

                Vector2 size = content.sizeDelta;
                size.y = bakedHeight;
                content.sizeDelta = size;
                content.pivot = new Vector2(content.pivot.x, 1f);

                ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
                if (fitter != null && fitter.enabled)
                {
                    Undo.RecordObject(fitter, "Verdant RC2 bake supporter content");
                    fitter.enabled = false;
                    EditorUtility.SetDirty(fitter);
                }

                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            }

            if (sr.verticalScrollbar != null)
            {
                Undo.RecordObject(sr.verticalScrollbar, "Verdant RC2 supporter scrollbar");
                sr.verticalScrollbar.direction = Scrollbar.Direction.BottomToTop;
                sr.verticalScrollbar.interactable = true;
                EditorUtility.SetDirty(sr.verticalScrollbar);
            }

            EditorUtility.SetDirty(sr);
            count++;
        }

        return count;
    }

    private static int RepairTabletBackgroundCards(Scene scene)
    {
        Component source = FindBackgroundSource(scene);
        if (source == null) return 0;

        UnityEngine.Object[] assets;
        string[] names;
        if (!TryGetBackgroundAssets(source, out assets, out names) || assets == null || assets.Length == 0)
            return 0;

        List<Button> cards = Resources.FindObjectsOfTypeAll<Button>()
            .Where(b =>
                b != null &&
                b.gameObject.scene == scene &&
                HierarchyPath(b.transform).ToLowerInvariant().Contains("tablet") &&
                HierarchyPath(b.transform).ToLowerInvariant().Contains("background") &&
                (b.GetComponentInChildren<RawImage>(true) != null || b.GetComponentInChildren<Image>(true) != null))
            .OrderBy(b => HierarchyPath(b.transform))
            .ToList();

        int changed = 0;
        int fallback = 0;

        foreach (Button card in cards)
        {
            int index = GetBackgroundIndex(card);
            if (index < 0 || index >= assets.Length) index = fallback;
            fallback++;
            if (index < 0 || index >= assets.Length) continue;

            UnityEngine.Object asset = assets[index];
            if (asset == null) continue;

            RawImage raw = card.GetComponentsInChildren<RawImage>(true).FirstOrDefault();
            Image image = card.GetComponentsInChildren<Image>(true)
                .FirstOrDefault(i => i != null && i.gameObject != card.gameObject && i.sprite != null);

            if (asset is Texture)
            {
                Texture tex = asset as Texture;
                if (raw != null)
                {
                    Undo.RecordObject(raw, "Verdant RC2 background preview");
                    raw.texture = tex;
                    raw.color = Color.white;
                    raw.material = null;
                    EditorUtility.SetDirty(raw);
                    changed++;
                }
            }
            else if (asset is Sprite)
            {
                Sprite sprite = asset as Sprite;
                if (image != null)
                {
                    Undo.RecordObject(image, "Verdant RC2 background preview");
                    image.sprite = sprite;
                    image.color = Color.white;
                    image.material = null;
                    EditorUtility.SetDirty(image);
                    changed++;
                }
                else if (raw != null)
                {
                    Undo.RecordObject(raw, "Verdant RC2 background preview");
                    raw.texture = sprite.texture;
                    raw.color = Color.white;
                    raw.material = null;
                    EditorUtility.SetDirty(raw);
                    changed++;
                }
            }

            TMP_Text label = card.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(t => t != null && !string.IsNullOrWhiteSpace(t.text) &&
                                     !t.name.ToLowerInvariant().Contains("tier") &&
                                     !t.name.ToLowerInvariant().Contains("lock"));

            if (label != null)
            {
                string labelText = names != null && index < names.Length && !string.IsNullOrWhiteSpace(names[index])
                    ? names[index]
                    : CleanAssetName(asset.name);

                Undo.RecordObject(label, "Verdant RC2 background label");
                label.text = labelText;
                EditorUtility.SetDirty(label);
                changed++;
            }
        }

        return changed;
    }

    private static Component FindBackgroundSource(Scene scene)
    {
        Component best = null;
        int bestScore = -1;

        foreach (Component c in Resources.FindObjectsOfTypeAll<Component>())
        {
            if (c == null || c.gameObject.scene != scene) continue;
            string type = c.GetType().Name.ToLowerInvariant();
            if (!(type.Contains("background") || type == "verdantboard" || type.Contains("theme"))) continue;

            int score = 0;
            foreach (FieldInfo f in c.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                string n = f.Name.ToLowerInvariant();
                if (!n.Contains("background")) continue;
                if (f.FieldType.IsArray) score += 2;
                if (n.Contains("local")) score += 2;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = c;
            }
        }

        return best;
    }

    private static bool TryGetBackgroundAssets(Component source, out UnityEngine.Object[] assets, out string[] names)
    {
        assets = null;
        names = null;
        int bestLen = 0;

        FieldInfo[] fields = source.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        foreach (FieldInfo f in fields)
        {
            if (!f.FieldType.IsArray || !f.Name.ToLowerInvariant().Contains("background")) continue;
            Array arr = f.GetValue(source) as Array;
            if (arr == null || arr.Length == 0) continue;

            Type element = f.FieldType.GetElementType();
            if (element == null) continue;
            bool supported = typeof(Texture).IsAssignableFrom(element) || typeof(Sprite).IsAssignableFrom(element);
            if (!supported || arr.Length <= bestLen) continue;

            List<UnityEngine.Object> list = new List<UnityEngine.Object>();
            foreach (object o in arr) list.Add(o as UnityEngine.Object);
            assets = list.ToArray();
            bestLen = arr.Length;
        }

        if (assets == null) return false;

        foreach (FieldInfo f in fields)
        {
            if (f.FieldType != typeof(string[])) continue;
            string n = f.Name.ToLowerInvariant();
            if (!n.Contains("background")) continue;
            string[] candidate = f.GetValue(source) as string[];
            if (candidate == null || candidate.Length != assets.Length) continue;
            if (n.Contains("name") || n.Contains("title") || n.Contains("label"))
            {
                names = candidate;
                break;
            }
        }

        return true;
    }

    private static int GetBackgroundIndex(Button button)
    {
        try
        {
            SerializedObject so = new SerializedObject(button);
            SerializedProperty calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            if (calls == null || !calls.isArray) return -1;

            for (int i = 0; i < calls.arraySize; i++)
            {
                SerializedProperty call = calls.GetArrayElementAtIndex(i);
                string method = call.FindPropertyRelative("m_MethodName").stringValue ?? "";
                SerializedProperty args = call.FindPropertyRelative("m_Arguments");
                string stringArg = args.FindPropertyRelative("m_StringArgument").stringValue ?? "";
                int intArg = args.FindPropertyRelative("m_IntArgument").intValue;

                string m = method.ToLowerInvariant();
                if (m == "sendcustomevent")
                {
                    int parsed;
                    if (TryParseTrailingNumber(stringArg, out parsed)) return parsed;
                }
                else if (m.Contains("background") || m.Contains("select"))
                {
                    return intArg;
                }
            }
        }
        catch { }

        return -1;
    }

    private static int RepairCommunityAdBars(Scene scene)
    {
        TMP_Text[] all = Resources.FindObjectsOfTypeAll<TMP_Text>();
        TMP_Text topText = all.FirstOrDefault(t => t != null && t.gameObject.scene == scene &&
            t.text != null && t.text.ToUpperInvariant().Contains("SPONSORED") &&
            HierarchyPath(t.transform).ToLowerInvariant().Contains("ad"));
        TMP_Text bottomText = all.FirstOrDefault(t => t != null && t.gameObject.scene == scene &&
            t.text != null && t.text.ToLowerInvariant().Contains("become a supporter"));

        int count = 0;
        count += MoveAdBarToEdge(topText, true);
        count += MoveAdBarToEdge(bottomText, false);
        return count;
    }

    private static int MoveAdBarToEdge(TMP_Text text, bool top)
    {
        if (text == null) return 0;

        RectTransform bar = text.transform.parent as RectTransform;
        if (bar == null) bar = text.rectTransform;
        RectTransform parent = bar.parent as RectTransform;
        if (parent == null) return 0;

        Undo.RecordObject(bar, "Verdant RC2 move sponsor bar");

        float halfParent = parent.rect.height * 0.5f;
        float halfBar = Mathf.Max(8f, bar.rect.height * 0.5f);
        float padding = Mathf.Max(5f, parent.rect.height * 0.012f);
        float targetY = top
            ? halfParent - halfBar - padding
            : -halfParent + halfBar + padding;

        Vector3 local = bar.localPosition;
        local.y = targetY;
        bar.localPosition = local;
        EditorUtility.SetDirty(bar);
        return 1;
    }

    private static int UpdateVersionLabels(Scene scene)
    {
        int changed = 0;
        foreach (TMP_Text t in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (t == null || t.gameObject.scene != scene || string.IsNullOrEmpty(t.text)) continue;
            if (!t.text.Contains("V14.") && !t.text.Contains("V15.")) continue;

            Undo.RecordObject(t, "Verdant RC2 version");
            t.text = "V" + Version;
            EditorUtility.SetDirty(t);
            changed++;
        }
        return changed;
    }

    private static bool IsYoutubeContext(Transform t)
    {
        string p = HierarchyPath(t).ToLowerInvariant();
        return p.Contains("youtube") || p.Contains("you tube") ||
               p.Contains("search result") || p.Contains("video search");
    }

    private static string HierarchyPath(Transform t)
    {
        if (t == null) return "";
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    private static bool TryParseTrailingNumber(string text, out int value)
    {
        value = -1;
        if (string.IsNullOrEmpty(text)) return false;

        int end = text.Length - 1;
        while (end >= 0 && !char.IsDigit(text[end])) end--;
        if (end < 0) return false;

        int start = end;
        while (start > 0 && char.IsDigit(text[start - 1])) start--;

        return int.TryParse(text.Substring(start, end - start + 1), out value);
    }

    private static string CleanAssetName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Background";
        string s = name.Replace("_", " ").Replace("-", " ");
        while (s.Contains("  ")) s = s.Replace("  ", " ");
        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.Trim().ToLowerInvariant());
    }

    private static void CreateBackup(Scene scene)
    {
        if (string.IsNullOrEmpty(scene.path)) return;

        string folder = Root + "/Generated/V15 Backups";
        EnsureFolder(folder);
        string file = System.IO.Path.GetFileNameWithoutExtension(scene.path) +
                      " - before V15 RC2 " + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
        string backup = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + file);
        EditorSceneManager.SaveScene(scene, backup, true);
        Debug.Log("[Verdant V15 RC2] Backup created: " + backup);
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
