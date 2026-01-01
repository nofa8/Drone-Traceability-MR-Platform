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
        // Update the card in the list
        if (activeCards.ContainsKey(telemetry.id))
        {
            activeCards[telemetry.id].UpdateFromLive(telemetry);
        }

        // If this is the drone we are looking at in Detail View, update the 3D model
        if (selectedDroneId == telemetry.id && detailController != null)
        {
            // detailController needs to accept the telemetry data object
            // You might need to update DroneTelemetryController to accept DroneTelemetryData directly
            // or map it manually here. Assuming you updated it:
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
}