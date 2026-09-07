using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// All of the game's sound, in one place, driven entirely by the
/// <see cref="GameEvents"/> bus — the same shape as <see cref="JuiceDirector"/>,
/// which handles the visual half of the same moments. No gameplay system
/// references this, and it references none of them.
///
///   GemSelected   → a soft click (the pick is acknowledged)
///   MatchResolved → ONE pop per group, pitch climbing with the cascade combo —
///                   a cascade literally sounds like going up a scale — plus a
///                   crash accent when a combo gets big
///   SwapDenied    → a low, quiet thud ("not that one")
///
/// Clips are plain serialized references rather than Addressables. The
/// Addressables path is exactly what makes PRESS START die on itch when content
/// is not built before the player, and audio gains nothing from it — a direct
/// reference is included in the build automatically and cannot fail to load.
/// </summary>
public class AudioDirector : MonoBehaviour
{
    [Header("Music")]
    [SerializeField] private AudioClip musicLoop;
    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 0.35f;

    [Header("Sound effects")]
    [Tooltip("The match pop. Played once per cleared group, pitch rising per combo.")]
    [FormerlySerializedAs("mergeClip")]
    [SerializeField] private AudioClip matchClip;

    [Tooltip("Accent crash layered on top of big cascades (combo 3+).")]
    [FormerlySerializedAs("shatterClip")]
    [SerializeField] private AudioClip crashClip;

    [Tooltip("Soft click when the player picks a gem.")]
    [FormerlySerializedAs("pickaxeArmClip")]
    [SerializeField] private AudioClip selectClip;

    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 0.7f;

    [Header("Combo pitch ladder")]
    [Tooltip("Pitch of the first match in a move. Each cascade step multiplies by " +
             "the step below, so a chain literally sounds like climbing.")]
    [Range(0.5f, 1.5f)]
    [SerializeField] private float basePitch = 0.95f;

    [Tooltip("Pitch multiplier per combo step. 1.12 ≈ two semitones — an audible " +
             "climb that stays musical over a long cascade.")]
    [Range(1.0f, 1.3f)]
    [SerializeField] private float pitchPerCombo = 1.12f;

    private AudioSource _musicSource;
    private AudioSource _sfxSource;

    /// <summary>
    /// Music mute state, shared by every instance and readable by any UI before
    /// this component has woken up. Static because the toggles live in the UI
    /// layer, which holds no reference to this object — and because the setting
    /// must survive the scene reload the restart flow performs on every run.
    /// Music is OFF by default: a browser tab that starts singing uninvited is
    /// the fastest way to lose a player, so they opt in with one click.
    /// </summary>
    public static bool MusicMuted { get { EnsurePrefsLoaded(); return s_musicMuted; } }

    /// <summary>Sound-effect mute state. Effects are ON by default — they are
    /// the game's feedback, not its soundtrack.</summary>
    public static bool SfxMuted { get { EnsurePrefsLoaded(); return s_sfxMuted; } }

    /// <summary>Raised after either mute flag changes, so every toggle button
    /// (HUD and title menu) can refresh its label without referencing the other.</summary>
    public static event System.Action MuteChanged;

    private const string MusicKey = "MusicMuted";
    private const string SfxKey = "SfxMuted";

    private static bool s_musicMuted = true;
    private static bool s_sfxMuted;
    private static bool s_prefsLoaded;

