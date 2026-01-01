using UnityEngine;
using TMPro;
using System;

public class DroneTelemetryController : MonoBehaviour
{
    [Header("Data Source")]
    // Drag your MockDroneBackend object here!
    public GameObject dataSourceObject; 
    private IDroneDataSource dataSource;

    [Header("UI References")]
    public TextMeshProUGUI speedText;
    public TextMeshProUGUI altitudeText;
    public TextMeshProUGUI batteryText;
    
    [Header("3D Digital Twin")]
    public Transform droneModel;
    public float positionScale = 0.1f; // 10 meters real world = 1 meter Unity
    public DroneVisualizer visualizer;
    
    void Start()
    {
        // 1. Find the data source (Interface pattern)
        if (dataSourceObject != null)
        {
            dataSource = dataSourceObject.GetComponent<IDroneDataSource>();
        }

        if (visualizer == null)
        {
            visualizer = GetComponent<DroneVisualizer>();
        }

        if (dataSource == null)
        {
            Debug.LogError("❌ No IDroneDataSource found! Please attach MockDroneBackend script.");
            return;
        }

        // 2. Subscribe to updates
        dataSource.OnTelemetryReceived += UpdateVisuals;
        
        // 3. Start the stream
        dataSource.Connect();
    }

    public void UpdateVisuals(DroneTelemetryData data)
    {
        // --- UI Updates ---
        // Calculate 2D speed magnitude
        double speed = Math.Sqrt(data.velX * data.velX + data.velZ * data.velZ);
        
        if (speedText) speedText.text = $"Speed: {speed:F1} m/s";
        if (altitudeText) altitudeText.text = $"Alt: {data.alt:F1} m";
        if (batteryText) batteryText.text = $"Bat: {data.batLvl:F0}%";

        // --- 3D Model Movement ---
        if (droneModel)
        {
            // Simple mapping: Altitude controls Y height
            Vector3 targetPos = droneModel.localPosition;
            targetPos.y = (float)data.alt * positionScale; 
            
            // Map GPS/Velocity to local X/Z (Simplified for Mock)
            // Here we just use the fake velocity to "drift" the model around its center
            // In a real GPS system, you'd convert Lat/Lon to Meters.
            
            droneModel.localPosition = targetPos;

            // Rotate drone to match heading
            droneModel.localRotation = Quaternion.Euler(0, (float)data.hdg, 0);

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