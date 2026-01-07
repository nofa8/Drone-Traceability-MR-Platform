using UnityEngine;
using TMPro;
using System;

public class DroneTelemetryController : MonoBehaviour
{
    [Header("Data Source")]
    public GameObject dataSourceObject; 
    private IDroneDataSource dataSource;

    [Header("UI References")]
    public TextMeshProUGUI speedText;
    public TextMeshProUGUI altitudeText;
    public TextMeshProUGUI batteryText;
    public TextMeshProUGUI latitudeText;
    public TextMeshProUGUI longitudeText;
    public TextMeshProUGUI modelText;
    public TextMeshProUGUI satelliteText;
    public TextMeshProUGUI statusText;
    
    [Header("Visuals")]
    public Transform droneModel;
    public float positionScale = 0.1f; 
    public DroneVisualizer visualizer;

    [Header("System")]
    public int assignedSlotId = 0;
    private string currentTargetId;

    void Start()
    {
        if (visualizer == null) visualizer = GetComponent<DroneVisualizer>();

        // Subscription
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnSlotSelectionChanged += OnSlotChanged;
            
            // Initial check
            string existingId = SelectionManager.Instance.GetDroneAtSlot(assignedSlotId);
            if (!string.IsNullOrEmpty(existingId))
            {
                OnSlotChanged(assignedSlotId, existingId);
            }
        }

        // Optional Mock Source
        if (dataSourceObject != null)
        {
            dataSource = dataSourceObject.GetComponent<IDroneDataSource>();
            if (dataSource != null)
            {
                dataSource.OnTelemetryReceived += UpdateVisuals;
                dataSource.Connect();
            }
        }
    }

    // 🔥 UPDATED LOGIC HERE
    void OnSlotChanged(int slotId, string newDroneId)
    {
        if (slotId != assignedSlotId) return;

        currentTargetId = newDroneId;
        
        // 1. CLEAR OLD DATA
        // This ensures no stale numbers remain if the new drone has no data yet.
        ClearVisuals();

        // 2. FETCH LAST KNOWN DATA
        // If the drone is valid, we check the cache immediately.
        if (!string.IsNullOrEmpty(newDroneId) && FleetUIManager.Instance != null)
        {
            DroneTelemetryData cachedData = FleetUIManager.Instance.GetLastKnownData(newDroneId);
            if (cachedData != null)
            {
                UpdateVisuals(cachedData);
            }
            else
            {
                // If no data exists yet, set status to loading
                if (statusText) statusText.text = "Status: <color=yellow>CONNECTING...</color>";
            }
        }
    }

    // New Helper to wipe the screen
    private void ClearVisuals()
    {
        if (speedText) speedText.text = "Speed: --";
        if (altitudeText) altitudeText.text = "Altitude: --";
        if (batteryText) batteryText.text = "Battery: --";
        if (latitudeText) latitudeText.text = "Latitude: --";
        if (longitudeText) longitudeText.text = "Longitude: --";
        if (modelText) modelText.text = "Model: --";
        if (satelliteText) satelliteText.text = "Satellite Count: --";
        if (statusText) statusText.text = "Status: --";

        if (visualizer != null) visualizer.ResetToIdle();
    }

    public void UpdateVisuals(DroneTelemetryData data)
    {
        if (data.droneId != currentTargetId) return; 
        
        // Math
        double speed = Math.Sqrt(data.velocityX * data.velocityX + data.velocityZ * data.velocityZ);
        
        // UI Updates
        if (speedText) speedText.text = $"Speed: {speed:F1} m/s";
        if (altitudeText) altitudeText.text = $"Altitude: {data.altitude:F1} m";
        if (batteryText) batteryText.text = $"Battery: {data.batteryLevel:F0}%";

        if (latitudeText) latitudeText.text = $"Latitude: {data.latitude:F5}";
        if (longitudeText) longitudeText.text = $"Longitude: {data.longitude:F5}";

        if (modelText) modelText.text = "Model: "+data.model;
        
        if (satelliteText) 
        {
            string color = data.satCount >= 10 ? "green" : (data.satCount >= 6 ? "yellow" : "red");
            satelliteText.text = $"Satellite Count: <color={color}>{data.satCount}</color>";
        }

        // Improved Status Logic
        if (statusText)
        {
            if (data.online)
            {
                if (data.isFlying) statusText.text = "Status: <color=green>FLYING</color>";
                else if (data.motorsOn) statusText.text = "Status: <color=yellow>ARMED</color>";
                else statusText.text = "Status: <color=white>ONLINE</color>";
            }
            else
            {
                statusText.text = "Status: <color=red>OFFLINE</color>";
            }
        }

        // 3D Model
        if (droneModel)
        {
            Vector3 targetPos = droneModel.localPosition;
            targetPos.y = (float)data.altitude * positionScale; 
            droneModel.localPosition = targetPos;
            droneModel.localRotation = Quaternion.Euler(0, (float)data.heading, 0);

            if (visualizer != null) visualizer.UpdateVisuals(data);
        }
    }

    void OnDestroy()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.OnSlotSelectionChanged -= OnSlotChanged;
        if (dataSource != null) dataSource.Disconnect();
    }
}