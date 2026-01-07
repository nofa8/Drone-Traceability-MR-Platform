using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DroneCardUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI idText;
    public TextMeshProUGUI modelText;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI batteryText; // NEW: Shows "85%"
    public Image batteryFill;
    public Image statusIcon; 
    public Image selectionBorder; 

    [Header("Visual Settings")]
    public Color slot0Color = Color.cyan;
    public Color slot1Color = new Color(1f, 0.5f, 0f); // Orange
    public Color offlineColor = Color.gray;
    public Color defaultBorderColor = new Color(0, 0, 0, 0); // Transparent

    public string droneId { get; private set; }

    // --- LIFECYCLE ---
    void Start()
    {
        if (SelectionManager.Instance != null)
        {
            // Listen for ANY change in selection
            SelectionManager.Instance.OnSlotSelectionChanged += HandleAnySelectionChange;
            // Check initial state
            RefreshSelectionVisuals();
        }
    }

    void OnDestroy()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.OnSlotSelectionChanged -= HandleAnySelectionChange;
    }

    // --- SETUP & INTERACTION ---
    public void Setup(string id)
    {
        this.droneId = id;
        if (idText) idText.text = id;
        
        Button btn = GetComponent<Button>();
        if (btn)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnClick);
        }
    }

    void OnClick()
    {
        SelectionManager.Instance.AssignDroneToActiveSlot(this.droneId);
        FleetUIManager.Instance.ShowDroneDetail(); 
    }

    // --- VISUAL FEEDBACK (Border Highlight) ---
    
    // We ignore the specific arguments and just refresh our own state
    private void HandleAnySelectionChange(int slotId, string selectedId)
    {
        RefreshSelectionVisuals();
    }

    private void RefreshSelectionVisuals()
    {
        if (!selectionBorder || SelectionManager.Instance == null) return;

        // Ask the Manager: "Am I assigned to any slot?"
        int mySlot = SelectionManager.Instance.GetSlotForDrone(this.droneId);

        if (mySlot == 0) selectionBorder.color = slot0Color;      // Cyan for Slot 0
        else if (mySlot == 1) selectionBorder.color = slot1Color; // Orange for Slot 1
        else selectionBorder.color = defaultBorderColor;          // Hidden
    }

    // --- DATA UPDATES ---

    public void UpdateFromSnapshot(DroneSnapshotModel data)
    {
        if (modelText) modelText.text = data.model;

        // Pass 'areMotorsOn' if available
        if (data.telemetry != null) 
        {
            UpdateVisuals(
                data.telemetry.batteryLevel, 
                data.telemetry.online, 
                data.telemetry.isFlying,
                data.telemetry.areMotorsOn // Use snapshot motors data
            );
        }
        else 
        {
            UpdateVisuals(0, data.isConnected, false, false);
        }
    }

    public void UpdateFromLive(DroneTelemetryData data)
    {
        UpdateVisuals(
            data.batteryLevel, 
            data.online, 
            data.isFlying,
            data.motorsOn // Use live motors data
        );
    }

    // 🔥 IMPROVED: Now handles "ARMED" state and Battery Text
    private void UpdateVisuals(double battery, bool isOnline, bool isFlying, bool areMotorsOn)
    {
        // 1. Battery Visuals
        float batVal = (float)battery;
        if (batteryFill)
        {
            batteryFill.fillAmount = batVal / 100f;
            if (batVal < 20) batteryFill.color = Color.red;
            else if (batVal < 50) batteryFill.color = Color.yellow;
            else batteryFill.color = Color.green;
        }
        
        // NEW: Battery Text
        if (batteryText) batteryText.text = $"{Mathf.RoundToInt(batVal)}%";

        // 2. Status Logic (Hierarchy of States)
        if (statusText && statusIcon)
        {
            if (!isOnline) 
            { 
                SetStatus("OFFLINE", offlineColor);
            }
            else if (isFlying) 
            { 
                SetStatus("FLYING", Color.cyan);
            }
            else if (areMotorsOn)
            {
                // New "Armed" state (Motors on, but on ground)
                SetStatus("ARMED", new Color(1f, 0.5f, 0f)); // Orange/Red warning
            }
            else 
            { 
                SetStatus("READY", Color.green);
            }
        }
    }

    private void SetStatus(string text, Color color)
    {
        statusText.text = text;
        statusIcon.color = color;
    }
}