using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Central visual "juice", fully decoupled: it only listens to the
/// <see cref="GameEvents"/> bus, so no gameplay system references it.
///
///   TileMerged (per gem)      → a spark burst in the gem's signature colour
///   MatchResolved (per group) → camera shake scaled by combo &amp; size,
///                               a floating "+points" in the group's colour,
///                               a COMBO / NICE! / GREAT! / AMAZING! popup
///   SpecialCreated            → a flash ring where the new special is born
///   SpecialFired              → beams along a Cross blast, a screen flash for a Prism
///   StageChanged / ColorUnlocked → STAGE and NEW GEM fanfares
///
/// Everything is code-driven — generated sprites and runtime TextMeshPro — so no
/// Editor-authored effect assets are needed. Lives on any always-present object.
/// </summary>
public class JuiceDirector : MonoBehaviour
{
    [Header("Master")]
    [SerializeField] private bool enableJuice = true;

    [Header("Popup text")]
    [Tooltip("Pixel font for every floating word. Falls back to TMP's default when unset.")]
    [SerializeField] private TMP_FontAsset popupFont;
    [Range(0f, 0.5f)]
    [SerializeField] private float popupOutline = 0.22f;
    [SerializeField] private Color popupOutlineColor = new Color(0.05f, 0.02f, 0.12f, 1f);

    [Header("Camera shake")]
    [SerializeField] private bool cameraShake = true;
    [SerializeField] private float shakeDuration = 0.14f;
    [Tooltip("Shake magnitude for a plain 3-match; combos and bigger groups add to it.")]
    [SerializeField] private float shakeMagnitude = 0.05f;
    [SerializeField] private float shakeMax = 0.18f;

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

    [Header("Screen flash")]
    [SerializeField] private bool screenFlash = true;
    [Range(0f, 1f)]
    [SerializeField] private float flashAlpha = 0.35f;

    private Camera _camera;
    private Coroutine _shakeRoutine;
    private Vector3 _cameraBasePos;
    private Material _popupMaterial;
    private SpriteRenderer _flash;
    private Coroutine _flashRoutine;
    private int _combo;   // combo of the group currently popping, for spark scaling

    // Words born in the same frame stack upward instead of printing on top of
    // each other ("CROSS!" over "BLAST!" over "+390" was one unreadable blob).
    private int _popupFrame = -1;
    private int _popupsThisFrame;
    private static Sprite _sparkSprite;
    private static Sprite _softSprite;

