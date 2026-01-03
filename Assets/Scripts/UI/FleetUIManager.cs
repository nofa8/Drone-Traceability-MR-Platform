using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class FleetUIManager : MonoBehaviour
{
    public static FleetUIManager Instance;

    [Header("Backend Configuration")]
    public string apiBaseUrl = "http://localhost:5101"; 

    [Header("Interaction State")]
    public int targetSlotId = 0; // The "Intent"

    [Header("Panel References")]
    public GameObject fleetViewPanel;   
    public GameObject detailViewPanel;  
    
    [Header("Grid Components")]
    public Transform gridContainer;     
    public GameObject droneCardPrefab;  

    [Header("Detail View Connection")]
    public DroneTelemetryController detailController; 
    public TextMeshProUGUI detailHeader;

    private Dictionary<string, DroneCardUI> activeCards = new Dictionary<string, DroneCardUI>();

    // ❌ REMOVED: private string selectedDroneId; (Obsolete)

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        ShowFleetView();
    }

    void Start() => StartCoroutine(FetchDroneList());

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
        // We do NOT use a local variable. We query the Truth (SelectionManager).
        // Check if this telemetry matches the drone currently assigned to Slot 0
        string activeDroneId = SelectionManager.Instance.GetDroneAtSlot(0);

        if (activeDroneId == telemetry.droneId && detailController != null)
        {
             detailController.UpdateVisuals(telemetry);
        }
    }

    // --- NAVIGATION ---

    public void ShowFleetViewForSlot(int slotId)
    {
        targetSlotId = slotId;
        Debug.Log($"👆 UI Intent: Picking drone for Slot {slotId}");
        
        ShowFleetView();
        if (detailHeader) detailHeader.text = "SELECT DRONE...";
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

        // Update header using the System State
        string currentId = SelectionManager.Instance.GetDroneAtSlot(0);
        if (detailHeader) detailHeader.text = $"COMMAND: {currentId ?? "NONE"}";
    }
}