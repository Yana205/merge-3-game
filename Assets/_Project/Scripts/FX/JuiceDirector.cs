using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Central visual "juice", fully decoupled: it only listens to the
/// <see cref="GameEvents"/> bus, so no gameplay system references it.
///
///   TileMerged (per gem)      → a spark burst in the gem's signature colour
///   MatchResolved (per group) → camera shake scaled by combo &amp; size,
///                               a floating "+points" score at the group's centre,
///                               a COMBO / NICE! / GREAT! popup when it's earned
///
/// Everything is code-driven — generated sprites and runtime TextMeshPro — so no
/// Editor-authored effect assets are needed. Lives on any always-present object.
/// </summary>
public class JuiceDirector : MonoBehaviour
{
    [Header("Master")]
    [SerializeField] private bool enableJuice = true;

    [Header("Camera shake")]
    [SerializeField] private bool cameraShake = true;
    [SerializeField] private float shakeDuration = 0.14f;
    [Tooltip("Shake magnitude for a plain 3-match; combos and bigger groups add to it.")]
    [SerializeField] private float shakeMagnitude = 0.05f;
    [SerializeField] private float shakeMax = 0.16f;

    [Header("Spark burst")]
    [SerializeField] private bool sparkBurst = true;
    [SerializeField] private int sparkCount = 10;
    [SerializeField] private float sparkSpeed = 3.6f;
    [SerializeField] private float sparkLifetime = 0.45f;
    [SerializeField] private float sparkSize = 0.16f;
    [SerializeField] private int sparkSortingOrder = 100;

    [Header("Floating score")]
    [SerializeField] private bool floatingScore = true;
    [SerializeField] private float scoreRise = 0.9f;
    [SerializeField] private float scoreLifetime = 0.8f;
    [SerializeField] private float scoreFontSize = 4.5f;

    [Header("Combo popups")]
    [SerializeField] private bool comboPopups = true;
    [SerializeField] private float popupFontSize = 7f;

    private Camera _camera;
    private Coroutine _shakeRoutine;
    private Vector3 _cameraBasePos;
    private static Sprite _sparkSprite;

    void OnEnable()
    {
        GameEvents.TileMerged += HandleTileMerged;
        GameEvents.MatchResolved += HandleMatchResolved;
        GameEvents.StoneDamaged += HandleStoneDamaged;
        GameEvents.StoneBroken += HandleStoneBroken;
    }

    void OnDisable()
    {
        GameEvents.TileMerged -= HandleTileMerged;
        GameEvents.MatchResolved -= HandleMatchResolved;
        GameEvents.StoneDamaged -= HandleStoneDamaged;
        GameEvents.StoneBroken -= HandleStoneBroken;
    }

    // --- Per-gem: the burst where it died ------------------------------------

    private void HandleTileMerged(Item gem, Cell cell)
    {
        if (!enableJuice || !sparkBurst) return;

        Vector3 pos = gem != null ? gem.transform.position
                    : (cell != null ? cell.transform.position : transform.position);
        Color tint = gem != null ? gem.SignatureColor : Color.white;

        StartCoroutine(SparkBurst(pos, tint));
    }

    // --- Per-group: shake, score, applause -----------------------------------

    private void HandleMatchResolved(int combo, int gemCount, int points, Vector3 centre)
    {
        if (!enableJuice) return;

        if (cameraShake)
        {
            // A 3-match murmurs; a cascade or a big group actually kicks.
            float mag = shakeMagnitude * (1f + 0.45f * (combo - 1) + 0.12f * (gemCount - 3));
            DoCameraShake(Mathf.Min(mag, shakeMax));
        }

        if (floatingScore && points > 0)
            StartCoroutine(FloatingText("+" + points, centre, scoreFontSize,
                                        new Color(1f, 0.92f, 0.55f), scoreRise, scoreLifetime));

        if (comboPopups)
        {
            // One line of applause, never two: a cascade outranks a big group.
            string line = combo >= 2 ? "COMBO x" + combo
                        : gemCount >= 5 ? "GREAT!"
                        : gemCount >= 4 ? "NICE!"
                        : null;
            if (line != null)
                StartCoroutine(FloatingText(line, centre + Vector3.up * 0.55f, popupFontSize,
                                            new Color(0.75f, 0.95f, 1f), scoreRise * 1.3f, scoreLifetime * 1.1f));
        }
    }

    // --- Stones --------------------------------------------------------------

    // A crack is a hit that held: a few dark chips fly, no shake — the stone is
    // still in the way, and the feedback should say "again".
    private void HandleStoneDamaged(Item stone, Cell cell)
    {
        if (!enableJuice || !sparkBurst || stone == null) return;
        StartCoroutine(SparkBurst(stone.transform.position, new Color(0.55f, 0.25f, 0.22f)));
    }

