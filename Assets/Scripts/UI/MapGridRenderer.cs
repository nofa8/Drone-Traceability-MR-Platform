using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class MapGridRenderer : MonoBehaviour
{
    [Header("Settings")]
    public float gridSizeMeters = 100f; // 1 Line every 100m
    
    private RawImage gridImage;

    void Start()
    {
        gridImage = GetComponent<RawImage>();
        
        // Listen to the Brain
        if (GeoMapContext.Instance != null)
        {
            GeoMapContext.Instance.OnMapUpdated += RefreshGrid;
            RefreshGrid(); // Initial draw
        }
    }

    void OnDestroy()
    {
        if (GeoMapContext.Instance != null)
            GeoMapContext.Instance.OnMapUpdated -= RefreshGrid;
    }

    void RefreshGrid()
    {
        if (!GeoMapContext.Instance || !gridImage) {
            Debug.LogWarning("❌ Missing: GeoMapContext or RawImage");
            return;
        }
            

        float pixelsPerMeter = GeoMapContext.Instance.pixelsPerMeter;

        // 1. Scale the UVs so the texture repeats every "100 meters"
        // If 100m = 100px, and our image is 500px, we need 5 tiles.
        float tileCountX = gridImage.rectTransform.rect.width / (gridSizeMeters * pixelsPerMeter);
        float tileCountY = gridImage.rectTransform.rect.height / (gridSizeMeters * pixelsPerMeter);

        // 2. Offset the UVs based on where the center Lat/Lon is
        // This makes the grid "slide" when we pan the map
        // (We assume Origin (0,0) is at UV (0.5, 0.5))
        
        // Trick: We don't need absolute position, just the modulo relative to grid size
        // But for simplicity in Phase 4, let's just scale.
        
        Rect uvRect = gridImage.uvRect;
        uvRect.width = tileCountX;
        uvRect.height = tileCountY;
        
        // Center the grid: Shift by half so lines align
        uvRect.x = -tileCountX / 2f;
        uvRect.y = -tileCountY / 2f;
        
        // Note: For true "sliding" relative to Earth, we would add the GeoMapContext origin offset here.
        // For Step 1 (Visual Validation), static scaling is enough to verify ZOOM.
        
        gridImage.uvRect = uvRect;
    }
}