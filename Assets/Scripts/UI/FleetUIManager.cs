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
    
    // We keep this reference only to toggle the panel on/off, 
    // NOT to pass data manually anymore.
    public DroneTelemetryController detailController; 
    public TextMeshProUGUI detailHeader;

    // Data Cache (Still used for the Cards/List view)
    private Dictionary<string, DroneCardUI> activeCards = new Dictionary<string, DroneCardUI>();
    private Dictionary<string, DroneTelemetryData> telemetryCache = new Dictionary<string, DroneTelemetryData>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        ShowFleetView();
    }

    void Start()
    {
        StartCoroutine(FetchDroneList());
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnActiveSlotChanged += HandleSlotSwitch;
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

    // --- NAVIGATION ---

    void HandleSlotSwitch(int slotId) { UpdateViewForSlot(slotId); }

    void HandleDroneAssignment(int slotId, string droneId)
    {
        if (SelectionManager.Instance != null && slotId == SelectionManager.Instance.ActiveSlotId)
            UpdateViewForSlot(slotId);
    }

    void UpdateViewForSlot(int slotId)
    {
        string droneId = SelectionManager.Instance.GetDroneAtSlot(slotId);
        bool hasDrone = !string.IsNullOrEmpty(droneId);

        if (detailHeader) detailHeader.text = hasDrone ? droneId : "NO DRONE SELECTED";

        if (hasDrone) ShowDroneDetail();
        else ShowFleetView();
    }

    public void ShowFleetView()
    {
        if(fleetViewPanel) fleetViewPanel.SetActive(true);
        if(detailViewPanel) detailViewPanel.SetActive(false);
    }

    public void ShowDroneDetail()
    {
        if(fleetViewPanel) fleetViewPanel.SetActive(false);
        if(detailViewPanel) detailViewPanel.SetActive(true);
        
        // ❌ REMOVED: Manual update logic. 
        // The DroneTelemetryController now fetches its own data from DroneStateRepository 
        // as soon as the panel opens or the slot changes.
    }

    // --- DATA HANDLING ---

    public void HandleLiveUpdate(DroneTelemetryData telemetry)
    {
        if (string.IsNullOrEmpty(telemetry.droneId)) return;

        // 1. Cache Data (For Fleet Cards)
        if (telemetryCache.ContainsKey(telemetry.droneId)) telemetryCache[telemetry.droneId] = telemetry;
        else telemetryCache.Add(telemetry.droneId, telemetry);

        // 2. Create/Update Card
        if (!activeCards.ContainsKey(telemetry.droneId)) CreateCardInternal(telemetry.droneId);
        activeCards[telemetry.droneId].UpdateFromLive(telemetry);

        // ❌ REMOVED: Redundant call to detailController.UpdateVisuals()
        // The Detail View listens to DroneStateRepository events automatically.
    }

    // --- REST FETCHING ---

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
            else Debug.LogWarning($"⚠️ REST Fetch Failed: {req.error}");
        }
    }

    

    public void CreateOrUpdateCard(DroneSnapshotModel snapshot)
    {
        if (string.IsNullOrEmpty(snapshot.droneId)) return;

        // 1. Create/Update the UI Card (Existing logic)
        if (!activeCards.ContainsKey(snapshot.droneId)) CreateCardInternal(snapshot.droneId);
        activeCards[snapshot.droneId].UpdateFromSnapshot(snapshot);

        // ✅ STEP 3: Push Snapshot to Repository
        // This ensures the Detail View has data immediately, even if the drone is offline!
        if (DroneStateRepository.Instance != null)
        {
            DroneStateRepository.Instance.UpdateFromSnapshot(snapshot);
        }
    }

    private void CreateCardInternal(string droneId)
    {
        GameObject newCard = Instantiate(droneCardPrefab, gridContainer);
        DroneCardUI cardUI = newCard.GetComponent<DroneCardUI>();
        cardUI.Setup(droneId);
        activeCards.Add(droneId, cardUI);
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
    }
}