    void Awake()
    {
        // One outlined material shared by every popup; vertex colour does the
        // per-word tint, so this never needs to be instanced per text.
        if (popupFont != null)
        {
            _popupMaterial = new Material(popupFont.material);
            _popupMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, popupOutline);
            _popupMaterial.SetColor(ShaderUtilities.ID_OutlineColor, popupOutlineColor);
            _popupMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
        }
    }

    void OnDestroy()
    {
        if (_popupMaterial != null) Destroy(_popupMaterial);
    }

    void OnEnable()
    {
        GameEvents.TileMerged += HandleTileMerged;
        GameEvents.MatchResolved += HandleMatchResolved;
        GameEvents.StoneDamaged += HandleStoneDamaged;
        GameEvents.StoneBroken += HandleStoneBroken;
        GameEvents.SpecialCreated += HandleSpecialCreated;
        GameEvents.SpecialFired += HandleSpecialFired;
        GameEvents.StageChanged += HandleStageChanged;
        GameEvents.ColorUnlocked += HandleColorUnlocked;
    }

    void OnDisable()
    {
        GameEvents.TileMerged -= HandleTileMerged;
        GameEvents.MatchResolved -= HandleMatchResolved;
        GameEvents.StoneDamaged -= HandleStoneDamaged;
        GameEvents.StoneBroken -= HandleStoneBroken;
        GameEvents.SpecialCreated -= HandleSpecialCreated;
        GameEvents.SpecialFired -= HandleSpecialFired;
        GameEvents.StageChanged -= HandleStageChanged;
        GameEvents.ColorUnlocked -= HandleColorUnlocked;
    }

    // --- Per-gem: the burst where it died ------------------------------------

    private void HandleTileMerged(Item gem, Cell cell)
    {
        if (!enableJuice || !sparkBurst) return;

        Vector3 pos = gem != null ? gem.transform.position
                    : (cell != null ? cell.transform.position : transform.position);
        Color tint = gem != null ? gem.SignatureColor : Color.white;

        // Cascades throw more sparks — the board should feel like it is boiling over.
        int count = Mathf.RoundToInt(sparkCount * (1f + 0.35f * Mathf.Max(0, _combo - 1)));
        StartCoroutine(SparkBurst(pos, tint, Mathf.Min(count, sparkCount * 3)));
    }

    // --- Per-group: shake, score, applause -----------------------------------

    private void HandleMatchResolved(int combo, int gemCount, int points, Vector3 centre, Color colour)
    {
        if (!enableJuice) return;
        _combo = combo;

        if (cameraShake)
        {
            // A 3-match murmurs; a cascade or a big group actually kicks.
            float mag = shakeMagnitude * (1f + 0.45f * (combo - 1) + 0.12f * (gemCount - 3));
            DoCameraShake(Mathf.Min(mag, shakeMax));
        }

        if (floatingScore && points > 0)
        {
            float size = scoreFontSize * (1f + 0.12f * Mathf.Min(combo - 1, 4));
            StartCoroutine(FloatingText("+" + points, centre, size, Brighten(colour), scoreRise, scoreLifetime));
        }

        if (comboPopups)
        {
            // One line of applause, never two: a cascade outranks a big group.
            string line = combo >= 4 ? "COMBO x" + combo + "!"
                        : combo >= 2 ? "COMBO x" + combo
                        : gemCount >= 6 ? "AMAZING!"
                        : gemCount >= 5 ? "GREAT!"
                        : gemCount >= 4 ? "NICE!"
                        : null;
            if (line != null)
            {
                float size = popupFontSize * (1f + 0.1f * Mathf.Min(combo, 5));
                StartCoroutine(FloatingText(line, centre + Vector3.up * 0.55f, size,
                                            Brighten(colour), scoreRise * 1.3f, scoreLifetime * 1.1f));
            }
        }

        if (screenFlash && combo >= 3)
            Flash(Brighten(colour), flashAlpha * 0.5f, 0.12f);
    }

    // --- Specials ------------------------------------------------------------

    private void HandleSpecialCreated(Item gem, Cell cell)
    {
        if (!enableJuice || gem == null) return;
        Vector3 pos = gem.transform.position;
        StartCoroutine(Ring(pos, gem.Special == SpecialKind.Prism ? Color.white : gem.SignatureColor, 1.6f, 0.35f));
        if (sparkBurst) StartCoroutine(SparkBurst(pos, Color.white, sparkCount));
        string word = gem.Special == SpecialKind.Prism ? "PRISM!" : "CROSS!";
        StartCoroutine(FloatingText(word, pos + Vector3.up * 0.4f, popupFontSize * 0.9f,
                                    gem.Special == SpecialKind.Prism ? Color.white : Brighten(gem.SignatureColor),
                                    scoreRise, scoreLifetime, rainbow: gem.Special == SpecialKind.Prism));
    }

    private void HandleSpecialFired(SpecialKind kind, Vector3 origin, Color colour, List<Cell> cells)
    {
        if (!enableJuice) return;

        if (kind == SpecialKind.Cross)
        {
            // Two beams along the arms, from the outermost cell to the outermost cell.
            float minX = origin.x, maxX = origin.x, minY = origin.y, maxY = origin.y;
            foreach (Cell cell in cells)
            {
                Vector3 p = cell.transform.position;
                if (Mathf.Abs(p.y - origin.y) < 0.01f) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); }
                if (Mathf.Abs(p.x - origin.x) < 0.01f) { minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
            }
            StartCoroutine(Beam(new Vector3(minX - 0.5f, origin.y), new Vector3(maxX + 0.5f, origin.y), colour));
            StartCoroutine(Beam(new Vector3(origin.x, minY - 0.5f), new Vector3(origin.x, maxY + 0.5f), colour));
            if (cameraShake) DoCameraShake(Mathf.Min(shakeMagnitude * 2.2f, shakeMax));
            StartCoroutine(FloatingText("BLAST!", origin + Vector3.up * 0.3f, popupFontSize, Brighten(colour),
                                        scoreRise * 1.2f, scoreLifetime));
        }
        else
        {
            bool everything = colour == Color.white;
            Flash(everything ? Color.white : Brighten(colour), flashAlpha, 0.22f);
            StartCoroutine(Ring(origin, everything ? Color.white : Brighten(colour), 9f, 0.5f));
            if (cameraShake) DoCameraShake(shakeMax);
            StartCoroutine(FloatingText(everything ? "CLEAR!" : "PRISM!", origin + Vector3.up * 0.3f,
                                        popupFontSize * 1.25f, Color.white, scoreRise * 1.2f, scoreLifetime * 1.2f,
                                        rainbow: true));
        }
    }

    // --- Progression ---------------------------------------------------------

    private int _lastStage = -1;

    private void HandleStageChanged(int stage, int colours, int stageStart, int nextStart)
    {
        if (!enableJuice) return;
        bool up = _lastStage >= 0 && stage > _lastStage && stageStart > 0;
        _lastStage = stage;
        if (!up) return;

        Vector3 centre = BoardCentre();
        Color gold = new Color(1f, 0.85f, 0.35f);
        Flash(gold, flashAlpha * 0.8f, 0.2f);
        StartCoroutine(Ring(centre, gold, 8f, 0.6f));
        StartCoroutine(FloatingText("STAGE " + (stage + 1), centre + Vector3.up * 1.2f, popupFontSize * 1.5f, gold,
                                    scoreRise * 0.6f, scoreLifetime * 1.8f));
    }

    private void HandleColorUnlocked(int colourIndex)
    {
        if (!enableJuice) return;
        Vector3 centre = BoardCentre();
        Color c = Brighten(GemPalette.ColorFor(colourIndex));
        StartCoroutine(FloatingText("NEW GEM: " + GemPalette.NameFor(colourIndex), centre + Vector3.up * 0.2f,
                                    popupFontSize * 1.05f, c, scoreRise * 0.6f, scoreLifetime * 2f));
        if (sparkBurst)
            for (int i = 0; i < 3; i++)
                StartCoroutine(SparkBurst(centre + (Vector3)Random.insideUnitCircle * 1.5f, c, sparkCount));
    }

    private Vector3 BoardCentre()
    {
        if (_camera == null) _camera = Camera.main;
        return _camera != null ? new Vector3(_cameraBasePos == Vector3.zero ? _camera.transform.position.x : _cameraBasePos.x,
                                             _cameraBasePos == Vector3.zero ? _camera.transform.position.y : _cameraBasePos.y, 0f)
                               : Vector3.zero;
    }

    // --- Stones --------------------------------------------------------------

    // A crack is a hit that held: a few dark chips fly, no shake — the stone is
    // still in the way, and the feedback should say "again".
    private void HandleStoneDamaged(Item stone, Cell cell)
    {
        if (!enableJuice || !sparkBurst || stone == null) return;
        StartCoroutine(SparkBurst(stone.transform.position, new Color(0.55f, 0.25f, 0.22f), sparkCount));
    }

    // The shatter is a payoff moment: red burst, a real kick of shake, and the
    // points fly up from the wreck.
    private void HandleStoneBroken(Item stone, Cell cell)
    {
        if (!enableJuice || stone == null) return;

        Vector3 pos = stone.transform.position;
        if (sparkBurst)
        {
            StartCoroutine(SparkBurst(pos, stone.SignatureColor, sparkCount));
            StartCoroutine(SparkBurst(pos, new Color(1f, 0.85f, 0.6f), sparkCount));   // double burst = bigger break
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

    // --- Screen flash -------------------------------------------------------

    // A camera-sized quad that blinks and fades — the cheapest "something big
    // just happened" there is.
    private void Flash(Color colour, float alpha, float duration)
    {
        if (!screenFlash) return;
        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return;

        if (_flash == null)
        {
            var go = new GameObject("ScreenFlash");
            go.transform.SetParent(_camera.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 1f);
            _flash = go.AddComponent<SpriteRenderer>();
            _flash.sprite = GetSparkSprite();
            _flash.sortingOrder = sparkSortingOrder + 50;
        }
        float h = _camera.orthographicSize * 2f + 1f;
        _flash.transform.localScale = new Vector3(h * _camera.aspect, h, 1f);

        if (_flashRoutine != null) StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashRoutine(colour, alpha, duration));
    }

    private IEnumerator FlashRoutine(Color colour, float alpha, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(t / duration);
            Color c = colour;
            c.a = alpha * k * k;
            _flash.color = c;
            yield return null;
        }
        _flash.color = Color.clear;
        _flashRoutine = null;
    }

    // --- Floating text (runtime TMP, no prefab) ------------------------------

    private IEnumerator FloatingText(string text, Vector3 pos, float fontSize,
                                     Color color, float rise, float lifetime, bool rainbow = false)
    {
        if (Time.frameCount != _popupFrame) { _popupFrame = Time.frameCount; _popupsThisFrame = 0; }
        int slot = _popupsThisFrame++;
        pos += new Vector3((slot % 2 == 0 ? -1f : 1f) * 0.25f * slot, 0.62f * slot, 0f);

        var go = new GameObject("FloatingText");
        go.transform.position = pos;

        var tmp = go.AddComponent<TextMeshPro>();
        if (popupFont != null) tmp.font = popupFont;
        if (_popupMaterial != null) tmp.fontSharedMaterial = _popupMaterial;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.sortingOrder = sparkSortingOrder + 1;
        tmp.enableVertexGradient = rainbow;

        // TextMeshPro (3D) meshes are huge by default; rectTransform sized small
        // keeps the layout box out of the way — the text just centres on it.
        tmp.rectTransform.sizeDelta = new Vector2(6f, 1f);

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
            float a = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;

            if (rainbow)
            {
                float h = Time.time * 0.6f;
                Color c1 = Color.HSVToRGB(Mathf.Repeat(h, 1f), 0.7f, 1f);
                Color c2 = Color.HSVToRGB(Mathf.Repeat(h + 0.25f, 1f), 0.7f, 1f);
                Color c3 = Color.HSVToRGB(Mathf.Repeat(h + 0.5f, 1f), 0.7f, 1f);
                Color c4 = Color.HSVToRGB(Mathf.Repeat(h + 0.75f, 1f), 0.7f, 1f);
                c1.a = c2.a = c3.a = c4.a = a;
                tmp.colorGradient = new VertexGradient(c1, c2, c3, c4);
                tmp.color = new Color(1f, 1f, 1f, a);
            }
            else
            {
                Color c = color;
                c.a = a;
                tmp.color = c;
            }

            yield return null;
        }
        Destroy(go);
    }

    // --- Beam (a Cross blast's arm) -----------------------------------------

    private IEnumerator Beam(Vector3 from, Vector3 to, Color colour)
    {
        var go = new GameObject("Beam");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetSparkSprite();
        sr.sortingOrder = sparkSortingOrder - 1;
        Vector3 mid = (from + to) * 0.5f;
        Vector3 dir = to - from;
        go.transform.position = mid;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        const float dur = 0.32f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float thickness = Mathf.Lerp(0.55f, 0.05f, Ease.OutCubic(k));
            go.transform.localScale = new Vector3(dir.magnitude, thickness, 1f);
            Color c = Color.Lerp(Color.white, colour, Mathf.Min(1f, k * 2f));
            c.a = 1f - k * k;
            sr.color = c;
            yield return null;
        }
        Destroy(go);
    }

    // --- Ring (an expanding shockwave) ---------------------------------------

    private IEnumerator Ring(Vector3 centre, Color colour, float radius, float duration)
    {
        var go = new GameObject("Ring");
        go.transform.position = centre;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetSoftSprite();
        sr.sortingOrder = sparkSortingOrder - 2;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            go.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, radius, Ease.OutCubic(k));
            Color c = colour;
            c.a = 0.6f * (1f - k);
            sr.color = c;
            yield return null;
        }
        Destroy(go);
    }

    // --- Spark burst (procedural, no ParticleSystem asset needed) -----------

    private IEnumerator SparkBurst(Vector3 center, Color tint, int count)
    {
        count = Mathf.Max(1, count);
        var sparks = new Transform[count];
        var velocities = new Vector2[count];
        var renderers = new SpriteRenderer[count];

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Spark");
            go.transform.position = center;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetSparkSprite();
            sr.color = i % 3 == 0 ? Color.Lerp(tint, Color.white, 0.6f) : tint;
            sr.sortingOrder = sparkSortingOrder;
            go.transform.localScale = Vector3.one * sparkSize * Random.Range(0.7f, 1.3f);

            float ang = (i / (float)count) * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
            velocities[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * sparkSpeed * Random.Range(0.6f, 1.1f);
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

    // Gem colours are saturated for glows; words want a lighter, readable tint.
    private static Color Brighten(Color c) => Color.Lerp(c, Color.white, 0.3f);

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

    // A soft disc for shockwave rings.
    private static Sprite GetSoftSprite()
    {
        if (_softSprite == null)
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            float half = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) * 6f);
                    px[y * size + x] = new Color(1f, 1f, 1f, ring);
                }
            tex.SetPixels(px);
            tex.Apply();
            _softSprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            _softSprite.name = "JuiceRing";
        }
        return _softSprite;
    }
}
