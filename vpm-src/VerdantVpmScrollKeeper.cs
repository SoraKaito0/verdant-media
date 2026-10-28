using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(32000)]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class VerdantVpmScrollKeeper : UdonSharpBehaviour
{
    public ScrollRect scrollRect;

    [Range(0.05f, 0.5f)] public float resetJumpThreshold = 0.12f;
    [Range(0.9f, 1f)] public float topThreshold = 0.995f;
    [Range(0.1f, 1f)] public float restoreCooldownSeconds = 0.45f;

    private bool ready;
    private bool restorePending;
    private float saved = 1f;
    private float lastObserved = 1f;
    private float lastHeight;
    private float lastRestoreTime = -10f;

    private void Start()
    {
        Initialise();
    }

    private void Update()
    {
        if (!ready) Initialise();
        if (scrollRect == null || scrollRect.content == null) return;

        float current = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
        float height = scrollRect.content.rect.height;
        bool moving = scrollRect.velocity.sqrMagnitude > 0.0001f;
        bool heightChanged = Mathf.Abs(height - lastHeight) > 0.5f;

        if (moving)
        {
            saved = current;
            lastObserved = current;
            lastHeight = height;
            return;
        }

        if (heightChanged)
        {
            lastHeight = height;

            if (saved < topThreshold)
                ScheduleRestore();

            return;
        }

        bool suspiciousTopReset =
            Time.time - lastRestoreTime >= restoreCooldownSeconds &&
            current >= topThreshold &&
            saved < topThreshold &&
            lastObserved < topThreshold &&
            current - saved >= resetJumpThreshold;

        if (suspiciousTopReset)
        {
            ScheduleRestore();
            return;
        }

        if (current < topThreshold)
            saved = current;

        lastObserved = current;
    }

    public void RestoreAfterLayout()
    {
        restorePending = false;

        if (scrollRect == null || scrollRect.content == null)
            return;

        float current = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);

        if (saved < topThreshold && current >= topThreshold)
        {
            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = saved;
            lastObserved = saved;
            lastRestoreTime = Time.time;
        }
    }

    private void ScheduleRestore()
    {
        if (restorePending) return;
        restorePending = true;
        SendCustomEventDelayedFrames("RestoreAfterLayout", 2);
    }

    private void Initialise()
    {
        if (scrollRect == null)
            scrollRect = GetComponent<ScrollRect>();

        if (scrollRect == null)
            return;

        saved = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
        lastObserved = saved;

        if (scrollRect.content != null)
            lastHeight = scrollRect.content.rect.height;

        ready = true;
    }
}
