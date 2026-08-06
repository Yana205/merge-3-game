using UnityEngine;

/// <summary>
/// All of the game's sound, in one place, driven entirely by the
/// <see cref="GameEvents"/> bus — the same shape as <see cref="JuiceDirector"/>,
/// which handles the visual half of the same moments. No gameplay system
/// references this, and it references none of them.
///
/// Clips are plain serialized references rather than Addressables. The
/// Addressables path is exactly what makes PRESS START die on itch when content
/// is not built before the player, and audio gains nothing from it — a direct
/// reference is included in the build automatically and cannot fail to load.
///
/// Drop this on any always-present GameObject alongside JuiceDirector.
/// </summary>
public class AudioDirector : MonoBehaviour
{
    [Header("Music")]
    [SerializeField] private AudioClip musicLoop;
    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 0.35f;

    [Header("Sound effects")]
    [SerializeField] private AudioClip mergeClip;
    [SerializeField] private AudioClip shatterClip;
    [SerializeField] private AudioClip pickaxeArmClip;
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 0.7f;

    [Header("Merge pitch ladder")]
    [Tooltip("Pitch of a tier-1 merge. Each tier above multiplies by the step " +
             "below, so merging up the ladder literally sounds like climbing.")]
    [Range(0.5f, 1.5f)]
    [SerializeField] private float basePitch = 0.85f;

    [Tooltip("Pitch multiplier per tier. 1.09 is roughly one semitone, so seven " +
             "tiers span about a fifth — audible as progress without the top of " +
             "the ladder turning into a whistle.")]
    [Range(1.0f, 1.3f)]
    [SerializeField] private float pitchPerTier = 1.09f;

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
        GameEvents.TileMerged += HandleTileMerged;
        GameEvents.TileShattered += HandleTileShattered;
        GameEvents.PickaxeChanged += HandlePickaxeChanged;
    }

    void OnDisable()
    {
        GameEvents.TileMerged -= HandleTileMerged;
        GameEvents.TileShattered -= HandleTileShattered;
        GameEvents.PickaxeChanged -= HandlePickaxeChanged;
    }

    // --- Bus handlers -------------------------------------------------------

    // Pitch rises with the tier that was PRODUCED, so a merge into tier 5 sounds
    // higher than one into tier 2 and a run up the ladder reads as an ascending
    // line rather than the same click seven times.
    private void HandleTileMerged(Item item, Cell cell)
    {
        if (mergeClip == null || _sfxSource == null) return;

        int tier = item != null ? item.Tier : 1;
        _sfxSource.pitch = basePitch * Mathf.Pow(pitchPerTier, Mathf.Max(0, tier - 1));
        _sfxSource.PlayOneShot(mergeClip, sfxVolume);
    }

    private void HandleTileShattered(Item item, Cell cell)
    {
        if (shatterClip == null || _sfxSource == null) return;

        // Flat pitch: the shatter is the same act whatever it destroys, and it
        // costs a charge either way.
        _sfxSource.pitch = 1f;
        _sfxSource.PlayOneShot(shatterClip, sfxVolume);
    }

    // Only the arming edge is worth a sound. Charge awards are silent — they
    // happen mid-merge and would collide with the merge chime; spending is already
    // covered by the shatter.
    private bool _wasArmed;
    private void HandlePickaxeChanged(int charges, bool armed)
    {
        if (armed && !_wasArmed && pickaxeArmClip != null && _sfxSource != null)
        {
            _sfxSource.pitch = 1f;
            _sfxSource.PlayOneShot(pickaxeArmClip, sfxVolume);
        }
        _wasArmed = armed;
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
