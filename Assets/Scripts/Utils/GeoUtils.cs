using UnityEngine;
using System;

public static class GeoUtils
{
    // Earth Radius in Meters
    private const double EarthRadius = 6378137.0;

    // Converts a Lat/Lon point to a local (X,Y) in meters, relative to an Origin point.
    public static Vector2 LatLonToMeters(double lat, double lon, double originLat, double originLon)
    {
        // Standard "Equirectangular Projection" (valid for small areas < 100km)
        // This is faster and simpler than full Mercator for a tactical drone map.

        double dLat = (lat - originLat) * Mathf.Deg2Rad;
        double dLon = (lon - originLon) * Mathf.Deg2Rad;

        // Calculate X (East/West) distance, adjusting for Latitude shrinking
        double x = EarthRadius * dLon * Math.Cos(originLat * Mathf.Deg2Rad);
        
        // Calculate Y (North/South) distance
        double y = EarthRadius * dLat;

        return new Vector2((float)x, (float)y);
    }

    // Helper to calculate distance between two GPS points
    public static float DistanceMeters(Vector2 gpsA, Vector2 gpsB)
    {
        Vector2 dist = LatLonToMeters(gpsA.x, gpsA.y, gpsB.x, gpsB.y);
        return dist.magnitude;
    }


    public static Vector2 MetersToLatLon(Vector2 metersOffset, double originLat, double originLon)
    {
        // 1. Calculate change in Radians
        double dLat = metersOffset.y / EarthRadius;
        // Adjust Longitude change based on Latitude (Cos(lat))
        double dLon = metersOffset.x / (EarthRadius * Math.Cos(originLat * Mathf.Deg2Rad));

        // 2. Convert to Degrees
        double newLat = originLat + (dLat * Mathf.Rad2Deg);
        double newLon = originLon + (dLon * Mathf.Rad2Deg);

        return new Vector2((float)newLat, (float)newLon);
    }
}