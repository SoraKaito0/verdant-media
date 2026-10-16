#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Verdant V15 final-media repair.
/// Editor-only and deliberately reflection/serialization based so it does not add another runtime Udon behaviour.
/// It repairs the current scene in place and makes a backup first.
/// </summary>
public static class VerdantFinalMediaRepair
{
    private const string Root = "Assets/VerdantBoard";
    private const string Generated = Root + "/Generated";
    private const string ReportPath = Generated + "/Verdant_Final_Media_Audit.txt";
    private const string RenderTexturePath = Generated + "/Verdant_Final_Video.renderTexture";

    [MenuItem("Tools/Verdant Board/Final Polish/Repair Media + YouTube")]
    public static void RepairMediaAndYouTube()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            EditorUtility.DisplayDialog("Verdant Final Polish",
                "Leave Play Mode and wait for Unity/UdonSharp to finish compiling, then run this again.", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("Verdant Final Polish", "No valid loaded scene was found.", "OK");
            return;
        }

        EnsureFolder(Root);
        EnsureFolder(Generated);
        MakeSceneBackup(scene);

        List<MonoBehaviour> behaviours = SceneBehaviours(scene);
        MonoBehaviour shared = FirstByTypeName(behaviours, "VerdantSharedVideo");
        MonoBehaviour youtube = FirstByTypeName(behaviours, "VerdantYoutubeBridge");
        Component player = FindBestVideoPlayer(behaviours, shared);
        AudioSource speaker = FindBestVideoAudio(scene, shared);

        int referenceFixes = RepairControllerReferences(behaviours, shared, youtube, player, speaker);

        List<RawImage> videoImages = FindVideoDisplayImages(scene);
        RenderTexture target = EnsureVideoTarget(player, videoImages);
        int videoVisualFixes = FixVideoDisplayImages(videoImages, target);

        HashSet<UnityEngine.Object> protectedMediaGraphics = new HashSet<UnityEngine.Object>();
        foreach (RawImage r in videoImages) if (r != null) protectedMediaGraphics.Add(r);

        int youtubeVisualFixes = 0;
        int overlayFixes = 0;
        List<Graphic> youtubeGraphics = FindYoutubeResultGraphics(scene, youtube != null ? youtube.transform : null);
        foreach (Graphic g in youtubeGraphics)
        {
            if (g == null) continue;
            protectedMediaGraphics.Add(g);
            if (MakeGraphicNeutralAndBright(g)) youtubeVisualFixes++;
            overlayFixes += ReduceThumbnailOverlays(g.transform);
        }

        int themeRemovals = RemoveProtectedMediaFromThemeFollowers(behaviours, protectedMediaGraphics);

