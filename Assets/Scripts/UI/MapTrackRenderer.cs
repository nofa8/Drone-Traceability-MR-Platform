using UnityEngine;
using System.Collections.Generic;

public class MapTrackRenderer : MonoBehaviour
{
    public LineRenderer lineRenderer;
    public int maxPoints = 100; 
    public float minDistanceMeters = 2.0f; // Store points based on real distance

    // STORE GPS (Lat/Lon), NOT PIXELS
    private Queue<Vector2> gpsPoints = new Queue<Vector2>(); 
    private Vector2 lastGpsPoint;

    public void Setup(Color color)
    {
        if (lineRenderer)
        {
            lineRenderer.startColor = color;
            lineRenderer.endColor = new Color(color.r, color.g, color.b, 0.0f);
            lineRenderer.positionCount = 0;
            lineRenderer.useWorldSpace = false;
            lineRenderer.alignment = LineAlignment.TransformZ;
            lineRenderer.widthMultiplier = 20.0f; 
        }
    }

    // Called whenever new Telemetry arrives
    public void AddGpsPoint(double lat, double lon)
    {
        if (!lineRenderer) return;

        Vector2 newGps = new Vector2((float)lat, (float)lon);

        // Optimization: Filter out jitter using meters
        if (gpsPoints.Count > 0 && GeoUtils.DistanceMeters(newGps, lastGpsPoint) < minDistanceMeters)
            return;

        gpsPoints.Enqueue(newGps);
        if (gpsPoints.Count > maxPoints)
            gpsPoints.Dequeue();

        lastGpsPoint = newGps;
        
        // Draw immediately
        Refresh();
    }

    // Called by MapPanelController when the map Zooms/Pans
    public void Refresh()
    {
        if (!lineRenderer || !GeoMapContext.Instance) return;

        lineRenderer.positionCount = gpsPoints.Count;
        int i = 0;
        
        foreach (Vector2 gps in gpsPoints)
        {
            // Ask GeoMapContext for the CURRENT screen pixel position
            Vector2 screenPos = GeoMapContext.Instance.GeoToScreenPosition(gps.x, gps.y);
            
            // Z = -2.5f ensures it draws above the map
            lineRenderer.SetPosition(i, new Vector3(screenPos.x, screenPos.y, -2.5f)); 
            i++;
        }
    }

    public void Clear()
    {
        gpsPoints.Clear();
        if (lineRenderer) lineRenderer.positionCount = 0;
    }

    public void SetColor(Color color)
    {
        if (lineRenderer)
        {
            lineRenderer.startColor = color;
            lineRenderer.endColor = new Color(color.r, color.g, color.b, 0.0f);
        }
    }
}