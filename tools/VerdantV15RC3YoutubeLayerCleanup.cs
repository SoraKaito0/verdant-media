using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public static class VerdantV15RC3YoutubeLayerCleanup
{
    private const string Root = "Assets/VerdantBoard";
    private const string Version = "15.0 RC3";

    [MenuItem("Tools/Verdant Board/V15 Release Candidate/Run RC3 YouTube Layer Cleanup")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            EditorUtility.DisplayDialog("Verdant V15 RC3", "Leave Play Mode and wait for Unity/UdonSharp to finish compiling, then run this again.", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("Verdant V15 RC3", "No valid loaded scene was found.", "OK");
            return;
        }

        CreateBackup(scene);

        Transform youtubeRoot = FindYoutubeRoot(scene);
        if (youtubeRoot == null)
        {
            EditorUtility.DisplayDialog(
                "Verdant V15 RC3",
                "Could not find the YouTube page/search root. No scene changes were made.",
                "OK"
            );
            return;
        }

        int removedLayers = RemoveUnnecessaryYoutubeLayers(youtubeRoot);
        int raycasts = ClearBlockingRaycasts(youtubeRoot);
        int thumbnails = RestoreThumbnailVisibility(youtubeRoot);
        int themeRefs = RemoveThumbnailThemeReferences(scene, youtubeRoot);
        int groups = RepairCanvasGroups(youtubeRoot);
        int versions = UpdateVersionLabels(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!string.IsNullOrEmpty(scene.path))
            EditorSceneManager.SaveScene(scene);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string summary =
            "Verdant V15 RC3 YouTube cleanup complete.\n\n" +
            "Unnecessary large visual layers disabled: " + removedLayers + "\n" +
            "Blocking raycasts cleared: " + raycasts + "\n" +
            "Thumbnail graphics restored: " + thumbnails + "\n" +
            "Thumbnail theme references removed: " + themeRefs + "\n" +
            "CanvasGroups repaired: " + groups + "\n" +
            "Version labels updated: " + versions + "\n\n" +
            "No new UI objects or background layers were created.\n" +
            "This pass only removes/neutralises unnecessary YouTube graphics and interaction blockers.";

        Debug.Log("[Verdant V15 RC3] " + summary.Replace("\n", " | "));
        EditorUtility.DisplayDialog("Verdant V15 RC3", summary, "OK");
    }

    private static Transform FindYoutubeRoot(Scene scene)
    {
        List<Transform> candidates = new List<Transform>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                string p = HierarchyPath(t).ToLowerInvariant();

                if (n.Contains("youtube") ||
                    n.Contains("you tube") ||
                    p.Contains("youtube search") ||
                    p.Contains("video search"))
                {
                    candidates.Add(t);
                }
            }
        }

        if (candidates.Count == 0) return null;

        // Prefer a page/panel that owns the whole YouTube UI rather than an individual
        // result card or thumbnail.
        return candidates
            .OrderByDescending(t => RootScore(t))
            .ThenByDescending(t => t.GetComponentsInChildren<Transform>(true).Length)
            .FirstOrDefault();
    }

    private static int RootScore(Transform t)
    {
        string n = t.name.ToLowerInvariant();
        int score = 0;
        if (n.Contains("page")) score += 100;
        if (n.Contains("panel")) score += 80;
        if (n.Contains("youtube")) score += 60;
        if (n.Contains("search")) score += 40;
        if (n.Contains("result")) score -= 80;
        if (n.Contains("thumb")) score -= 100;
        if (t.GetComponent<Button>() != null) score -= 100;
        return score;
    }

    private static int RemoveUnnecessaryYoutubeLayers(Transform root)
    {
        int changed = 0;
        RectTransform rootRect = root as RectTransform;

        foreach (Image img in root.GetComponentsInChildren<Image>(true))
        {
            if (img == null || !img.enabled || !IsRemovableLayer(img, rootRect)) continue;

            Undo.RecordObject(img, "Verdant RC3 remove YouTube UI layer");
            img.enabled = false;
            img.raycastTarget = false;
            EditorUtility.SetDirty(img);
            changed++;
        }

        foreach (RawImage raw in root.GetComponentsInChildren<RawImage>(true))
        {
            if (raw == null || !raw.enabled || IsThumbnail(raw.transform)) continue;
            if (!IsRemovableLayer(raw, rootRect)) continue;

            Undo.RecordObject(raw, "Verdant RC3 remove YouTube RawImage layer");
            raw.enabled = false;
            raw.raycastTarget = false;
            EditorUtility.SetDirty(raw);
            changed++;
        }

        return changed;
    }

    private static bool IsRemovableLayer(Graphic graphic, RectTransform rootRect)
    {
        if (graphic == null || graphic.transform == null) return false;

        Transform t = graphic.transform;
        string n = t.name.ToLowerInvariant();
        string p = HierarchyPath(t).ToLowerInvariant();

        if (IsThumbnail(t)) return false;
        if (IsInteractiveGraphic(graphic)) return false;

        // Never touch useful icons, text decorations, masks, scrollbars, or result cards.
        if (n.Contains("icon") || n.Contains("logo") || n.Contains("mask") ||
            n.Contains("scroll") || n.Contains("handle") || n.Contains("checkmark") ||
            n.Contains("arrow") || n.Contains("search icon") || n.Contains("result image"))
            return false;

        bool obviousLayer =
            n.Contains("overlay") ||
            n.Contains("blocker") ||
            n.Contains("scrim") ||
            n.Contains("shade") ||
            n.Contains("dimmer") ||
            n.Contains("dim") ||
            n.Contains("tint") ||
            n.Contains("backdrop") ||
            p.Contains("/overlay") ||
            p.Contains("/blocker") ||
            p.Contains("/scrim");

        RectTransform rt = t as RectTransform;
        bool large = IsLargeRect(rt, rootRect);

        bool plainPanel =
            large &&
            (n.Contains("background") ||
             n == "bg" ||
             n.EndsWith(" bg") ||
             n.Contains("page bg") ||
             n.Contains("panel bg") ||
             n.Contains("content bg") ||
             n.Contains("results bg") ||
             n.Contains("search results background"));

        // A large, dark, non-interactive Image directly around search/results content is
        // the class of layer that was covering the page in the reported build.
        bool largeDarkPlain = large && IsDarkPlainGraphic(graphic) &&
                              !HasInteractiveDescendant(t) &&
                              !IsResultCard(t);

        return obviousLayer || plainPanel || largeDarkPlain;
    }

    private static bool IsInteractiveGraphic(Graphic graphic)
    {
        Transform t = graphic.transform;

        if (t.GetComponent<Button>() != null ||
            t.GetComponent<InputField>() != null ||
            t.GetComponent<TMP_InputField>() != null ||
            t.GetComponent<Scrollbar>() != null ||
            t.GetComponent<Slider>() != null ||
            t.GetComponent<Toggle>() != null)
            return true;

        Button parentButton = t.GetComponentInParent<Button>(true);
        if (parentButton != null) return true;

        TMP_InputField parentTmpInput = t.GetComponentInParent<TMP_InputField>(true);
        if (parentTmpInput != null) return true;

        return false;
    }

    private static bool HasInteractiveDescendant(Transform t)
    {
        if (t == null) return false;
        return t.GetComponentInChildren<Button>(true) != null ||
               t.GetComponentInChildren<TMP_InputField>(true) != null ||
               t.GetComponentInChildren<InputField>(true) != null ||
               t.GetComponentInChildren<ScrollRect>(true) != null ||
               t.GetComponentInChildren<Scrollbar>(true) != null ||
               t.GetComponentInChildren<Slider>(true) != null;
    }

    private static bool IsResultCard(Transform t)
    {
        string p = HierarchyPath(t).ToLowerInvariant();
        string n = t.name.ToLowerInvariant();
        return n.Contains("result") || n.Contains("card") || n.Contains("item") ||
               p.Contains("result card") || p.Contains("search result/");
    }

    private static bool IsLargeRect(RectTransform rt, RectTransform root)
    {
        if (rt == null || root == null) return false;

        float rootArea = Mathf.Abs(root.rect.width * root.rect.height);
        float area = Mathf.Abs(rt.rect.width * rt.rect.height);
        if (rootArea <= 1f || area <= 1f) return false;

        return area / rootArea >= 0.40f;
    }

    private static bool IsDarkPlainGraphic(Graphic g)
    {
        Color c = g.color;
        float brightness = (c.r + c.g + c.b) / 3f;

        if (g is Image)
        {
            Image img = g as Image;
            bool plainSprite = img.sprite == null || img.type == Image.Type.Simple || img.type == Image.Type.Sliced;
            return plainSprite && c.a > 0.20f && brightness < 0.20f;
        }

        if (g is RawImage)
        {
            RawImage raw = g as RawImage;
            return raw.texture == null && c.a > 0.20f && brightness < 0.20f;
        }

        return false;
    }

    private static int ClearBlockingRaycasts(Transform root)
    {
        int changed = 0;

        foreach (Graphic g in root.GetComponentsInChildren<Graphic>(true))
        {
            if (g == null || !g.raycastTarget) continue;
            if (g is TMP_Text)
            {
                Undo.RecordObject(g, "Verdant RC3 clear YouTube text raycast");
                g.raycastTarget = false;
                EditorUtility.SetDirty(g);
                changed++;
                continue;
            }

            if (IsInteractiveGraphic(g)) continue;

            Undo.RecordObject(g, "Verdant RC3 clear YouTube blocking raycast");
            g.raycastTarget = false;
            EditorUtility.SetDirty(g);
            changed++;
        }

        foreach (Button b in root.GetComponentsInChildren<Button>(true))
        {
            if (b == null) continue;

            Undo.RecordObject(b, "Verdant RC3 restore YouTube button");
            b.interactable = true;

            if (b.targetGraphic != null)
            {
                Undo.RecordObject(b.targetGraphic, "Verdant RC3 restore button raycast");
                b.targetGraphic.raycastTarget = true;
                EditorUtility.SetDirty(b.targetGraphic);
            }

            EditorUtility.SetDirty(b);
        }

        return changed;
    }

    private static int RestoreThumbnailVisibility(Transform root)
    {
        int changed = 0;

        foreach (RawImage raw in root.GetComponentsInChildren<RawImage>(true))
        {
            if (raw == null || !IsThumbnail(raw.transform)) continue;

            Undo.RecordObject(raw, "Verdant RC3 restore YouTube thumbnail");
            raw.enabled = true;
            raw.color = Color.white;
            raw.material = null;
            raw.raycastTarget = false;
            EditorUtility.SetDirty(raw);
            changed++;
        }

        foreach (Image img in root.GetComponentsInChildren<Image>(true))
        {
            if (img == null || !IsThumbnail(img.transform)) continue;
            if (img.GetComponent<Button>() != null) continue;

            Undo.RecordObject(img, "Verdant RC3 restore YouTube image thumbnail");
            img.enabled = true;
            img.color = Color.white;
            img.material = null;
            img.raycastTarget = false;
            EditorUtility.SetDirty(img);
            changed++;
        }

        return changed;
    }

    private static int RemoveThumbnailThemeReferences(Scene scene, Transform youtubeRoot)
    {
        int changed = 0;

        foreach (Component c in Resources.FindObjectsOfTypeAll<Component>())
        {
            if (c == null || c.gameObject.scene != scene) continue;
            if (!c.GetType().Name.ToLowerInvariant().Contains("themefollower")) continue;

            FieldInfo[] fields = c.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (FieldInfo f in fields)
            {
                if (!f.FieldType.IsArray) continue;
                Type elementType = f.FieldType.GetElementType();
                if (elementType == null || !typeof(UnityEngine.Object).IsAssignableFrom(elementType)) continue;

                Array oldArray;
                try { oldArray = f.GetValue(c) as Array; }
                catch { continue; }

                if (oldArray == null || oldArray.Length == 0) continue;

                List<UnityEngine.Object> kept = new List<UnityEngine.Object>();
                bool removed = false;

                foreach (object item in oldArray)
                {
                    UnityEngine.Object obj = item as UnityEngine.Object;
                    Component component = obj as Component;
                    if (component != null &&
                        component.transform.IsChildOf(youtubeRoot) &&
                        IsThumbnail(component.transform))
                    {
                        removed = true;
                        changed++;
                        continue;
                    }

                    kept.Add(obj);
                }

                if (!removed) continue;

                Array newArray = Array.CreateInstance(elementType, kept.Count);
                for (int i = 0; i < kept.Count; i++)
                    newArray.SetValue(kept[i], i);

                try
                {
                    Undo.RecordObject(c, "Verdant RC3 remove thumbnail theme tint");
                    f.SetValue(c, newArray);
                    EditorUtility.SetDirty(c);
                }
                catch { }
            }
        }

        return changed;
    }

    private static int RepairCanvasGroups(Transform root)
    {
        int changed = 0;

        foreach (CanvasGroup cg in root.GetComponentsInChildren<CanvasGroup>(true))
        {
            if (cg == null) continue;

            Undo.RecordObject(cg, "Verdant RC3 YouTube CanvasGroup");

            if (cg.alpha < 0.999f)
            {
                cg.alpha = 1f;
                changed++;
            }

            if (!cg.interactable)
            {
                cg.interactable = true;
                changed++;
            }

            if (!cg.blocksRaycasts && HasInteractiveDescendant(cg.transform))
            {
                cg.blocksRaycasts = true;
                changed++;
            }

            EditorUtility.SetDirty(cg);
        }

        return changed;
    }

    private static bool IsThumbnail(Transform t)
    {
        if (t == null) return false;
        string n = t.name.ToLowerInvariant();
        string p = HierarchyPath(t).ToLowerInvariant();

        return n.Contains("thumb") ||
               n.Contains("thumbnail") ||
               n.Contains("preview") ||
               n.Contains("result image") ||
               p.Contains("/thumbnail") ||
               p.Contains("/thumb") ||
               p.Contains("search result") && (n.Contains("image") || n.Contains("preview"));
    }

    private static int UpdateVersionLabels(Scene scene)
    {
        int changed = 0;

        foreach (TMP_Text t in Resources.FindObjectsOfTypeAll<TMP_Text>())
        {
            if (t == null || t.gameObject.scene != scene || string.IsNullOrEmpty(t.text)) continue;
            if (!t.text.Contains("V14.") && !t.text.Contains("V15.")) continue;

            Undo.RecordObject(t, "Verdant RC3 version");
            t.text = "V" + Version;
            EditorUtility.SetDirty(t);
            changed++;
        }

        return changed;
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

    private static void CreateBackup(Scene scene)
    {
        if (string.IsNullOrEmpty(scene.path)) return;

        string folder = Root + "/Generated/V15 Backups";
        EnsureFolder(folder);

        string file = System.IO.Path.GetFileNameWithoutExtension(scene.path) +
                      " - before V15 RC3 YouTube cleanup " +
                      DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";

        string backup = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + file);
        EditorSceneManager.SaveScene(scene, backup, true);
        Debug.Log("[Verdant V15 RC3] Backup created: " + backup);
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
