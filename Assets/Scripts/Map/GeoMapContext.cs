using UnityEngine;
using System;

public class GeoMapContext : MonoBehaviour
{
    public static GeoMapContext Instance;

    [Header("Geographic Origin (Center)")]
    public double originLat;
    public double originLon;

    [Header("Scale State")]
    public float pixelsPerMeter = 1.0f; // Zoom level
    
    // Events for when the map moves/zooms
    public event Action OnMapUpdated;

    void Awake()
    {
        Instance = this;
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

    // The "Contract": Convert Lat/Lon to Local UI Pixels
    public Vector2 GeoToScreenPosition(double lat, double lon)
    {
        // 1. Get distance in meters from center
        Vector2 meters = GeoUtils.LatLonToMeters(lat, lon, originLat, originLon);
        
        // 2. Scale to pixels
        return meters * pixelsPerMeter;
    }
    
    // Reverse "Contract": Screen Pixels to Lat/Lon (for Panning)
    public Vector2 ScreenToGeoPosition(Vector2 screenDelta)
    {
         // Convert pixels -> meters -> Lat/Lon
         Vector2 meters = screenDelta / pixelsPerMeter;
         return GeoUtils.MetersToLatLon(meters, originLat, originLon);
    }
}