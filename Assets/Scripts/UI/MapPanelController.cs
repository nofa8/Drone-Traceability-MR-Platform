using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class MapPanelController : MonoBehaviour, IDragHandler, IScrollHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Configuration")]
    public RectTransform mapRect; 
    public GameObject markerPrefab; 
    public GameObject trailPrefab; 

    [Header("Dynamic Controls")]
    public float zoomSensitivity = 0.1f;
    public float minScale = 0.1f; 
    public float maxScale = 50.0f;

    [Header("Slot Colors")]
    public Color[] slotColors = new Color[] { Color.cyan, new Color(1f, 0.5f, 0f) };
    public Color unassignedColor = Color.white;

    // --- VISUAL STATE ---
    private Dictionary<string, RectTransform> markers = new Dictionary<string, RectTransform>();
    private Dictionary<string, MapTrackRenderer> trails = new Dictionary<string, MapTrackRenderer>();

    // --- DATA CACHE (The Fix) ---
    // We store the last known data so we can re-render immediately when zooming
    private Dictionary<string, DroneTelemetryData> cachedTelemetry = new Dictionary<string, DroneTelemetryData>();

    // --- MULTI-TOUCH STATE ---
    private Dictionary<int, Vector2> activeTouches = new Dictionary<int, Vector2>();
    private float previousTouchDistance = -1f;

    void Start()
    {
        // 1. Listen for Selection Changes
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.OnSlotSelectionChanged += HandleSelectionChanged;
        
        // 2. Listen for New Data (Network)
        DroneNetworkClient.OnGlobalTelemetry += HandleTelemetry;

        // 3. NEW: Listen for Map Updates (Zoom/Pan)
        if (GeoMapContext.Instance != null)
            GeoMapContext.Instance.OnMapUpdated += ReDrawAllMarkers;
    }

    void OnDestroy()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.OnSlotSelectionChanged -= HandleSelectionChanged;
        
        DroneNetworkClient.OnGlobalTelemetry -= HandleTelemetry;

        if (GeoMapContext.Instance != null)
            GeoMapContext.Instance.OnMapUpdated -= ReDrawAllMarkers;
    }

    // --- INPUT: TOUCH TRACKING ---

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!activeTouches.ContainsKey(eventData.pointerId))
            activeTouches.Add(eventData.pointerId, eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (activeTouches.ContainsKey(eventData.pointerId))
            activeTouches.Remove(eventData.pointerId);
        
        if (activeTouches.Count < 2) previousTouchDistance = -1f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!GeoMapContext.Instance) return;

        // Update finger position
        if (activeTouches.ContainsKey(eventData.pointerId))
            activeTouches[eventData.pointerId] = eventData.position;

        // --- MODE A: PINCH TO ZOOM (2+ Fingers) ---
        if (activeTouches.Count >= 2)
        {
            var ids = new List<int>(activeTouches.Keys);
            Vector2 p1 = activeTouches[ids[0]];
            Vector2 p2 = activeTouches[ids[1]];

            float currentDistance = Vector2.Distance(p1, p2);

            if (previousTouchDistance < 0)
            {
                previousTouchDistance = currentDistance;
            }
            else
            {
                float delta = currentDistance - previousTouchDistance;
                float zoomFactor = delta * 0.01f; // Sensitivity

                // Apply zoom if meaningful change detected
                if (Mathf.Abs(zoomFactor) > 0.001f)
                {
                    ApplyZoom(zoomFactor);
                    previousTouchDistance = currentDistance;
                }
            }
            return; // Block Pan while Zooming
        }

        // --- MODE B: PAN (1 Finger) ---
        if (activeTouches.Count == 1)
        {
             PanMap(eventData.delta);
             previousTouchDistance = -1f;
        }
    }

    public void OnScroll(PointerEventData eventData)
    {
        float scrollDelta = eventData.scrollDelta.y;
        if (Mathf.Abs(scrollDelta) > 0.01f)
            ApplyZoom(scrollDelta > 0 ? zoomSensitivity : -zoomSensitivity);
    }

    // --- LOGIC: MAP MANIPULATION ---

    private void ApplyZoom(float delta)
    {
        if (!GeoMapContext.Instance) return;

        float currentScale = GeoMapContext.Instance.pixelsPerMeter;
        float newScale = currentScale;

        float strength = Mathf.Abs(delta);
        if (delta > 0) newScale *= (1f + strength);
        else newScale /= (1f + strength);

        newScale = Mathf.Clamp(newScale, minScale, maxScale);
        
        // This fires 'OnMapUpdated', which triggers 'ReDrawAllMarkers' immediately
        GeoMapContext.Instance.SetZoom(newScale); 
    }

    private void PanMap(Vector2 deltaPixels)
    {
        if (!GeoMapContext.Instance) return;
        float scale = GeoMapContext.Instance.pixelsPerMeter;
        if (scale <= 0.001f) return;

        float deltaMetersX = -deltaPixels.x / scale;
        float deltaMetersY = -deltaPixels.y / scale;

        Vector2 newCenter = GeoMapContext.Instance.ScreenToGeoPosition(new Vector2(deltaMetersX, deltaMetersY));
        GeoMapContext.Instance.SetCenter(newCenter.x, newCenter.y);
    }

    // --- VISUALS: INSTANT RE-DRAW (The Fix) ---

    // Called automatically when GeoMapContext changes (Zoom/Pan)
    void ReDrawAllMarkers()
    {
        // Use cached data to update positions without waiting for network
        foreach (var kvp in cachedTelemetry)
        {
            UpdateMarkerPosition(kvp.Value);
        }
    }

    void HandleTelemetry(DroneTelemetryData data)
    {
        // 1. Cache the data
        if (!cachedTelemetry.ContainsKey(data.droneId))
            cachedTelemetry.Add(data.droneId, data);
        else
            cachedTelemetry[data.droneId] = data;

        // 2. Update the marker normally
        UpdateMarkerPosition(data);
    }

    void UpdateMarkerPosition(DroneTelemetryData data)
    {
        if (!GeoMapContext.Instance) return;

        // Calculate Screen Position
        Vector2 localPos = GeoMapContext.Instance.GeoToScreenPosition(data.latitude, data.longitude);

        // Ensure Marker Exists
        if (!markers.ContainsKey(data.droneId)) CreateMarkerAndTrail(data.droneId);
        
        RectTransform marker = markers[data.droneId];
        MapTrackRenderer trail = trails.ContainsKey(data.droneId) ? trails[data.droneId] : null;

        // Bounds Check
        float halfW = mapRect.rect.width / 2f;
        float halfH = mapRect.rect.height / 2f;
        bool isInside = (localPos.x >= -halfW && localPos.x <= halfW && localPos.y >= -halfH && localPos.y <= halfH);

        if (!isInside)
        {
            if (marker.gameObject.activeSelf) marker.gameObject.SetActive(false);
            if (trail != null) trail.gameObject.SetActive(false);
            return;
        }

        if (!marker.gameObject.activeSelf) marker.gameObject.SetActive(true);
        if (trail != null && !trail.gameObject.activeSelf) trail.gameObject.SetActive(true);

        // Apply
        marker.anchoredPosition = localPos;
        marker.localRotation = Quaternion.Euler(0, 0, -(float)data.heading);

        // Visuals
        UpdateVisuals(data.droneId);
        
        // Only add trail points on live updates, not re-draws (optional optimization)
        // But for simplicity, we let the trail renderer handle distance checks
        if (trail != null) trail.AddPoint(localPos);
    }

    // --- HELPERS (Standard) ---
    void CreateMarkerAndTrail(string id)
    {
        GameObject newMarker = Instantiate(markerPrefab, mapRect);
        newMarker.transform.localPosition = Vector3.zero; newMarker.transform.localScale = Vector3.one;
        newMarker.AddComponent<MapMarkerUI>().Setup(id);
        markers.Add(id, newMarker.GetComponent<RectTransform>());

        if (trailPrefab != null) {
            GameObject newTrail = Instantiate(trailPrefab, mapRect);
            newTrail.transform.localPosition = Vector3.zero; newTrail.transform.localScale = Vector3.one;
            newTrail.transform.SetAsFirstSibling();
            MapTrackRenderer tr = newTrail.GetComponent<MapTrackRenderer>();
            tr.Setup(unassignedColor);
            trails.Add(id, tr);
        }
    }

    void HandleSelectionChanged(int slotId, string droneId) { foreach(var id in markers.Keys) UpdateVisuals(id); }

    void UpdateVisuals(string droneId)
    {
        if (!markers.ContainsKey(droneId)) return;
        RectTransform marker = markers[droneId];
        Image img = marker.GetComponent<Image>();
        
        int ownerSlot = -1;
        if (SelectionManager.Instance != null) ownerSlot = SelectionManager.Instance.GetSlotForDrone(droneId);

        Color targetColor = unassignedColor;
        float scale = 1.0f;
        if (ownerSlot != -1) {
            if (ownerSlot < slotColors.Length) targetColor = slotColors[ownerSlot];
            else targetColor = Color.magenta;
            scale = 1.5f;
        }
        if (img) img.color = targetColor;
        marker.localScale = Vector3.one * scale;
        if (ownerSlot != -1) marker.SetAsLastSibling();
        if (trails.ContainsKey(droneId)) trails[droneId].SetColor(targetColor);
    }
}