using UnityEngine;
using System.Collections.Generic;

public class MapTrackRenderer : MonoBehaviour
{
    public LineRenderer lineRenderer;
    public int maxPoints = 50; // History length
    public float minDistance = 1.0f; // Meters (approx) before adding new point

    private Queue<Vector2> points = new Queue<Vector2>();
    private Vector2 lastPoint;

    public void Setup(Color color)
    {
        if (lineRenderer)
        {
            lineRenderer.startColor = color;
            lineRenderer.endColor = new Color(color.r, color.g, color.b, 0.1f); // Fade out
            lineRenderer.positionCount = 0;
            lineRenderer.useWorldSpace = false; // CRITICAL for UI
        }
    }

    public void AddPoint(Vector2 localPos)
    {
        if (!lineRenderer) return;

        // Optimization: Don't record if we haven't moved much (threshold in UI pixels)
        if (points.Count > 0 && Vector2.Distance(localPos, lastPoint) < minDistance)
            return;

        points.Enqueue(localPos);
        if (points.Count > maxPoints)
            points.Dequeue();

        lastPoint = localPos;
        UpdateLine();
    }

    private void UpdateLine()
    {
        lineRenderer.positionCount = points.Count;
        int i = 0;
        
        // Z = -1 to ensure it draws In Front of the map image but Behind the marker
        foreach (Vector2 p in points)
        {
            lineRenderer.SetPosition(i, new Vector3(p.x, p.y, -0.1f)); 
            i++;
        }
    }
}