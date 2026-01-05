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

    // REMOVED: public int targetSlotId; (Legacy)

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
        
        // Listen to Active Slot changes to update the header text
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnActiveSlotChanged += UpdateHeaderContext;
        }
    }
    
    void OnDestroy()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.OnActiveSlotChanged -= UpdateHeaderContext;
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
        // We do NOT use a local variable. We query the Truth (SelectionManager).
        // Check if this telemetry matches the drone currently assigned to Slot 0
        string activeDroneId = SelectionManager.Instance.GetDroneAtSlot(0);

        if (activeDroneId == telemetry.droneId && detailController != null)
        {
             detailController.UpdateVisuals(telemetry);
        }
    }

    // --- UPDATED NAVIGATION ---

    public void ShowFleetView()
    {
        if(fleetViewPanel) fleetViewPanel.SetActive(true);
        if(detailViewPanel) detailViewPanel.SetActive(false);
        
        // Update header so user knows what they are picking for
        if (SelectionManager.Instance != null)
            UpdateHeaderContext(SelectionManager.Instance.ActiveSlotId);
    }

    public void ShowDroneDetail()
    {
        if(fleetViewPanel) fleetViewPanel.SetActive(false);
        if(detailViewPanel) detailViewPanel.SetActive(true);
    }

    void UpdateHeaderContext(int slotId)
    {
        if (detailHeader) 
            detailHeader.text = $"SELECT DRONE FOR SLOT {slotId}";
    }

    // DEBUG: Right-click component in Inspector to test UI without backend
    [ContextMenu("Test: Add Fake Drone")]
    public void TestAddFakeDrone()
    {
        DroneSnapshotModel fake = new DroneSnapshotModel();
        fake.droneId = "Sim-" + Random.Range(100, 999);
        fake.model = "Debug-X1";
        fake.telemetry = new DroneSnapshotTelemetry { batteryLevel = 50, isFlying = true, online = true };
        CreateOrUpdateCard(fake);
    }
}