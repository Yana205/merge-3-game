using UnityEngine;

/// <summary>
/// One gem on the board. Its <see cref="Tier"/> is the match-3 colour index
/// (1..GemPalette.Count); the sprite it wears comes from the Standard gem ladder
/// via <see cref="GemPalette.SpriteTierFor"/>, and a soft glow quad behind the
/// sprite carries the vivid identity colour — the crystals' shared dark frame
/// made the raw sprites too similar to tell apart at board size.
/// </summary>
public class Item : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Config")]
    [SerializeField] private GemConfig gemConfig;

    [Header("Glow backing")]
    [Tooltip("Glow quad diameter as a multiple of the gem sprite's width.")]
    [Range(0.8f, 2f)]
    [SerializeField] private float glowScale = 1.25f;

    [Range(0f, 1f)]
    [SerializeField] private float glowAlpha = 0.55f;

    private static GemConfig _sharedConfig;
    public static int MaxTier => _sharedConfig != null ? _sharedConfig.MaxTier : GemTierTable.Count;

    static Sprite _whiteSquare;
    static Sprite _glowSprite;

    /// <summary>The gem's colour index — its whole identity in match-3.</summary>
    public int Tier { get; private set; }

    /// <summary>Legacy family axis. Match-3 only ever spawns Standard; kept because
    /// GemConfig and ItemFactory are organised around it.</summary>
    public GemFamily Family { get; private set; }

    public GemTierData GemData { get; private set; }

    /// <summary>The vivid signature colour of this gem — for sparks and popups.
    /// Stones are red on purpose: their sprites are the red eye crystals.</summary>
    public Color SignatureColor => IsStone ? new Color(1f, 0.42f, 0.30f) : GemPalette.ColorFor(Tier);

    /// <summary>
    /// True when this item is a STONE — an eye crystal squatting on a cell. A
    /// stone never matches (its Tier is 0), can't be selected or swapped, and is
    /// broken by clearing a match on an orthogonally adjacent cell: the first hit
    /// cracks it (the eye goes dark), the second shatters it for points.
    /// </summary>
    public bool IsStone { get; private set; }

    /// <summary>Hits left before the stone shatters (2 = intact, 1 = cracked).</summary>
    public int StoneHp { get; private set; }

    // Direct (parent -> child) event: raised when this Item is about to return to
    // the pool, passing itself so its owner can react at the item's last position
    // (a clear burst, a sound). The subscriber list is cleared at the end of
    // ResetForPool — see the note there — so a recycled instance never carries a
    // previous owner's handler.
    public event System.Action<Item> OnDespawned;

    private SpriteRenderer _glow;

    /// <summary>
    /// The gem's authored resting scale, captured from the prefab at Awake. The
    /// prefab is NOT scale 1 (it is sized to fit the cell), so every animation
    /// that scales a gem — pops, squashes, pulses, slides — must multiply this,
    /// never Vector3.one. Assuming 1 is exactly how refilled gems once spawned
    /// half again larger than the board they fell into.
    /// </summary>
    public Vector3 BaseScale { get; private set; } = Vector3.one;

    void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        // Runs once at Instantiate, before any animation can have touched the
        // transform — this IS the prefab's authored size.
        BaseScale = transform.localScale;
    }

    public void Setup(int tier, GemFamily family = GemFamily.Standard)
    {
        Tier = tier;
        Family = family;

        if (gemConfig != null)
        {
            _sharedConfig = gemConfig;
            // The colour index picks which ladder sprite it wears — see GemPalette.
            GemData = gemConfig.GetTier(GemPalette.SpriteTierFor(tier), GemFamily.Standard);
        }

        if (GemData != null && GemData.sprite != null)
        {
            spriteRenderer.sprite = GemData.sprite;
            spriteRenderer.color = Color.white;
        }
        else
        {
            // No GemConfig wired — fall back to a flat coloured square so the board
            // stays playable (colour identity survives even with zero art).
            spriteRenderer.sprite = GetWhiteSquare();
            spriteRenderer.color = GemPalette.ColorFor(tier);
        }

        UpdateGlow();
    }

    /// <summary>
    /// Turn this item into a stone blocker. Wears the red eye-crystal ladder
    /// (already wired in GemConfig.redTiers): awake eye while intact, dark
    /// dormant eye once cracked. Tier stays 0 so no match can ever include it.
    /// </summary>
    public void SetupStone(int hp = 2)
    {
        Tier = 0;
        Family = GemFamily.Red;
        IsStone = true;
        StoneHp = Mathf.Max(1, hp);

        if (gemConfig != null)
            _sharedConfig = gemConfig;

        ApplyStoneLook();
    }

    /// <summary>
    /// One hit from an adjacent match. Returns true when the stone shatters
    /// (the caller despawns it); otherwise the eye goes dark and it holds on.
    /// </summary>
    public bool DamageStone()
    {
        if (!IsStone) return false;
        StoneHp--;
        if (StoneHp <= 0) return true;
        ApplyStoneLook();
        return false;
    }

    // Intact = the awake red eye (red tier 3); cracked = the dark dormant one
    // (red tier 1). Both carry a scoreValue the break can award.
    private void ApplyStoneLook()
    {
        GemData = gemConfig != null ? gemConfig.GetTier(StoneHp >= 2 ? 3 : 1, GemFamily.Red) : null;

        if (GemData != null && GemData.sprite != null)
        {
            spriteRenderer.sprite = GemData.sprite;
            spriteRenderer.color = Color.white;
        }
        else
        {
            spriteRenderer.sprite = GetWhiteSquare();
            spriteRenderer.color = new Color(0.45f, 0.20f, 0.18f);
        }

        UpdateGlow();
    }

    // The glow child is created lazily and reused for the item's whole pooled life.
    private void UpdateGlow()
    {
        if (_glow == null)
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(transform, false);
            _glow = go.AddComponent<SpriteRenderer>();
            _glow.sprite = GetGlowSprite();
        }

        // Stones smoulder rather than shine — dimmer, so a blocker never
        // out-glows the gems the player can actually use.
        Color c = SignatureColor;
        c.a = IsStone ? glowAlpha * 0.45f : glowAlpha;
        _glow.color = c;

        // Cells draw at order 0. The gem must sit at 2+ so the glow can take the
        // slot between them — at the cell's own order the glow ties with the cell
        // background and loses.
        spriteRenderer.sortingOrder = Mathf.Max(spriteRenderer.sortingOrder, 2);
        _glow.sortingLayerID = spriteRenderer.sortingLayerID;
        _glow.sortingOrder = spriteRenderer.sortingOrder - 1;
        _glow.enabled = true;

        // Size the glow from the sprite it backs, so it tracks any art swap.
        float gemWidth = spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.size.x : 1f;
        _glow.transform.localScale = Vector3.one * gemWidth * glowScale;
    }

    /// <summary>
    /// Clears per-life state before the item goes back into the pool, so a
    /// recycled instance never leaks the previous gem's colour, data, or visuals.
    /// </summary>
    public void ResetForPool()
    {
        // Announce the despawn BEFORE clearing state, so listeners can still read
        // this item's final colour / GemData / world position (e.g. to spawn a
        // burst there). ?.Invoke keeps this safe when nobody is subscribed.
        OnDespawned?.Invoke(this);

        Tier = 0;
        Family = GemFamily.Standard;
        GemData = null;
        IsStone = false;
        StoneHp = 0;

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = null;
            spriteRenderer.color = Color.white;
        }

        if (_glow != null)
            _glow.enabled = false;

        // Pop/land animations scale the transform; make sure a recycled instance
        // never inherits a mid-animation scale.
        transform.localScale = BaseScale;
        transform.rotation = Quaternion.identity;

        // Pooled-object cleanup: unsubscription happens on return to pool, not in
        // OnDestroy (a pooled item is rarely destroyed). Dropping every subscriber
        // here means the recycled instance starts its next life with a clean event.
        OnDespawned = null;
    }

    static Sprite GetWhiteSquare()
    {
        if (_whiteSquare == null)
        {
            Texture2D tex = new Texture2D(64, 64);
            Color[] pixels = new Color[64 * 64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            _whiteSquare = Sprite.Create(tex, new Rect(0, 0, 64, 64), Vector2.one * 0.5f, 64);
            _whiteSquare.name = "ItemWhiteSquare";
        }
        return _whiteSquare;
    }

    // A soft radial falloff, generated once — the same no-imported-assets approach
    // as the white square. White so SpriteRenderer.color does all the tinting.
    static Sprite GetGlowSprite()
    {
        if (_glowSprite == null)
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] px = new Color[size * size];
            float half = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
                    // Smooth quadratic falloff to fully transparent at the rim.
                    float a = Mathf.Clamp01(1f - d);
                    px[y * size + x] = new Color(1f, 1f, 1f, a * a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            _glowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            _glowSprite.name = "GemGlow";
        }
        return _glowSprite;
    }
}
