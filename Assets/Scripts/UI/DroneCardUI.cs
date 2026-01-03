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
    public Image statusIcon; 
    public Image selectionBorder; // Optional: Assign this in Inspector for visual feedback

    [Header("Settings")]
    public Color selectedColor = Color.yellow;
    public Color defaultColor = new Color(0, 0, 0, 0); // Transparent

    public string droneId { get; private set; }

    // --- LIFECYCLE EVENTS (Fixing the Subscription Error) ---
    void Start()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.OnSlotSelectionChanged += HandleSlotSelectionChanged;
    }

    void OnDestroy()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.OnSlotSelectionChanged -= HandleSlotSelectionChanged;
    }

    // --- SETUP & INTERACTION ---
    public void Setup(string id)
    {
        this.droneId = id;
        if (idText) idText.text = id;
        
        Button btn = GetComponent<Button>();
        if (btn)
        {
            // FIX: Ensure clean wiring so OnClick actually fires
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnClick);
        }
    }

    // The Logic: Set Intent -> Update State -> Navigate
    void OnClick()
    {
        // 1. Get Intent (Which slot are we filling?)
        int targetSlot = FleetUIManager.Instance.targetSlotId;

        // 2. Update System State (The Database)
        SelectionManager.Instance.SetDroneAtSlot(targetSlot, this.droneId);
        
        // 3. Navigate
        FleetUIManager.Instance.ShowDroneDetail();
    }

    // --- VISUAL FEEDBACK (Fixing the Event Handler) ---
    private void HandleSlotSelectionChanged(int slotId, string selectedId)
    {
        // Highlight this card if it is selected in ANY slot
        bool isSelected = (selectedId == this.droneId);

        if (selectionBorder)
        {
            selectionBorder.color = isSelected ? selectedColor : defaultColor;
        }
    }

    // --- DATA UPDATES (Keep existing logic) ---
    public void UpdateFromSnapshot(DroneSnapshotModel data)
    {
        if (modelText) modelText.text = data.model;
        if (data.telemetry != null) UpdateVisuals(data.telemetry.batteryLevel, data.telemetry.online, data.telemetry.isFlying);
        else UpdateVisuals(0, data.isConnected, false);
    }

    public void UpdateFromLive(DroneTelemetryData data)
    {
        UpdateVisuals(data.batteryLevel, data.online, data.isFlying);
    }

    private void UpdateVisuals(double battery, bool isOnline, bool isFlying)
    {
        if (batteryFill)
        {
            batteryFill.fillAmount = (float)battery / 100f;
            if (battery < 20) batteryFill.color = Color.red;
            else if (battery < 50) batteryFill.color = Color.yellow;
            else batteryFill.color = Color.green;
        }

        if (statusText && statusIcon)
        {
            if (!isOnline) { statusText.text = "OFFLINE"; statusIcon.color = Color.gray; }
            else if (isFlying) { statusText.text = "FLYING"; statusIcon.color = Color.cyan; }
            else { statusText.text = "ONLINE"; statusIcon.color = Color.green; }
        }
    }
}