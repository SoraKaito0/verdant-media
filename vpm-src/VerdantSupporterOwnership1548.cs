using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(32000)]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class VerdantSupporterOwnership1548 : UdonSharpBehaviour
{
    public ScrollRect primaryScroll;
    public GameObject[] legacyRoots;

    [Range(0.05f, 1f)] public float checkInterval = 0.15f;
    [Range(0.9f, 1f)] public float topThreshold = 0.995f;

    private float nextCheck;
    private float savedPosition = 1f;
    private bool ready;

    private void Start()
    {
        DisableLegacyRoots();
        Capture();
    }

    private void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + checkInterval;

        DisableLegacyRoots();

        if (!ready)
            Capture();

        if (primaryScroll == null)
            return;

        float current = Mathf.Clamp01(primaryScroll.verticalNormalizedPosition);
        bool moving = primaryScroll.velocity.sqrMagnitude > 0.0001f;

        if (moving || current < topThreshold)
        {
            savedPosition = current;
            return;
        }

        if (savedPosition < topThreshold && current >= topThreshold)
        {
            primaryScroll.StopMovement();
            primaryScroll.verticalNormalizedPosition = savedPosition;
        }
    }

    public void DisableLegacyRoots()
    {
        if (legacyRoots == null) return;

        for (int i = 0; i < legacyRoots.Length; i++)
        {
            GameObject root = legacyRoots[i];
            if (root != null && root.activeSelf)
                root.SetActive(false);
        }
    }

    public void Capture()
    {
        if (primaryScroll == null)
            return;

        savedPosition = Mathf.Clamp01(primaryScroll.verticalNormalizedPosition);
        ready = true;
    }
}
