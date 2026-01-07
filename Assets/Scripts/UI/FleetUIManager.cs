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

    // UI Cache
    private Dictionary<string, DroneCardUI> activeCards = new Dictionary<string, DroneCardUI>();
    
    // 🔥 DATA CACHE: Stores the last known state (from REST or WS) for every drone
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
            SelectionManager.Instance.OnActiveSlotChanged += UpdateHeaderContext;
            SelectionManager.Instance.OnSlotSelectionChanged += HandleSelectionChanged;
        }
    }
    
    void OnDestroy()
    {
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnActiveSlotChanged -= UpdateHeaderContext;
            SelectionManager.Instance.OnSlotSelectionChanged -= HandleSelectionChanged;
        }
    }

    // --- SELECTION LOGIC ---

    void HandleSelectionChanged(int slotId, string droneId)
    {
        // Only update if this event is for the slot we are currently watching
        if (SelectionManager.Instance != null && slotId == SelectionManager.Instance.ActiveSlotId)
        {
            if (string.IsNullOrEmpty(droneId))
            {
                UpdateHeaderContext(slotId); 
            }
            else
            {
                if (detailHeader) detailHeader.text = $"{droneId}";

                // 🔥 FIX: Immediately populate Detail View from Cache
                if (telemetryCache.ContainsKey(droneId) && detailController != null)
                {
                    detailController.UpdateVisuals(telemetryCache[droneId]);
                }
            }
        }
    }

    void UpdateHeaderContext(int slotId)
    {
        if (detailHeader) 
        {
            string droneId = SelectionManager.Instance != null ? 
                             SelectionManager.Instance.GetDroneAtSlot(slotId) : null;

            if (string.IsNullOrEmpty(droneId))
            {
                detailHeader.text = "NO DRONE SELECTED";
            }
            else
            {
                detailHeader.text = droneId; 
                
                // 🔥 FIX: Also refresh visuals if we just switched active slots
                if (telemetryCache.ContainsKey(droneId) && detailController != null)
                {
                    detailController.UpdateVisuals(telemetryCache[droneId]);
                }
            }
        }
    }

    // --- DATA FETCHING ---

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

        // 1. Create/Update the UI Card
        if (!activeCards.ContainsKey(snapshot.droneId))
        {
            GameObject newCard = Instantiate(droneCardPrefab, gridContainer);
            DroneCardUI cardUI = newCard.GetComponent<DroneCardUI>();
            cardUI.Setup(snapshot.droneId);
            activeCards.Add(snapshot.droneId, cardUI);
        }
        activeCards[snapshot.droneId].UpdateFromSnapshot(snapshot);

        // 🔥 FIX: Cache this snapshot as Telemetry Data
        // Even if the drone is Offline, we now have its last known location/battery.
        DroneTelemetryData adaptedData = ConvertSnapshotToTelemetry(snapshot);
        if (telemetryCache.ContainsKey(snapshot.droneId))
            telemetryCache[snapshot.droneId] = adaptedData;
        else
            telemetryCache.Add(snapshot.droneId, adaptedData);
    }

    public void HandleLiveUpdate(DroneTelemetryData telemetry)
    {
        if (string.IsNullOrEmpty(telemetry.droneId)) return;

        // 1. Update Cache (Always keep the latest)
        if (telemetryCache.ContainsKey(telemetry.droneId))
            telemetryCache[telemetry.droneId] = telemetry;
        else
            telemetryCache.Add(telemetry.droneId, telemetry);

        // 2. Update Fleet Card
        if (activeCards.ContainsKey(telemetry.droneId))
        {
            activeCards[telemetry.droneId].UpdateFromLive(telemetry);
        }

        // 3. Update Detail View (Only if we are looking at this drone)
        if (SelectionManager.Instance != null)
        {
            string activeDroneId = SelectionManager.Instance.GetDroneAtSlot(SelectionManager.Instance.ActiveSlotId);
            if (activeDroneId == telemetry.droneId && detailController != null)
            {
                 detailController.UpdateVisuals(telemetry);
            }
        }
    }

    // --- HELPERS ---

    // Adapter to convert REST Snapshot -> Live Telemetry format
    private DroneTelemetryData ConvertSnapshotToTelemetry(DroneSnapshotModel snap)
    {
        DroneTelemetryData data = new DroneTelemetryData();
        
        // 1. Top Level Info
        data.droneId = snap.droneId;
        data.model = snap.model; 

        // 2. Nested Telemetry Info
        if (snap.telemetry != null)
        {
            data.latitude = snap.telemetry.latitude;
            data.longitude = snap.telemetry.longitude;
            data.altitude = snap.telemetry.altitude;
            
            data.heading = snap.telemetry.heading;
            data.velocityX = snap.telemetry.velocityX;
            data.velocityZ = snap.telemetry.velocityZ; // Assuming Z is the other horizontal component
            
            data.batteryLevel = snap.telemetry.batteryLevel;
            data.satCount = snap.telemetry.satelliteCount;
            
            data.isFlying = snap.telemetry.isFlying;
            data.online = snap.telemetry.online;
            data.motorsOn = snap.telemetry.areMotorsOn; // Maps 'areMotorsOn' -> 'motorsOn'
        }
        else
        {
            // Fallback if telemetry is null (e.g. freshly registered drone)
            data.online = snap.isConnected;
        }

        return data;
    }

    public void ShowFleetView()
    {
        if(fleetViewPanel) fleetViewPanel.SetActive(true);
        if(detailViewPanel) detailViewPanel.SetActive(false);
        if (SelectionManager.Instance != null) UpdateHeaderContext(SelectionManager.Instance.ActiveSlotId);
    }

    public void ShowDroneDetail()
    {
        if(fleetViewPanel) fleetViewPanel.SetActive(false);
        if(detailViewPanel) detailViewPanel.SetActive(true);
        
        // 🔥 FIX: Force update when opening the panel
        if (SelectionManager.Instance != null)
        {
            string currentDrone = SelectionManager.Instance.GetDroneAtSlot(SelectionManager.Instance.ActiveSlotId);
            if (!string.IsNullOrEmpty(currentDrone))
            {
                if (detailHeader) detailHeader.text = $"{currentDrone}";
                
                // Load from Cache immediately
                if (telemetryCache.ContainsKey(currentDrone) && detailController != null)
                {
                    detailController.UpdateVisuals(telemetryCache[currentDrone]);
                }
            }
        }
    }

    public void DisconnectActiveDrone()
    {
        int activeSlot = SelectionManager.Instance.ActiveSlotId;
        SelectionManager.Instance.ClearSlot(activeSlot);
        ShowFleetView();
    }
}