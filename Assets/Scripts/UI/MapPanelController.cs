using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MapPanelController : MonoBehaviour
{
    [Header("Configuration")]
    public RectTransform mapRect; // The RawImage of the map
    public GameObject markerPrefab; // The tiny dot/arrow prefab
    public GameObject trailPrefab; // Trail renderer prefab

    [Header("GPS Bounds (Calibration)")]
    // 🛑 CRITICAL: These must match the corners of your screenshot EXACTLY
    // Defaulting to Leiria, Portugal (Sim Area)
    public Vector2 topLeftGPS = new Vector2(39.7480f, -8.8130f);     // Lat (X), Lon (Y) for inspector convenience
    public Vector2 bottomRightGPS = new Vector2(39.7390f, -8.8010f); 

    [Header("System Architecture")]
    public int assignedSlotId = 0; // 0 = Main Monitor

    // State
    private Dictionary<string, RectTransform> markers = new Dictionary<string, RectTransform>();
    private Dictionary<string, MapTrackRenderer> trails = new Dictionary<string, MapTrackRenderer>();
    private string selectedDroneId;

    void Start()
    {
        // 1. Listen for Selection Changes (To highlight the specific drone)
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnSlotSelectionChanged += HandleSelectionChanged;
            selectedDroneId = SelectionManager.Instance.GetDroneAtSlot(assignedSlotId);
        }

        // 2. Listen for Global Telemetry (To move ALL dots)
        DroneNetworkClient.OnGlobalTelemetry += HandleTelemetry;
    }

    void OnDestroy()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.OnSlotSelectionChanged -= HandleSelectionChanged;
        
        DroneNetworkClient.OnGlobalTelemetry -= HandleTelemetry;
    }

    // --- THE MATH LAYER ---
    void HandleTelemetry(DroneTelemetryData data)
    {
        // 1. Get or Create Marker
        if (!markers.ContainsKey(data.droneId))
        {
            GameObject newMarker = Instantiate(markerPrefab, mapRect);
            newMarker.transform.localPosition = Vector3.zero; 
            newMarker.transform.localScale = Vector3.one;
            // Ensure z-position is 0
            newMarker.transform.localPosition = new Vector3(newMarker.transform.localPosition.x, newMarker.transform.localPosition.y, 0);
            
            markers.Add(data.droneId, newMarker.GetComponent<RectTransform>());
            
            // Spawn Trail (behind markers)
            if (trailPrefab != null)
            {
                GameObject newTrail = Instantiate(trailPrefab, mapRect);
                newTrail.transform.localPosition = Vector3.zero;
                newTrail.transform.localScale = Vector3.one;
                newTrail.transform.SetAsFirstSibling(); // Put BEHIND markers
                
                MapTrackRenderer tr = newTrail.GetComponent<MapTrackRenderer>();
                tr.Setup(Color.cyan);
                trails.Add(data.droneId, tr);
            }
        }

        RectTransform marker = markers[data.droneId];

        // 2. Normalize GPS (Lat/Lon) to [0..1]
        // "Mathf.InverseLerp" calculates where a value sits between a min and max
        
        // Longitude (X): Left (Min) to Right (Max)
        // topLeftGPS.y is Left Longitude, bottomRightGPS.y is Right Longitude
        float normX = Mathf.InverseLerp(topLeftGPS.y, bottomRightGPS.y, (float)data.longitude);
        
        // Latitude (Y): Bottom (Min) to Top (Max)
        // bottomRightGPS.x is Bottom Lat, topLeftGPS.x is Top Lat
        float normY = Mathf.InverseLerp(bottomRightGPS.x, topLeftGPS.x, (float)data.latitude);

        // 3. Project to Canvas Coordinates
        // This assumes your Map Rect pivot is Center (0.5, 0.5)
        float xPos = (normX - 0.5f) * mapRect.rect.width;
        float yPos = (normY - 0.5f) * mapRect.rect.height;

        marker.anchoredPosition = new Vector2(xPos, yPos);

        // 4. Update Rotation (Heading)
        marker.localRotation = Quaternion.Euler(0, 0, -(float)data.heading);

        // 5. Update Trail
        if (trails.ContainsKey(data.droneId))
        {
            trails[data.droneId].AddPoint(new Vector2(xPos, yPos));
        }

        // 6. Refresh Colors
        UpdateMarkerVisuals(data.droneId, marker);
    }

    void HandleSelectionChanged(int slotId, string droneId)
    {
        if (slotId != assignedSlotId) return;

        selectedDroneId = droneId;
        
        // Refresh all markers to update highlights
        foreach(var kvp in markers)
        {
            UpdateMarkerVisuals(kvp.Key, kvp.Value);
        }
    }

    void UpdateMarkerVisuals(string droneId, RectTransform marker)
    {
        Image img = marker.GetComponent<Image>();
        if (img)
        {
            if (droneId == selectedDroneId)
            {
                img.color = Color.cyan; // Highlight
                marker.SetAsLastSibling(); // Bring to front
                marker.localScale = Vector3.one * 1.5f; 
            }
            else
            {
                img.color = Color.white; // Default
                marker.localScale = Vector3.one;
            }
        }
    }
}