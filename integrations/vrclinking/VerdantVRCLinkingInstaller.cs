using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.Udon;
using UdonSharp;
using UdonSharpEditor;

public static class VerdantVRCLinkingInstaller
{
    private const string GeneratedFolder = "Assets/VerdantBoard/Generated/VRCLinking";
    private const string GeneratedBridgePath = GeneratedFolder + "/VerdantVRCLinkingBridge.generated.cs";
    private const string RootName = "Verdant VRCLinking";
    private const string AuthorityName = "Verdant Role Authority";
    private const string BridgeName = "Verdant VRCLinking Bridge";
    private const string DownloaderName = "VRCLinking Downloader";
    private const string RepoUrl = "https://vpm.vrclinking.com/index.json";

    [MenuItem("Tools/Verdant Board/Integrations/VRCLinking/Open SDK Install Page")]
    public static void OpenSdkPage()
    {
        Application.OpenURL("https://vrclinking.com/vpm-listing");
    }

    [MenuItem("Tools/Verdant Board/Integrations/VRCLinking/Copy VPM Repository URL")]
    public static void CopyRepoUrl()
    {
        EditorGUIUtility.systemCopyBuffer = RepoUrl;
        Debug.Log("[Verdant VRCLinking] Copied VPM repository URL: " + RepoUrl);
    }

