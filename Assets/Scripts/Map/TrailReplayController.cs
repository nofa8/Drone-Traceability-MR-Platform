using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class TrailReplayController : MonoBehaviour
{
    [Header("UI References")]
    public Slider timeSlider; // Drag a UI Slider here
    public GameObject ghostMarkerPrefab; // Drag your existing MapMarker prefab here
    public Transform mapContainer; // The same parent your MapMarkers use

    [Header("State")]
    public string targetDroneId;
    
    private GameObject ghostMarker;
    private List<DroneTelemetryData> currentHistory;
    private bool isReplaying = false;

    void Start()
    {
        // Auto-hide slider at start
        if (timeSlider)
        {
            timeSlider.gameObject.SetActive(false);
            timeSlider.onValueChanged.AddListener(OnTimeScrub);
        }

        if (SelectionManager.Instance != null)
            SelectionManager.Instance.OnSlotSelectionChanged += OnDroneSelected;
    }

    void OnDestroy()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.OnSlotSelectionChanged -= OnDroneSelected;
    }

    // 1. When user selects a drone, load its history
    void OnDroneSelected(int slotId, string droneId)
    {
        if (slotId != SelectionManager.Instance.ActiveSlotId) return;

        targetDroneId = droneId;
        
        // Fetch History from Repository
        if (DroneStateRepository.Instance != null)
        {
            var state = DroneStateRepository.Instance.GetState(droneId);
            if (state != null && state.history.Count > 1)
            {
                currentHistory = state.history;
                EnableReplayUI(true);
            }
            else
            {
                EnableReplayUI(false);
            }
        }
    }

    // 2. The Logic: Interpolate based on slider 0.0 -> 1.0
    void OnTimeScrub(float value)
    {
        if (currentHistory == null || currentHistory.Count == 0) return;
        if (!GeoMapContext.Instance) return;

        // Ensure Ghost Marker Exists
        if (ghostMarker == null && ghostMarkerPrefab != null)
        {
            ghostMarker = Instantiate(ghostMarkerPrefab, mapContainer);
            // Visual tweak: Make ghost semi-transparent or a different color
            var img = ghostMarker.GetComponent<Image>();
            if (img) img.color = new Color(1f, 1f, 1f, 0.5f); 
        }

        ghostMarker.SetActive(true);

        // Math: Find the index in the list
        int index = Mathf.Clamp(Mathf.RoundToInt(value * (currentHistory.Count - 1)), 0, currentHistory.Count - 1);
        DroneTelemetryData historicalData = currentHistory[index];

        // Position the Ghost
        Vector2 screenPos = GeoMapContext.Instance.GeoToScreenPosition(historicalData.latitude, historicalData.longitude);
        RectTransform rt = ghostMarker.GetComponent<RectTransform>();
        rt.anchoredPosition3D = new Vector3(screenPos.x, screenPos.y, -10); // On top of map
        rt.localRotation = Quaternion.Euler(0, 0, -(float)historicalData.heading);
    }

    void EnableReplayUI(bool enable)
    {
        if (timeSlider) timeSlider.gameObject.SetActive(enable);
        if (ghostMarker) ghostMarker.SetActive(false); // Hide ghost when not scrubbing
    }
}