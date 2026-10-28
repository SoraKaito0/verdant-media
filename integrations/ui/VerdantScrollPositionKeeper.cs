using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(32000)]
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class VerdantScrollPositionKeeper : UdonSharpBehaviour
{
    public ScrollRect scrollRect;

    [Header("Preservation")]
    public bool preserveVertical = true;
    public bool protectUnexpectedTopReset = true;
    [Range(0.01f, 0.5f)] public float minimumUnexpectedJump = 0.08f;
    [Range(0.9f, 1f)] public float topResetThreshold = 0.992f;
    [Range(0f, 10f)] public float layoutHeightTolerance = 0.25f;

    private bool _ready;
    private float _savedVertical = 1f;
    private float _lastObservedVertical = 1f;
    private float _lastContentHeight;

    private void Start()
    {
        Initialise();
    }

    private void Update()
    {
        if (!_ready || scrollRect == null || !preserveVertical)
            return;

        float current = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
        bool moving = scrollRect.velocity.sqrMagnitude > 0.00005f;

        if (moving)
        {
            _savedVertical = current;
            _lastObservedVertical = current;
        }
    }

    private void LateUpdate()
    {
        if (!preserveVertical || scrollRect == null || scrollRect.content == null)
            return;

        if (!_ready)
        {
            Initialise();
            return;
        }

        float current = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
        float contentHeight = scrollRect.content.rect.height;
        bool contentHeightChanged = Mathf.Abs(contentHeight - _lastContentHeight) > layoutHeightTolerance;
        bool moving = scrollRect.velocity.sqrMagnitude > 0.00005f;

        bool suspiciousTopReset =
            protectUnexpectedTopReset &&
            !moving &&
            current >= topResetThreshold &&
            _savedVertical < topResetThreshold &&
            current - _savedVertical >= minimumUnexpectedJump;

        if ((contentHeightChanged || suspiciousTopReset) && _savedVertical < 0.999f)
        {
            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = _savedVertical;
            current = _savedVertical;
        }
        else
        {
            float delta = Mathf.Abs(current - _lastObservedVertical);

            // Normal user wheel/drag movement should become the new saved position.
            // A single large zero-velocity jump to the top is treated as a programmatic reset.
            if (moving || current < topResetThreshold || delta < minimumUnexpectedJump)
                _savedVertical = current;
        }

        _lastObservedVertical = current;
        _lastContentHeight = contentHeight;
    }

    public void CaptureCurrentPosition()
    {
        if (scrollRect == null) return;

        _savedVertical = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
        _lastObservedVertical = _savedVertical;

        if (scrollRect.content != null)
            _lastContentHeight = scrollRect.content.rect.height;

        _ready = true;
    }

    public void RestoreSavedPosition()
    {
        if (scrollRect == null || !_ready) return;
        scrollRect.StopMovement();
        scrollRect.verticalNormalizedPosition = _savedVertical;
    }

    public void AllowTop()
    {
        _savedVertical = 1f;
        _lastObservedVertical = 1f;

        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    private void Initialise()
    {
        if (scrollRect == null)
            scrollRect = GetComponent<ScrollRect>();

        if (scrollRect == null)
            return;

        _savedVertical = Mathf.Clamp01(scrollRect.verticalNormalizedPosition);
        _lastObservedVertical = _savedVertical;

        if (scrollRect.content != null)
            _lastContentHeight = scrollRect.content.rect.height;

        _ready = true;
    }
}
