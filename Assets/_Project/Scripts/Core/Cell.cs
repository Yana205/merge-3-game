using UnityEngine;

// CACHE AUDIT (Lesson 3.1)
// - CreateSquareSprite(): built a fresh 64x64 Texture2D for every cell that
//   lacked a sprite (25 identical textures on a 5x5 grid). The sprite is now
//   cached in the static _squareSprite field, mirroring Item.GetWhiteSquare().
[ExecuteInEditMode]
public class Cell : MonoBehaviour
{
    [Header("Grid Position (set by GridManager)")]
    public int row;
    public int col;

    public Item CurrentItem { get; private set; }

    [Header("Crystal socket look")]
    [Tooltip("Base colour of the crystal socket the gem sits in (the visible frame around each gem).")]
    [SerializeField] private Color socketColor = new Color(0.16f, 0.22f, 0.46f, 1f);
    [Tooltip("Peak colour the socket shimmers up to, so the frame feels alive.")]
    [SerializeField] private Color socketGlow = new Color(0.34f, 0.55f, 0.95f, 1f);
    [SerializeField] private float shimmerSpeed = 1.6f;

    private SpriteRenderer sr;
    private Color originalColor;
    private bool _highlighted;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite == null)
            sr.sprite = CreateSquareSprite();

        if (sr != null)
        {
            sr.color = socketColor;
            originalColor = socketColor;
        }
    }

    // Gentle per-cell shimmer so the crystal frame breathes. Runs only in play
    // mode and yields to an active drag highlight so input feedback still shows.
    // (The custom MagicalCrystal shader does not render on a URP SpriteRenderer,
    // so the "alive crystal" look is driven here via the sprite's vertex colour,
    // which renders reliably through the default sprite material.)
    void Update()
    {
        if (!Application.isPlaying || sr == null || _highlighted) return;

        float phase = (row + col) * 0.55f;
        float t = 0.5f + 0.5f * Mathf.Sin(Time.time * shimmerSpeed + phase);
        Color c = Color.Lerp(socketColor, socketGlow, t * 0.6f);
        sr.color = c;
        originalColor = c;
    }

    public void SetHighlight(Color color) { _highlighted = true; if (sr != null) sr.color = color; }
    public void ClearHighlight()          { _highlighted = false; if (sr != null) sr.color = originalColor; }

    static Sprite _squareSprite;

    static Sprite CreateSquareSprite()
    {
        if (_squareSprite == null)
        {
            Texture2D tex = new Texture2D(64, 64);
            for (int x = 0; x < 64; x++)
                for (int y = 0; y < 64; y++)
                    tex.SetPixel(x, y, Color.white);
            tex.Apply();
            _squareSprite = Sprite.Create(tex, new Rect(0, 0, 64, 64), Vector2.one * 0.5f, 64);
            _squareSprite.name = "CellSquare";
        }
        return _squareSprite;
    }

    public bool IsOccupied()
    {
        return CurrentItem != null;
    }

    public void PlaceItem(Item item)
    {
        CurrentItem = item;
        item.transform.position = transform.position;
    }

    public void RemoveItem()
    {
        CurrentItem = null;
    }

    public Item GetItem()
    {
        return CurrentItem;
    }
}
