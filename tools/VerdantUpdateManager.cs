using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public class VerdantUpdateManager : EditorWindow
{
    private const string LocalVersionPath = "Assets/VerdantBoard/VERSION.txt";
    private const string ManifestUrl = "https://sorakaito0.github.io/verdant-media/updates/latest.json";
    private const string ReleasesUrl = "https://github.com/SoraKaito0/verdant-media/tree/main/releases";

    [Serializable]
    private class LatestManifest
    {
        public int schemaVersion;
        public string latestVersion;
        public string releaseName;
        public string published;
        public string channel;
        public string packageType;
        public string minimumPatchVersion;
        public string packageUrl;
        public string releasePageUrl;
        public string message;
        public string[] notes;
    }

    private UnityWebRequest request;
    private UnityWebRequestAsyncOperation operation;
    private LatestManifest manifest;
    private string localVersion;
    private string status = "Not checked yet.";
    private bool downloadingPackage;
    private string pendingPackagePath;
    private Vector2 scroll;

    [MenuItem("Tools/Verdant Board/Updates/Update Manager", priority = 10)]
    public static void Open()
    {
        VerdantUpdateManager window = GetWindow<VerdantUpdateManager>();
        window.titleContent = new GUIContent("Verdant Updates");
        window.minSize = new Vector2(520f, 360f);
        window.Show();
    }

    [MenuItem("Tools/Verdant Board/Updates/Check Latest Version", priority = 11)]
    public static void CheckFromMenu()
    {
        VerdantUpdateManager window = GetWindow<VerdantUpdateManager>();
        window.titleContent = new GUIContent("Verdant Updates");
        window.Show();
        window.CheckForUpdates();
    }

    private void OnEnable()
    {
        localVersion = ReadLocalVersion();
        if (manifest == null && request == null)
            CheckForUpdates();
    }

    private void OnDisable()
    {
        DisposeRequest();
    }

    private void Update()
    {
        if (operation == null || !operation.isDone) return;

        if (request.result != UnityWebRequest.Result.Success)
        {
            status = "Request failed: " + request.error;
            Debug.LogWarning("[Verdant Update Manager] " + status);
            DisposeRequest();
            Repaint();
            return;
        }

        if (downloadingPackage)
        {
            try
            {
                byte[] bytes = request.downloadHandler.data;
                string directory = Path.GetDirectoryName(pendingPackagePath);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                File.WriteAllBytes(pendingPackagePath, bytes);

                status = "Download complete. Opening Unity package import...";
                Debug.Log("[Verdant Update Manager] Downloaded update package to " + pendingPackagePath);

                DisposeRequest();
                downloadingPackage = false;
                AssetDatabase.ImportPackage(pendingPackagePath, true);
                Repaint();
                return;
            }
            catch (Exception ex)
            {
                status = "Could not save/import update: " + ex.Message;
                Debug.LogException(ex);
                DisposeRequest();
                downloadingPackage = false;
                Repaint();
                return;
            }
        }

        try
        {
            manifest = JsonUtility.FromJson<LatestManifest>(request.downloadHandler.text);
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.latestVersion))
            {
                status = "Latest-version manifest was empty or invalid.";
            }
            else
            {
                int compare = CompareVersions(localVersion, manifest.latestVersion);
                status = compare < 0
                    ? "Update available: " + manifest.latestVersion
                    : compare == 0
                        ? "Verdant is up to date."
                        : "Local version is newer than the published version.";

                Debug.Log("[Verdant Update Manager] Local " + localVersion + " | Latest " + manifest.latestVersion + " | " + status);
            }
        }
        catch (Exception ex)
        {
            status = "Could not parse update manifest: " + ex.Message;
            Debug.LogException(ex);
        }

        DisposeRequest();
        Repaint();
    }

    private void OnGUI()
    {
        localVersion = ReadLocalVersion();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Verdant Update Manager", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Checks the official Verdant update manifest and can download the latest published Unity update package.",
            EditorStyles.wordWrappedLabel
        );

        EditorGUILayout.Space(10);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Installed version", string.IsNullOrEmpty(localVersion) ? "Legacy / unknown" : localVersion);
            EditorGUILayout.LabelField("Latest published", manifest != null ? manifest.latestVersion : "Not loaded");
            if (manifest != null && !string.IsNullOrWhiteSpace(manifest.releaseName))
                EditorGUILayout.LabelField("Release", manifest.releaseName);
            if (manifest != null && !string.IsNullOrWhiteSpace(manifest.published))
                EditorGUILayout.LabelField("Published", manifest.published);
            if (manifest != null && !string.IsNullOrWhiteSpace(manifest.packageType))
                EditorGUILayout.LabelField("Package type", manifest.packageType);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(status, EditorStyles.wordWrappedLabel);
        }

        if (manifest != null && !string.IsNullOrWhiteSpace(manifest.message))
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(manifest.message, MessageType.Info);
        }

        if (manifest != null && manifest.notes != null && manifest.notes.Length > 0)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("What's new", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(90f), GUILayout.MaxHeight(150f));
            foreach (string note in manifest.notes)
            {
                if (string.IsNullOrWhiteSpace(note)) continue;
                EditorGUILayout.LabelField("• " + note, EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        if (manifest != null &&
            !string.IsNullOrWhiteSpace(manifest.minimumPatchVersion) &&
            CompareVersions(localVersion, manifest.minimumPatchVersion) < 0)
        {
            EditorGUILayout.HelpBox(
                "This project predates the minimum version for the current patch (" + manifest.minimumPatchVersion + "). " +
                "You can still open the release page, but a full/cumulative Verdant package is safer than applying only the newest patch.",
                MessageType.Warning
            );
        }

        EditorGUILayout.Space(10);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.enabled = request == null;
            if (GUILayout.Button("Check for Updates", GUILayout.Height(32f)))
                CheckForUpdates();

            GUI.enabled = manifest != null &&
                          !string.IsNullOrWhiteSpace(manifest.packageUrl) &&
                          request == null &&
                          CompareVersions(localVersion, manifest.latestVersion) < 0;

            if (GUILayout.Button("Download + Import Latest", GUILayout.Height(32f)))
                DownloadLatestPackage();

            GUI.enabled = true;
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Open Latest Release", GUILayout.Height(26f)))
            {
                string url = manifest != null && !string.IsNullOrWhiteSpace(manifest.releasePageUrl)
                    ? manifest.releasePageUrl
                    : ReleasesUrl;
                Application.OpenURL(url);
            }

            if (GUILayout.Button("Open Verdant Media Repository", GUILayout.Height(26f)))
                Application.OpenURL("https://github.com/SoraKaito0/verdant-media");
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "From V15.2 onward, keep this updater in every Verdant project. When a future update is published, this window will show the new version and offer the update package.",
            MessageType.None
        );

        GUI.enabled = true;
    }

    private void CheckForUpdates()
    {
        if (request != null) return;

        localVersion = ReadLocalVersion();
        status = "Checking latest Verdant version...";

        string url = ManifestUrl + "?t=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        request = UnityWebRequest.Get(url);
        request.timeout = 20;
        operation = request.SendWebRequest();
        downloadingPackage = false;
        Repaint();
    }

    private void DownloadLatestPackage()
    {
        if (manifest == null || string.IsNullOrWhiteSpace(manifest.packageUrl) || request != null) return;

        string fileName;
        try
        {
            Uri uri = new Uri(manifest.packageUrl);
            fileName = Path.GetFileName(uri.LocalPath);
        }
        catch
        {
            fileName = "Verdant_Update_" + manifest.latestVersion.Replace(".", "_") + ".unitypackage";
        }

        if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
            fileName = "Verdant_Update_" + manifest.latestVersion.Replace(".", "_") + ".unitypackage";

        pendingPackagePath = Path.Combine(Directory.GetCurrentDirectory(), "Library", "VerdantUpdates", fileName);
        status = "Downloading " + manifest.latestVersion + "...";

        string separator = manifest.packageUrl.Contains("?") ? "&" : "?";
        request = UnityWebRequest.Get(manifest.packageUrl + separator + "t=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        request.timeout = 120;
        operation = request.SendWebRequest();
        downloadingPackage = true;
        Repaint();
    }

    private static string ReadLocalVersion()
    {
        try
        {
            if (!File.Exists(LocalVersionPath)) return "0.0.0";
            string value = File.ReadAllText(LocalVersionPath).Trim();
            return string.IsNullOrWhiteSpace(value) ? "0.0.0" : value;
        }
        catch
        {
            return "0.0.0";
        }
    }

    private static int CompareVersions(string a, string b)
    {
        int[] av = ParseVersion(a);
        int[] bv = ParseVersion(b);

        int length = Mathf.Max(av.Length, bv.Length);
        for (int i = 0; i < length; i++)
        {
            int ai = i < av.Length ? av[i] : 0;
            int bi = i < bv.Length ? bv[i] : 0;
            if (ai != bi) return ai.CompareTo(bi);
        }

        return 0;
    }

    private static int[] ParseVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version)) return new[] { 0, 0, 0 };

        string clean = "";
        foreach (char c in version)
        {
            if (char.IsDigit(c) || c == '.') clean += c;
            else if (clean.Length > 0) break;
        }

        string[] parts = clean.Split('.');
        int[] values = new int[Mathf.Max(3, parts.Length)];

        for (int i = 0; i < parts.Length; i++)
        {
            int value;
            if (int.TryParse(parts[i], out value)) values[i] = value;
        }

        return values;
    }

    private void DisposeRequest()
    {
        if (request != null)
        {
            request.Dispose();
            request = null;
        }

        operation = null;
    }
}
