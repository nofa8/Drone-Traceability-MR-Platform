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

    // --- DATA CACHE ---
    private Dictionary<string, DroneTelemetryData> cachedTelemetry = new Dictionary<string, DroneTelemetryData>();

    // --- MULTI-TOUCH STATE ---
    private Dictionary<int, Vector2> activeTouches = new Dictionary<int, Vector2>();
    private float previousTouchDistance = -1f;

    void Start()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.OnSlotSelectionChanged += HandleSelectionChanged;
        
        DroneNetworkClient.OnGlobalTelemetry += HandleTelemetry;

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

    // --- INPUT HANDLERS (Zoom/Pan) ---

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

        if (activeTouches.ContainsKey(eventData.pointerId))
            activeTouches[eventData.pointerId] = eventData.position;

        // MODE A: ZOOM (Pinch)
        if (activeTouches.Count >= 2)
        {
            var ids = new List<int>(activeTouches.Keys);
            float currentDistance = Vector2.Distance(activeTouches[ids[0]], activeTouches[ids[1]]);

            if (previousTouchDistance < 0) previousTouchDistance = currentDistance;
            else
            {
                float delta = currentDistance - previousTouchDistance;
                float zoomFactor = delta * 0.01f; 
                if (Mathf.Abs(zoomFactor) > 0.001f)
                {
                    ApplyZoom(zoomFactor);
                    previousTouchDistance = currentDistance;
                }
            }
            return; 
        }

        // MODE B: PAN (1 Finger)
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
        GeoMapContext.Instance.SetZoom(newScale); 
    }

    private void PanMap(Vector2 deltaPixels)
    {
        if (!GeoMapContext.Instance) return;
        float scale = GeoMapContext.Instance.pixelsPerMeter;
        if (scale <= 0.001f) return;

        GeoMapContext.Instance.SetFreeMode(); // Stop following drone

        float deltaMetersX = -deltaPixels.x / scale;
        float deltaMetersY = -deltaPixels.y / scale;

        Vector2 newCenter = GeoMapContext.Instance.ScreenToGeoPosition(new Vector2(deltaMetersX, deltaMetersY));
        GeoMapContext.Instance.SetCenter(newCenter.x, newCenter.y);
    }

    // --- VISUALS ---

    void ReDrawAllMarkers()
    {
        // 1. Redraw Markers (without adding new points to trail)
        foreach (var kvp in cachedTelemetry)
        {
            UpdateMarkerPosition(kvp.Value, false); 
        }
        // 2. Refresh Trails (Recalculate GPS -> Pixels)
        foreach(var trail in trails.Values) trail.Refresh();
    }

    void HandleTelemetry(DroneTelemetryData data)
    {
        if (!cachedTelemetry.ContainsKey(data.droneId)) cachedTelemetry.Add(data.droneId, data);
        else cachedTelemetry[data.droneId] = data;

        UpdateMarkerPosition(data, true);
    }

    // 🔥 THE MANUAL BOUNDS CHECK (Code Mask)
    void UpdateMarkerPosition(DroneTelemetryData data, bool updateTrail)
    {
        if (!GeoMapContext.Instance) return;

        // 1. Calculate Screen Position
        Vector2 localPos = GeoMapContext.Instance.GeoToScreenPosition(data.latitude, data.longitude);

        // 2. Ensure Marker Exists
        if (!markers.ContainsKey(data.droneId)) CreateMarkerAndTrail(data.droneId);
        
        RectTransform marker = markers[data.droneId];
        MapTrackRenderer trail = trails.ContainsKey(data.droneId) ? trails[data.droneId] : null;

        // 3. BOUNDS CHECK: Is the point inside the visible map area?
        float halfW = mapRect.rect.width / 2f;
        float halfH = mapRect.rect.height / 2f;
        float buffer = 10f; // Small buffer to prevent popping at edge

        bool isInside = (localPos.x >= -(halfW + buffer) && localPos.x <= (halfW + buffer) && 
                         localPos.y >= -(halfH + buffer) && localPos.y <= (halfH + buffer));

        // 4. Toggle Marker Visibility
        if (marker.gameObject.activeSelf != isInside) 
            marker.gameObject.SetActive(isInside);

        // 5. Toggle Trail Visibility (UPDATED LOGIC)
        // Now we hide the trail if the drone is off-screen.
        if (trail != null)
        {
            if (trail.gameObject.activeSelf != isInside)
                trail.gameObject.SetActive(isInside);
        }

        // 6. Apply Position (Even if hidden, so it's ready when it comes back)
        marker.anchoredPosition = localPos;
        marker.localRotation = Quaternion.Euler(0, 0, -(float)data.heading);

        if (isInside) UpdateVisuals(data.droneId);
        
        // 7. Update Trail (Always add GPS point so we don't lose history while hidden)
        if (trail != null && updateTrail) 
        {
            trail.AddGpsPoint(data.latitude, data.longitude);
        }
    }

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