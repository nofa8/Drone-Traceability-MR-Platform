using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class FleetUIManager : MonoBehaviour
{
    public static FleetUIManager Instance;

    [Header("Backend")]
    public string apiBaseUrl = "http://localhost:5101"; 

    [Header("References")]
    public GameObject fleetViewPanel;
    public GameObject detailViewPanel;
    public Transform gridContainer;
    public GameObject droneCardPrefab;
    public DroneTelemetryController detailController; 
    public TextMeshProUGUI detailHeader;

    // Data Cache
    private Dictionary<string, DroneCardUI> activeCards = new Dictionary<string, DroneCardUI>();
    private Dictionary<string, DroneTelemetryData> telemetryCache = new Dictionary<string, DroneTelemetryData>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Default start
        ShowFleetView();
    }

    void Start()
    {
        StartCoroutine(FetchDroneList());
        
        if (SelectionManager.Instance != null)
        {
            // 1. Listen for Slot Switching (User presses Slot 1, Slot 2...)
            SelectionManager.Instance.OnActiveSlotChanged += HandleSlotSwitch;
            
            // 2. Listen for Drone Assignment (User clicks a card)
            SelectionManager.Instance.OnSlotSelectionChanged += HandleDroneAssignment;
        }
    }
    
    void OnDestroy()
    {
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnActiveSlotChanged -= HandleSlotSwitch;
            SelectionManager.Instance.OnSlotSelectionChanged -= HandleDroneAssignment;
        }
    }

    // --- 🔥 NEW NAVIGATION LOGIC ---

    // Triggered when user clicks "Slot 1", "Slot 2" buttons
    void HandleSlotSwitch(int slotId)
    {
        UpdateViewForSlot(slotId);
    }

    // Triggered when a drone is assigned/unassigned
    void HandleDroneAssignment(int slotId, string droneId)
    {
        // Only react if we modified the CURRENT slot
        if (SelectionManager.Instance != null && slotId == SelectionManager.Instance.ActiveSlotId)
        {
            UpdateViewForSlot(slotId);
        }
    }

    // Central "Brain" that decides which panel to show
    void UpdateViewForSlot(int slotId)
    {
        string droneId = SelectionManager.Instance.GetDroneAtSlot(slotId);
        bool hasDrone = !string.IsNullOrEmpty(droneId);

        // 1. Update Header Text
        if (detailHeader) detailHeader.text = hasDrone ? droneId : "NO DRONE SELECTED";

        // 2. AUTO-NAVIGATION
        if (hasDrone)
        {
            // If the slot has a drone, assume user wants to see Details
            ShowDroneDetail();
        }
        else
        {
            // If the slot is empty, force Fleet View so user can pick one
            ShowFleetView();
        }
    }

    // --- STANDARD UI METHODS ---

    public void ShowFleetView()
    {
        if(fleetViewPanel) fleetViewPanel.SetActive(true);
        if(detailViewPanel) detailViewPanel.SetActive(false);
    }

    public void ShowDroneDetail()
    {
        if(fleetViewPanel) fleetViewPanel.SetActive(false);
        if(detailViewPanel) detailViewPanel.SetActive(true);
        
        // Refresh Data from Cache immediately
        if (SelectionManager.Instance != null)
        {
            string currentDrone = SelectionManager.Instance.GetDroneAtSlot(SelectionManager.Instance.ActiveSlotId);
            if (!string.IsNullOrEmpty(currentDrone) && telemetryCache.ContainsKey(currentDrone))
            {
                if (detailController != null) 
                    detailController.UpdateVisuals(telemetryCache[currentDrone]);
            }
        }
    }

    // --- DATA HANDLING (No Changes) ---

    IEnumerator FetchDroneList()
    {
        string url = $"{apiBaseUrl}/api/drones?limit=50";
        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                var result = JsonUtility.FromJson<PagedSnapshotResult>(req.downloadHandler.text);
                if (result?.items != null)
                {
                    foreach (var drone in result.items) CreateOrUpdateCard(drone);
                }
            }
            else Debug.LogError($"❌ REST Error: {req.error}");
        }
    }

    public void CreateOrUpdateCard(DroneSnapshotModel snapshot)
    {
        if (string.IsNullOrEmpty(snapshot.droneId)) return;

        if (!activeCards.ContainsKey(snapshot.droneId))
        {
            GameObject newCard = Instantiate(droneCardPrefab, gridContainer);
            DroneCardUI cardUI = newCard.GetComponent<DroneCardUI>();
            cardUI.Setup(snapshot.droneId);
            activeCards.Add(snapshot.droneId, cardUI);
        }
        activeCards[snapshot.droneId].UpdateFromSnapshot(snapshot);

        // Cache Data
        DroneTelemetryData adaptedData = ConvertSnapshotToTelemetry(snapshot);
        if (telemetryCache.ContainsKey(snapshot.droneId)) telemetryCache[snapshot.droneId] = adaptedData;
        else telemetryCache.Add(snapshot.droneId, adaptedData);
    }

    public void HandleLiveUpdate(DroneTelemetryData telemetry)
    {
        if (string.IsNullOrEmpty(telemetry.droneId)) return;

        // Cache Data
        if (telemetryCache.ContainsKey(telemetry.droneId)) telemetryCache[telemetry.droneId] = telemetry;
        else telemetryCache.Add(telemetry.droneId, telemetry);

        if (activeCards.ContainsKey(telemetry.droneId))
        {
            activeCards[telemetry.droneId].UpdateFromLive(telemetry);
        }

        // Live Update Detail View
        if (SelectionManager.Instance != null)
        {
            string activeDroneId = SelectionManager.Instance.GetDroneAtSlot(SelectionManager.Instance.ActiveSlotId);
            if (activeDroneId == telemetry.droneId && detailController != null)
            {
                 detailController.UpdateVisuals(telemetry);
            }
        }
    }

    private DroneTelemetryData ConvertSnapshotToTelemetry(DroneSnapshotModel snap)
    {
        DroneTelemetryData data = new DroneTelemetryData();
        data.droneId = snap.droneId;
        data.model = snap.model; 

        if (snap.telemetry != null)
        {
            data.latitude = snap.telemetry.latitude;
            data.longitude = snap.telemetry.longitude;
            data.altitude = snap.telemetry.altitude;
            data.heading = snap.telemetry.heading;
            data.velocityX = snap.telemetry.velocityX;
            data.velocityZ = snap.telemetry.velocityZ;
            data.batteryLevel = snap.telemetry.batteryLevel;
            data.satCount = snap.telemetry.satelliteCount;
            data.isFlying = snap.telemetry.isFlying;
            data.online = snap.telemetry.online;
            data.motorsOn = snap.telemetry.areMotorsOn;
        }
        else
        {
            data.online = snap.isConnected;
        }
        return data;
    }
    
    public void DisconnectActiveDrone()
    {
        int activeSlot = SelectionManager.Instance.ActiveSlotId;
        SelectionManager.Instance.ClearSlot(activeSlot);
        // The event handler HandleDroneAssignment will catch this and automatically ShowFleetView()
    }
}