    [MenuItem("Tools/Verdant Board/Integrations/VRCLinking/Setup Hybrid Bridge")]
    public static void Setup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            EditorUtility.DisplayDialog("Verdant VRCLinking", "Leave Play Mode and wait for Unity to finish compiling before running the installer.", "OK");
            return;
        }

        Type baseType = FindType("VrcLinkingModuleBase");
        Type downloaderType = FindType("VRCLinkingDownloader");

        if (baseType == null || downloaderType == null)
        {
            EditorUtility.DisplayDialog(
                "VRCLinking SDK required",
                "Verdant could not find VrcLinkingModuleBase / VRCLinkingDownloader.\n\n" +
                "Install the VRCLinking SDK through VCC first.\n\nRepository:\n" + RepoUrl +
                "\n\nAfter Unity finishes compiling, run Setup Hybrid Bridge again.",
                "Open VRCLinking",
                "Close"
            );

            Application.OpenURL("https://vrclinking.com/vpm-listing");
            return;
        }

        MethodInfo formattedMethod = FindFormattedRoleMethod(downloaderType);
        if (formattedMethod == null)
        {
            LogAvailableRoleMethods(downloaderType);
            EditorUtility.DisplayDialog(
                "VRCLinking API changed",
                "The SDK is installed, but Verdant could not find a supported TryGetFormattedGuildMembersByRoleName overload.\n\n" +
                "The available VRCLinking role methods were written to the Console. No scene changes were made.",
                "OK"
            );
            return;
        }

        EnsureFolder(GeneratedFolder);

        Type generatedBridgeType = FindType("VerdantVRCLinkingBridge");
        if (generatedBridgeType == null)
        {
            string source = GenerateBridgeSource(baseType, formattedMethod);
            File.WriteAllText(GeneratedBridgePath, source);
            AssetDatabase.ImportAsset(GeneratedBridgePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Verdant VRCLinking bridge generated",
                "The bridge source has been generated for the exact VRCLinking SDK API installed in this project.\n\n" +
                "Let Unity/UdonSharp finish compiling, then run:\n\nTools > Verdant Board > Integrations > VRCLinking > Setup Hybrid Bridge\n\none more time to wire it into the scene.",
                "OK"
            );
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("Verdant VRCLinking", "No valid loaded scene was found.", "OK");
            return;
        }

        CreateBackup(scene);

        GameObject root = FindSceneObject(scene, RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Verdant VRCLinking");
        }

        VerdantRoleAuthority authority = FindSceneComponent<VerdantRoleAuthority>(scene);
        if (authority == null)
        {
            GameObject authorityGo = new GameObject(AuthorityName);
            Undo.RegisterCreatedObjectUndo(authorityGo, "Create Verdant Role Authority");
            authorityGo.transform.SetParent(root.transform, false);
            authority = authorityGo.AddUdonSharpComponent<VerdantRoleAuthority>();
        }

        Component downloader = FindSceneComponent(scene, downloaderType);
        if (downloader == null)
        {
            GameObject downloaderGo = new GameObject(DownloaderName);
            Undo.RegisterCreatedObjectUndo(downloaderGo, "Create VRCLinking Downloader");
            downloaderGo.transform.SetParent(root.transform, false);
            downloader = Undo.AddComponent(downloaderGo, downloaderType);
        }
        else if (downloader.transform.parent == null && downloader.gameObject.name == DownloaderName)
        {
            downloader.transform.SetParent(root.transform, true);
        }

        Component bridge = FindSceneComponent(scene, generatedBridgeType);
        if (bridge == null)
        {
            GameObject bridgeGo = new GameObject(BridgeName);
            Undo.RegisterCreatedObjectUndo(bridgeGo, "Create Verdant VRCLinking Bridge");
            bridgeGo.transform.SetParent(root.transform, false);
            bridge = Undo.AddComponent(bridgeGo, generatedBridgeType);
        }

        ConfigureBridge(bridge, authority);
        AutoWireVerdantTargets(scene, authority);

        EditorUtility.SetDirty(authority);
        EditorUtility.SetDirty(downloader);
        EditorUtility.SetDirty(bridge);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!string.IsNullOrEmpty(scene.path)) EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = downloader.gameObject;

        EditorUtility.DisplayDialog(
            "Verdant VRCLinking hybrid bridge installed",
            "Verdant is now wired for the hybrid role setup.\n\n" +
            "NEXT IN THE VRCLinking DOWNLOADER INSPECTOR:\n" +
            "1. Sign in to VRCLinking.\n" +
            "2. Select your Discord server and Verdant world.\n" +
            "3. In World Sync, use ALWAYS ENCODE ROLES.\n" +
            "4. Encode: Member, Basic, Supporter, VIP, Elite, Guardian, Moderator, Admin, Manager, Owner.\n\n" +
            "Verdant Bot/manual data remains the fallback until VRCLinking successfully loads. Once VRCLinking loads, its result becomes authoritative for the encoded roles.",
            "OK"
        );
    }

    [MenuItem("Tools/Verdant Board/Integrations/VRCLinking/Validate Hybrid Bridge")]
    public static void ValidateBridge()
    {
        Scene scene = SceneManager.GetActiveScene();
        Type baseType = FindType("VrcLinkingModuleBase");
        Type downloaderType = FindType("VRCLinkingDownloader");
        Type bridgeType = FindType("VerdantVRCLinkingBridge");

        VerdantRoleAuthority authority = scene.IsValid() ? FindSceneComponent<VerdantRoleAuthority>(scene) : null;
        Component downloader = scene.IsValid() && downloaderType != null ? FindSceneComponent(scene, downloaderType) : null;
        Component bridge = scene.IsValid() && bridgeType != null ? FindSceneComponent(scene, bridgeType) : null;

        string report =
            "SDK base: " + (baseType != null ? baseType.FullName : "MISSING") + "\n" +
            "Downloader: " + (downloader != null ? HierarchyPath(downloader.transform) : "MISSING / NOT CONFIGURED") + "\n" +
            "Generated bridge: " + (bridge != null ? HierarchyPath(bridge.transform) : "MISSING") + "\n" +
            "Role authority: " + (authority != null ? HierarchyPath(authority.transform) : "MISSING") + "\n" +
            "Tier target bindings: " + Count(authority != null ? authority.supporterTierTargets : null) + "\n" +
            "Staff target bindings: " + Count(authority != null ? authority.staffRankTargets : null) + "\n" +
            "Linked target bindings: " + Count(authority != null ? authority.linkedTargets : null) + "\n" +
            "Refresh bindings: " + Count(authority != null ? authority.refreshTargets : null);

        Debug.Log("[Verdant VRCLinking] " + report.Replace("\n", " | "));
        EditorUtility.DisplayDialog("Verdant VRCLinking validation", report, "OK");
    }

    private static MethodInfo FindFormattedRoleMethod(Type downloaderType)
    {
        MethodInfo[] methods = downloaderType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.Name == "TryGetFormattedGuildMembersByRoleName")
            .OrderBy(m => m.GetParameters().Length)
            .ToArray();

        foreach (MethodInfo method in methods)
        {
            ParameterInfo[] p = method.GetParameters();

            if (method.ReturnType == typeof(string) &&
                p.Length == 1 &&
                p[0].ParameterType == typeof(string))
                return method;

            if ((method.ReturnType == typeof(bool) || method.ReturnType == typeof(void)) &&
                p.Length == 2 &&
                p[0].ParameterType == typeof(string) &&
                p[1].IsOut &&
                p[1].ParameterType.GetElementType() == typeof(string))
                return method;
        }

        return null;
    }

    private static string GenerateBridgeSource(Type baseType, MethodInfo method)
    {
        string baseName = "global::" + baseType.FullName.Replace("+", ".");
        string getter = GenerateRoleGetter(method);

        return @"// AUTO-GENERATED by VerdantVRCLinkingInstaller.
// Do not hand-edit this file; regenerate it after a VRCLinking SDK API update.

using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class VerdantVRCLinkingBridge : " + baseName + @"
{
    public VerdantRoleAuthority authority;

    [Header(""Discord role names - supporter hierarchy"")]
    public string memberRole = ""Member"";
    public string basicRole = ""Basic"";
    public string supporterRole = ""Supporter"";
    public string vipRole = ""VIP"";
    public string eliteRole = ""Elite"";
    public string guardianRole = ""Guardian"";

    [Header(""Discord role names - staff hierarchy"")]
    public string moderatorRole = ""Moderator"";
    public string adminRole = ""Admin"";
    public string managerRole = ""Manager"";
    public string ownerRole = ""Owner"";

    public override void OnDataLoaded()
    {
        RefreshLocalRoleState();
    }

    public void RefreshLocalRoleState()
    {
        if (authority == null || Networking.LocalPlayer == null) return;

        string displayName = Networking.LocalPlayer.displayName;

        int tier = 0;
        string supporterLabel = """";

        if (IsRoleMember(basicRole, displayName)) { tier = 1; supporterLabel = ""Basic""; }
        if (IsRoleMember(supporterRole, displayName)) { tier = 2; supporterLabel = ""Supporter""; }
        if (IsRoleMember(vipRole, displayName)) { tier = 3; supporterLabel = ""VIP""; }
        if (IsRoleMember(eliteRole, displayName)) { tier = 4; supporterLabel = ""Elite""; }
        if (IsRoleMember(guardianRole, displayName)) { tier = 5; supporterLabel = ""Guardian""; }

        int staff = 0;
        string staffLabel = """";

        if (IsRoleMember(moderatorRole, displayName)) { staff = 1; staffLabel = ""Moderator""; }
        if (IsRoleMember(adminRole, displayName)) { staff = 2; staffLabel = ""Admin""; }
        if (IsRoleMember(managerRole, displayName)) { staff = 3; staffLabel = ""Manager""; }
        if (IsRoleMember(ownerRole, displayName)) { staff = 4; staffLabel = ""Owner""; }

        bool member = !string.IsNullOrEmpty(memberRole) && IsRoleMember(memberRole, displayName);
        bool recognized = member || tier > 0 || staff > 0;

        authority.providerHasData = true;
        authority.providerLinked = recognized;
        authority.providerSupporterTier = tier;
        authority.providerStaffRank = staff;
        authority.providerSupporterRole = supporterLabel;
        authority.providerStaffRole = staffLabel;
        authority.providerDisplayName = displayName;
        authority.providerSource = ""VRCLinking"";
        authority.ApplyProviderState();
    }

    private bool IsRoleMember(string roleName, string displayName)
    {
        if (string.IsNullOrEmpty(roleName) || string.IsNullOrEmpty(displayName)) return false;

        string formatted = GetFormattedMembers(roleName);
        if (string.IsNullOrEmpty(formatted)) return false;

        string list = formatted.ToLower();
        string name = displayName.ToLower();

        list = list.Replace(""\r"", ""\n"");
        list = list.Replace("","", ""\n"");
        list = list.Replace("";"", ""\n"");
        list = list.Replace(""|"", ""\n"");
        list = list.Replace(""• "", """");
        list = list.Replace(""* "", """");
        list = list.Replace(""- "", """");

        string padded = ""\n"" + list + ""\n"";
        string exact = ""\n"" + name + ""\n"";

        if (padded.Contains(exact)) return true;

        // Some VRCLinking formatting includes a short prefix before each name.
        if (padded.Contains("" "" + name + ""\n"")) return true;
        if (padded.Contains(""\t"" + name + ""\n"")) return true;

        return list == name;
    }

" + getter + @"
}
";
    }

    private static string GenerateRoleGetter(MethodInfo method)
    {
        ParameterInfo[] p = method.GetParameters();
        string methodName = method.Name;

        if (method.ReturnType == typeof(string) && p.Length == 1)
        {
            return @"    private string GetFormattedMembers(string roleName)
    {
        return downloader." + methodName + @"(roleName);
    }
";
        }

        if (method.ReturnType == typeof(bool))
        {
            return @"    private string GetFormattedMembers(string roleName)
    {
        string formatted;
        bool ok = downloader." + methodName + @"(roleName, out formatted);
        return ok ? formatted : """";
    }
";
        }

        return @"    private string GetFormattedMembers(string roleName)
    {
        string formatted;
        downloader." + methodName + @"(roleName, out formatted);
        return formatted;
    }
";
    }

    private static void ConfigureBridge(Component bridge, VerdantRoleAuthority authority)
    {
        SerializedObject so = new SerializedObject(bridge);
        SerializedProperty authorityProp = so.FindProperty("authority");
        if (authorityProp != null) authorityProp.objectReferenceValue = authority;

        SetString(so, "memberRole", "Member");
        SetString(so, "basicRole", "Basic");
        SetString(so, "supporterRole", "Supporter");
        SetString(so, "vipRole", "VIP");
        SetString(so, "eliteRole", "Elite");
        SetString(so, "guardianRole", "Guardian");
        SetString(so, "moderatorRole", "Moderator");
        SetString(so, "adminRole", "Admin");
        SetString(so, "managerRole", "Manager");
        SetString(so, "ownerRole", "Owner");

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AutoWireVerdantTargets(Scene scene, VerdantRoleAuthority authority)
    {
        List<UdonBehaviour> tierTargets = new List<UdonBehaviour>();
        List<string> tierVars = new List<string>();
        List<UdonBehaviour> staffRankTargets = new List<UdonBehaviour>();
        List<string> staffRankVars = new List<string>();
        List<UdonBehaviour> linkedTargets = new List<UdonBehaviour>();
        List<string> linkedVars = new List<string>();
        List<UdonBehaviour> supporterTargets = new List<UdonBehaviour>();
        List<string> supporterVars = new List<string>();
        List<UdonBehaviour> vipTargets = new List<UdonBehaviour>();
        List<string> vipVars = new List<string>();
        List<UdonBehaviour> staffTargets = new List<UdonBehaviour>();
        List<string> staffVars = new List<string>();
        List<UdonBehaviour> roleLabelTargets = new List<UdonBehaviour>();
        List<string> roleLabelVars = new List<string>();
        List<UdonBehaviour> refreshTargets = new List<UdonBehaviour>();
        List<string> refreshEvents = new List<string>();

        foreach (UdonSharpBehaviour proxy in Resources.FindObjectsOfTypeAll<UdonSharpBehaviour>())
        {
            if (proxy == null || proxy.gameObject.scene != scene || proxy == authority) continue;

            Type type = proxy.GetType();
            string typeName = type.Name.ToLowerInvariant();
            if (!typeName.Contains("verdant")) continue;

            UdonBehaviour backing = UdonSharpEditorUtility.GetBackingUdonBehaviour(proxy);
            if (backing == null) continue;

            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);

            foreach (FieldInfo field in fields)
            {
                string n = field.Name.ToLowerInvariant();

                if (field.FieldType == typeof(int))
                {
                    if (IsSupporterTierField(typeName, n))
                    {
                        tierTargets.Add(backing);
                        tierVars.Add(field.Name);
                    }
                    else if (IsStaffRankField(typeName, n))
                    {
                        staffRankTargets.Add(backing);
                        staffRankVars.Add(field.Name);
                    }
                }
                else if (field.FieldType == typeof(bool))
                {
                    if (n == "islinked" || n == "linkedidentity" || n == "islinkedidentity" || n == "haslinkedidentity")
                    {
                        linkedTargets.Add(backing);
                        linkedVars.Add(field.Name);
                    }
                    else if (n == "issupporter" || n == "hassupporter" || n == "supporteraccess")
                    {
                        supporterTargets.Add(backing);
                        supporterVars.Add(field.Name);
                    }
                    else if (n == "isvip" || n == "hasvip" || n == "vipaccess")
                    {
                        vipTargets.Add(backing);
                        vipVars.Add(field.Name);
                    }
                    else if (n == "isstaff" || n == "hasstaff" || n == "staffaccess")
                    {
                        staffTargets.Add(backing);
                        staffVars.Add(field.Name);
                    }
                }
                else if (field.FieldType == typeof(string))
                {
                    if (n == "primaryrole" || n == "rolelabel" || n == "currentrole")
                    {
                        roleLabelTargets.Add(backing);
                        roleLabelVars.Add(field.Name);
                    }
                }
            }

            string refresh = FindRefreshMethod(type);
            if (!string.IsNullOrEmpty(refresh))
            {
                refreshTargets.Add(backing);
                refreshEvents.Add(refresh);
            }
        }

        authority.supporterTierTargets = tierTargets.ToArray();
        authority.supporterTierVariables = tierVars.ToArray();
        authority.staffRankTargets = staffRankTargets.ToArray();
        authority.staffRankVariables = staffRankVars.ToArray();
        authority.linkedTargets = linkedTargets.ToArray();
        authority.linkedVariables = linkedVars.ToArray();
        authority.supporterTargets = supporterTargets.ToArray();
        authority.supporterVariables = supporterVars.ToArray();
        authority.vipTargets = vipTargets.ToArray();
        authority.vipVariables = vipVars.ToArray();
        authority.staffTargets = staffTargets.ToArray();
        authority.staffVariables = staffVars.ToArray();
        authority.roleLabelTargets = roleLabelTargets.ToArray();
        authority.roleLabelVariables = roleLabelVars.ToArray();
        authority.refreshTargets = refreshTargets.ToArray();
        authority.refreshEvents = refreshEvents.ToArray();

        Debug.Log(
            "[Verdant VRCLinking] Auto-wired " +
            tierTargets.Count + " supporter tier variables, " +
            staffRankTargets.Count + " staff rank variables, " +
            linkedTargets.Count + " linked identity variables and " +
            refreshTargets.Count + " refresh events."
        );
    }

    private static bool IsSupporterTierField(string typeName, string fieldName)
    {
        if (!fieldName.Contains("tier")) return false;
        return fieldName.Contains("support") ||
               fieldName.Contains("effective") ||
               typeName.Contains("supporter") ||
               typeName.Contains("communitylink") ||
               typeName.Contains("rolemanager");
    }

    private static bool IsStaffRankField(string typeName, string fieldName)
    {
        if (fieldName.Contains("staff") && (fieldName.Contains("rank") || fieldName.Contains("level") || fieldName.Contains("role")))
            return true;

        if ((typeName.Contains("staff") || typeName.Contains("rolemanager")) &&
            (fieldName == "rank" || fieldName == "rolelevel"))
            return true;

        return false;
    }

    private static string FindRefreshMethod(Type type)
    {
        string[] preferred =
        {
            "RefreshPermissions",
            "RefreshRoles",
            "RefreshRole",
            "RefreshSupporter",
            "ApplyRoleState",
            "ApplyLocalState",
            "RefreshUI",
            "Refresh"
        };

        MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public);
        foreach (string name in preferred)
        {
            MethodInfo method = methods.FirstOrDefault(m => m.Name == name && m.GetParameters().Length == 0);
            if (method != null) return method.Name;
        }

        return "";
    }

    private static Type FindType(string simpleName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
            catch { continue; }

            foreach (Type type in types)
            {
                if (type != null && type.Name == simpleName)
                    return type;
            }
        }

        return null;
    }

    private static void LogAvailableRoleMethods(Type downloaderType)
    {
        foreach (MethodInfo method in downloaderType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (!method.Name.Contains("GuildMembersByRole")) continue;

            string signature = method.ReturnType.Name + " " + method.Name + "(" +
                               string.Join(", ", method.GetParameters().Select(p =>
                                   (p.IsOut ? "out " : "") + p.ParameterType.Name + " " + p.Name)) + ")";

            Debug.Log("[Verdant VRCLinking] API method: " + signature);
        }
    }

    private static T FindSceneComponent<T>(Scene scene) where T : Component
    {
        return Resources.FindObjectsOfTypeAll<T>()
            .FirstOrDefault(c => c != null && c.gameObject.scene == scene);
    }

    private static Component FindSceneComponent(Scene scene, Type type)
    {
        foreach (Component c in Resources.FindObjectsOfTypeAll<Component>())
        {
            if (c != null && c.gameObject.scene == scene && type.IsAssignableFrom(c.GetType()))
                return c;
        }

        return null;
    }

    private static GameObject FindSceneObject(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t.gameObject;
            }
        }

        return null;
    }

    private static void SetString(SerializedObject so, string name, string value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null && p.propertyType == SerializedPropertyType.String)
            p.stringValue = value;
    }

    private static int Count(Array array)
    {
        return array != null ? array.Length : 0;
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

        string folder = "Assets/VerdantBoard/Generated/VRCLinking Backups";
        EnsureFolder(folder);

        string name = Path.GetFileNameWithoutExtension(scene.path) +
                      " - before VRCLinking " + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
        string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + name);

        EditorSceneManager.SaveScene(scene, path, true);
        Debug.Log("[Verdant VRCLinking] Scene backup created: " + path);
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
