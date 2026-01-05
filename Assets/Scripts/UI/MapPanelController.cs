using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class MapPanelController : MonoBehaviour, IDragHandler, IScrollHandler, IPointerDownHandler
{
    [Header("Debug")]
    public bool debugMode = true;

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

    // State
    private Dictionary<string, RectTransform> markers = new Dictionary<string, RectTransform>();
    private Dictionary<string, MapTrackRenderer> trails = new Dictionary<string, MapTrackRenderer>();

    void Start()
    {
        // No local calibration anymore! We rely on GeoMapContext.
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.OnSlotSelectionChanged += HandleSelectionChanged;
        
        DroneNetworkClient.OnGlobalTelemetry += HandleTelemetry;
    }

    void OnDestroy()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.OnSlotSelectionChanged -= HandleSelectionChanged;
        DroneNetworkClient.OnGlobalTelemetry -= HandleTelemetry;
    }

    // --- INTERACTION: ZOOM (Remote Control) ---
    public void OnScroll(PointerEventData eventData)
    {
        if (!GeoMapContext.Instance) return;

        float scrollDelta = eventData.scrollDelta.y;
        if (Mathf.Abs(scrollDelta) < 0.01f) return;

        float currentScale = GeoMapContext.Instance.pixelsPerMeter;
        float newScale = currentScale;

        if (scrollDelta > 0) newScale *= (1f + zoomSensitivity);
        else newScale /= (1f + zoomSensitivity);

        newScale = Mathf.Clamp(newScale, minScale, maxScale);

        GeoMapContext.Instance.SetZoom(newScale);
    }

    // --- INTERACTION: PAN (Remote Control) ---
    public void OnPointerDown(PointerEventData eventData) { }

    public void OnDrag(PointerEventData eventData)
    {
        PanMap(eventData.delta);
    }

    private void PanMap(Vector2 deltaPixels)
    {
        if (!GeoMapContext.Instance) return;

        float scale = GeoMapContext.Instance.pixelsPerMeter;
        double lat = GeoMapContext.Instance.originLat;
        double lon = GeoMapContext.Instance.originLon;

        if (scale <= 0.001f) return;

        float deltaMetersX = -deltaPixels.x / scale;
        float deltaMetersY = -deltaPixels.y / scale;

        Vector2 newCenter = GeoUtils.MetersToLatLon(
            new Vector2(deltaMetersX, deltaMetersY), 
            lat, 
            lon
        );

        GeoMapContext.Instance.SetCenter(newCenter.x, newCenter.y);
    }

    // --- RENDERING ---
    void HandleTelemetry(DroneTelemetryData data)
    {
        if (!GeoMapContext.Instance) 
        {
            if(debugMode) Debug.LogWarning("⚠️ Missing GeoMapContext in scene!");
            return;
        }

        // 1. Math (DELEGATED TO CONTEXT)
        Vector2 localPos = GeoMapContext.Instance.GeoToScreenPosition(data.latitude, data.longitude);

        // 2. Bounds Check (Simple UI check)
        float halfW = mapRect.rect.width / 2f;
        float halfH = mapRect.rect.height / 2f;
        
        bool isInside = (localPos.x >= -halfW && localPos.x <= halfW && 
                         localPos.y >= -halfH && localPos.y <= halfH);

        // 3. Object Management
        if (!markers.ContainsKey(data.droneId)) CreateMarkerAndTrail(data.droneId);
        
        RectTransform marker = markers[data.droneId];
        MapTrackRenderer trail = trails.ContainsKey(data.droneId) ? trails[data.droneId] : null;

        if (!isInside)
        {
            if (marker.gameObject.activeSelf) marker.gameObject.SetActive(false);
            if (trail != null) trail.gameObject.SetActive(false);
            return;
        }

        if (!marker.gameObject.activeSelf) marker.gameObject.SetActive(true);
        if (trail != null && !trail.gameObject.activeSelf) trail.gameObject.SetActive(true);

        // 4. Update Position
        marker.anchoredPosition = localPos;
        marker.localRotation = Quaternion.Euler(0, 0, -(float)data.heading);

        UpdateVisuals(data.droneId);
        if (trail != null) trail.AddPoint(localPos);
    }

    // --- RESTORED HELPER FUNCTIONS ---
    
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

    void HandleSelectionChanged(int slotId, string droneId)
    {
        foreach(var id in markers.Keys) UpdateVisuals(id);
    }

    void UpdateVisuals(string droneId)
    {
        if (!markers.ContainsKey(droneId)) return;

        RectTransform marker = markers[droneId];
        Image img = marker.GetComponent<Image>();
        
        int ownerSlot = -1;
        if (SelectionManager.Instance != null) 
            ownerSlot = SelectionManager.Instance.GetSlotForDrone(droneId);

        Color targetColor = unassignedColor;
        float scale = 1.0f;

        if (ownerSlot != -1)
        {
            if (ownerSlot < slotColors.Length) targetColor = slotColors[ownerSlot];
            else targetColor = Color.magenta;
            scale = 1.5f;
        }

        if (img) img.color = targetColor;
        marker.localScale = Vector3.one * scale;
        
        if (ownerSlot != -1) marker.SetAsLastSibling();
        
        if (trails.ContainsKey(droneId)) 
            trails[droneId].SetColor(targetColor);
    }
}