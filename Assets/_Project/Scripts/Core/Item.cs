using UnityEngine;

/// <summary>
/// One gem on the board. Its <see cref="Tier"/> is the match-3 colour index
/// (1..GemPalette.Count); the sprite it wears comes from the Standard gem ladder
/// via <see cref="GemPalette.SpriteTierFor"/>, and a soft glow quad behind the
/// sprite carries the vivid identity colour — the crystals' shared dark frame
/// made the raw sprites too similar to tell apart at board size.
///
/// A gem can also be a <see cref="Special"/> (made by a match of four or five):
/// it keeps its colour and matches normally, wears a spinning overlay so the
/// player can read it at a glance, and does something big when it goes off.
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
    static Sprite _crossSprite;
    static Sprite _ringSprite;

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

    /// <summary>What this gem does when it goes off. None for an ordinary gem.</summary>
    public SpecialKind Special { get; private set; }

    public bool IsSpecial => Special != SpecialKind.None;

    // Direct (parent -> child) event: raised when this Item is about to return to
    // the pool, passing itself so its owner can react at the item's last position
    // (a clear burst, a sound). The subscriber list is cleared at the end of
    // ResetForPool — see the note there — so a recycled instance never carries a
    // previous owner's handler.
    public event System.Action<Item> OnDespawned;

    private SpriteRenderer _glow;
    private SpriteRenderer _specialFx;
    private float _specialClock;

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
        if (IsSpecial) UpdateSpecialFx();   // a reshuffle recolours a special in place
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
        SetSpecial(SpecialKind.None);

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

    // --- Specials ---------------------------------------------------------------

    /// <summary>Promote (or demote) this gem. The overlay is created lazily and
    /// reused for the item's whole pooled life, like the glow.</summary>
    public void SetSpecial(SpecialKind kind)
    {
        Special = kind;
        _specialClock = 0f;
        UpdateSpecialFx();
    }

    private void UpdateSpecialFx()
    {
        if (!IsSpecial)
        {
            if (_specialFx != null) _specialFx.enabled = false;
            return;
        }

        if (_specialFx == null)
        {
            var go = new GameObject("SpecialFx");
            go.transform.SetParent(transform, false);
            _specialFx = go.AddComponent<SpriteRenderer>();
        }

        _specialFx.sprite = Special == SpecialKind.Prism ? GetRingSprite() : GetCrossSprite();
        _specialFx.sortingLayerID = spriteRenderer.sortingLayerID;
        _specialFx.sortingOrder = spriteRenderer.sortingOrder + 1;
        _specialFx.enabled = true;

        float gemWidth = spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.size.x : 1f;
        _specialFx.transform.localScale = Vector3.one * gemWidth * (Special == SpecialKind.Prism ? 1.35f : 1.5f);
        _specialFx.transform.localRotation = Quaternion.identity;
        TintSpecial(0f);
    }

    // The overlay lives: a Cross spins its blades, a Prism spins and cycles the
    // whole palette so it reads as "every colour" without a word of tutorial.
    void Update()
    {
        if (!IsSpecial || _specialFx == null || !_specialFx.enabled) return;

        _specialClock += Time.deltaTime;
        float spin = Special == SpecialKind.Prism ? 70f : 45f;
        _specialFx.transform.localRotation = Quaternion.Euler(0f, 0f, _specialClock * spin);

        float pulse = 1f + 0.08f * Mathf.Sin(_specialClock * 6f);
        float gemWidth = spriteRenderer.sprite != null ? spriteRenderer.sprite.bounds.size.x : 1f;
        _specialFx.transform.localScale = Vector3.one * gemWidth * (Special == SpecialKind.Prism ? 1.35f : 1.5f) * pulse;

        TintSpecial(_specialClock);
    }

    private void TintSpecial(float t)
    {
        if (Special == SpecialKind.Prism)
        {
            Color c = Color.HSVToRGB(Mathf.Repeat(t * 0.35f, 1f), 0.75f, 1f);
            c.a = 0.95f;
            _specialFx.color = c;
        }
        else
        {
            Color c = Color.Lerp(SignatureColor, Color.white, 0.55f);
            c.a = 0.9f;
            _specialFx.color = c;
        }
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
        Special = SpecialKind.None;

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = null;
            spriteRenderer.color = Color.white;
        }

        if (_glow != null)
            _glow.enabled = false;
        if (_specialFx != null)
            _specialFx.enabled = false;

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

    // Four soft blades — the Cross gem's "I clear a row and a column" badge.
    static Sprite GetCrossSprite()
    {
        if (_crossSprite == null)
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] px = new Color[size * size];
            float half = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - half) / half, dy = Mathf.Abs(y - half) / half;
                    // A blade is thin near the centre and fades toward the tip.
                    float blade = Mathf.Max(Mathf.Clamp01(1f - dy * 9f) * (1f - dx),
                                            Mathf.Clamp01(1f - dx * 9f) * (1f - dy));
                    float core = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 3f);
                    float a = Mathf.Clamp01(blade * 1.2f + core * 0.6f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            _crossSprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            _crossSprite.name = "GemCross";
        }
        return _crossSprite;
    }

    // A soft ring with four notches — the Prism's badge, tinted by hue cycling.
    static Sprite GetRingSprite()
    {
        if (_ringSprite == null)
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
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) * 9f);
                    float ang = Mathf.Atan2(y - half, x - half);
                    float notch = Mathf.Clamp01(Mathf.Cos(ang * 4f) * 0.5f + 0.7f);
                    px[y * size + x] = new Color(1f, 1f, 1f, ring * notch);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            _ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            _ringSprite.name = "GemRing";
        }
        return _ringSprite;
    }
}
