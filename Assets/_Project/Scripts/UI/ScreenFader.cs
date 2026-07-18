using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Full-screen fade used for level transitions. Lives on an always-active
/// CanvasGroup (topmost under the canvas) so transition coroutines survive
/// panels being deactivated mid-transition.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ScreenFader : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float fadeDuration = 0.25f;

    private CanvasGroup _group;
    private Coroutine _active;

    void Awake()
    {
        _group = GetComponent<CanvasGroup>();
        SetClear();
    }

    /// <summary>Fade to black, run the action at the midpoint, fade back in.</summary>
    public void RunTransition(Action midpoint)
    {
        // A transition is already running — ignore double-clicks.
        if (_active != null)
            return;

        _active = StartCoroutine(Transition(midpoint));
    }

    IEnumerator Transition(Action midpoint)
    {
        _group.blocksRaycasts = true;
        yield return Fade(0f, 1f);
        midpoint?.Invoke();
        yield return Fade(1f, 0f);
        SetClear();
        _active = null;
    }

    IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }
        _group.alpha = to;
    }

    void SetClear()
    {
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
    }
}
