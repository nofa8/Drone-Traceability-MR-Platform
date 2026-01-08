using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class TrailReplayController : MonoBehaviour
{
    [Header("UI References")]
    public Slider timeSlider; 
    public Button btnBackToLive; 
    public GameObject ghostMarkerPrefab; 
    public Transform mapContainer; 

    [Header("Links")]
    public DroneTelemetryController detailController; 

    [Header("State")]
    public string targetDroneId;
    
    private GameObject ghostMarker;
    private List<DroneTelemetryData> currentHistory;
    
    // 🔥 New: Update Loop Data
    private DroneTelemetryData currentReplayData;
    private bool isReplayActive = false;

    void Start()
    {
        // Auto-Link
        if (detailController == null)
            detailController = FindObjectOfType<DroneTelemetryController>();

        if (detailController == null)
            Debug.LogError("❌ CRITICAL: TrailReplayController cannot find DroneTelemetryController.");

        // UI Setup
        if (timeSlider) {
            timeSlider.gameObject.SetActive(false);
            timeSlider.onValueChanged.AddListener(OnTimeScrub);
        }
        if (btnBackToLive) {
            btnBackToLive.gameObject.SetActive(false); 
            btnBackToLive.onClick.AddListener(ExitReplayMode);
        }

        // 🔥 LISTEN FOR BOTH EVENTS
        if (SelectionManager.Instance != null)
        {
            // Event A: Drone Assigned to Slot
            SelectionManager.Instance.OnSlotSelectionChanged += OnSlotAssignment;
            // Event B: User Clicked a Different Slot (The Fix)
            SelectionManager.Instance.OnActiveSlotChanged += OnActiveSlotChanged;
        }
    }

    void OnDestroy()
    {
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnSlotSelectionChanged -= OnSlotAssignment;
            SelectionManager.Instance.OnActiveSlotChanged -= OnActiveSlotChanged;
        }
    }

    // --- LATE UPDATE (Pins Ghost to Map) ---
    void LateUpdate()
    {
        if (!isReplayActive || currentReplayData == null) return;
        if (!GeoMapContext.Instance || ghostMarker == null) return;

        Vector2 screenPos = GeoMapContext.Instance.GeoToScreenPosition(currentReplayData.latitude, currentReplayData.longitude);
        
        RectTransform rt = ghostMarker.GetComponent<RectTransform>();
        if (rt)
        {
            rt.anchoredPosition3D = new Vector3(screenPos.x, screenPos.y, 0); 
            rt.localRotation = Quaternion.Euler(0, 0, -(float)currentReplayData.heading);
        }
    }

    // --- EVENT HANDLERS ---

    // 1. User clicked a different slot tab (e.g., Slot 1 -> Slot 2)
    void OnActiveSlotChanged(int newSlotId)
    {
        // 🔥 HARD RESET: Clear everything immediately
        ExitReplayMode();

        // Load the drone for the new slot
        string droneId = SelectionManager.Instance.GetDroneAtSlot(newSlotId);
        LoadDroneHistory(droneId);
    }

    // 2. A drone was assigned to a slot
    void OnSlotAssignment(int slotId, string droneId)
    {
        if (slotId != SelectionManager.Instance.ActiveSlotId) return;

        ExitReplayMode();
        LoadDroneHistory(droneId);
    }

    // Helper to load history
    void LoadDroneHistory(string droneId)
    {
        targetDroneId = droneId;
        
        if (string.IsNullOrEmpty(droneId))
        {
            if (timeSlider) timeSlider.gameObject.SetActive(false);
            return;
        }

        if (DroneStateRepository.Instance != null)
        {
            var state = DroneStateRepository.Instance.GetState(droneId);
            if (state != null && state.history.Count > 1)
            {
                currentHistory = state.history;
                if (timeSlider) timeSlider.gameObject.SetActive(true);
            }
            else
            {
                if (timeSlider) timeSlider.gameObject.SetActive(false);
            }
        }
    }

    // --- REPLAY LOGIC ---

    void OnTimeScrub(float value)
    {
        if (currentHistory == null || currentHistory.Count == 0) return;
        
        isReplayActive = true;
        if (btnBackToLive) btnBackToLive.gameObject.SetActive(true);

        // 1. Ghost Marker
        if (ghostMarker == null && ghostMarkerPrefab != null)
        {
            ghostMarker = Instantiate(ghostMarkerPrefab, mapContainer);
            var img = ghostMarker.GetComponent<Image>();
            if (img) img.color = new Color(1f, 0f, 1f, 0.6f); 
            ghostMarker.transform.SetAsFirstSibling(); 
        }
        if (ghostMarker) ghostMarker.SetActive(true);

        // 2. Select Data
        int index = Mathf.Clamp(Mathf.RoundToInt(value * (currentHistory.Count - 1)), 0, currentHistory.Count - 1);
        currentReplayData = currentHistory[index];

        // 3. Hijack Detail View
        if (detailController != null)
        {
            detailController.SetReplayOverride(currentReplayData);
        }
    }

    public void ExitReplayMode()
    {
        // 1. Reset Internal State
        isReplayActive = false;
        currentReplayData = null;

        // 2. Hide UI
        if (btnBackToLive) btnBackToLive.gameObject.SetActive(false);
        if (ghostMarker) ghostMarker.SetActive(false);

        // 3. Reset Slider (Without triggering event)
        if (timeSlider)
        {
            timeSlider.onValueChanged.RemoveListener(OnTimeScrub);
            timeSlider.value = 1.0f; 
            timeSlider.onValueChanged.AddListener(OnTimeScrub);
        }

        // 4. 🔥 RELEASE HIJACK (Critical)
        if (detailController != null)
        {
            detailController.ClearReplayOverride();
        }
    }
}