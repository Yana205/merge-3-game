using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Generates the intro banner's non-art layers as textures at runtime: the two
/// gradient washes and the scanline tile.
///
/// None of these can be authored as a sprite without shipping a bitmap per
/// tweak — uGUI has no gradient fill, and the vignette is a radial with an
/// off-centre origin and an elliptical extent. Baking them here keeps the
/// values editable in the inspector.
///
/// Everything bakes at the art's native 320x180 (or a 4px tile for the
/// scanlines) on Point filtering, so the result bands on whole pixels the way
/// the rest of the kit does instead of smoothing across the upscale.
/// </summary>
/// <remarks>
/// [ExecuteAlways] so the layers are visible while authoring. Without it the
/// Image has no sprite outside play mode and renders as a solid white rect that
/// hides the whole banner in the scene and game views.
/// </remarks>
[ExecuteAlways]
[RequireComponent(typeof(Image))]
public class BannerOverlay : MonoBehaviour
{
    public enum OverlayMode
    {
        /// <summary>Top-to-bottom wash. CSS: linear-gradient(180deg, ...).</summary>
        VerticalWash,

        /// <summary>Off-centre elliptical vignette. CSS: radial-gradient(120% 80% at 50% 22%, ...).</summary>
        RadialVignette,

        /// <summary>Repeating horizontal scanlines, tiled up the rect.</summary>
        Scanlines,
    }

    [Header("Layer")]
    [SerializeField] private OverlayMode _mode = OverlayMode.VerticalWash;

    [Header("Gradient — wash and vignette")]
    [Tooltip("Colour AND alpha keys are both read; the CSS stops map onto them 1:1.")]
    [SerializeField] private Gradient _gradient = new Gradient();

    [Tooltip("Native size the gradient bakes at. 320x180 keeps the banding on the art's own pixel grid.")]
    [SerializeField] private int _bakeWidth = 320;
    [SerializeField] private int _bakeHeight = 180;

    [Header("Radial vignette shape")]
    [Tooltip("Normalised origin, measured from the TOP-left to match CSS 'at 50% 22%'.")]
    [SerializeField] private Vector2 _radialCenter = new Vector2(0.5f, 0.22f);

    [Tooltip("Ellipse radii as a fraction of the rect, matching CSS '120% 80%'.")]
    [SerializeField] private Vector2 _radialExtent = new Vector2(1.2f, 0.8f);

    [Header("Scanlines")]
    [Tooltip("Tile height in pixels — one dark band plus one gap.")]
    [SerializeField] private int _linePeriod = 4;

    [Tooltip("How many of those pixels are the dark band.")]
    [SerializeField] private int _lineThickness = 2;

    [SerializeField] private Color _lineColor = new Color(15f / 255f, 10f / 255f, 32f / 255f, 0.34f);

    // A sprite created at the Canvas's own reference PPU maps one texel to one
    // canvas pixel, so a 4px scanline tile stays 4px on a 1280x720 reference.
    private const float CanvasReferencePixelsPerUnit = 100f;

    // Both are created here, so both are destroyed here — generated textures and
    // sprites are not collected along with the GameObject.
    private Texture2D _texture;
    private Sprite _sprite;

    void Awake()
    {
        Image image = GetComponent<Image>();
        if (image == null)
        {
            Debug.LogError("BannerOverlay: no Image component on '" + name + "'.");
            return;
        }

        _texture = Build();
        if (_texture == null)
        {
            Debug.LogError("BannerOverlay: could not build the '" + _mode + "' texture on '" + name + "'.");
            return;
        }

        _texture.filterMode = FilterMode.Point;
        _texture.wrapMode = _mode == OverlayMode.Scanlines
            ? TextureWrapMode.Repeat
            : TextureWrapMode.Clamp;
        _texture.Apply();

        _sprite = Sprite.Create(
            _texture,
            new Rect(0f, 0f, _texture.width, _texture.height),
            new Vector2(0.5f, 0.5f),
            CanvasReferencePixelsPerUnit,
            0,
            SpriteMeshType.FullRect);

        image.sprite = _sprite;
        image.color = Color.white;

        // The washes stretch across the whole banner; the scanlines repeat.
        image.type = _mode == OverlayMode.Scanlines ? Image.Type.Tiled : Image.Type.Simple;
    }

    void OnDestroy()
    {
        // Destroy() is a no-op outside play mode, and [ExecuteAlways] means this
        // runs there too — so the generated pair would leak on every recompile.
        if (Application.isPlaying)
        {
            if (_sprite != null) Destroy(_sprite);
            if (_texture != null) Destroy(_texture);
        }
        else
        {
            if (_sprite != null) DestroyImmediate(_sprite);
            if (_texture != null) DestroyImmediate(_texture);
        }
    }

    Texture2D Build()
    {
        switch (_mode)
        {
            case OverlayMode.VerticalWash: return BuildVerticalWash();
            case OverlayMode.RadialVignette: return BuildRadialVignette();
            case OverlayMode.Scanlines: return BuildScanlines();
            default: return null;
        }
    }

    // One pixel wide is enough — the Image stretches it across the banner.
    Texture2D BuildVerticalWash()
    {
        int height = Mathf.Max(2, _bakeHeight);
        var texture = NewTexture(1, height);
        var pixels = new Color[height];

        for (int y = 0; y < height; y++)
            pixels[y] = _gradient.Evaluate(CssRowToGradientTime(y, height));

        texture.SetPixels(pixels);
        return texture;
    }

    Texture2D BuildRadialVignette()
    {
        int width = Mathf.Max(2, _bakeWidth);
        int height = Mathf.Max(2, _bakeHeight);
        var texture = NewTexture(width, height);
        var pixels = new Color[width * height];

        // Guard the divides: a zero extent would collapse the ellipse.
        float extentX = Mathf.Max(0.0001f, _radialExtent.x);
        float extentY = Mathf.Max(0.0001f, _radialExtent.y);

        for (int y = 0; y < height; y++)
        {
            // CSS measures the origin from the top, textures from the bottom.
            float cssY = CssRowToGradientTime(y, height);
            float dy = (cssY - _radialCenter.y) / extentY;

            for (int x = 0; x < width; x++)
            {
                float cssX = (x + 0.5f) / width;
                float dx = (cssX - _radialCenter.x) / extentX;

                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                pixels[y * width + x] = _gradient.Evaluate(Mathf.Clamp01(distance));
            }
        }

        texture.SetPixels(pixels);
        return texture;
    }

    Texture2D BuildScanlines()
    {
        int period = Mathf.Max(1, _linePeriod);
        int thickness = Mathf.Clamp(_lineThickness, 0, period);

        var texture = NewTexture(1, period);
        var pixels = new Color[period];

        for (int y = 0; y < period; y++)
        {
            // Count bands from the top so the tile reads the way the CSS does.
            int rowFromTop = period - 1 - y;
            pixels[y] = rowFromTop < thickness ? _lineColor : Color.clear;
        }

        texture.SetPixels(pixels);
        return texture;
    }

    // Texture row -> gradient time, flipped so key 0 is the TOP of the banner.
    static float CssRowToGradientTime(int y, int height)
    {
        return 1f - ((y + 0.5f) / height);
    }

    static Texture2D NewTexture(int width, int height)
    {
        return new Texture2D(width, height, TextureFormat.RGBA32, false, false)
        {
            name = "BannerOverlay_Generated",
            hideFlags = HideFlags.DontSave,
        };
    }
}
