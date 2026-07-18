using UnityEngine;
using TMPro;

public class Item : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private TextMeshPro tierLabel;

    [Header("Config")]
    [SerializeField] private GemConfig gemConfig;

    private static GemConfig _sharedConfig;
    public static int MaxTier => _sharedConfig != null ? _sharedConfig.MaxTier : GemTierTable.Count;

    static Sprite _whiteSquare;

    public int Tier { get; private set; }
    public GemTierData GemData { get; private set; }

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

        if (tierLabel == null)
            tierLabel = GetComponentInChildren<TextMeshPro>();

        // Label MeshRenderer must render above the SpriteRenderer
        if (tierLabel != null)
        {
            MeshRenderer labelRenderer = tierLabel.GetComponent<MeshRenderer>();
            if (labelRenderer != null)
            {
                labelRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
                labelRenderer.sortingOrder = spriteRenderer.sortingOrder + 1;
            }
        }
    }

    public void Setup(int tier)
    {
        Tier = tier;

        if (gemConfig != null)
        {
            _sharedConfig = gemConfig;
            GemData = gemConfig.GetTier(tier);

            if (GemData.sprite != null)
            {
                spriteRenderer.sprite = GemData.sprite;
                spriteRenderer.color = Color.white;
            }
            else
            {
                spriteRenderer.sprite = GetWhiteSquare();
                spriteRenderer.color = GemData.tintColor;
            }

            // Full gem names overflow the cell; the number keeps tiers readable.
            if (tierLabel != null)
            {
                tierLabel.text = tier.ToString();
                tierLabel.color = GemTierTable.LabelColorFor(tier);
            }
        }
        else
        {
            // No GemConfig assigned — fall back to the built-in tier ladder so the
            // merge progression is readable (distinct color per level + number).
            spriteRenderer.sprite = GetWhiteSquare();
            spriteRenderer.color = GemTierTable.ColorFor(tier);
            if (tierLabel != null)
            {
                tierLabel.text = tier.ToString();
                tierLabel.color = GemTierTable.LabelColorFor(tier);
            }
        }
    }

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
        GemData = null;

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = null;
            spriteRenderer.color = Color.white;
        }

        if (tierLabel != null)
            tierLabel.text = "";

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
