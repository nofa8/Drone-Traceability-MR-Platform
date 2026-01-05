using UnityEngine;
using System.Collections.Generic;

public class MapTrackRenderer : MonoBehaviour
{
    public LineRenderer lineRenderer;
    public int maxPoints = 100; 
    
    // Lower threshold (0.5 pixel) to catch slow movement immediately
    public float minDistance = 0.5f; 

    private Queue<Vector2> points = new Queue<Vector2>();
    private Vector2 lastPoint;

    public void Setup(Color color)
    {
        if (lineRenderer)
        {
            lineRenderer.startColor = color;
            lineRenderer.endColor = new Color(color.r, color.g, color.b, 0.0f);
            lineRenderer.positionCount = 0;
            
            lineRenderer.useWorldSpace = false;
            lineRenderer.alignment = LineAlignment.TransformZ;
            
            // Force width in code (20 pixels) to prevent "Invisible Thin Line"
            lineRenderer.widthMultiplier = 20.0f; 
        }
    }

    public void AddPoint(Vector2 localPos)
    {
        if (!lineRenderer) return;

        // Optimization: Filter out jitter, but allow small moves
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
        if (lineRenderer == null) return;

        lineRenderer.positionCount = points.Count;
        int i = 0;
        
        foreach (Vector2 p in points)
        {
            // Push Z to -5.0f to guarantee it sits above the map image
            lineRenderer.SetPosition(i, new Vector3(p.x, p.y, -2.5f)); 
            i++;
        }
    }

    public void Clear()
    {
        points.Clear();
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