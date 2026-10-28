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

    public TMP_Dropdown[] tmpFormatDropdowns;
    public int[] tmpDropdown12Values;
    public int[] tmpDropdown24Values;

    public Dropdown[] legacyFormatDropdowns;
    public int[] legacyDropdown12Values;
    public int[] legacyDropdown24Values;

    public bool use24Hour = true;
    [Range(0.05f, 1f)] public float refreshInterval = 0.1f;

    private bool[] lastToggleStates;
    private int[] lastTmpDropdownValues;
    private int[] lastLegacyDropdownValues;
    private float nextRefresh;

    private void Start()
    {
        CacheStates();
        RefreshClock();
    }

    private void Update()
    {
        PollToggles();
        PollTmpDropdowns();
        PollLegacyDropdowns();
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

    private void CacheStates()
    {
        int toggleCount = formatToggles != null ? formatToggles.Length : 0;
        lastToggleStates = new bool[toggleCount];

        for (int i = 0; i < toggleCount; i++)
            lastToggleStates[i] = formatToggles[i] != null && formatToggles[i].isOn;

        int tmpCount = tmpFormatDropdowns != null ? tmpFormatDropdowns.Length : 0;
        lastTmpDropdownValues = new int[tmpCount];

        for (int i = 0; i < tmpCount; i++)
            lastTmpDropdownValues[i] = tmpFormatDropdowns[i] != null ? tmpFormatDropdowns[i].value : -1;

        int legacyCount = legacyFormatDropdowns != null ? legacyFormatDropdowns.Length : 0;
        lastLegacyDropdownValues = new int[legacyCount];

        for (int i = 0; i < legacyCount; i++)
            lastLegacyDropdownValues[i] = legacyFormatDropdowns[i] != null ? legacyFormatDropdowns[i].value : -1;
    }

    private void PollToggles()
    {
        if (formatToggles == null) return;

        if (lastToggleStates == null || lastToggleStates.Length != formatToggles.Length)
            CacheStates();

        for (int i = 0; i < formatToggles.Length; i++)
        {
            Toggle toggle = formatToggles[i];
            if (toggle == null) continue;

            bool current = toggle.isOn;
            if (current == lastToggleStates[i]) continue;

            bool onMeans24 = toggleOnMeans24Hour == null ||
                             i >= toggleOnMeans24Hour.Length ||
                             toggleOnMeans24Hour[i];

            lastToggleStates[i] = current;
            SetFormat(onMeans24 ? current : !current);
            return;
        }
    }

    private void PollTmpDropdowns()
    {
        if (tmpFormatDropdowns == null) return;

        if (lastTmpDropdownValues == null || lastTmpDropdownValues.Length != tmpFormatDropdowns.Length)
            CacheStates();

        for (int i = 0; i < tmpFormatDropdowns.Length; i++)
        {
            TMP_Dropdown dropdown = tmpFormatDropdowns[i];
            if (dropdown == null) continue;

            int current = dropdown.value;
            if (current == lastTmpDropdownValues[i]) continue;

            lastTmpDropdownValues[i] = current;

            if (tmpDropdown24Values != null && i < tmpDropdown24Values.Length && current == tmpDropdown24Values[i])
            {
                SetFormat(true);
                return;
            }

            if (tmpDropdown12Values != null && i < tmpDropdown12Values.Length && current == tmpDropdown12Values[i])
            {
                SetFormat(false);
                return;
            }
        }
    }

    private void PollLegacyDropdowns()
    {
        if (legacyFormatDropdowns == null) return;

        if (lastLegacyDropdownValues == null || lastLegacyDropdownValues.Length != legacyFormatDropdowns.Length)
            CacheStates();

        for (int i = 0; i < legacyFormatDropdowns.Length; i++)
        {
            Dropdown dropdown = legacyFormatDropdowns[i];
            if (dropdown == null) continue;

            int current = dropdown.value;
            if (current == lastLegacyDropdownValues[i]) continue;

            lastLegacyDropdownValues[i] = current;

            if (legacyDropdown24Values != null && i < legacyDropdown24Values.Length && current == legacyDropdown24Values[i])
            {
                SetFormat(true);
                return;
            }

            if (legacyDropdown12Values != null && i < legacyDropdown12Values.Length && current == legacyDropdown12Values[i])
            {
                SetFormat(false);
                return;
            }
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
            string h = hour < 10 ? "0" + hour.ToString() : hour.ToString();
            value = h + ":" + minute;
        }
        else
        {
            int h = hour % 12;
            if (h == 0) h = 12;
            value = h.ToString() + ":" + minute + (hour >= 12 ? " PM" : " AM");
        }

        if (tmpClockTexts != null)
        {
            for (int i = 0; i < tmpClockTexts.Length; i++)
                if (tmpClockTexts[i] != null && tmpClockTexts[i].text != value)
                    tmpClockTexts[i].text = value;
        }

        if (legacyClockTexts != null)
        {
            for (int i = 0; i < legacyClockTexts.Length; i++)
                if (legacyClockTexts[i] != null && legacyClockTexts[i].text != value)
                    legacyClockTexts[i].text = value;
        }
    }
}