        // One final pass after follower arrays are changed.
        foreach (Graphic g in protectedMediaGraphics.OfType<Graphic>())
            MakeGraphicNeutralAndBright(g);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!string.IsNullOrEmpty(scene.path))
            EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        string report = BuildAudit(scene, behaviours, shared, youtube, player, speaker, target,
            videoImages, youtubeGraphics, referenceFixes, videoVisualFixes, youtubeVisualFixes,
            overlayFixes, themeRemovals);
        File.WriteAllText(ReportPath, report);
        AssetDatabase.ImportAsset(ReportPath, ImportAssetOptions.ForceUpdate);

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(ReportPath);

        EditorUtility.DisplayDialog("Verdant media repair complete",
            "Finished the final media/UI pass.\n\n" +
            "Reference repairs: " + referenceFixes + "\n" +
            "Video display fixes: " + videoVisualFixes + "\n" +
            "YouTube image fixes: " + youtubeVisualFixes + "\n" +
            "Thumbnail overlay fixes: " + overlayFixes + "\n" +
            "Theme exclusions: " + themeRemovals + "\n\n" +
            "A validation report was created at:\n" + ReportPath + "\n\n" +
            "Important: AVPro and normal YouTube watch URLs must be tested with VRChat Build & Test, not ordinary Unity Play Mode.",
            "OK");
    }

    [MenuItem("Tools/Verdant Board/Final Polish/Run Media Audit Only")]
    public static void RunAuditOnly()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) return;

        EnsureFolder(Root);
        EnsureFolder(Generated);

        List<MonoBehaviour> behaviours = SceneBehaviours(scene);
        MonoBehaviour shared = FirstByTypeName(behaviours, "VerdantSharedVideo");
        MonoBehaviour youtube = FirstByTypeName(behaviours, "VerdantYoutubeBridge");
        Component player = FindBestVideoPlayer(behaviours, shared);
        AudioSource speaker = FindBestVideoAudio(scene, shared);
        List<RawImage> videoImages = FindVideoDisplayImages(scene);
        List<Graphic> youtubeGraphics = FindYoutubeResultGraphics(scene, youtube != null ? youtube.transform : null);
        RenderTexture target = FindExistingVideoTarget(player, videoImages);

        string report = BuildAudit(scene, behaviours, shared, youtube, player, speaker, target,
            videoImages, youtubeGraphics, 0, 0, 0, 0, 0);
        File.WriteAllText(ReportPath, report);
        AssetDatabase.ImportAsset(ReportPath, ImportAssetOptions.ForceUpdate);
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(ReportPath);
        Debug.Log(report);
    }

    private static List<MonoBehaviour> SceneBehaviours(Scene scene)
    {
        List<MonoBehaviour> result = new List<MonoBehaviour>();
        foreach (MonoBehaviour b in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
        {
            if (b == null || b.gameObject == null) continue;
            if (b.gameObject.scene != scene) continue;
            result.Add(b);
        }
        return result;
    }

    private static MonoBehaviour FirstByTypeName(List<MonoBehaviour> all, string typeName)
    {
        return all.FirstOrDefault(x => x != null &&
            string.Equals(x.GetType().Name, typeName, StringComparison.OrdinalIgnoreCase));
    }

    private static Component FindBestVideoPlayer(List<MonoBehaviour> all, MonoBehaviour shared)
    {
        if (shared != null)
        {
            try
            {
                SerializedObject so = new SerializedObject(shared);
                SerializedProperty p = so.FindProperty("player");
                if (p != null && p.propertyType == SerializedPropertyType.ObjectReference &&
                    p.objectReferenceValue is Component c && c != null)
                    return c;
            }
            catch { }
        }

        List<Component> candidates = new List<Component>();
        foreach (MonoBehaviour b in all)
        {
            if (b == null) continue;
            string n = b.GetType().Name;
            if (n == "VRCAVProVideoPlayer" || n == "VRCUnityVideoPlayer")
                candidates.Add(b);
        }

        return candidates
            .OrderByDescending(c => c.gameObject.activeInHierarchy)
            .ThenByDescending(c => HierarchyPath(c.transform).ToLowerInvariant().Contains("verdant"))
            .ThenByDescending(c => HierarchyPath(c.transform).ToLowerInvariant().Contains("shared video"))
            .FirstOrDefault();
    }

    private static AudioSource FindBestVideoAudio(Scene scene, MonoBehaviour shared)
    {
        if (shared != null)
        {
            AudioSource local = shared.GetComponentInChildren<AudioSource>(true);
            if (local != null) return local;

            try
            {
                SerializedObject so = new SerializedObject(shared);
                SerializedProperty p = so.FindProperty("speaker");
                if (p != null && p.propertyType == SerializedPropertyType.ObjectReference &&
                    p.objectReferenceValue is AudioSource a && a != null)
                    return a;
            }
            catch { }
        }

        return UnityEngine.Object.FindObjectsOfType<AudioSource>(true)
            .Where(a => a != null && a.gameObject.scene == scene)
            .OrderByDescending(a => HierarchyPath(a.transform).ToLowerInvariant().Contains("video"))
            .ThenByDescending(a => HierarchyPath(a.transform).ToLowerInvariant().Contains("media"))
            .FirstOrDefault();
    }

    private static int RepairControllerReferences(List<MonoBehaviour> all, MonoBehaviour shared,
        MonoBehaviour youtube, Component player, AudioSource speaker)
    {
        int changed = 0;

        if (shared != null)
        {
            if (player != null) changed += TryAssignReference(shared, "player", player) ? 1 : 0;
            if (speaker != null) changed += TryAssignReference(shared, "speaker", speaker) ? 1 : 0;
        }

        string[] mediaTypes =
        {
            "VerdantPersonalVideo", "VerdantVideoUI", "VerdantYoutubeBridge",
            "VerdantSponsorSkip", "VerdantMediaReliability"
        };

        foreach (MonoBehaviour b in all)
        {
            if (b == null || !mediaTypes.Contains(b.GetType().Name)) continue;

            if (shared != null)
            {
                changed += TryAssignReference(b, "controller", shared) ? 1 : 0;
                changed += TryAssignReference(b, "sharedVideo", shared) ? 1 : 0;
                changed += TryAssignReference(b, "shared", shared) ? 1 : 0;
            }

            if (player != null)
                changed += TryAssignReference(b, "player", player) ? 1 : 0;

            if (speaker != null)
                changed += TryAssignReference(b, "speaker", speaker) ? 1 : 0;
        }

        return changed;
    }

    private static bool TryAssignReference(UnityEngine.Object owner, string propertyName, UnityEngine.Object value)
    {
        if (owner == null || value == null) return false;
        try
        {
            SerializedObject so = new SerializedObject(owner);
            SerializedProperty p = so.FindProperty(propertyName);
            if (p == null || p.propertyType != SerializedPropertyType.ObjectReference) return false;
            if (p.objectReferenceValue != null) return false;

            p.objectReferenceValue = value;
            if (p.objectReferenceValue != value)
            {
                so.Update();
                return false;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(owner);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static List<RawImage> FindVideoDisplayImages(Scene scene)
    {
        List<RawImage> result = new List<RawImage>();
        foreach (RawImage r in UnityEngine.Object.FindObjectsOfType<RawImage>(true))
        {
            if (r == null || r.gameObject.scene != scene) continue;
            string name = r.name.ToLowerInvariant();
            string path = HierarchyPath(r.transform).ToLowerInvariant();

            bool exact = name == "picture" || name == "video display" || name == "video screen";
            bool contextual = (path.Contains("video") || path.Contains("media")) &&
                              (name.Contains("picture") || name.Contains("display") || name.Contains("screen"));

            if (exact || contextual)
                result.Add(r);
        }
        return result.Distinct().ToList();
    }

    private static RenderTexture FindExistingVideoTarget(Component player, List<RawImage> displays)
    {
        if (player != null)
        {
            try
            {
                SerializedObject so = new SerializedObject(player);
                foreach (string propName in new[] { "targetTexture", "_targetTexture", "renderTexture" })
                {
                    SerializedProperty p = so.FindProperty(propName);
                    if (p != null && p.propertyType == SerializedPropertyType.ObjectReference &&
                        p.objectReferenceValue is RenderTexture rt && rt != null)
                        return rt;
                }
            }
            catch { }
        }

        foreach (RawImage r in displays)
            if (r != null && r.texture is RenderTexture rt && rt != null)
                return rt;

        return null;
    }

    private static RenderTexture EnsureVideoTarget(Component player, List<RawImage> displays)
    {
        RenderTexture existing = FindExistingVideoTarget(player, displays);
        if (existing != null)
        {
            AssignTargetTexture(player, existing);
            return existing;
        }

        if (player == null) return null;

        SerializedObject so;
        SerializedProperty targetProp = null;
        try
        {
            so = new SerializedObject(player);
            foreach (string propName in new[] { "targetTexture", "_targetTexture", "renderTexture" })
            {
                SerializedProperty p = so.FindProperty(propName);
                if (p != null && p.propertyType == SerializedPropertyType.ObjectReference)
                {
                    targetProp = p;
                    break;
                }
            }
        }
        catch
        {
            return null;
        }

        if (targetProp == null) return null;

        RenderTexture target = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
        if (target == null)
        {
            target = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32);
            target.name = "Verdant Final Video";
            target.wrapMode = TextureWrapMode.Clamp;
            target.filterMode = FilterMode.Bilinear;
            AssetDatabase.CreateAsset(target, RenderTexturePath);
        }

        AssignTargetTexture(player, target);
        return target;
    }

    private static void AssignTargetTexture(Component player, RenderTexture target)
    {
        if (player == null || target == null) return;
        try
        {
            SerializedObject so = new SerializedObject(player);
            SerializedProperty targetProp = null;
            foreach (string propName in new[] { "targetTexture", "_targetTexture", "renderTexture" })
            {
                SerializedProperty p = so.FindProperty(propName);
                if (p != null && p.propertyType == SerializedPropertyType.ObjectReference)
                {
                    targetProp = p;
                    break;
                }
            }

            if (targetProp != null)
                targetProp.objectReferenceValue = target;

            SerializedProperty mode = so.FindProperty("renderMode");
            if (mode != null && mode.propertyType == SerializedPropertyType.Enum)
            {
                int ix = Array.FindIndex(mode.enumNames,
                    x => x.IndexOf("RenderTexture", StringComparison.OrdinalIgnoreCase) >= 0);
                if (ix >= 0) mode.enumValueIndex = ix;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(player);
        }
        catch { }
    }

    private static int FixVideoDisplayImages(List<RawImage> displays, RenderTexture target)
    {
        int changed = 0;
        foreach (RawImage r in displays)
        {
            if (r == null) continue;
            bool dirty = false;

            if (r.color != Color.white)
            {
                r.color = Color.white;
                dirty = true;
            }

            if (target != null && r.texture != target)
            {
                r.texture = target;
                dirty = true;
            }

            CanvasGroup cg = r.GetComponent<CanvasGroup>();
            if (cg != null && cg.alpha < 0.999f)
            {
                cg.alpha = 1f;
                dirty = true;
                EditorUtility.SetDirty(cg);
            }

            if (dirty)
            {
                EditorUtility.SetDirty(r);
                changed++;
            }
        }
        return changed;
    }

    private static List<Graphic> FindYoutubeResultGraphics(Scene scene, Transform bridgeRoot)
    {
        List<Graphic> result = new List<Graphic>();

        foreach (RawImage r in UnityEngine.Object.FindObjectsOfType<RawImage>(true))
        {
            if (r == null || r.gameObject.scene != scene) continue;
            if (LooksLikeYoutubePreview(r.transform, bridgeRoot))
                result.Add(r);
        }

        foreach (Image i in UnityEngine.Object.FindObjectsOfType<Image>(true))
        {
            if (i == null || i.gameObject.scene != scene || i.sprite == null) continue;
            if (LooksLikeYoutubePreview(i.transform, bridgeRoot))
                result.Add(i);
        }

        return result.Distinct().ToList();
    }

    private static bool LooksLikeYoutubePreview(Transform t, Transform bridgeRoot)
    {
        if (t == null) return false;

        string name = t.name.ToLowerInvariant();
        string path = HierarchyPath(t).ToLowerInvariant();

        bool previewName = name.Contains("thumbnail") || name.Contains("thumb") ||
                           name.Contains("preview") || name.Contains("result image") ||
                           name.Contains("result picture");

        bool youtubeContext = path.Contains("youtube") ||
                              (path.Contains("search") && path.Contains("result")) ||
                              (path.Contains("media") && path.Contains("result"));

        bool underBridge = bridgeRoot != null && (t == bridgeRoot || t.IsChildOf(bridgeRoot));

        return previewName && (youtubeContext || underBridge);
    }

    private static bool MakeGraphicNeutralAndBright(Graphic g)
    {
        if (g == null) return false;
        bool changed = false;

        if (g.color != Color.white)
        {
            g.color = Color.white;
            changed = true;
        }

        CanvasGroup cg = g.GetComponent<CanvasGroup>();
        if (cg != null && cg.alpha < 0.999f)
        {
            cg.alpha = 1f;
            EditorUtility.SetDirty(cg);
            changed = true;
        }

        if (changed) EditorUtility.SetDirty(g);
        return changed;
    }

    private static int ReduceThumbnailOverlays(Transform graphicTransform)
    {
        if (graphicTransform == null || graphicTransform.parent == null) return 0;

        int changed = 0;
        Transform parent = graphicTransform.parent;
        foreach (Image i in parent.GetComponentsInChildren<Image>(true))
        {
            if (i == null || i.transform == graphicTransform) continue;
            string n = i.name.ToLowerInvariant();

            bool overlay = n.Contains("overlay") || n.Contains("shade") ||
                           n.Contains("dim") || n.Contains("tint");
            if (!overlay) continue;

            Color c = i.color;
            if (c.a > 0.12f)
            {
                c.a = 0.12f;
                i.color = c;
                EditorUtility.SetDirty(i);
                changed++;
            }
        }
        return changed;
    }

    private static int RemoveProtectedMediaFromThemeFollowers(List<MonoBehaviour> all,
        HashSet<UnityEngine.Object> protectedObjects)
    {
        int removed = 0;
        string[] fields = { "panelImages", "rowImages", "accentImages", "backgroundImages" };

        foreach (MonoBehaviour b in all)
        {
            if (b == null || b.GetType().Name != "VerdantThemeFollower") continue;

            try
            {
                SerializedObject so = new SerializedObject(b);
                bool dirty = false;

                foreach (string field in fields)
                {
                    SerializedProperty arr = so.FindProperty(field);
                    if (arr == null || !arr.isArray) continue;

                    for (int i = arr.arraySize - 1; i >= 0; i--)
                    {
                        SerializedProperty e = arr.GetArrayElementAtIndex(i);
                        if (e.propertyType != SerializedPropertyType.ObjectReference) continue;
                        UnityEngine.Object o = e.objectReferenceValue;
                        if (o == null || !protectedObjects.Contains(o)) continue;

                        e.objectReferenceValue = null;
                        arr.DeleteArrayElementAtIndex(i);
                        removed++;
                        dirty = true;
                    }
                }

                if (dirty)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(b);
                }
            }
            catch { }
        }

        return removed;
    }

    private static string BuildAudit(Scene scene, List<MonoBehaviour> behaviours,
        MonoBehaviour shared, MonoBehaviour youtube, Component player, AudioSource speaker,
        RenderTexture target, List<RawImage> videoImages, List<Graphic> youtubeGraphics,
        int referenceFixes, int videoFixes, int youtubeFixes, int overlayFixes, int themeRemovals)
    {
        List<string> lines = new List<string>();
        lines.Add("VERDANT FINAL MEDIA AUDIT");
        lines.Add("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        lines.Add("Scene: " + scene.name);
        lines.Add("");

        lines.Add("MEDIA CORE");
        lines.Add("Shared controller: " + ObjPath(shared));
        lines.Add("Video player: " + ObjPath(player));
        lines.Add("Audio source: " + ObjPath(speaker));
        lines.Add("Render texture: " + (target != null ? AssetDatabase.GetAssetPath(target) : "NOT FOUND"));
        lines.Add("Video display RawImages: " + videoImages.Count);
        lines.Add("");

        lines.Add("YOUTUBE");
        lines.Add("YouTube bridge: " + ObjPath(youtube));
        lines.Add("Detected thumbnail/preview graphics: " + youtubeGraphics.Count);
        lines.Add("");

        int activePlayers = behaviours.Count(b => b != null && b.gameObject.activeInHierarchy &&
            (b.GetType().Name == "VRCAVProVideoPlayer" || b.GetType().Name == "VRCUnityVideoPlayer"));

        lines.Add("PERFORMANCE / SAFETY");
        lines.Add("Active VRC video players: " + activePlayers);
        if (activePlayers > 2)
            lines.Add("WARNING: More than two active video players were found. VRChat recommends keeping video-player count low.");
        lines.Add("");

        lines.Add("CHANGES THIS RUN");
        lines.Add("Reference repairs: " + referenceFixes);
        lines.Add("Video display fixes: " + videoFixes);
        lines.Add("YouTube graphic fixes: " + youtubeFixes);
        lines.Add("Thumbnail overlay fixes: " + overlayFixes);
        lines.Add("Theme exclusions: " + themeRemovals);
        lines.Add("");

        lines.Add("VALIDATION");
        if (shared == null) lines.Add("WARNING: VerdantSharedVideo was not found.");
        if (player == null) lines.Add("WARNING: No VRCAVProVideoPlayer/VRCUnityVideoPlayer was found.");
        if (videoImages.Count == 0) lines.Add("WARNING: No video display RawImage was found.");
        if (youtube == null) lines.Add("WARNING: VerdantYoutubeBridge was not found.");
        if (youtubeGraphics.Count == 0) lines.Add("NOTE: No YouTube thumbnail graphics were detected by name/hierarchy.");
        if (shared != null && player != null && videoImages.Count > 0)
            lines.Add("PASS: Core Verdant video references/display were found.");
        if (youtube != null)
            lines.Add("PASS: YouTube bridge is present.");

        lines.Add("");
        lines.Add("TESTING NOTE");
        lines.Add("AVPro does not play in ordinary Unity editor Play Mode. Hosted YouTube watch URLs must be tested through VRChat Build & Test / client.");
        lines.Add("VRChat also rate-limits new video URLs to one request per five seconds globally across video players.");
        lines.Add("Remote image downloads are likewise rate-limited and can queue when many thumbnails are requested.");

        return string.Join("\n", lines);
    }

    private static string ObjPath(UnityEngine.Object o)
    {
        if (o == null) return "NOT FOUND";
        Component c = o as Component;
        if (c != null) return c.GetType().Name + " @ " + HierarchyPath(c.transform);
        return o.name;
    }

    private static string HierarchyPath(Transform t)
    {
        if (t == null) return "";
        string path = t.name;
        Transform p = t.parent;
        while (p != null)
        {
            path = p.name + "/" + path;
            p = p.parent;
        }
        return path;
    }

    private static void MakeSceneBackup(Scene scene)
    {
        if (string.IsNullOrEmpty(scene.path)) return;
        EnsureFolder(Generated + "/Backups");

        string file = Path.GetFileNameWithoutExtension(scene.path) + " - before Final Media Repair.unity";
        string backup = AssetDatabase.GenerateUniqueAssetPath(Generated + "/Backups/" + file);
        EditorSceneManager.SaveScene(scene, backup, true);
        Debug.Log("[Verdant] Scene backup created: " + backup);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        string[] parts = folder.Split('/');
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
#endif
