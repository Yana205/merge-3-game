using System.Collections;
using UnityEngine;

/// <summary>
/// Central "juice" for merges — presentation polish that stays fully decoupled: it
/// only listens to the Lesson 1 <see cref="GameEvents.TileMerged"/> bus, so no
/// gameplay system references it. On every merge it fires three code-driven effects
/// (no Editor-authored assets needed):
///   • a short camera shake,
///   • a scale "punch" on the newly merged crystal,
///   • a tinted spark burst at the merge cell.
///
/// Drop this on any always-present GameObject (e.g. the GridManager or a "_Juice"
/// object). Everything is tunable in the Inspector and individually toggleable.
/// </summary>
public class JuiceDirector : MonoBehaviour
{
    [Header("Master")]
    [SerializeField] private bool enableJuice = true;

    [Header("Camera shake")]
    [SerializeField] private bool cameraShake = true;
    [SerializeField] private float shakeDuration = 0.12f;
    [SerializeField] private float shakeMagnitude = 0.06f;

    [Header("Merge punch")]
    [SerializeField] private bool scalePunch = true;
    [SerializeField] private float punchScale = 0.28f;   // extra scale at the peak
    [SerializeField] private float punchDuration = 0.16f;

    [Header("Spark burst")]
    [SerializeField] private bool sparkBurst = true;
    [SerializeField] private int sparkCount = 8;
    [SerializeField] private float sparkSpeed = 3.2f;
    [SerializeField] private float sparkLifetime = 0.4f;
    [SerializeField] private float sparkSize = 0.14f;
    [SerializeField] private int sparkSortingOrder = 100;

    private Camera _camera;
    private Coroutine _shakeRoutine;
    private Vector3 _cameraBasePos;
    private static Sprite _sparkSprite;

    void OnEnable()  => GameEvents.TileMerged += HandleTileMerged;
    void OnDisable() => GameEvents.TileMerged -= HandleTileMerged;

    private void HandleTileMerged(Item item, Cell cell)
    {
        if (!enableJuice) return;

        Vector3 pos = item != null ? item.transform.position
                    : (cell != null ? cell.transform.position : transform.position);
        Color tint = (item != null && item.GemData != null) ? item.GemData.tintColor : Color.white;

        if (cameraShake)
            DoCameraShake();

        if (scalePunch && item != null)
            StartCoroutine(PunchScale(item.transform));

        if (sparkBurst)
            StartCoroutine(SparkBurst(pos, tint));
    }

    // --- Camera shake -------------------------------------------------------

    private void DoCameraShake()
    {
        if (_camera == null) _camera = Camera.main;
        if (_camera == null) return;

        if (_shakeRoutine != null)
        {
            StopCoroutine(_shakeRoutine);
            _camera.transform.localPosition = _cameraBasePos; // restore before re-shaking
        }
        _cameraBasePos = _camera.transform.localPosition;
        _shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    private IEnumerator ShakeRoutine()
    {
        float t = 0f;
        while (t < shakeDuration)
        {
            t += Time.unscaledDeltaTime;
            float damper = 1f - (t / shakeDuration);          // ease out
            Vector2 off = Random.insideUnitCircle * shakeMagnitude * damper;
            _camera.transform.localPosition = _cameraBasePos + new Vector3(off.x, off.y, 0f);
            yield return null;
        }
        _camera.transform.localPosition = _cameraBasePos;
        _shakeRoutine = null;
    }

    // --- Scale punch --------------------------------------------------------

    private IEnumerator PunchScale(Transform target)
    {
        Vector3 baseScale = target.localScale;
        Vector3 peakScale = baseScale * (1f + punchScale);
        float t = 0f;
        while (t < punchDuration)
        {
            // Guard the pooled item: if it despawned mid-punch, restore and bail.
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                if (target != null) target.localScale = baseScale;
                yield break;
            }
            t += Time.deltaTime;
            float p = t / punchDuration;
            // 0 -> peak at the halfway point -> back to base (a single sine arch).
            float s = Mathf.Sin(p * Mathf.PI);
            target.localScale = Vector3.LerpUnclamped(baseScale, peakScale, s);
            yield return null;
        }
        if (target != null) target.localScale = baseScale;
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
