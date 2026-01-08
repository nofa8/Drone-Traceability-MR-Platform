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
    public float minScale = 0.0005f; 
    public float maxScale = 50.0f;

    [Tooltip("Negative values bring the marker closer to the camera.")]
    public float markerZIndex = -5.0f;

    [Header("Panning Feel")]
    [Tooltip("Base sensitivity when zoomed IN (1.0 = 1:1 movement)")]
    [Range(0.1f, 2.0f)] public float panSensitivity = 1.0f;
    
    [Tooltip("How much 'heavier' the map feels when zoomed OUT (0.1 = very heavy)")]
    [Range(0.01f, 1.0f)] public float zoomedOutDamping = 0.2f;

    [Header("Visual Feedback")]
    public Color[] slotColors = new Color[] { Color.cyan, new Color(1f, 0.5f, 0f) };
    public Color unassignedColor = Color.white;
    [Range(0.1f, 1f)] public float offlineAlpha = 0.4f;

    // --- VISUAL STATE ---
    private Dictionary<string, RectTransform> markers = new Dictionary<string, RectTransform>();
    private Dictionary<string, MapTrackRenderer> trails = new Dictionary<string, MapTrackRenderer>();
    private Dictionary<string, DroneState> cachedStates = new Dictionary<string, DroneState>();

    // --- MULTI-TOUCH STATE ---
    private Dictionary<int, Vector2> activeTouches = new Dictionary<int, Vector2>();
    private float previousTouchDistance = -1f;

    void Start()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.OnSlotSelectionChanged += HandleSelectionChanged;
        
        if (DroneStateRepository.Instance != null)
        {
            DroneStateRepository.Instance.OnDroneStateUpdated += HandleStateUpdate;
            foreach(var state in DroneStateRepository.Instance.GetAllStates())
            {
                HandleStateUpdate(state.droneId, state);
            }
        }

        if (GeoMapContext.Instance != null)
            GeoMapContext.Instance.OnMapUpdated += ReDrawAllMarkers;
    }

    void OnDestroy()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.OnSlotSelectionChanged -= HandleSelectionChanged;
        
        if (DroneStateRepository.Instance != null)
            DroneStateRepository.Instance.OnDroneStateUpdated -= HandleStateUpdate;

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
        float newScale = delta > 0 ? currentScale * (1f + Mathf.Abs(delta)) : currentScale / (1f + Mathf.Abs(delta));
        newScale = Mathf.Clamp(newScale, minScale, maxScale);
        GeoMapContext.Instance.SetZoom(newScale); 
    }

    // 🔥 THE FIX: Dynamic Panning Damping
    private void PanMap(Vector2 deltaPixels)
    {
        if (!GeoMapContext.Instance) return;
        
        GeoMapContext.Instance.SetFreeMode();

        float scale = GeoMapContext.Instance.pixelsPerMeter;
        if (scale <= 0.00001f) return;

        // 1. Calculate Zoom Percentage (Logarithmic is best for Maps)
        // t = 0 (Zoomed Out / World View)
        // t = 1 (Zoomed In / Street View)
        float t = Mathf.InverseLerp(Mathf.Log(minScale), Mathf.Log(maxScale), Mathf.Log(scale));

        // 2. Adjust Sensitivity dynamically
        // When zoomed out, we blend towards 'zoomedOutDamping' (e.g. 0.2)
        // When zoomed in, we use full 'panSensitivity' (e.g. 1.0)
        float dynamicSens = Mathf.Lerp(zoomedOutDamping, 1.0f, t) * panSensitivity;

        // 3. Apply Damped Delta
        Vector2 dampedDelta = deltaPixels * dynamicSens;

        Vector2 newCenter = GeoMapContext.Instance.ScreenToGeoPosition(new Vector2(-dampedDelta.x / scale, -dampedDelta.y / scale));
        GeoMapContext.Instance.SetCenter(newCenter.x, newCenter.y);
    }

    // --- VISUALS ---

    void ReDrawAllMarkers()
    {
        foreach (var kvp in cachedStates) UpdateMarkerPosition(kvp.Value, false); 
        foreach(var trail in trails.Values) trail.Refresh();
    }

    void HandleStateUpdate(string droneId, DroneState state)
    {
        if (!cachedStates.ContainsKey(droneId)) cachedStates.Add(droneId, state);
        else cachedStates[droneId] = state;

        UpdateMarkerPosition(state, true);
    }

    void UpdateMarkerPosition(DroneState state, bool updateTrail)
    {
        if (!GeoMapContext.Instance || state == null) return;
        DroneTelemetryData data = state.data;

        Vector2 localPos = GeoMapContext.Instance.GeoToScreenPosition(data.latitude, data.longitude);

        if (!markers.ContainsKey(data.droneId)) CreateMarkerAndTrail(data.droneId);
        
        RectTransform marker = markers[data.droneId];
        MapTrackRenderer trail = trails.ContainsKey(data.droneId) ? trails[data.droneId] : null;

        float halfW = mapRect.rect.width / 2f;
        float halfH = mapRect.rect.height / 2f;
        float buffer = 10f; 

        bool isInside = (localPos.x >= -(halfW + buffer) && localPos.x <= (halfW + buffer) && 
                         localPos.y >= -(halfH + buffer) && localPos.y <= (halfH + buffer));

        if (marker.gameObject.activeSelf != isInside) marker.gameObject.SetActive(isInside);
        if (trail != null && trail.gameObject.activeSelf != isInside) trail.gameObject.SetActive(isInside);

        marker.anchoredPosition3D = new Vector3(localPos.x, localPos.y, markerZIndex);
        marker.localRotation = Quaternion.Euler(0, 0, -(float)data.heading);

        if (isInside) UpdateMarkerVisuals(state);
        
        if (trail != null && updateTrail) 
        {
            if (state.isConnected && !state.IsStale && data.isFlying)
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

    void HandleSelectionChanged(int slotId, string droneId) 
    { 
        foreach(var kvp in cachedStates) UpdateMarkerVisuals(kvp.Value); 
    }

    void UpdateMarkerVisuals(DroneState state)
    {
        string droneId = state.droneId;
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
            marker.SetAsLastSibling(); 
        }

        // Apply Ghost Effect
        if (state.IsStale || !state.isConnected) targetColor.a = offlineAlpha; 
        else targetColor.a = 1.0f; 

        if (img) img.color = targetColor;
        marker.localScale = Vector3.one * scale;

        if (trails.ContainsKey(droneId)) trails[droneId].SetColor(targetColor);
    }
}