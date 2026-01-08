using UnityEngine;
using System.Collections.Generic;
using System;

public class DroneStateRepository : MonoBehaviour
{
    public static DroneStateRepository Instance;

    // The Master List
    private Dictionary<string, DroneState> _states = new Dictionary<string, DroneState>();

    // Events for UI to listen to
    public event Action<string, DroneState> OnDroneStateUpdated;

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;
    }

    // --- READ API ---
    public DroneState GetState(string droneId)
    {
        if (string.IsNullOrEmpty(droneId)) return null;

        // Ensure we always return a valid object, never null
        if (!_states.ContainsKey(droneId))
        {
            _states[droneId] = new DroneState(droneId);
        }
        return _states[droneId];
    }

    public List<DroneState> GetAllStates()
    {
        return new List<DroneState>(_states.Values);
    }

    // --- WRITE API (Live Telemetry - High Priority) ---
    public void UpdateFromTelemetry(DroneTelemetryData incomingData)
    {
        if (string.IsNullOrEmpty(incomingData.droneId)) return;

        DroneState state = GetState(incomingData.droneId);

        // Update current state (Existing)
        state.data = incomingData; 
        state.isConnected = true;
        state.lastHeartbeatTime = DateTime.UtcNow;

        // 🔥 NEW: Record to History
        // Optional: Limit size to prevent memory issues (e.g. last 1000 points)
        if (state.history.Count > 2000) state.history.RemoveAt(0); 
        state.history.Add(incomingData);

        OnDroneStateUpdated?.Invoke(state.droneId, state);
    }

    // --- WRITE API (HTTP Snapshot - Low Priority / Bootstrap) ---
    public void UpdateFromSnapshot(DroneSnapshotModel snapshot)
    {
        if (string.IsNullOrEmpty(snapshot.droneId)) return;

        DroneState state = GetState(snapshot.droneId);

        // 1. ALWAYS Update Static Data (Model, ID)
        // This is safe because it rarely changes.
        state.data.model = snapshot.model;

        // 2. SAFEGUARD (Rule 1): Do not overwrite live telemetry with old snapshots.
        // If we are currently connected via WebSocket (Live), we ignore the snapshot's position data.
        // We only use snapshot telemetry if we are 'Offline' or haven't heard from the drone recently.
        bool isLive = state.isConnected && (DateTime.UtcNow - state.lastHeartbeatTime).TotalSeconds < 5;
        
        if (isLive)
        {
            // Just notify that static data might have changed, but don't touch physics.
            OnDroneStateUpdated?.Invoke(state.droneId, state);
            return; 
        }

        // 3. Update Telemetry (Only if we are not live)
        if (snapshot.telemetry != null)
        {
            var t = snapshot.telemetry;

            // Map Snapshot fields to Internal Data fields
            state.data.latitude = t.latitude;
            state.data.longitude = t.longitude;
            state.data.altitude = t.altitude;
            
            state.data.velocityX = t.velocityX;
            state.data.velocityY = t.velocityY;
            state.data.velocityZ = t.velocityZ;
            state.data.heading = t.heading;

            state.data.batteryLevel = t.batteryLevel;
            state.data.satCount = t.satelliteCount;

            state.data.isFlying = t.isFlying;
            state.data.online = t.online;
            state.data.motorsOn = t.areMotorsOn;
            state.data.lightsOn = t.areLightsOn;

            // Update System State
            // We treat the snapshot timestamp as the "last known time"
            state.lastHeartbeatTime = DateTime.UtcNow; 
            
            // Note: We TRUST the snapshot's online status if we aren't live
            state.isConnected = t.online;
        }
        else
        {
            // Fallback: If snapshot has no telemetry, trust the wrapper's status
            state.data.online = snapshot.isConnected;
            state.isConnected = snapshot.isConnected;
        }

        // Notify Listeners (Map, Details, Cards will all update instantly)
        OnDroneStateUpdated?.Invoke(state.droneId, state);
    }

    public void MarkDisconnected(string droneId)
    {
        DroneState state = GetState(droneId);
        state.isConnected = false;
        OnDroneStateUpdated?.Invoke(droneId, state);
    }
}