using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(32000)]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class VerdantVpmScrollKeeper : UdonSharpBehaviour
{
    public ScrollRect scrollRect;

    [Header("Smooth preservation")]
    [Range(0.05f, 0.5f)] public float resetJumpThreshold = 0.12f;
    [Range(0.9f, 1f)] public float topThreshold = 0.995f;
    [Range(0.1f, 1f)] public float userGraceSeconds = 0.35f;
    [Range(0.1f, 1f)] public float restoreCooldownSeconds = 0.45f;

    private bool ready;
    private bool restorePending;
    private float saved = 1f;
    private float lastObserved = 1f;
    private float lastHeight;
    private float userGraceUntil;
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
        bool userActive = IsUserInteracting();

        if (userActive)
        {
            saved = current;
            lastObserved = current;
            userGraceUntil = Time.time + userGraceSeconds;
            return;
        }

        float height = scrollRect.content.rect.height;
        bool heightChanged = Mathf.Abs(height - lastHeight) > 0.5f;

        if (heightChanged)
        {
            lastHeight = height;

            if (saved < 0.995f && Time.time >= userGraceUntil)
                ScheduleRestore();

            return;
        }

        bool suspiciousTopReset =
            Time.time >= userGraceUntil &&
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

        // Normal non-top movement becomes the remembered position.
        if (current < topThreshold)
            saved = current;

        lastObserved = current;
    }

    public void RestoreAfterLayout()
    {
        restorePending = false;

        if (scrollRect == null || scrollRect.content == null)
            return;

        if (Time.time < userGraceUntil)
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
        SendCustomEventDelayedFrames(nameof(RestoreAfterLayout), 2);
    }

    private bool IsUserInteracting()
    {
        if (scrollRect == null) return false;

        if (scrollRect.velocity.sqrMagnitude > 0.0001f)
            return true;

        if (Input.GetMouseButton(0))
            return true;

        Vector2 wheel = Input.mouseScrollDelta;
        if (wheel.sqrMagnitude > 0.0001f)
            return true;

        return false;
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
