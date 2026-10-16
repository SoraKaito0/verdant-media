using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class VerdantV15ReleaseFinalizer
{
    private const string Root = "Assets/VerdantBoard";
    private const string VideoRoot = "Assets/VerdantVideo";
    private const string Version = "15.0 RC1";

    [MenuItem("Tools/Verdant Board/V15 Release Candidate/Run Final Media + Stability Pass")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            EditorUtility.DisplayDialog("Verdant V15", "Leave Play Mode and wait for Unity to finish compiling, then run the finalizer again.", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("Verdant V15", "No valid loaded scene was found.", "OK");
            return;
        }

        CreateBackup(scene);

        Component shared = FindBestComponent(scene, "VerdantSharedVideo");
        Component player = GetMember(shared, "player") as Component;
        Component speaker = GetMember(shared, "speaker") as Component;

        if (player == null && shared != null)
            player = FindLikelyVideoPlayer(shared.transform);

        RenderTexture target = EnsureSharedRenderTexture(player);

        int rewired = RewireMediaAdapters(scene, shared, player, speaker);
        int videoSurfaces = RepairVideoSurfaces(scene, target);
        int thumbnails = RepairYoutubeThumbnails(scene);
        int removedThemeRefs = ProtectMediaFromThemeFollowers(scene);
        int removedLegacy = DisableKnownLegacyVideoWrappers(scene);
        int fixedDeadPosterFields = RetireKnownDeadPosterStrings(scene);
        int arcadeFixes = StabilizeArcadeRoots(scene);

        UpdateVersionLabels(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!string.IsNullOrEmpty(scene.path))
            EditorSceneManager.SaveScene(scene);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string summary =
            "Verdant V15 Release Candidate pass complete.\n\n" +
            "Shared media controller: " + (shared != null ? "FOUND" : "NOT FOUND") + "\n" +
            "Video player: " + (player != null ? player.GetType().Name : "NOT FOUND") + "\n" +
            "Shared RenderTexture: " + (target != null ? target.name : "NOT FOUND") + "\n" +
            "Media adapters rewired: " + rewired + "\n" +
            "Video surfaces repaired: " + videoSurfaces + "\n" +
            "YouTube thumbnails repaired: " + thumbnails + "\n" +
            "Theme references removed from media: " + removedThemeRefs + "\n" +
            "Legacy video wrappers disabled: " + removedLegacy + "\n" +
            "Dead poster fields retired: " + fixedDeadPosterFields + "\n" +
            "Arcade roots stabilized: " + arcadeFixes + "\n\n" +
            "Now test with Build & Test as well as ClientSim.";

        Debug.Log("[Verdant V15] " + summary.Replace("\n", " | "));
        EditorUtility.DisplayDialog("Verdant V15 - Release Candidate", summary, "OK");
    }

    private static Component FindBestComponent(Scene scene, string typeName)
    {
        Component[] all = Resources.FindObjectsOfTypeAll<Component>();
        return all.Where(c => c != null && c.gameObject.scene == scene && c.GetType().Name == typeName)
            .OrderByDescending(c => c.gameObject.activeInHierarchy)
            .FirstOrDefault();
    }

    private static Component FindLikelyVideoPlayer(Transform root)
    {
        if (root == null) return null;
        foreach (Component c in root.GetComponentsInChildren<Component>(true))
        {
            if (c == null) continue;
            string n = c.GetType().Name.ToLowerInvariant();
            if ((n.Contains("video") || n.Contains("avpro")) && HasSerializedProperty(c, "targetTexture"))
                return c;
        }
        return null;
    }

    private static bool HasSerializedProperty(UnityEngine.Object obj, string property)
    {
        if (obj == null) return false;
        try
        {
            SerializedObject so = new SerializedObject(obj);
            return so.FindProperty(property) != null;
        }
        catch { return false; }
    }

    private static RenderTexture EnsureSharedRenderTexture(Component player)
    {
        if (player == null) return null;

        SerializedObject so;
        try { so = new SerializedObject(player); }
        catch { return null; }

        SerializedProperty targetProp = so.FindProperty("targetTexture");
        RenderTexture target = targetProp != null ? targetProp.objectReferenceValue as RenderTexture : null;

        EnsureFolder(VideoRoot);

        if (target == null)
        {
            string path = VideoRoot + "/VerdantSharedVideo_V15.renderTexture";
            target = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
            if (target == null)
            {
                target = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32)
                {
                    name = "Verdant Shared Video V15",
                    useMipMap = false,
                    autoGenerateMips = false,
                    antiAliasing = 1,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                AssetDatabase.CreateAsset(target, path);
            }

            if (targetProp != null)
                targetProp.objectReferenceValue = target;
        }

        SerializedProperty renderMode = so.FindProperty("renderMode");
        if (renderMode != null && renderMode.propertyType == SerializedPropertyType.Enum)
        {
            int index = Array.FindIndex(renderMode.enumNames, x => string.Equals(x, "RenderTexture", StringComparison.OrdinalIgnoreCase));
            if (index >= 0) renderMode.enumValueIndex = index;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(player);
        return target;
    }

    private static int RewireMediaAdapters(Scene scene, Component shared, Component player, Component speaker)
    {
        int count = 0;
        if (shared == null) return count;

        foreach (Component c in Resources.FindObjectsOfTypeAll<Component>())
        {
            if (c == null || c.gameObject.scene != scene) continue;

            string typeName = c.GetType().Name;
            if (typeName != "VerdantPersonalVideo" &&
                typeName != "VerdantVideoUI" &&
                typeName != "VerdantV10TabletUI" &&
                typeName != "VerdantYoutubeBridge")
                continue;

            bool changed = false;
            changed |= SetMemberIfCompatible(c, "controller", shared);
            changed |= SetMemberIfCompatible(c, "video", shared);
            changed |= SetMemberIfCompatible(c, "sharedVideo", shared);
            if (player != null) changed |= SetMemberIfCompatible(c, "player", player);
            if (speaker != null) changed |= SetMemberIfCompatible(c, "speaker", speaker);

            if (changed)
            {
                EditorUtility.SetDirty(c);
                count++;
            }
        }
        return count;
    }

    private static int RepairVideoSurfaces(Scene scene, RenderTexture target)
    {
        int count = 0;

        foreach (RawImage raw in Resources.FindObjectsOfTypeAll<RawImage>())
        {
            if (raw == null || raw.gameObject.scene != scene) continue;
            if (IsYoutubeThumbnail(raw)) continue;
            if (!IsVideoSurface(raw)) continue;

            Undo.RecordObject(raw, "Verdant V15 video surface repair");
            if (target != null) raw.texture = target;
            raw.color = Color.white;
            raw.material = null;
            raw.raycastTarget = false;
            EditorUtility.SetDirty(raw);
            count++;

            Image frame = raw.transform.parent != null ? raw.transform.parent.GetComponent<Image>() : null;
            if (frame != null && LooksLikeVideoFrame(frame))
            {
                Undo.RecordObject(frame, "Verdant V15 video frame repair");
                frame.color = Color.black;
                EditorUtility.SetDirty(frame);
            }
        }

        return count;
    }

    private static bool IsVideoSurface(RawImage raw)
    {
        string path = HierarchyPath(raw.transform).ToLowerInvariant();
        string n = raw.name.ToLowerInvariant();

        if (n.Contains("thumbnail") || n.Contains("thumb")) return false;

        return n == "picture" ||
               n.Contains("video display") ||
               n.Contains("video screen") ||
               n.Contains("shared video") ||
               path.Contains("world video screen") ||
               path.Contains("shared video") && (n.Contains("display") || n.Contains("picture"));
    }

    private static bool LooksLikeVideoFrame(Image image)
    {
        string n = image.name.ToLowerInvariant();
        return n.Contains("display frame") || n.Contains("video frame") || n.Contains("screen frame");
    }

    private static int RepairYoutubeThumbnails(Scene scene)
    {
        int count = 0;

        foreach (RawImage raw in Resources.FindObjectsOfTypeAll<RawImage>())
        {
            if (raw == null || raw.gameObject.scene != scene) continue;
            if (!IsYoutubeThumbnail(raw)) continue;

            Undo.RecordObject(raw, "Verdant V15 YouTube thumbnail repair");
            raw.color = Color.white;
            raw.material = null;
            raw.raycastTarget = false;
            EditorUtility.SetDirty(raw);
            count++;

            Transform p = raw.transform.parent;
            if (p == null) continue;

            foreach (Image overlay in p.GetComponentsInChildren<Image>(true))
            {
                if (overlay == null || overlay.gameObject == raw.gameObject) continue;
                string on = overlay.name.ToLowerInvariant();
                if (!(on.Contains("overlay") || on.Contains("shade") || on.Contains("dim") || on.Contains("tint")))
                    continue;

                Color c = overlay.color;
                if (c.a > 0.10f)
                {
                    Undo.RecordObject(overlay, "Verdant V15 thumbnail overlay repair");
                    c.a = 0.10f;
                    overlay.color = c;
                    EditorUtility.SetDirty(overlay);
                }
            }
        }

        return count;
    }

    private static bool IsYoutubeThumbnail(RawImage raw)
    {
        string path = HierarchyPath(raw.transform).ToLowerInvariant();
        string n = raw.name.ToLowerInvariant();

        bool thumbnailName = n.Contains("thumbnail") || n.Contains("thumb") || n.Contains("preview") || n.Contains("result image");
        bool youtubeContext = path.Contains("youtube") || path.Contains("search result") || path.Contains("results");

        return thumbnailName && youtubeContext;
    }

    private static int ProtectMediaFromThemeFollowers(Scene scene)
    {
        HashSet<UnityEngine.Object> protectedGraphics = new HashSet<UnityEngine.Object>();

        foreach (RawImage raw in Resources.FindObjectsOfTypeAll<RawImage>())
        {
            if (raw == null || raw.gameObject.scene != scene) continue;
            if (IsYoutubeThumbnail(raw) || IsVideoSurface(raw))
                protectedGraphics.Add(raw);
        }

        int removed = 0;

        foreach (Component follower in Resources.FindObjectsOfTypeAll<Component>())
        {
            if (follower == null || follower.gameObject.scene != scene) continue;
            if (follower.GetType().Name != "VerdantThemeFollower") continue;

            bool changed = false;
            Type t = follower.GetType();

            foreach (FieldInfo field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                try
                {
                    if (field.FieldType.IsArray)
                    {
                        Array arr = field.GetValue(follower) as Array;
                        if (arr == null) continue;

                        Type elementType = field.FieldType.GetElementType();
                        if (elementType == null || !typeof(UnityEngine.Object).IsAssignableFrom(elementType)) continue;

                        List<object> keep = new List<object>();
                        bool localChanged = false;

                        foreach (object item in arr)
                        {
                            UnityEngine.Object obj = item as UnityEngine.Object;
                            if (obj != null && protectedGraphics.Contains(obj))
                            {
                                removed++;
                                localChanged = true;
                            }
                            else keep.Add(item);
                        }

                        if (localChanged)
                        {
                            Array newArr = Array.CreateInstance(elementType, keep.Count);
                            for (int i = 0; i < keep.Count; i++) newArr.SetValue(keep[i], i);
                            field.SetValue(follower, newArr);
                            changed = true;
                        }
                    }
                    else if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                    {
                        UnityEngine.Object obj = field.GetValue(follower) as UnityEngine.Object;
                        if (obj != null && protectedGraphics.Contains(obj))
                        {
                            field.SetValue(follower, null);
                            removed++;
                            changed = true;
                        }
                    }
                }
                catch { }
            }

            if (changed) EditorUtility.SetDirty(follower);
        }

        return removed;
    }

    private static int DisableKnownLegacyVideoWrappers(Scene scene)
    {
        string[] exactNames =
        {
            "Home with video shortcut",
            "Personal video page",
            "Open video player",
            "Verdant V4 Home"
        };

        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!exactNames.Contains(t.name)) continue;
                if (!t.gameObject.activeSelf) continue;

                Undo.RecordObject(t.gameObject, "Verdant V15 disable legacy video wrapper");
                t.gameObject.SetActive(false);
                EditorUtility.SetDirty(t.gameObject);
                count++;
            }
        }
        return count;
    }

    private static int RetireKnownDeadPosterStrings(Scene scene)
    {
        int count = 0;

        foreach (Component c in Resources.FindObjectsOfTypeAll<Component>())
        {
            if (c == null || c.gameObject.scene != scene) continue;

            foreach (FieldInfo field in c.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.FieldType != typeof(string)) continue;

                try
                {
                    string value = field.GetValue(c) as string;
                    if (string.IsNullOrEmpty(value)) continue;
                    if (!value.Contains("Brothers.png")) continue;

                    field.SetValue(c, string.Empty);
                    EditorUtility.SetDirty(c);
                    count++;
                }
                catch { }
            }
        }

        return count;
    }

    private static int StabilizeArcadeRoots(Scene scene)
    {
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (!(n == "arcade" || n.Contains("arcade page") || n.Contains("arcade content")))
                    continue;

                CanvasGroup cg = t.GetComponent<CanvasGroup>();
                if (cg == null) continue;

                if (cg.alpha <= 0f && t.gameObject.activeSelf)
                {
                    Undo.RecordObject(cg, "Verdant V15 arcade visibility stabilization");
                    cg.alpha = 1f;
                    cg.interactable = true;
                    cg.blocksRaycasts = true;
                    EditorUtility.SetDirty(cg);
                    count++;
                }
            }
        }
        return count;
    }

    private static void UpdateVersionLabels(Scene scene)
    {
        foreach (Component c in Resources.FindObjectsOfTypeAll<Component>())
        {
            if (c == null || c.gameObject.scene != scene) continue;

            string typeName = c.GetType().FullName ?? "";
            if (!typeName.Contains("TMPro.TMP_Text")) continue;

            PropertyInfo textProp = c.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
            if (textProp == null || !textProp.CanRead || !textProp.CanWrite) continue;

            try
            {
                string current = textProp.GetValue(c, null) as string;
                if (string.IsNullOrEmpty(current)) continue;
                if (!current.Contains("V14.") && !current.Contains("V15.")) continue;

                textProp.SetValue(c, "V" + Version, null);
                EditorUtility.SetDirty(c);
            }
            catch { }
        }
    }

    private static object GetMember(object obj, string name)
    {
        if (obj == null) return null;
        Type t = obj.GetType();

        FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null) return f.GetValue(obj);

        PropertyInfo p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.CanRead) return p.GetValue(obj, null);

        return null;
    }

    private static bool SetMemberIfCompatible(object obj, string name, object value)
    {
        if (obj == null || value == null) return false;
        Type t = obj.GetType();

        FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (f != null && f.FieldType.IsAssignableFrom(value.GetType()))
        {
            object old = f.GetValue(obj);
            if (ReferenceEquals(old, value)) return false;
            f.SetValue(obj, value);
            return true;
        }

        PropertyInfo p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.CanWrite && p.PropertyType.IsAssignableFrom(value.GetType()))
        {
            object old = p.CanRead ? p.GetValue(obj, null) : null;
            if (ReferenceEquals(old, value)) return false;
            p.SetValue(obj, value, null);
            return true;
        }

        return false;
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

        string file = Path.GetFileNameWithoutExtension(scene.path) + " - before V15 " + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
        string backup = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + file);
        EditorSceneManager.SaveScene(scene, backup, true);
        Debug.Log("[Verdant V15] Backup created: " + backup);
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
