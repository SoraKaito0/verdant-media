using UdonSharp;
using UnityEngine;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class VerdantRoleAuthority : UdonSharpBehaviour
{
    [Header("Provider state")]
    public bool providerHasData;
    public bool providerLinked;
    [Range(0, 5)] public int providerSupporterTier;
    [Range(0, 4)] public int providerStaffRank;
    public string providerSupporterRole = "";
    public string providerStaffRole = "";
    public string providerDisplayName = "";
    public string providerSource = "Fallback";

    [Header("Effective local state")]
    [Range(0, 5)] public int effectiveSupporterTier;
    [Range(0, 4)] public int effectiveStaffRank;
    public bool isSupporter;
    public bool isVIP;
    public bool isStaff;
    public bool isLinkedIdentity;
    public string effectiveRoleLabel = "Member";

    [Header("Supporter tier targets")]
    public UdonBehaviour[] supporterTierTargets;
    public string[] supporterTierVariables;

    [Header("Staff rank targets")]
    public UdonBehaviour[] staffRankTargets;
    public string[] staffRankVariables;

    [Header("Linked identity targets")]
    public UdonBehaviour[] linkedTargets;
    public string[] linkedVariables;

    [Header("Supporter boolean targets")]
    public UdonBehaviour[] supporterTargets;
    public string[] supporterVariables;

    [Header("VIP boolean targets")]
    public UdonBehaviour[] vipTargets;
    public string[] vipVariables;

    [Header("Staff boolean targets")]
    public UdonBehaviour[] staffTargets;
    public string[] staffVariables;

    [Header("Primary role / role label targets")]
    public UdonBehaviour[] roleLabelTargets;
    public string[] roleLabelVariables;

    [Header("Refresh targets")]
    public UdonBehaviour[] refreshTargets;
    public string[] refreshEvents;

    public void ApplyProviderState()
    {
        if (!providerHasData) return;

        effectiveSupporterTier = Mathf.Clamp(providerSupporterTier, 0, 5);
        effectiveStaffRank = Mathf.Clamp(providerStaffRank, 0, 4);

        isSupporter = effectiveSupporterTier > 0;
        isVIP = effectiveSupporterTier >= 3;
        isStaff = effectiveStaffRank > 0;
        isLinkedIdentity = providerLinked;

        effectiveRoleLabel = BuildRoleLabel();

        ApplyInt(supporterTierTargets, supporterTierVariables, effectiveSupporterTier);
        ApplyInt(staffRankTargets, staffRankVariables, effectiveStaffRank);
        ApplyBool(linkedTargets, linkedVariables, isLinkedIdentity);
        ApplyBool(supporterTargets, supporterVariables, isSupporter);
        ApplyBool(vipTargets, vipVariables, isVIP);
        ApplyBool(staffTargets, staffVariables, isStaff);
        ApplyString(roleLabelTargets, roleLabelVariables, effectiveRoleLabel);

        SendRefreshEvents();
    }

    // Use this only when the trusted provider explicitly loses its data after it
    // had already supplied permissions. It clears privileges instead of leaving
    // stale staff/supporter access active.
    public void FailClosed()
    {
        providerHasData = true;
        providerLinked = false;
        providerSupporterTier = 0;
        providerStaffRank = 0;
        providerSupporterRole = "";
        providerStaffRole = "";
        providerSource = "Provider unavailable";
        ApplyProviderState();
    }

    // Used while VRCLinking has not loaded at all. No existing Verdant fallback
    // state is overwritten, so the existing trusted manual/GitHub path can keep
    // working until VRCLinking successfully supplies data.
    public void MarkProviderPending()
    {
        providerHasData = false;
        providerSource = "Fallback";
    }

    public bool HasSupporterTier(int minimumTier)
    {
        return effectiveSupporterTier >= minimumTier;
    }

    public bool HasStaffRank(int minimumRank)
    {
        return effectiveStaffRank >= minimumRank;
    }

    private string BuildRoleLabel()
    {
        if (!string.IsNullOrEmpty(providerStaffRole) && !string.IsNullOrEmpty(providerSupporterRole))
            return providerSupporterRole + " + " + providerStaffRole;

        if (!string.IsNullOrEmpty(providerStaffRole))
            return providerStaffRole;

        if (!string.IsNullOrEmpty(providerSupporterRole))
            return providerSupporterRole;

        return "Member";
    }

    private void ApplyInt(UdonBehaviour[] targets, string[] variables, int value)
    {
        if (targets == null || variables == null) return;
        int count = Mathf.Min(targets.Length, variables.Length);

        for (int i = 0; i < count; i++)
        {
            UdonBehaviour target = targets[i];
            string variableName = variables[i];
            if (target == null || string.IsNullOrEmpty(variableName)) continue;
            target.SetProgramVariable(variableName, value);
        }
    }

    private void ApplyBool(UdonBehaviour[] targets, string[] variables, bool value)
    {
        if (targets == null || variables == null) return;
        int count = Mathf.Min(targets.Length, variables.Length);

        for (int i = 0; i < count; i++)
        {
            UdonBehaviour target = targets[i];
            string variableName = variables[i];
            if (target == null || string.IsNullOrEmpty(variableName)) continue;
            target.SetProgramVariable(variableName, value);
        }
    }

    private void ApplyString(UdonBehaviour[] targets, string[] variables, string value)
    {
        if (targets == null || variables == null) return;
        int count = Mathf.Min(targets.Length, variables.Length);

        for (int i = 0; i < count; i++)
        {
            UdonBehaviour target = targets[i];
            string variableName = variables[i];
            if (target == null || string.IsNullOrEmpty(variableName)) continue;
            target.SetProgramVariable(variableName, value);
        }
    }

    private void SendRefreshEvents()
    {
        if (refreshTargets == null || refreshEvents == null) return;
        int count = Mathf.Min(refreshTargets.Length, refreshEvents.Length);

        for (int i = 0; i < count; i++)
        {
            UdonBehaviour target = refreshTargets[i];
            string eventName = refreshEvents[i];
            if (target == null || string.IsNullOrEmpty(eventName)) continue;
            target.SendCustomEvent(eventName);
        }
    }
}
