using UnityEngine;
using TMPro;
using System;

public class DroneTelemetryController : MonoBehaviour
{
    [Header("Data Source (Optional)")]
    [Tooltip("Leave empty if using Network Client. Assign MockDroneBackend for offline testing.")]
    public GameObject dataSourceObject; 
    private IDroneDataSource dataSource;

    [Header("UI References")]
    public TextMeshProUGUI speedText;
    public TextMeshProUGUI altitudeText;
    public TextMeshProUGUI batteryText;
    
    [Header("3D Digital Twin")]
    public Transform droneModel;
    public float positionScale = 0.1f; 
    public DroneVisualizer visualizer;

    [Header("System Architecture")]
    [Tooltip("Which 'Monitor' is this? 0 = Main, 1 = Secondary")]
    public int assignedSlotId = 0;
    
    // The ID of the drone this dashboard is currently tracking
    private string currentTargetId;

    void Start()
    {
        // 1. Setup Visualizer
        if (visualizer == null) visualizer = GetComponent<DroneVisualizer>();

        // 2. Setup Selection System (The "Brain")
        if (SelectionManager.Instance != null)
        {
            // Subscribe to the slot change event
            SelectionManager.Instance.OnSlotSelectionChanged += OnSlotChanged;
            
            // Initialize with whatever is currently in this slot
            // FIX 1: Correct Method Name (GetDroneAtSlot)
            string existingId = SelectionManager.Instance.GetDroneAtSlot(assignedSlotId);
            
            if (!string.IsNullOrEmpty(existingId))
            {
                OnSlotChanged(assignedSlotId, existingId);
            }
        }

        // 3. Setup Direct Data Source (Optional Mock)
        if (dataSourceObject != null)
        {
            dataSource = dataSourceObject.GetComponent<IDroneDataSource>();
            if (dataSource != null)
            {
                dataSource.OnTelemetryReceived += UpdateVisuals;
                dataSource.Connect();
            }
        }
        else
        {
            // FIX 3: Don't error out if missing. We might be in "Passive Mode" (Network driven).
            Debug.Log($"ℹ️ DroneController (Slot {assignedSlotId}): Passive Mode. Waiting for FleetManager.");
        }
    }

    // The Filter Logic
    void OnSlotChanged(int slotId, string newDroneId)
    {
        // 🛑 CRITICAL: Only update if the event is for MY slot
        if (slotId != assignedSlotId) return;

        currentTargetId = newDroneId;
        
        // Visual Reset if deselected
        if (string.IsNullOrEmpty(newDroneId))
        {
            if (speedText) speedText.text = "Waiting...";
            return;
        }
    }

    // FIX 2: Removed dead code "OnSelectionChanged" which was never used.

    public void UpdateVisuals(DroneTelemetryData data)
    {
        // 🔒 SECURITY CHECK: Only update if this data belongs to the selected drone
        // If currentTargetId is null, we shouldn't show anything.
        if (data.droneId != currentTargetId) return; 
        
        // --- UI Updates ---
        double speed = Math.Sqrt(data.velocityX * data.velocityX + data.velocityZ * data.velocityZ);
        
        if (speedText) speedText.text = $"Speed: {speed:F1} m/s";
        if (altitudeText) altitudeText.text = $"Alt: {data.altitude:F1} m";
        if (batteryText) batteryText.text = $"Bat: {data.batteryLevel:F0}%";

        // --- 3D Model Movement ---
        if (droneModel)
        {
            Vector3 targetPos = droneModel.localPosition;
            targetPos.y = (float)data.altitude * positionScale; 
            
            droneModel.localPosition = targetPos;

            // Rotation
            droneModel.localRotation = Quaternion.Euler(0, (float)data.heading, 0);

            if (visualizer != null)
            {
                visualizer.UpdateVisuals(data);
            }
        }
    }

    void OnDestroy()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.OnSlotSelectionChanged -= OnSlotChanged;
            
        if (dataSource != null) dataSource.Disconnect();
    }
}