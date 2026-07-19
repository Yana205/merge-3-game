using UnityEngine;

/// <summary>
/// Drives the <c>Merge3/CrystalAura</c> shader's <c>_Intensity</c> at runtime.
///
/// Demonstrates the Lesson 4 runtime pattern: it reads <c>renderer.material</c> (a
/// per-object INSTANCE, not <c>sharedMaterial</c>), animates a property on it every
/// frame, and — because that instance is ours — destroys it in <c>OnDestroy</c> to
/// avoid a leaked material. It also flares on each merge by listening to the
/// Lesson 1 <see cref="GameEvents.TileMerged"/> bus, so the effect is genuinely
/// reactive rather than a fixed loop.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class CrystalAuraController : MonoBehaviour
{
    [Header("Intensity drive")]
    [SerializeField] private float baseIntensity = 0.9f;
    [SerializeField] private float idleWaveSpeed = 1.5f;
    [SerializeField] private float idleWaveAmount = 0.15f;

    [Header("Merge flare")]
    [SerializeField] private float mergePulse = 0.8f;   // added the instant a tile merges
    [SerializeField] private float pulseDecay = 2.5f;   // units per second

    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    private Renderer _renderer;
    private Material _mat;      // owned instance — MUST be destroyed in OnDestroy
    private float _pulse;

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        // renderer.material returns a per-object instance (clones sharedMaterial the
        // first time). We now own that clone and are responsible for destroying it.
        _mat = _renderer.material;
    }

    void OnEnable()  => GameEvents.TileMerged += HandleTileMerged;
    void OnDisable() => GameEvents.TileMerged -= HandleTileMerged;

    private void HandleTileMerged(Item item, Cell cell)
    {
        _pulse = mergePulse;
    }

    void Update()
    {
        if (_mat == null) return;

        _pulse = Mathf.Max(0f, _pulse - pulseDecay * Time.deltaTime);
        float idle = Mathf.Sin(Time.time * idleWaveSpeed) * idleWaveAmount;
        float intensity = Mathf.Clamp(baseIntensity + idle + _pulse, 0f, 2f);

        _mat.SetFloat(IntensityId, intensity);
    }

    void OnDestroy()
    {
        // Free the material instance we created via renderer.material.
        if (_mat != null)
            Destroy(_mat);
    }
}
