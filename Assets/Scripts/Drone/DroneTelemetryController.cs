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
    
    // 🔥 NEW: Replay Override State (The "Hijack" Variable)
    private DroneTelemetryData replayOverrideData = null;

    void Start()
    {
        if (visualizer == null) visualizer = GetComponent<DroneVisualizer>();

        // 1. Setup Selection System
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnSlotSelectionChanged += OnSlotAssignmentChanged;

            if (trackActiveSlot)
            {
                SelectionManager.Instance.OnActiveSlotChanged += OnActiveSlotChanged;
                assignedSlotId = SelectionManager.Instance.ActiveSlotId;
            }
            
            RefreshTarget();
        }

        // 2. Setup Repository Listener
        if (DroneStateRepository.Instance != null)
        {
            DroneStateRepository.Instance.OnDroneStateUpdated += HandleStateUpdate;
        }

        // 3. Setup Direct Data Source
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

    // --- 🎮 REPLAY API (The Safe Override) ---

    public void SetReplayOverride(DroneTelemetryData historyData)
    {
        replayOverrideData = historyData;
        // Force immediate render of history data
        UpdateSharedVisuals(historyData, isReplay: true);
    }

    public void ClearReplayOverride()
    {
        replayOverrideData = null;
        // Immediately revert to live state
        RefreshTarget();
    }

    // --- EVENT HANDLERS ---

    void OnActiveSlotChanged(int newSlotId)
    {
        assignedSlotId = newSlotId;
        RefreshTarget();
    }

    void OnSlotAssignmentChanged(int slotId, string newDroneId)
    {
        if (slotId != assignedSlotId) return;
        RefreshTarget();
    }

    void RefreshTarget()
    {
        if (SelectionManager.Instance == null) return;

        currentTargetId = SelectionManager.Instance.GetDroneAtSlot(assignedSlotId);
        
        if (string.IsNullOrEmpty(currentTargetId))
        {
            ClearVisuals();
            return;
        }

        // Fetch last known state immediately
        if (DroneStateRepository.Instance != null)
        {
            DroneState savedState = DroneStateRepository.Instance.GetState(currentTargetId);
            UpdateVisuals(savedState);
        }
    }

    void HandleStateUpdate(string droneId, DroneState state)
    {
        if (droneId == currentTargetId)
        {
            UpdateVisuals(state);
        }
    }

    void HandleDirectTelemetry(DroneTelemetryData data)
    {
        if (DroneStateRepository.Instance != null)
        {
            DroneStateRepository.Instance.UpdateFromTelemetry(data);
        }
        else 
        {
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

    // The "Live" Entry Point
    public void UpdateVisuals(DroneState state)
    {
        // 🔒 SAFETY: If Replay is active, ignore live updates!
        if (replayOverrideData != null) {
            return;
        }

        if (state == null) return;
        DroneTelemetryData data = state.data;

        if (data.droneId != currentTargetId) return; 

        // 1. Handle Status Logic (State-Dependent)
        // This includes your "Stale" and "Offline" checks
        if (statusText)
        {
            if (state.IsStale) statusText.text = "Status: <color=yellow>STALE</color>";
            else if (!state.isConnected) statusText.text = "Status: <color=red>OFFLINE</color>";
            else if (data.isFlying) statusText.text = "Status: <color=green>FLYING</color>";
            else if (data.motorsOn) statusText.text = "Status: <color=orange>ARMED</color>";
            else statusText.text = "Status: <color=white>ONLINE</color>";
        }

        // 2. Handle "Ghost Flying" Prevention
        // If data is Stale, Force Idle 3D model
        if (state.IsStale || !state.isConnected)
        {
            if (visualizer != null) visualizer.ResetToIdle();
            // We still update text, but maybe not position if you prefer
            UpdateSharedVisuals(data, isReplay: false, skipModel: true);
        }
        else
        {
            // Normal Live Render
            UpdateSharedVisuals(data, isReplay: false, skipModel: false);
        }
    }

    // 🔥 NEW: Shared Renderer (Used by both Live and Replay)
    private void UpdateSharedVisuals(DroneTelemetryData data, bool isReplay, bool skipModel = false)
    {
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

        // Replay specific status override
        if (isReplay && statusText)
        {
            statusText.text = "Status: REPLAY";
        }

        // 3D Model Logic
        if (!skipModel && droneModel)
        {
            Vector3 targetPos = droneModel.localPosition;
            targetPos.y = (float)data.altitude * positionScale; 
            droneModel.localPosition = targetPos;
            droneModel.localRotation = Quaternion.Euler(0, (float)data.heading, 0);
        }

        // Only update visualizer (props spinning) if not skipping model
        if (!skipModel && visualizer != null) 
        {
            visualizer.UpdateVisuals(data);
        }
        else if (visualizer != null && isReplay)
        {
             // Optional: If you want props to spin during replay, allow it. 
             // If not, call ResetToIdle(). Usually Replay = Static or Flying state from history.
             visualizer.UpdateVisuals(data);
        }
    }
}