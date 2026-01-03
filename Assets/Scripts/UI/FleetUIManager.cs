using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class FleetUIManager : MonoBehaviour
{
    public static FleetUIManager Instance;

    [Header("Backend Configuration")]
    // Matches your launchSettings.json profile
    public string apiBaseUrl = "http://localhost:5101"; 

    [Header("Panel References")]
    public GameObject fleetViewPanel;   // The Grid View
    public GameObject detailViewPanel;  // The Dashboard View
    
    [Header("Grid Components")]
    public Transform gridContainer;     // The Content object of your Scroll View
    public GameObject droneCardPrefab;  // The prefab with DroneCardUI attached

    [Header("Detail View Connection")]
    public DroneTelemetryController detailController; // Your existing script for the 3D drone
    public TextMeshProUGUI detailHeader;

    // State Tracking
    private Dictionary<string, DroneCardUI> activeCards = new Dictionary<string, DroneCardUI>();
    private string selectedDroneId = null;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Start by showing the list
        ShowFleetView();
    }

    void Start()
    {
        // Fetch the initial list of drones from the REST API
        StartCoroutine(FetchDroneList());
    }

    IEnumerator FetchDroneList()
    {
        // Hits your Backend's DroneSnapshotController.GetAll()
        string url = $"{apiBaseUrl}/api/drones?limit=50";
        Debug.Log($"📡 Fetching fleet from: {url}");

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                // Parse the JSON response
                var result = JsonUtility.FromJson<PagedSnapshotResult>(req.downloadHandler.text);
                
                if (result != null && result.items != null)
                {
                    Debug.Log($"✅ Found {result.items.Count} drones in database.");
                    foreach (var drone in result.items)
                    {
                        CreateOrUpdateCard(drone);
                    }
                }
            }
            else
            {
                Debug.LogError($"❌ REST Error: {req.error}");
            }
        }
    }

    // 1. Create/Update Card from REST
    public void CreateOrUpdateCard(DroneSnapshotModel snapshot)
    {
        if (string.IsNullOrEmpty(snapshot.droneId)) return;

        // If card doesn't exist, create it
        if (!activeCards.ContainsKey(snapshot.droneId))
        {
            GameObject newCard = Instantiate(droneCardPrefab, gridContainer);
            DroneCardUI cardUI = newCard.GetComponent<DroneCardUI>();
            
            cardUI.Setup(snapshot.droneId);
            activeCards.Add(snapshot.droneId, cardUI);
        }

        // Push data to card
        activeCards[snapshot.droneId].UpdateFromSnapshot(snapshot);
    }

    // 2. Handle Live WebSocket Data
    // (Call this from DroneNetworkClient.cs)
    public void HandleLiveUpdate(DroneTelemetryData telemetry)
    {
        // Safety: Ignore bad data
        if (string.IsNullOrEmpty(telemetry.droneId)) return;

        if (activeCards.ContainsKey(telemetry.droneId))
        {
            activeCards[telemetry.droneId].UpdateFromLive(telemetry);
        }

        if (selectedDroneId == telemetry.droneId && detailController != null)
        {
             detailController.UpdateVisuals(telemetry);
        }
    }

    // --- Navigation Logic ---

    public void ShowFleetView()
    {
        selectedDroneId = null;
        if(fleetViewPanel) fleetViewPanel.SetActive(true);
        if(detailViewPanel) detailViewPanel.SetActive(false);
    }

    public void ShowDroneDetail(string droneId)
    {
        selectedDroneId = droneId;
        
        if(fleetViewPanel) fleetViewPanel.SetActive(false);
        if(detailViewPanel) detailViewPanel.SetActive(true);

        // Update Header Text
        if (detailHeader) detailHeader.text = $"COMMAND: {droneId}";
        
        // Optional: Reset the 3D model position here?
    }



    // ---------------- DEBUG TESTING ----------------
    // This adds a "Right-Click" menu to the script in the Inspector
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