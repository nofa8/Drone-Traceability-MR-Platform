using UnityEngine;
using TMPro;
using System;

public class DroneTelemetryController : MonoBehaviour
{
    [Header("Data Source")]
    [Tooltip("Optional: For offline testing without the full Network Client")]
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
    [Tooltip("If TRUE, this UI always shows the currently selected slot. If FALSE, it stays locked to 'assignedSlotId'.")]
    public bool trackActiveSlot = true; 
    public int assignedSlotId = 0;

    private string currentTargetId;

    void Start()
    {
        if (visualizer == null) visualizer = GetComponent<DroneVisualizer>();

        // 1. Setup Selection System
        if (SelectionManager.Instance != null)
        {
            // A. Handle assigning a drone to a slot
            SelectionManager.Instance.OnSlotSelectionChanged += OnSlotAssignmentChanged;

            // B. Handle switching which slot we are looking at (Slot 1 -> Slot 2)
            if (trackActiveSlot)
            {
                SelectionManager.Instance.OnActiveSlotChanged += OnActiveSlotChanged;
                assignedSlotId = SelectionManager.Instance.ActiveSlotId;
            }
            
            // Initial Refresh
            RefreshTarget();
        }

        // 2. Setup Repository Listener (The Gold Standard)
        if (DroneStateRepository.Instance != null)
        {
            DroneStateRepository.Instance.OnDroneStateUpdated += HandleStateUpdate;
        }

        // 3. Setup Direct Data Source (Optional Mock)
        if (dataSourceObject != null)
        {
            dataSource = dataSourceObject.GetComponent<IDroneDataSource>();
            if (dataSource != null)
            {
                dataSource.OnTelemetryReceived += HandleDirectTelemetry;
                dataSource.Connect();
            }
        }
    }

    void OnDestroy()
    {
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnSlotSelectionChanged -= OnSlotAssignmentChanged;
            SelectionManager.Instance.OnActiveSlotChanged -= OnActiveSlotChanged;
        }

        if (DroneStateRepository.Instance != null)
            DroneStateRepository.Instance.OnDroneStateUpdated -= HandleStateUpdate;
            
        if (dataSource != null) dataSource.Disconnect();
    }

    // --- EVENT HANDLERS ---

    // Event A: User switches tabs (Slot 1 -> Slot 2)
    void OnActiveSlotChanged(int newSlotId)
    {
        assignedSlotId = newSlotId;
        RefreshTarget();
    }

    // Event B: User assigns a drone to a slot
    void OnSlotAssignmentChanged(int slotId, string newDroneId)
    {
        if (slotId != assignedSlotId) return;
        RefreshTarget();
    }

    // Helper: Find the correct drone ID and update UI
    void RefreshTarget()
    {
        if (SelectionManager.Instance == null) return;

        currentTargetId = SelectionManager.Instance.GetDroneAtSlot(assignedSlotId);
        
        // 1. Clear immediately to avoid confusion
        if (string.IsNullOrEmpty(currentTargetId))
        {
            ClearVisuals();
            return;
        }

        // 2. Fetch last known state immediately from Repository
        // 🔥 This fixes the "Empty Detail View" bug!
        if (DroneStateRepository.Instance != null)
        {
            DroneState savedState = DroneStateRepository.Instance.GetState(currentTargetId);
            UpdateVisuals(savedState);
        }
    }

    // Event C: Repository has new data
    void HandleStateUpdate(string droneId, DroneState state)
    {
        if (droneId == currentTargetId)
        {
            UpdateVisuals(state);
        }
    }

    // Event D: Direct Data Source (Mock/Test)
    void HandleDirectTelemetry(DroneTelemetryData data)
    {
        // Option A: If we have a Repository, feed it (Best Practice)
        if (DroneStateRepository.Instance != null)
        {
            DroneStateRepository.Instance.UpdateFromTelemetry(data);
        }
        else 
        {
            // Option B: Fallback (Direct Mode)
            DroneState tempState = new DroneState(data.droneId);
            tempState.data = data;
            tempState.isConnected = true;
            tempState.lastHeartbeatTime = DateTime.UtcNow;
            
            UpdateVisuals(tempState);
        }
    }

    // --- UI LOGIC ---

    private void ClearVisuals()
    {
        if (speedText) speedText.text = "Speed: --";
        if (altitudeText) altitudeText.text = "Altitude: --";
        if (batteryText) batteryText.text = "Battery: --";
        if (latitudeText) latitudeText.text = "Lat: --";
        if (longitudeText) longitudeText.text = "Lon: --";
        if (modelText) modelText.text = "Model: --";
        if (satelliteText) satelliteText.text = "Sats: --";
        if (statusText) statusText.text = "Status: NO DRONE";

        if (visualizer != null) visualizer.ResetToIdle();
    }

    public void UpdateVisuals(DroneState state)
    {
        if (state == null) return;
        DroneTelemetryData data = state.data;

        // Security Check
        if (data.droneId != currentTargetId) return; 
        
        // Math
        double speed = Math.Sqrt(data.velocityX * data.velocityX + data.velocityZ * data.velocityZ);
        
        // Text Updates
        if (speedText) speedText.text = $"Speed: {speed:F1} m/s";
        if (altitudeText) altitudeText.text = $"Altitude: {data.altitude:F1} m";
        if (batteryText) batteryText.text = $"Battery: {data.batteryLevel:F0}%";
        if (latitudeText) latitudeText.text = $"Lat: {data.latitude:F5}";
        if (longitudeText) longitudeText.text = $"Lon: {data.longitude:F5}";
        if (modelText) modelText.text = "Model: " + (string.IsNullOrEmpty(data.model) ? "Unknown" : data.model);
        
        if (satelliteText) 
        {
            string color = data.satCount >= 10 ? "green" : (data.satCount >= 6 ? "yellow" : "red");
            satelliteText.text = $"Sats: <color={color}>{data.satCount}</color>";
        }

        // Status Logic (Uses State Meta-Data)
        if (statusText)
        {
            if (state.IsStale) statusText.text = "Status: <color=yellow>STALE</color>";
            else if (!state.isConnected) statusText.text = "Status: <color=red>OFFLINE</color>";
            else if (data.isFlying) statusText.text = "Status: <color=green>FLYING</color>";
            else if (data.motorsOn) statusText.text = "Status: <color=orange>ARMED</color>";
            else statusText.text = "Status: <color=white>ONLINE</color>";
        }

        // 3D Model Logic
        // 🔥 FIX: If data is Stale (old snapshot), Force Idle to avoid "Ghost Flying"
        if (state.IsStale || !state.isConnected)
        {
            if (visualizer != null) visualizer.ResetToIdle();
            // We can still show the position if you want, but rotation/props should stop
        }
        else
        {
            // Only move the model if we have FRESH data
            if (droneModel)
            {
                Vector3 targetPos = droneModel.localPosition;
                targetPos.y = (float)data.altitude * positionScale; 
                droneModel.localPosition = targetPos;
                droneModel.localRotation = Quaternion.Euler(0, (float)data.heading, 0);
            }
            if (visualizer != null) visualizer.UpdateVisuals(data);
        }
    }
}