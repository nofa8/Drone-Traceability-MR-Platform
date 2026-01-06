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

    // --- INPUT HANDLERS ---

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

        // MODE A: PINCH (Zoom)
        if (activeTouches.Count >= 2)
        {
            var ids = new List<int>(activeTouches.Keys);
            float currentDistance = Vector2.Distance(activeTouches[ids[0]], activeTouches[ids[1]]);

            if (previousTouchDistance < 0) previousTouchDistance = currentDistance;
            else
            {
                float delta = currentDistance - previousTouchDistance;
                if (Mathf.Abs(delta * 0.01f) > 0.001f)
                {
                    ApplyZoom(delta * 0.01f);
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

    // --- LOGIC ---

    private void ApplyZoom(float delta)
    {
        if (!GeoMapContext.Instance) return;
        float currentScale = GeoMapContext.Instance.pixelsPerMeter;
        float newScale = currentScale;

        if (delta > 0) newScale *= (1f + Mathf.Abs(delta));
        else newScale /= (1f + Mathf.Abs(delta));

        newScale = Mathf.Clamp(newScale, minScale, maxScale);
        GeoMapContext.Instance.SetZoom(newScale);
    }

    private void PanMap(Vector2 deltaPixels)
    {
        if (!GeoMapContext.Instance) return;
        
        // Stop following drone if user interacts
        GeoMapContext.Instance.SetFreeMode();

        float scale = GeoMapContext.Instance.pixelsPerMeter;
        if (scale <= 0.001f) return;

        float deltaMetersX = -deltaPixels.x / scale;
        float deltaMetersY = -deltaPixels.y / scale;

        Vector2 newCenter = GeoMapContext.Instance.ScreenToGeoPosition(new Vector2(deltaMetersX, deltaMetersY));
        GeoMapContext.Instance.SetCenter(newCenter.x, newCenter.y);
    }

    // --- VISUALS ---

    void ReDrawAllMarkers()
    {
        // 1. Redraw Markers (Pass FALSE so we don't add duplicate trail points)
        foreach (var kvp in cachedTelemetry)
        {
            UpdateMarkerPosition(kvp.Value, false); 
        }

        // 2. Refresh Trails (Recalculate GPS to Screen for the new Zoom level)
        foreach (var trail in trails.Values)
        {
            trail.Refresh(); 
        }
    }

    void HandleTelemetry(DroneTelemetryData data)
    {
        if (!cachedTelemetry.ContainsKey(data.droneId))
            cachedTelemetry.Add(data.droneId, data);
        else
            cachedTelemetry[data.droneId] = data;

        // Pass TRUE because this is new data, so we want to add to history
        UpdateMarkerPosition(data, true); 
    }

    // FIX: Added 'bool updateTrail' parameter to prevent duplicates during Redraw
    void UpdateMarkerPosition(DroneTelemetryData data, bool updateTrail)
    {
        if (!GeoMapContext.Instance) return;

        Vector2 localPos = GeoMapContext.Instance.GeoToScreenPosition(data.latitude, data.longitude);

        if (!markers.ContainsKey(data.droneId)) CreateMarkerAndTrail(data.droneId);
        
        RectTransform marker = markers[data.droneId];
        MapTrackRenderer trail = trails.ContainsKey(data.droneId) ? trails[data.droneId] : null;

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

        marker.anchoredPosition = localPos;
        marker.localRotation = Quaternion.Euler(0, 0, -(float)data.heading);

        UpdateVisuals(data.droneId);
        
        // FIX: Use AddGpsPoint (Lat/Lon) instead of AddPoint (Pixels)
        if (trail != null && updateTrail) 
        {
            trail.AddGpsPoint(data.latitude, data.longitude);
        }
    }

    void CreateMarkerAndTrail(string id)
    {
        GameObject newMarker = Instantiate(markerPrefab, mapRect);
        newMarker.transform.localPosition = Vector3.zero; 
        newMarker.transform.localScale = Vector3.one;
        newMarker.AddComponent<MapMarkerUI>().Setup(id);
        markers.Add(id, newMarker.GetComponent<RectTransform>());

        if (trailPrefab != null) {
            GameObject newTrail = Instantiate(trailPrefab, mapRect);
            newTrail.transform.localPosition = Vector3.zero; 
            newTrail.transform.localScale = Vector3.one;
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