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
    /// Muted state, shared by every instance and readable by the HUD before this
    /// component has woken up. Static because the mute button lives in the UI
    /// layer, which has no reference to this object — and because the setting
    /// should survive a scene reload, which the restart flow does on every run.
    /// </summary>
    public static bool Muted { get; private set; }

    private const string MuteKey = "AudioMuted";

    void Awake()
    {
        Muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;

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
    }

    void OnDisable()
    {
        GameEvents.MatchResolved -= HandleMatchResolved;
        GameEvents.SwapDenied -= HandleSwapDenied;
        GameEvents.GemSelected -= HandleGemSelected;
    }

    // --- Bus handlers -------------------------------------------------------

    // One pop per cleared GROUP, not per gem — five gems popping at once should be
    // one satisfying burst, not five copies of the same clip clipping the mixer.
    private void HandleMatchResolved(int combo, int gemCount, int points, Vector3 centre)
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

    // --- Mute ---------------------------------------------------------------

    public static void SetMuted(bool muted)
    {
        Muted = muted;
        PlayerPrefs.SetInt(MuteKey, muted ? 1 : 0);
        PlayerPrefs.Save();

        // Static setter, instance state: find whoever is live and tell them. There
        // is normally exactly one, and none during a scene load, which is why this
        // is a search rather than a stored reference.
        foreach (AudioDirector d in FindObjectsByType<AudioDirector>(FindObjectsSortMode.None))
            d.ApplyMute();
    }

    private void ApplyMute()
    {
        if (_musicSource != null) _musicSource.mute = Muted;
        if (_sfxSource != null) _sfxSource.mute = Muted;
    }
}
