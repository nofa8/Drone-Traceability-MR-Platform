using UnityEngine;
using System;

// The States of our Map
public enum MapMode
{
    Follow, // Map is locked to the drone
    Free    // User is panning manually
}

public class GeoMapContext : MonoBehaviour
{
    public static GeoMapContext Instance;

    [Header("Configuration")]
    [Tooltip("-1 = Follow Active Slot (Main Map). 0, 1, etc = Lock to specific slot.")]
    public int boundSlotId = -1; 

    [Header("Geographic State")]
    public double originLat;
    public double originLon;
    public float pixelsPerMeter = 1.0f;
    
    [Header("Logic State")]
    public MapMode currentMode = MapMode.Follow;
    public string followTargetId; // The Drone ID we want to follow

    // Events
    public event Action OnMapUpdated;           // Request Redraw
    public event Action<MapMode> OnModeChanged; // UI Update (Show/Hide Recenter Button)

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // 1. Listen for Telemetry (To move the map)
        DroneNetworkClient.OnGlobalTelemetry += HandleTelemetry;

        // 2. Listen for Slot Changes (To switch targets)
        if (SelectionManager.Instance != null)
        {
            if (boundSlotId == -1)
            {
                // Dynamic Mode: Follow whatever is Active
                SelectionManager.Instance.OnActiveSlotChanged += RefreshFollowTarget;
                RefreshFollowTarget(SelectionManager.Instance.ActiveSlotId);
            }
            else
            {
                // Locked Mode: Only listen to our specific slot
                RefreshFollowTarget(boundSlotId);
            }

            // Also listen for drone swaps (e.g., assigning a new drone to the slot)
            SelectionManager.Instance.OnSlotSelectionChanged += (slot, drone) => 
            {
                if (boundSlotId == -1 && slot == SelectionManager.Instance.ActiveSlotId) RefreshFollowTarget(slot);
                else if (slot == boundSlotId) RefreshFollowTarget(slot);
            };
        }
    }

    void OnDestroy()
    {
        DroneNetworkClient.OnGlobalTelemetry -= HandleTelemetry;
        if (SelectionManager.Instance != null && boundSlotId == -1)
        {
            SelectionManager.Instance.OnActiveSlotChanged -= RefreshFollowTarget;
        }
    }

    // --- LOGIC: TARGETING ---

    void RefreshFollowTarget(int slotId)
    {
        if (SelectionManager.Instance == null) return;

        // Get the drone ID for this slot
        string droneId = SelectionManager.Instance.GetDroneAtSlot(slotId);
        followTargetId = droneId;
        
        // If we found a valid target, auto-engage Follow Mode
        if (!string.IsNullOrEmpty(droneId))
        {
            SetFollowMode(); 
        }
    }

    // --- LOGIC: MOVEMENT ---

    void HandleTelemetry(DroneTelemetryData data)
    {
        // 🛑 The Core Logic: Only move if we are in Follow Mode AND data matches our target
        if (currentMode == MapMode.Follow && !string.IsNullOrEmpty(followTargetId))
        {
            if (data.droneId == followTargetId)
            {
                // Move center to drone
                SetCenter(data.latitude, data.longitude);
            }
        }
    }

    // --- PUBLIC API (State Transitions) ---

    public void SetFreeMode()
    {
        if (currentMode != MapMode.Free)
        {
            currentMode = MapMode.Free;
            OnModeChanged?.Invoke(MapMode.Free);
            // Debug.Log("🗺️ Map Mode: FREE PAN");
        }
    }

    public void SetFollowMode()
    {
        currentMode = MapMode.Follow;
        OnModeChanged?.Invoke(MapMode.Follow);
        // Debug.Log($"🗺️ Map Mode: FOLLOWING {followTargetId}");
        
        // Optional: Force instant snap if we have cached data
    }

    // --- MATH & STATE ---

    public void SetCenter(double lat, double lon)
    {
        originLat = lat;
        originLon = lon;
        OnMapUpdated?.Invoke();
    }

    public void SetZoom(float newScale)
    {
        pixelsPerMeter = newScale;
        OnMapUpdated?.Invoke();
    }

    public Vector2 GeoToScreenPosition(double lat, double lon)
    {
        Vector2 meters = GeoUtils.LatLonToMeters(lat, lon, originLat, originLon);
        return meters * pixelsPerMeter;
    }

    public Vector2 ScreenToGeoPosition(Vector2 screenDelta)
    {
         Vector2 meters = screenDelta / pixelsPerMeter;
         return GeoUtils.MetersToLatLon(meters, originLat, originLon);
    }
}