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
    public static int MaxTier => _sharedConfig != null ? _sharedConfig.MaxTier : 9;

    static Sprite _whiteSquare;

    public int Tier { get; private set; }
    public GemTierData GemData { get; private set; }

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

            if (tierLabel != null)
                tierLabel.text = GemData.gemName;
        }
        else
        {
            spriteRenderer.sprite = GetWhiteSquare();
            spriteRenderer.color = Color.white;
            if (tierLabel != null)
                tierLabel.text = tier.ToString();
        }
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
