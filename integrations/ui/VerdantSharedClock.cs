using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DefaultExecutionOrder(32000)]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class VerdantSharedClock : UdonSharpBehaviour
{
    [Header("Clock displays")]
    public TMP_Text[] clockTexts;
    public Text[] legacyClockTexts;

    [Header("12 / 24 hour controls")]
    public Toggle[] formatToggles;
    public bool[] toggleOnMeans24Hour;
    public bool use24Hour = true;

    [Header("Refresh")]
    [Range(0.05f, 2f)] public float refreshInterval = 0.15f;

    private bool[] _lastToggleStates;
    private bool _toggleStateReady;
    private float _nextRefresh;

    private void Start()
    {
        InitialiseToggleState();
        RefreshClock();
    }

    private void Update()
    {
        PollFormatToggles();
    }

    private void LateUpdate()
    {
        if (Time.time < _nextRefresh) return;
        _nextRefresh = Time.time + refreshInterval;
        RefreshClock();
    }

    public void Use12Hour()
    {
        SetFormat(false);
    }

    public void Use24Hour()
    {
        SetFormat(true);
    }

    public void ToggleHourFormat()
    {
        SetFormat(!use24Hour);
    }

    private void InitialiseToggleState()
    {
        int count = formatToggles != null ? formatToggles.Length : 0;
        _lastToggleStates = new bool[count];

        for (int i = 0; i < count; i++)
        {
            Toggle toggle = formatToggles[i];
            _lastToggleStates[i] = toggle != null && toggle.isOn;
        }

        _toggleStateReady = true;
        ApplyFormatToToggles();
    }

    private void PollFormatToggles()
    {
        if (!_toggleStateReady)
            InitialiseToggleState();

        if (formatToggles == null || _lastToggleStates == null) return;

        int count = formatToggles.Length;
        if (_lastToggleStates.Length != count)
        {
            InitialiseToggleState();
            return;
        }

        for (int i = 0; i < count; i++)
        {
            Toggle toggle = formatToggles[i];
            if (toggle == null) continue;

            bool current = toggle.isOn;
            if (current == _lastToggleStates[i]) continue;

            bool onMeans24 = true;
            if (toggleOnMeans24Hour != null && i < toggleOnMeans24Hour.Length)
                onMeans24 = toggleOnMeans24Hour[i];

            bool requested24 = onMeans24 ? current : !current;
            SetFormat(requested24);
            return;
        }
    }

    private void SetFormat(bool value)
    {
        use24Hour = value;
        ApplyFormatToToggles();
        RefreshClock();
    }

    private void ApplyFormatToToggles()
    {
        if (formatToggles == null) return;

        if (_lastToggleStates == null || _lastToggleStates.Length != formatToggles.Length)
            _lastToggleStates = new bool[formatToggles.Length];

        for (int i = 0; i < formatToggles.Length; i++)
        {
            Toggle toggle = formatToggles[i];
            if (toggle == null) continue;

            bool onMeans24 = true;
            if (toggleOnMeans24Hour != null && i < toggleOnMeans24Hour.Length)
                onMeans24 = toggleOnMeans24Hour[i];

            bool desired = onMeans24 ? use24Hour : !use24Hour;

            if (toggle.isOn != desired)
                toggle.isOn = desired;

            _lastToggleStates[i] = toggle.isOn;
        }
    }

    public void RefreshClock()
    {
        DateTime now = DateTime.Now;
        int hour = now.Hour;
        int minute = now.Minute;

        string value;

        if (use24Hour)
        {
            value = TwoDigits(hour) + ":" + TwoDigits(minute);
        }
        else
        {
            int displayHour = hour % 12;
            if (displayHour == 0) displayHour = 12;

            value = displayHour.ToString() + ":" + TwoDigits(minute) + (hour >= 12 ? " PM" : " AM");
        }

        if (clockTexts != null)
        {
            for (int i = 0; i < clockTexts.Length; i++)
            {
                TMP_Text text = clockTexts[i];
                if (text != null && text.text != value)
                    text.text = value;
            }
        }

        if (legacyClockTexts != null)
        {
            for (int i = 0; i < legacyClockTexts.Length; i++)
            {
                Text text = legacyClockTexts[i];
                if (text != null && text.text != value)
                    text.text = value;
            }
        }
    }

    private string TwoDigits(int value)
    {
        if (value < 10) return "0" + value.ToString();
        return value.ToString();
    }
}
