using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DefaultExecutionOrder(32000)]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class VerdantVpmSharedClock : UdonSharpBehaviour
{
    public TMP_Text[] tmpClockTexts;
    public Text[] legacyClockTexts;
    public Toggle[] formatToggles;
    public bool[] toggleOnMeans24Hour;
    public bool use24Hour = true;
    [Range(0.05f, 2f)] public float refreshInterval = 0.15f;

    private bool[] lastToggleStates;
    private float nextRefresh;

    private void Start()
    {
        CacheToggleStates();
        ApplyToggleState();
        RefreshClock();
    }

    private void Update()
    {
        PollToggles();
    }

    private void LateUpdate()
    {
        if (Time.time < nextRefresh) return;
        nextRefresh = Time.time + refreshInterval;
        RefreshClock();
    }

    public void Use12Hour() { SetFormat(false); }
    public void Use24Hour() { SetFormat(true); }
    public void ToggleHourFormat() { SetFormat(!use24Hour); }

    private void SetFormat(bool value)
    {
        use24Hour = value;
        ApplyToggleState();
        RefreshClock();
    }

    private void CacheToggleStates()
    {
        int count = formatToggles != null ? formatToggles.Length : 0;
        lastToggleStates = new bool[count];
        for (int i = 0; i < count; i++)
            lastToggleStates[i] = formatToggles[i] != null && formatToggles[i].isOn;
    }

    private void PollToggles()
    {
        if (formatToggles == null) return;
        if (lastToggleStates == null || lastToggleStates.Length != formatToggles.Length)
            CacheToggleStates();

        for (int i = 0; i < formatToggles.Length; i++)
        {
            Toggle toggle = formatToggles[i];
            if (toggle == null) continue;

            bool current = toggle.isOn;
            if (current == lastToggleStates[i]) continue;

            bool onMeans24 = toggleOnMeans24Hour == null ||
                             i >= toggleOnMeans24Hour.Length ||
                             toggleOnMeans24Hour[i];

            SetFormat(onMeans24 ? current : !current);
            return;
        }
    }

    private void ApplyToggleState()
    {
        if (formatToggles == null) return;
        if (lastToggleStates == null || lastToggleStates.Length != formatToggles.Length)
            lastToggleStates = new bool[formatToggles.Length];

        for (int i = 0; i < formatToggles.Length; i++)
        {
            Toggle toggle = formatToggles[i];
            if (toggle == null) continue;

            bool onMeans24 = toggleOnMeans24Hour == null ||
                             i >= toggleOnMeans24Hour.Length ||
                             toggleOnMeans24Hour[i];

            bool desired = onMeans24 ? use24Hour : !use24Hour;
            if (toggle.isOn != desired) toggle.isOn = desired;
            lastToggleStates[i] = toggle.isOn;
        }
    }

    public void RefreshClock()
    {
        DateTime now = DateTime.Now;
        int hour = now.Hour;
        string minute = now.Minute < 10 ? "0" + now.Minute.ToString() : now.Minute.ToString();
        string value;

        if (use24Hour)
        {
            string hourText = hour < 10 ? "0" + hour.ToString() : hour.ToString();
            value = hourText + ":" + minute;
        }
        else
        {
            int h = hour % 12;
            if (h == 0) h = 12;
            value = h.ToString() + ":" + minute + (hour >= 12 ? " PM" : " AM");
        }

        if (tmpClockTexts != null)
            for (int i = 0; i < tmpClockTexts.Length; i++)
                if (tmpClockTexts[i] != null) tmpClockTexts[i].text = value;

        if (legacyClockTexts != null)
            for (int i = 0; i < legacyClockTexts.Length; i++)
                if (legacyClockTexts[i] != null) legacyClockTexts[i].text = value;
    }
}
