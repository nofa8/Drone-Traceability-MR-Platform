using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MapPanelController : MonoBehaviour
{
    [Header("Configuration")]
    public RectTransform mapRect; 
    public GameObject markerPrefab; 
    public GameObject trailPrefab; 

    [Header("GPS Bounds (Calibration)")]
    public Vector2 topLeftGPS = new Vector2(39.7480f, -8.8130f);     
    public Vector2 bottomRightGPS = new Vector2(39.7390f, -8.8010f); 

    [Header("Slot Colors")]
    public Color[] slotColors = new Color[] { Color.cyan, new Color(1f, 0.5f, 0f) }; // Slot 0 = Cyan, Slot 1 = Orange
    public Color unassignedColor = Color.white;

    // State
    private Dictionary<string, RectTransform> markers = new Dictionary<string, RectTransform>();
    private Dictionary<string, MapTrackRenderer> trails = new Dictionary<string, MapTrackRenderer>();

    void Start()
    {
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnSlotSelectionChanged += HandleSelectionChanged;
        }
        DroneNetworkClient.OnGlobalTelemetry += HandleTelemetry;
    }

    void OnDestroy()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.OnSlotSelectionChanged -= HandleSelectionChanged;
        
        DroneNetworkClient.OnGlobalTelemetry -= HandleTelemetry;
    }

    void HandleTelemetry(DroneTelemetryData data)
    {
        // 1. Calculate Normalized Position using RAW MATH (not InverseLerp which clamps to 0-1)
        float rangeX = bottomRightGPS.y - topLeftGPS.y;
        float normX = ((float)data.longitude - topLeftGPS.y) / rangeX;

        float rangeY = topLeftGPS.x - bottomRightGPS.x;
        float normY = ((float)data.latitude - bottomRightGPS.x) / rangeY;

        // 2. STRICT BOUNDS CHECK
        bool isInsideMap = (normX >= 0f && normX <= 1f && normY >= 0f && normY <= 1f);

        // --- ENSURE OBJECTS EXIST ---
        if (!markers.ContainsKey(data.droneId))
        {
            CreateMarkerAndTrail(data.droneId);
        }

        RectTransform marker = markers[data.droneId];
        MapTrackRenderer trail = trails.ContainsKey(data.droneId) ? trails[data.droneId] : null;

        // --- VISIBILITY ENFORCEMENT ---
        if (!isInsideMap)
        {
            if (marker.gameObject.activeSelf) marker.gameObject.SetActive(false);
            
            if (trail != null)
            {
                if (trail.gameObject.activeSelf)
                {
                    trail.Clear();
                    trail.gameObject.SetActive(false);
                }
            }
            return;
        }

        // --- DRONE IS VISIBLE ---
        if (!marker.gameObject.activeSelf) marker.gameObject.SetActive(true);
        if (trail != null && !trail.gameObject.activeSelf) trail.gameObject.SetActive(true);

        // 3. Position Calculation
        float xPos = (normX - 0.5f) * mapRect.rect.width;
        float yPos = (normY - 0.5f) * mapRect.rect.height;
        Vector2 localPos = new Vector2(xPos, yPos);

        // 4. Update Marker
        marker.anchoredPosition = localPos;
        marker.localRotation = Quaternion.Euler(0, 0, -(float)data.heading);

        // 5. Update Visuals (Slot-Based Coloring)
        UpdateVisuals(data.droneId);

        // 6. Update Trail
        if (trail != null)
        {
            trail.AddPoint(localPos);
        }
    }

    void CreateMarkerAndTrail(string id)
    {
        // Marker
        GameObject newMarker = Instantiate(markerPrefab, mapRect);
        newMarker.transform.localPosition = Vector3.zero; 
        newMarker.transform.localScale = Vector3.one;
        newMarker.transform.localPosition = new Vector3(newMarker.transform.localPosition.x, newMarker.transform.localPosition.y, 0);
        
        newMarker.AddComponent<MapMarkerUI>().Setup(id);
        markers.Add(id, newMarker.GetComponent<RectTransform>());

        // Trail
        if (trailPrefab != null)
        {
            GameObject newTrail = Instantiate(trailPrefab, mapRect);
            newTrail.transform.localPosition = Vector3.zero;
            newTrail.transform.localScale = Vector3.one;
            newTrail.transform.SetAsFirstSibling();
            
            MapTrackRenderer tr = newTrail.GetComponent<MapTrackRenderer>();
            tr.Setup(unassignedColor); // Start with unassigned color
            trails.Add(id, tr);
        }
    }

    void HandleSelectionChanged(int slotId, string droneId)
    {
        // When assignment changes, refresh visuals for ALL drones
        foreach(var id in markers.Keys)
        {
            UpdateVisuals(id);
        }
    }

    // Centralized Visual Logic - Slot-Based Coloring
    void UpdateVisuals(string droneId)
    {
        if (!markers.ContainsKey(droneId)) return;

        RectTransform marker = markers[droneId];
        Image img = marker.GetComponent<Image>();
        
        // Ask the Brain: "Which slot owns this drone?"
        int ownerSlot = -1;
        if (SelectionManager.Instance != null)
        {
            ownerSlot = SelectionManager.Instance.GetSlotForDrone(droneId);
        }

        // Determine Color based on slot ownership
        Color targetColor = unassignedColor;
        float scale = 1.0f;

        if (ownerSlot != -1)
        {
            // Assigned to a slot - use that slot's color
            if (ownerSlot < slotColors.Length) 
                targetColor = slotColors[ownerSlot];
            else 
                targetColor = Color.magenta; // Fallback for unknown slots

            scale = 1.5f; // Assigned drones are bigger
        }

        // Apply to Marker
        if (img) img.color = targetColor;
        marker.localScale = Vector3.one * scale;
        if (ownerSlot != -1) marker.SetAsLastSibling(); // Assigned drones on top

        // Apply to Trail (Sync color)
        if (trails.ContainsKey(droneId))
        {
            trails[droneId].SetColor(targetColor);
        }
    }
}