    // Static state outlives a play session when Domain Reload is off in the
    // Editor; re-reading the prefs on the next load keeps the flags honest.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_prefsLoaded = false;
        MuteChanged = null;
    }

    private static void EnsurePrefsLoaded()
    {
        if (s_prefsLoaded) return;
        s_musicMuted = PlayerPrefs.GetInt(MusicKey, 1) == 1;   // default: muted
        s_sfxMuted = PlayerPrefs.GetInt(SfxKey, 0) == 1;       // default: audible
        s_prefsLoaded = true;
    }

    void Awake()
    {
        EnsurePrefsLoaded();

        // Two sources, not one: music loops and must keep its own volume while
        // one-shots come and go. PlayOneShot on the music source would work but
        // would tie SFX volume to the music slider.
        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.clip = musicLoop;
        _musicSource.loop = true;
        _musicSource.playOnAwake = false;
        _musicSource.volume = musicVolume;

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.playOnAwake = false;
        _sfxSource.volume = sfxVolume;

        ApplyMute();
    }

    void Start()
    {
        if (musicLoop != null)
            _musicSource.Play();
    }

    void OnEnable()
    {
        GameEvents.MatchResolved += HandleMatchResolved;
        GameEvents.SwapDenied += HandleSwapDenied;
        GameEvents.GemSelected += HandleGemSelected;
        GameEvents.StoneDamaged += HandleStoneDamaged;
        GameEvents.StoneBroken += HandleStoneBroken;
        GameEvents.SpecialFired += HandleSpecialFired;
        GameEvents.StageChanged += HandleStageChanged;
    }

    void OnDisable()
    {
        GameEvents.MatchResolved -= HandleMatchResolved;
        GameEvents.SwapDenied -= HandleSwapDenied;
        GameEvents.GemSelected -= HandleGemSelected;
        GameEvents.StoneDamaged -= HandleStoneDamaged;
        GameEvents.StoneBroken -= HandleStoneBroken;
        GameEvents.SpecialFired -= HandleSpecialFired;
        GameEvents.StageChanged -= HandleStageChanged;
    }

    // --- Bus handlers -------------------------------------------------------

    // One pop per cleared GROUP, not per gem — five gems popping at once should be
    // one satisfying burst, not five copies of the same clip clipping the mixer.
    private void HandleMatchResolved(int combo, int gemCount, int points, Vector3 centre, Color colour)
    {
        if (_sfxSource == null) return;

        if (matchClip != null)
        {
            _sfxSource.pitch = basePitch * Mathf.Pow(pitchPerCombo, Mathf.Max(0, combo - 1));
            _sfxSource.PlayOneShot(matchClip, sfxVolume);
        }

        // Big cascades earn a crash on top — the moment the run feels lucky.
        if (crashClip != null && combo >= 3)
        {
            _sfxSource.pitch = 1f;
            _sfxSource.PlayOneShot(crashClip, sfxVolume * 0.6f);
        }
    }

    // Low and quiet: information, not punishment.
    private void HandleSwapDenied(Vector3 centre)
    {
        if (matchClip == null || _sfxSource == null) return;
        _sfxSource.pitch = 0.55f;
        _sfxSource.PlayOneShot(matchClip, sfxVolume * 0.45f);
    }

    private void HandleGemSelected(Vector3 position)
    {
        if (selectClip == null || _sfxSource == null) return;
        _sfxSource.pitch = Random.Range(1.02f, 1.10f);   // tiny variance stops the machine-gun effect
        _sfxSource.PlayOneShot(selectClip, sfxVolume * 0.35f);
    }

    // A dull knock: the stone took the hit and held.
    private void HandleStoneDamaged(Item stone, Cell cell)
    {
        if (crashClip == null || _sfxSource == null) return;
        _sfxSource.pitch = 0.7f;
        _sfxSource.PlayOneShot(crashClip, sfxVolume * 0.4f);
    }

    // The full crash — a stone is out of the way.
    private void HandleStoneBroken(Item stone, Cell cell)
    {
        if (crashClip == null || _sfxSource == null) return;
        _sfxSource.pitch = 1.05f;
        _sfxSource.PlayOneShot(crashClip, sfxVolume * 0.8f);
    }

    // A Cross is a sharp crack; a Prism is the crash, low and wide.
    private void HandleSpecialFired(SpecialKind kind, Vector3 origin, Color colour, System.Collections.Generic.List<Cell> cells)
    {
        if (crashClip == null || _sfxSource == null) return;
        _sfxSource.pitch = kind == SpecialKind.Prism ? 0.8f : 1.25f;
        _sfxSource.PlayOneShot(crashClip, sfxVolume * (kind == SpecialKind.Prism ? 0.9f : 0.6f));
    }

    private int _lastStage = -1;

    // A stage-up climbs the whole match ladder in one go — the run's fanfare.
    private void HandleStageChanged(int stage, int colours, int stageStart, int nextStart)
    {
        bool up = _lastStage >= 0 && stage > _lastStage && stageStart > 0;
        _lastStage = stage;
        if (!up || _sfxSource == null) return;
        StartCoroutine(StageJingle());
    }

    private System.Collections.IEnumerator StageJingle()
    {
        for (int i = 0; i < 4; i++)
        {
            if (matchClip != null)
            {
                _sfxSource.pitch = basePitch * Mathf.Pow(pitchPerCombo, i + 1);
                _sfxSource.PlayOneShot(matchClip, sfxVolume * 0.8f);
            }
            yield return new WaitForSeconds(0.09f);
        }
        if (crashClip != null)
        {
            _sfxSource.pitch = 1.1f;
            _sfxSource.PlayOneShot(crashClip, sfxVolume * 0.7f);
        }
    }

    // --- Mute ---------------------------------------------------------------

    public static void SetMusicMuted(bool muted)
    {
        EnsurePrefsLoaded();
        s_musicMuted = muted;
        PlayerPrefs.SetInt(MusicKey, muted ? 1 : 0);
        PlayerPrefs.Save();
        ApplyToAll();
    }

    public static void SetSfxMuted(bool muted)
    {
        EnsurePrefsLoaded();
        s_sfxMuted = muted;
        PlayerPrefs.SetInt(SfxKey, muted ? 1 : 0);
        PlayerPrefs.Save();
        ApplyToAll();
    }

    public static void ToggleMusic() => SetMusicMuted(!MusicMuted);
    public static void ToggleSfx() => SetSfxMuted(!SfxMuted);

    // Static setter, instance state: find whoever is live and tell them. There
    // is normally exactly one, and none during a scene load, which is why this
    // is a search rather than a stored reference.
    private static void ApplyToAll()
    {
        foreach (AudioDirector d in FindObjectsByType<AudioDirector>(FindObjectsInactive.Include))
            d.ApplyMute();
        MuteChanged?.Invoke();
    }

    private void ApplyMute()
    {
        if (_musicSource != null) _musicSource.mute = s_musicMuted;
        if (_sfxSource != null) _sfxSource.mute = s_sfxMuted;
    }
}
