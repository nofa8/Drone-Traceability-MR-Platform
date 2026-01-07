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

    private Dictionary<string, DroneCardUI> activeCards = new Dictionary<string, DroneCardUI>();

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
            // 1. Listen for Slot Switching (Context)
            SelectionManager.Instance.OnActiveSlotChanged += UpdateHeaderContext;
            
            // 2. NEW: Listen for Drone Selection (Data)
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

    // --- FIX 1: Handle Drone Selection ---
    void HandleSelectionChanged(int slotId, string droneId)
    {
        // Only update header if this assignment is for the slot we are currently looking at
        if (slotId == SelectionManager.Instance.ActiveSlotId)
        {
            if (string.IsNullOrEmpty(droneId))
            {
                UpdateHeaderContext(slotId); // Go back to "Select Drone..."
            }
            else
            {
                if (detailHeader) detailHeader.text = $"{droneId}";
            }
        }
    }

    // --- FIX 2: Dynamic Text instead of Hardcoded ---
    void UpdateHeaderContext(int slotId)
    {
        if (detailHeader) 
        {
            // 1. Get the ID currently assigned to this slot
            string droneId = SelectionManager.Instance != null ? 
                             SelectionManager.Instance.GetDroneAtSlot(slotId) : null;

            // 2. Set the text
            if (string.IsNullOrEmpty(droneId))
            {
                detailHeader.text = "NO DRONE SELECTED";
            }
            else
            {
                // Displays just the ID (e.g., "RD001")
                detailHeader.text = droneId; 

            }
        }
    }

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
    }

    public void HandleLiveUpdate(DroneTelemetryData telemetry)
    {
        if (string.IsNullOrEmpty(telemetry.droneId)) return;

        // 1. Update the Card List (Always happens)
        if (activeCards.ContainsKey(telemetry.droneId))
        {
            activeCards[telemetry.droneId].UpdateFromLive(telemetry);
        }

        // 2. Route to Dashboard (Corrected Logic)
        // Check against the ACTIVE slot, not just slot 0
        string activeDroneId = SelectionManager.Instance.GetDroneAtSlot(SelectionManager.Instance.ActiveSlotId);

        if (activeDroneId == telemetry.droneId && detailController != null)
        {
             detailController.UpdateVisuals(telemetry);
        }
    }

    public void ShowFleetView()
    {
        if(fleetViewPanel) fleetViewPanel.SetActive(true);
        if(detailViewPanel) detailViewPanel.SetActive(false);
        
        // Refresh header context
        if (SelectionManager.Instance != null)
            UpdateHeaderContext(SelectionManager.Instance.ActiveSlotId);
    }

    public void ShowDroneDetail()
    {
        if(fleetViewPanel) fleetViewPanel.SetActive(false);
        if(detailViewPanel) detailViewPanel.SetActive(true);
        
        // NEW: Ensure header is correct when entering detail view manually
        string currentDrone = SelectionManager.Instance.GetDroneAtSlot(SelectionManager.Instance.ActiveSlotId);
        if (!string.IsNullOrEmpty(currentDrone))
        {
             if (detailHeader) detailHeader.text = $"{currentDrone}";
        }
    }

    public void DisconnectActiveDrone()
    {
        int activeSlot = SelectionManager.Instance.ActiveSlotId;
        SelectionManager.Instance.ClearSlot(activeSlot);
        ShowFleetView();
    }
}