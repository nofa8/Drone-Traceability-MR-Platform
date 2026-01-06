using UnityEngine;
using System;

public class GeoMapContext : MonoBehaviour
{
    public static GeoMapContext Instance;

    [Header("Geographic Origin (Center)")]
    public double originLat;
    public double originLon;

    [Header("Scale State")]
    public float pixelsPerMeter = 1.0f; // 1.0 means 1 meter = 1 pixel
    
    // Events: "The world moved"
    public event Action OnMapUpdated;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void SetCenter(double lat, double lon)
    {
        originLat = lat;
        originLon = lon;
        OnMapUpdated?.Invoke();
    }

    public void SetZoom(float newScale)
    {
        pixelsPerMeter = newScale;
        OnMapUpdated?.Invoke();
    }

    // --- THE CONTRACT ---

    // Converts Real World Lat/Lon -> UI Pixels (Relative to Center)
    public Vector2 GeoToScreenPosition(double lat, double lon)
    {
        Vector2 meters = GeoUtils.LatLonToMeters(lat, lon, originLat, originLon);
        return meters * pixelsPerMeter;
    }

    // Converts Screen Drag -> Real World Change (For Panning)
    public Vector2 ScreenToGeoPosition(Vector2 screenDelta)
    {
         Vector2 meters = screenDelta / pixelsPerMeter;
         return GeoUtils.MetersToLatLon(meters, originLat, originLon);
    }
}