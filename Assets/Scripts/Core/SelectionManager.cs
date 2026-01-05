using UnityEngine;
using System;
using System.Collections.Generic;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance;

    // --- PURE SYSTEM STATE ---
    // Mapping: Slot ID -> Drone ID
    private Dictionary<int, string> slotSelections = new Dictionary<int, string>();

    // Event: "Slot X changed its drone to Y"
    public event Action<int, string> OnSlotSelectionChanged;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Initialize Slot 0 as empty (We can add more later)
        slotSelections[0] = null;
    }

    // The ONLY way to select a drone now. No ambiguity.
    public void SetDroneAtSlot(int slotId, string droneId)
    {
        // 1. Check if actually changing
        if (slotSelections.ContainsKey(slotId) && slotSelections[slotId] == droneId)
            return;

        // 2. Update State
        slotSelections[slotId] = droneId;
        Debug.Log($"🎯 System State Update: Slot {slotId} = {droneId}");

        // 3. Notify Listeners (Dashboards)
        OnSlotSelectionChanged?.Invoke(slotId, droneId);
    }

    public string GetDroneAtSlot(int slotId)
    {
        return slotSelections.ContainsKey(slotId) ? slotSelections[slotId] : null;
    }

    // Reverse lookup: Drone ID -> Slot ID
    // Returns -1 if the drone is not currently assigned to any slot
    public int GetSlotForDrone(string droneId)
    {
        foreach (var kvp in slotSelections)
        {
            if (kvp.Value == droneId) return kvp.Key;
        }
        return -1; // Not assigned
    }
}