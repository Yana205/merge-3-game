using UnityEngine;

public class Item : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Config")]
    [SerializeField] private GemConfig gemConfig;

    private static GemConfig _sharedConfig;
    public static int MaxTier => _sharedConfig != null ? _sharedConfig.MaxTier : GemTierTable.Count;

    /// <summary>
    /// Top of the ladder for one family. Each family has its own ceiling — the red
    /// chain is deliberately shorter than the standard one — so every merge and
    /// jam check must ask per family rather than reading <see cref="MaxTier"/>.
    /// Returns 0 for a family with no tiers configured, which disables it.
    /// </summary>
    public static int MaxTierFor(GemFamily family)
    {
        if (_sharedConfig != null) return _sharedConfig.MaxTierFor(family);

        // No config loaded yet: only the built-in standard ladder exists.
        return family == GemFamily.Standard ? GemTierTable.Count : 0;
    }

    static Sprite _whiteSquare;

    public int Tier { get; private set; }

    /// <summary>Which merge chain this gem belongs to. Together with
    /// <see cref="Tier"/> it forms the gem's full identity — both must match for
    /// two gems to merge.</summary>
    public GemFamily Family { get; private set; }

    public GemTierData GemData { get; private set; }

    /// <summary>
    /// True while this gem is a live bomb — a red that reached the top of its
    /// ladder. An armed bomb is not draggable and not mergeable (MergeManager
    /// already rejects a maxed gem); tapping it detonates. See BombController.
    /// </summary>
    public bool IsArmedBomb { get; private set; }

    // The sprite colour this gem had before it armed, so disarming restores the
    // gem rather than forcing it white. Items without configured art are tinted by
    // GemTierTable, so "white" is not a safe default to restore to.
    private Color _colorBeforeArming = Color.white;

    // Direct (parent -> child) event: raised when this Item is about to return to
    // the pool, passing itself so its owner can react at the item's last position
    // (a merge/despawn burst, a sound). The subscriber list is cleared at the end
    // of ResetForPool — see the note there — so a recycled instance never carries
    // a previous owner's handler.
    public event System.Action<Item> OnDespawned;

    void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Setup(int tier, GemFamily family = GemFamily.Standard)
    {
        Tier = tier;
        Family = family;

        if (gemConfig != null)
        {
            _sharedConfig = gemConfig;
            GemData = gemConfig.GetTier(tier, family);
        }

        if (gemConfig != null && GemData != null)
        {
            // Tier is read from the artwork alone: each tier has its own colour
            // AND its own silhouette, so the crystals carry no number overlay.
            // PickSprite rolls the cosmetic variant (the gold-tear crystals) — a
            // look only, never an identity, so nothing else on the item changes.
            Sprite look = GemData.PickSprite();
            if (look != null)
            {
                spriteRenderer.sprite = look;
                spriteRenderer.color = Color.white;
            }
            else
            {
                spriteRenderer.sprite = GetWhiteSquare();
                spriteRenderer.color = GemData.tintColor;
            }
        }
        else
        {
            // No GemConfig assigned, or this family's ladder is unwired — fall back
            // to the built-in tier ladder so the merge progression stays readable
            // (distinct colour per tier).
            spriteRenderer.sprite = GetWhiteSquare();
            spriteRenderer.color = GemTierTable.ColorFor(tier);
        }
    }

    /// <summary>
    /// Arm or disarm this gem as a bomb. Driven by BombController when a red merge
    /// completes the ladder; disarming restores the gem's own colour.
    /// </summary>
    public void SetArmedBomb(bool armed)
    {
        if (IsArmedBomb == armed) return;

        if (armed && spriteRenderer != null)
            _colorBeforeArming = spriteRenderer.color;

        IsArmedBomb = armed;

        if (!armed && spriteRenderer != null)
            spriteRenderer.color = _colorBeforeArming;
    }

    /// <summary>
    /// Drive one frame of the armed-bomb glow. <paramref name="t"/> is 0..1.
    ///
    /// Pushed in by BombController rather than run from an Update() here on purpose:
    /// a board holds 36 Items and at most a couple of them are ever bombs, so an
    /// Update per Item would be 36 calls a frame to do nothing. The one system that
    /// knows which gems are armed drives only those.
    /// </summary>
    public void ApplyBombPulse(float t)
    {
        if (!IsArmedBomb || spriteRenderer == null) return;
        spriteRenderer.color = Color.Lerp(_colorBeforeArming, BombGlow, t);
    }

    // Hot amber. Deliberately outside the cyan/red palette of both ladders: a bomb
    // is not another crystal to sort, it is a button, and it must not read as one
    // more red in the chain the player is building.
    static readonly Color BombGlow = new Color(1f, 0.78f, 0.30f);

    /// <summary>
    /// Clears per-life state before the item goes back into the pool, so a
    /// recycled instance never leaks the previous gem's tier, data, or visuals.
    /// </summary>
    public void ResetForPool()
    {
        // Announce the despawn BEFORE clearing state, so listeners can still read
        // this item's final tier / GemData / world position (e.g. to spawn a burst
        // there). ?.Invoke keeps this safe when nobody is subscribed.
        OnDespawned?.Invoke(this);

        Tier = 0;
        Family = GemFamily.Standard;
        GemData = null;

        // Clear the bomb flag on the way into the pool. BombController prunes its
        // armed list by re-reading this flag, so a recycled instance that stayed
        // "armed" would keep a dead gem in the list forever — and worse, would
        // report a live bomb to the jam check on a board that has none.
        IsArmedBomb = false;
        _colorBeforeArming = Color.white;

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = null;
            spriteRenderer.color = Color.white;
        }

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
}
