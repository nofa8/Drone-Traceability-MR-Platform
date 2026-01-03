using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DroneCardUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI idText;
    public TextMeshProUGUI modelText;
    public TextMeshProUGUI statusText;
    public Image batteryFill;
    public Image statusIcon; // The colored circle

    [Header("Selection Visuals")]
    public Image selectionBorder; // Drag an 'Outline' image here
    public Color selectedColor = Color.cyan;
    public Color defaultColor = new Color(1, 1, 1, 0f);


    // Private state
    public string droneId { get; private set; }
    public string modelName { get; private set; }


    void Start()
    {
        // Subscribe to global selection changes
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnDroneSelected += HandleSelectionChanged;
        }
    }

    void OnDestroy()
    {
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnDroneSelected -= HandleSelectionChanged;
        }
    }
    // Initialize the card
    public void Setup(string id)
    {
        this.droneId = id;
        if (idText) idText.text = id;

        // Make the entire card a button that opens details
        Button btn = GetComponent<Button>();
        if (btn)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => FleetUIManager.Instance.ShowDroneDetail(this.droneId));
        }
    }


    void OnClick() // Ensure this is linked to your Button component
    {
        // Tell the Manager to select THIS drone
        SelectionManager.Instance.SelectDrone(this.droneId);
        
        // Optional: Auto-switch to Dashboard view via PanelManager
        PanelManager.Instance.TogglePanel("Dashboard");
    }

    private void HandleSelectionChanged(string newId)
    {
        bool isMe = (newId == this.droneId);
        
        if (selectionBorder)
        {
            selectionBorder.color = isMe ? selectedColor : defaultColor;
        }
    }   
    
    // REST API (Snapshot)
    public void UpdateFromSnapshot(DroneSnapshotModel data)
    {
        modelName = data.model;
        if (modelText) modelText.text = data.model;
        
        // Handle Nested Telemetry (Safe Navigation)
        if (data.telemetry != null)
        {
            UpdateVisuals(data.telemetry.batteryLevel, data.telemetry.online, data.telemetry.isFlying);
        }
        else
        {
            UpdateVisuals(0, data.isConnected, false);
        }
    }

    // WebSocket (Live)
    public void UpdateFromLive(DroneTelemetryData data)
    {
        UpdateVisuals(data.batteryLevel, data.online, data.isFlying);
    }

    // Shared visual logic
    private void UpdateVisuals(double battery, bool isOnline, bool isFlying)
    {
        // Battery Bar (0 to 1)
        if (batteryFill)
        {
            batteryFill.fillAmount = (float)battery / 100f;
            
            // Color Coding
            if (battery < 20) batteryFill.color = Color.red;
            else if (battery < 50) batteryFill.color = Color.yellow;
            else batteryFill.color = Color.green;
        }

        // Status Text & Icon
        if (statusText && statusIcon)
        {
            if (!isOnline)
            {
                statusText.text = "OFFLINE";
                statusIcon.color = Color.gray;
            }
            else if (isFlying)
            {
                statusText.text = "FLYING";
                statusIcon.color = Color.cyan;
            }
            else
            {
                statusText.text = "ONLINE"; // Idle
                statusIcon.color = Color.green;
            }
        }
    }
}