using UnityEngine;

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
        {
            sr.sprite = CreateSquareSprite();
            sr.color = new Color(0.15f, 0.15f, 0.15f);
        }
        if (sr != null) originalColor = sr.color;
    }

    public void SetHighlight(Color color) { if (sr != null) sr.color = color; }
    public void ClearHighlight()          { if (sr != null) sr.color = originalColor; }

    static Sprite CreateSquareSprite()
    {
        Texture2D tex = new Texture2D(64, 64);
        for (int x = 0; x < 64; x++)
            for (int y = 0; y < 64; y++)
                tex.SetPixel(x, y, Color.white);
        tex.Apply();
        Sprite s = Sprite.Create(tex, new Rect(0, 0, 64, 64), Vector2.one * 0.5f, 64);
        s.name = "CellSquare";
        return s;
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
