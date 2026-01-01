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

    // Private state
    public string droneId { get; private set; }
    public string modelName { get; private set; }

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

    // UPDATE 1: From REST API (Snapshot)
    public void UpdateFromSnapshot(DroneSnapshotModel data)
    {
        modelName = data.model;
        if (modelText) modelText.text = data.model;
        
        // "isConnected" from backend usually means "Online"
        UpdateVisuals(data.batteryLevel, data.isConnected, data.isFlying);
    }

    // UPDATE 2: From WebSocket (Live Telemetry)
    public void UpdateFromLive(DroneTelemetryData data)
    {
        // WebSocket uses 'batLvl' instead of 'batteryLevel'
        UpdateVisuals(data.batLvl, data.online, data.isFlying);
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