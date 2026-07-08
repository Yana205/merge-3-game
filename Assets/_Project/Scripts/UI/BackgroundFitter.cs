using UnityEngine;

/// <summary>
/// Scales the attached SpriteRenderer so its sprite covers the orthographic
/// camera frustum at any aspect ratio (cover-fit: overscan, never letterbox).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class BackgroundFitter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;

    void Awake()
    {
        Fit();
    }

    public void Fit()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null || !targetCamera.orthographic)
        {
            Debug.LogError("BackgroundFitter: no orthographic camera found.");
            return;
        }

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            Debug.LogError("BackgroundFitter: missing SpriteRenderer or sprite.");
            return;
        }

        Vector2 spriteSize = spriteRenderer.sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            return;

        float worldHeight = 2f * targetCamera.orthographicSize;
        float worldWidth = worldHeight * targetCamera.aspect;

        float scale = Mathf.Max(worldWidth / spriteSize.x, worldHeight / spriteSize.y);
        transform.localScale = new Vector3(scale, scale, 1f);

        // Keep the background centered on the camera (XY only).
        Vector3 camPos = targetCamera.transform.position;
        transform.position = new Vector3(camPos.x, camPos.y, transform.position.z);
    }
}
