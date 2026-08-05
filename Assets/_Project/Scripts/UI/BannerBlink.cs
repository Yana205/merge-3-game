using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hard on/off blink for the banner's PRESS START label.
///
/// The design specifies `blink 1.1s steps(1, end)`: opacity holds at 1 for the
/// first 55% of the cycle and at 0 for the rest. steps() means there is no fade
/// between them — a Lerp here would read as a pulse and be visibly wrong, so
/// this snaps.
///
/// Alpha is driven through the CanvasRenderer rather than by toggling the
/// Graphic, so the label keeps receiving pointer events while it is invisible
/// and a click never lands in a dead half-second.
/// </summary>
public class BannerBlink : MonoBehaviour
{
    [Header("Target (assign in Inspector)")]
    [Tooltip("Label to blink. Falls back to a Graphic on this GameObject.")]
    [SerializeField] private Graphic _target;

    [Header("Timing")]
    [Tooltip("Full cycle length in seconds.")]
    [SerializeField] private float _period = 1.1f;

    [Tooltip("Fraction of the cycle the label is visible for.")]
    [Range(0f, 1f)]
    [SerializeField] private float _onFraction = 0.55f;

    // Tracked so the alpha is only pushed on an actual change rather than every
    // frame, and so the first Update always writes a known state.
    private bool _isOn = true;
    private bool _hasState;

    void Awake()
    {
        if (_target == null)
            _target = GetComponent<Graphic>();

        if (_target == null)
            Debug.LogError("BannerBlink: no target Graphic assigned on '" + name + "'.");
    }

    void OnEnable()
    {
        // Always come back visible, so a disabled-then-enabled banner never
        // reappears mid-blink with the label blanked.
        _hasState = false;
    }

    void Update()
    {
        if (_target == null || _period <= 0f) return;

        // Unscaled: the banner keeps blinking even if the game pauses behind it.
        float phase = Mathf.Repeat(Time.unscaledTime, _period) / _period;
        bool shouldBeOn = phase < _onFraction;

        if (_hasState && shouldBeOn == _isOn) return;

        _isOn = shouldBeOn;
        _hasState = true;
        _target.canvasRenderer.SetAlpha(_isOn ? 1f : 0f);
    }

    void OnDisable()
    {
        // Leave the label visible for whatever shows it next.
        if (_target != null)
            _target.canvasRenderer.SetAlpha(1f);
    }
}
