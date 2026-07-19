using UnityEngine;

/// <summary>
/// Data-driven definition of one gem type. Behaviour stays in code; everything the
/// designer tunes (id, name, rarity, score, colour, tier range) lives here as data.
///
/// Every field is <c>[SerializeField] private</c> with a read-only getter, so no
/// script outside this class can mutate a definition at runtime — the asset is the
/// single source of truth. Create instances via
/// <b>Assets ▸ Create ▸ Merge Game ▸ Gem Definition</b>.
/// </summary>
[CreateAssetMenu(fileName = "Gem_", menuName = "Merge Game/Gem Definition")]
public class GemDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string gemId;
    [SerializeField] private string displayName;
    [SerializeField] private GemRarity rarity = GemRarity.Common;

    [Header("Gameplay")]
    [SerializeField] private int scoreValue = 10;
    [SerializeField, Min(1)] private int minTier = 1;
    [SerializeField, Min(1)] private int maxTier = 1;

    [Header("Visuals")]
    [SerializeField] private Color tintColor = Color.white;
    [SerializeField] private Sprite sprite;

    // Read-only getters — the only way outside code touches this data.
    public string GemId => gemId;
    public string DisplayName => displayName;
    public GemRarity Rarity => rarity;
    public int ScoreValue => scoreValue;
    public int MinTier => minTier;
    public int MaxTier => maxTier;
    public Color TintColor => tintColor;
    public Sprite Sprite => sprite;

    /// <summary>
    /// Populate this instance from typed values. Used only by the JSON pipeline /
    /// Editor generator (not gameplay), which is why it lives here rather than
    /// exposing public setters that any script could call.
    /// </summary>
    public void Initialize(string id, string name, GemRarity gemRarity, int score,
                           int min, int max, Color tint, Sprite gemSprite = null)
    {
        gemId = id;
        displayName = name;
        rarity = gemRarity;
        scoreValue = score;
        minTier = min;
        maxTier = max;
        tintColor = tint;
        sprite = gemSprite;
    }
}
