using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class VerdantScrollPositionKeeper : UdonSharpBehaviour
{
    public ScrollRect scrollRect;

    [Header("Preservation")]
    public bool preserveVertical = true;
    public bool protectUnexpectedTopReset = true;
    [Range(0.01f, 0.5f)] public float minimumUnexpectedJump = 0.12f;
    [Range(0.9f, 1f)] public float topResetThreshold = 0.995f;
    [Range(0f, 10f)] public float layoutHeightTolerance = 0.5f;

    private bool _ready;
    private float _savedVertical = 1f;
    private float _lastObservedVertical = 1f;
    private float _lastContentHeight;

    private void Start()
    {
        Initialise();
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
        bool moving = scrollRect.velocity.sqrMagnitude > 0.0001f;

        if (contentHeightChanged)
        {
            if (_savedVertical < 0.999f)
            {
                scrollRect.verticalNormalizedPosition = _savedVertical;
                current = _savedVertical;
            }
        }
        else if (protectUnexpectedTopReset &&
                 !moving &&
                 current >= topResetThreshold &&
                 _savedVertical < topResetThreshold &&
                 current - _savedVertical >= minimumUnexpectedJump)
        {
            scrollRect.verticalNormalizedPosition = _savedVertical;
            current = _savedVertical;
        }
        else
        {
            float delta = Mathf.Abs(current - _lastObservedVertical);

            if (moving || delta < minimumUnexpectedJump || current < topResetThreshold)
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