    // The shatter is a payoff moment: red burst, a real kick of shake, and the
    // points fly up from the wreck.
    private void HandleStoneBroken(Item stone, Cell cell)
    {
        if (!enableJuice || stone == null) return;

        Vector3 pos = stone.transform.position;
        if (sparkBurst)
        {
            StartCoroutine(SparkBurst(pos, stone.SignatureColor));
            StartCoroutine(SparkBurst(pos, new Color(1f, 0.85f, 0.6f)));   // double burst = bigger break
        }
        if (cameraShake)
            DoCameraShake(Mathf.Min(shakeMagnitude * 1.8f, shakeMax));
        if (floatingScore && stone.GemData != null)
            StartCoroutine(FloatingText("+" + stone.GemData.scoreValue, pos, scoreFontSize,
                                        new Color(1f, 0.62f, 0.5f), scoreRise, scoreLifetime));
    }

    // --- Camera shake -------------------------------------------------------

    private void DoCameraShake(float magnitude)
    {
        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return;

        if (_shakeRoutine != null)
        {
            StopCoroutine(_shakeRoutine);
            _camera.transform.localPosition = _cameraBasePos; // restore before re-shaking
        }
        _cameraBasePos = _camera.transform.localPosition;
        _shakeRoutine = StartCoroutine(ShakeRoutine(magnitude));
    }

    private IEnumerator ShakeRoutine(float magnitude)
    {
        float t = 0f;
        while (t < shakeDuration)
        {
            t += Time.unscaledDeltaTime;
            float damper = 1f - (t / shakeDuration);          // ease out
            Vector2 off = Random.insideUnitCircle * magnitude * damper;
            _camera.transform.localPosition = _cameraBasePos + new Vector3(off.x, off.y, 0f);
            yield return null;
        }
        _camera.transform.localPosition = _cameraBasePos;
        _shakeRoutine = null;
    }

    // --- Floating text (runtime TMP, no prefab) ------------------------------

    private IEnumerator FloatingText(string text, Vector3 pos, float fontSize,
                                     Color color, float rise, float lifetime)
    {
        var go = new GameObject("FloatingText");
        go.transform.position = pos;

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.sortingOrder = sparkSortingOrder + 1;

        // TextMeshPro (3D) meshes are huge by default; rectTransform sized small
        // keeps the layout box out of the way — the text just centres on it.
        tmp.rectTransform.sizeDelta = new Vector2(4f, 1f);

        float t = 0f;
        Vector3 from = pos;
        while (t < lifetime)
        {
            if (tmp == null) yield break;
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / lifetime);

            go.transform.position = from + Vector3.up * (rise * Ease.OutCubic(k));
            // Pop in fast, hold, fade out over the back half.
            go.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, Ease.OutBack(Mathf.Min(1f, k * 3f)));
            Color c = color;
            c.a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            tmp.color = c;

            yield return null;
        }
        Destroy(go);
    }

    // --- Spark burst (procedural, no ParticleSystem asset needed) -----------

    private IEnumerator SparkBurst(Vector3 center, Color tint)
    {
        int count = Mathf.Max(1, sparkCount);
        var sparks = new Transform[count];
        var velocities = new Vector2[count];
        var renderers = new SpriteRenderer[count];

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Spark");
            go.transform.position = center;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetSparkSprite();
            sr.color = tint;
            sr.sortingOrder = sparkSortingOrder;
            go.transform.localScale = Vector3.one * sparkSize;

            float ang = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
            velocities[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * sparkSpeed * Random.Range(0.6f, 1f);
            sparks[i] = go.transform;
            renderers[i] = sr;
        }

        float t = 0f;
        while (t < sparkLifetime)
        {
            t += Time.deltaTime;
            float k = t / sparkLifetime;
            for (int i = 0; i < count; i++)
            {
                if (sparks[i] == null) continue;
                velocities[i] *= 1f - 2.5f * Time.deltaTime;                 // drag
                velocities[i] += Vector2.down * (4f * Time.deltaTime);       // a little gravity
                sparks[i].position += (Vector3)(velocities[i] * Time.deltaTime);
                sparks[i].localScale = Vector3.one * sparkSize * (1f - k);   // shrink
                Color c = renderers[i].color;
                c.a = 1f - k;                                                // fade
                renderers[i].color = c;
            }
            yield return null;
        }

        for (int i = 0; i < count; i++)
            if (sparks[i] != null) Destroy(sparks[i].gameObject);
    }

    // Reuse the project's generated-white-square technique (see Item.cs) so sparks
    // never depend on an imported texture.
    private static Sprite GetSparkSprite()
    {
        if (_sparkSprite == null)
        {
            var tex = new Texture2D(8, 8);
            var px = new Color[8 * 8];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            _sparkSprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), Vector2.one * 0.5f, 8);
            _sparkSprite.name = "JuiceSpark";
        }
        return _sparkSprite;
    }
}
