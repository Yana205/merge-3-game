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

    private SpriteRenderer sr;
    private Color originalColor;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite == null)
            sr.sprite = CreateSquareSprite();

        // The cell stays white so the animated MagicalCrystal shader shows its
        // own colours; the sprite is just an alpha mask. SetHighlight tints the
        // crystal for drag feedback, ClearHighlight restores it.
        if (sr != null)
        {
            sr.color = Color.white;
            originalColor = Color.white;
        }
    }

    public void SetHighlight(Color color) { if (sr != null) sr.color = color; }
    public void ClearHighlight()          { if (sr != null) sr.color = originalColor; }

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
