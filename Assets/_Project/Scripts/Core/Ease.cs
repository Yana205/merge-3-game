using UnityEngine;

/// <summary>
/// The few easing curves the game feel is built from, in one place so the swap,
/// the falls and the pops all share the same vocabulary. Pure functions of
/// normalised time t (0..1) — every animation in the project is a coroutine
/// stepping one of these.
/// </summary>
public static class Ease
{
    /// <summary>Fast start, soft landing — the default for anything sliding.</summary>
    public static float OutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float u = 1f - t;
        return 1f - u * u * u;
    }

    /// <summary>Soft start, fast finish — reads as gravity taking hold.</summary>
    public static float InQuad(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t;
    }

    /// <summary>Slides past the target and settles back — the springy overshoot
    /// that makes a committed swap feel alive. Overshoot ≈ 10%.</summary>
    public static float OutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float s = 1.70158f;
        float u = t - 1f;
        return 1f + u * u * ((s + 1f) * u + s);
    }
}
