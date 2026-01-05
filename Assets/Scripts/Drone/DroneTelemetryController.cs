using UnityEngine;
using TMPro;
using System;

public class DroneTelemetryController : MonoBehaviour
{
    [Header("Data Source (Optional)")]
    [Tooltip("Leave empty if using Network Client. Assign MockDroneBackend for offline testing.")]
    public GameObject dataSourceObject; 
    private IDroneDataSource dataSource;

    [Header("UI References - Flight Data")]
    public TextMeshProUGUI speedText;
    public TextMeshProUGUI altitudeText;
    public TextMeshProUGUI batteryText;

    [Header("UI References - Location")]
    public TextMeshProUGUI latitudeText;
    public TextMeshProUGUI longitudeText;

    [Header("UI References - Status")]
    public TextMeshProUGUI modelText;
    public TextMeshProUGUI satelliteText;
    public TextMeshProUGUI statusText;
    
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
            if (speedText) speedText.text = "Speed: -";
            if (latitudeText) latitudeText.text = "Latitude: -";
            if (longitudeText) longitudeText.text = "Longitude: -";
            if (modelText) modelText.text = "Model: -";
            if (satelliteText) satelliteText.text = "Satellite Count: -";
            if (statusText) statusText.text = "Status: -";
            return;
        }
    }

    public void UpdateVisuals(DroneTelemetryData data)
    {
        // 🔒 SECURITY CHECK: Only update if this data belongs to the selected drone
        if (data.droneId != currentTargetId) return; 
        
        // --- UI Updates ---
        double speed = Math.Sqrt(data.velocityX * data.velocityX + data.velocityZ * data.velocityZ);
        
        if (speedText) speedText.text = $"Speed: {speed:F1} m/s";
        if (altitudeText) altitudeText.text = $"Altitude: {data.altitude:F1} m";
        if (batteryText) batteryText.text = $"Battery: {data.batteryLevel:F0}%";

        // --- Location Updates ---
        if (latitudeText) latitudeText.text = $"Latitude: {data.latitude:F5}";
        if (longitudeText) longitudeText.text = $"Longitude: {data.longitude:F5}";

        // --- Status Updates ---
        if (modelText) modelText.text = "Model: "+data.model;
        
        if (satelliteText) 
        {
            // Color code: Green if good GPS (>=10), Yellow if marginal, Red if bad
            string color = data.satCount >= 10 ? "green" : (data.satCount >= 6 ? "yellow" : "red");
            satelliteText.text = $"Satellite Count: <color={color}>{data.satCount}</color>";
        }

        if (statusText)
        {
            if (data.isFlying) statusText.text = "<color=green>FLYING</color>";
            else if (data.motorsOn) statusText.text = "<color=yellow>ARMED</color>";
            else statusText.text = "<color=grey>IDLE</color>";
        }

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