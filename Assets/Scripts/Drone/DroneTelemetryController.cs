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
    
    [Header("3D Digital Twin")]
    public Transform droneModel;
    public float positionScale = 0.1f; 
    public DroneVisualizer visualizer;
    
    private string currentTargetId;
    void Start()
    {
        if (dataSourceObject != null)
        {
            dataSource = dataSourceObject.GetComponent<IDroneDataSource>();
        }

        if (visualizer == null)
        {
            visualizer = GetComponent<DroneVisualizer>();
        }

        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnDroneSelected += OnSelectionChanged;
        }

        if (dataSource == null)
        {
            Debug.LogError("❌ No IDroneDataSource found! Please attach MockDroneBackend script.");
            return;
        }

        dataSource.OnTelemetryReceived += UpdateVisuals;
        dataSource.Connect();
    }

    void OnSelectionChanged(string newId)
    {
        currentTargetId = newId;
        
        // Clear UI if deselected
        if (string.IsNullOrEmpty(newId))
        {
             // Reset text to "Waiting..."
             return;
        }
        
        // Optional: Force a refresh of static data (Model name) from FleetManager here
    }

    public void UpdateVisuals(DroneTelemetryData data)
    {

        // 🔒 SECURITY CHECK: Only update if this data belongs to the selected drone
        if (data.droneId != currentTargetId) return; 
        
        // --- UI Updates ---
        // FIX: velX/velZ -> velocityX/velocityZ
        double speed = Math.Sqrt(data.velocityX * data.velocityX + data.velocityZ * data.velocityZ);
        
        // FIX: alt -> altitude, batLvl -> batteryLevel
        if (speedText) speedText.text = $"Speed: {speed:F1} m/s";
        if (altitudeText) altitudeText.text = $"Alt: {data.altitude:F1} m";
        if (batteryText) batteryText.text = $"Bat: {data.batteryLevel:F0}%";

        // --- 3D Model Movement ---
        if (droneModel)
        {
            Vector3 targetPos = droneModel.localPosition;
            targetPos.y = (float)data.altitude * positionScale; 
            
            droneModel.localPosition = targetPos;

            // FIX: hdg -> heading
            droneModel.localRotation = Quaternion.Euler(0, (float)data.heading, 0);

            if (visualizer != null)
            {
                visualizer.UpdateVisuals(data);
            }
        }
    }

    void OnDestroy()
    {
        if (dataSource != null) dataSource.Disconnect();
    }
}