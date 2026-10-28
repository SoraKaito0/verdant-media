using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(32000)]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class VerdantVpmScrollKeeper : UdonSharpBehaviour
{
    public ScrollRect scrollRect;
    [Range(0.01f, 0.5f)] public float resetJumpThreshold = 0.08f;
    [Range(0.9f, 1f)] public float topThreshold = 0.992f;

    private bool ready;
    private float saved = 1f;
    private float last = 1f;
    private float lastHeight;

    private void Start() { Initialise(); }

    private void Update()
    {
        if (!ready || scrollRect == null) return;
        if (scrollRect.velocity.sqrMagnitude > 0.00005f)
        {
            saved = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
            last = saved;
        }
    }

    private void LateUpdate()
    {
        if (!ready) Initialise();
        if (scrollRect == null || scrollRect.content == null) return;

        float current = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
        float height = scrollRect.content.rect.height;
        bool moving = scrollRect.velocity.sqrMagnitude > 0.00005f;
        bool heightChanged = Mathf.Abs(height - lastHeight) > 0.25f;
        bool snappedTop = !moving && current >= topThreshold && saved < topThreshold &&
                          current - saved >= resetJumpThreshold;

        if ((heightChanged || snappedTop) && saved < 0.999f)
        {
            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = saved;
            current = saved;
        }
        else
        {
            float delta = Mathf.Abs(current - last);
            if (moving || current < topThreshold || delta < resetJumpThreshold)
                saved = current;
        }

        last = current;
        lastHeight = height;
    }

    private void Initialise()
    {
        if (scrollRect == null) scrollRect = GetComponent<ScrollRect>();
        if (scrollRect == null) return;
        saved = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
        last = saved;
        if (scrollRect.content != null) lastHeight = scrollRect.content.rect.height;
        ready = true;
    }
}
