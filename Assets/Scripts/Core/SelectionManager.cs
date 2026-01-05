using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq; // Useful for keys

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance;

    [Header("Slot Configuration")]
    public int maxSlots = 4;

    // --- STATE ---
    // Mapping: Slot ID -> Drone ID
    private Dictionary<int, string> slotSelections = new Dictionary<int, string>();
    
    // The "Focus": Which slot is currently receiving assignments?
    public int ActiveSlotId { get; private set; } = 0;

    // --- EVENTS ---
    public event Action<int, string> OnSlotSelectionChanged; // Slot X assigned to Drone Y
    public event Action<int> OnActiveSlotChanged;            // User switched focus to Slot X
    public event Action<int> OnSlotCreated;                  // New Dashboard needed
    public event Action<int> OnSlotRemoved;                  // Dashboard closed

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Always start with Slot 0
        CreateSlot(); 
        SetActiveSlot(0);
    }

    // --- LIFECYCLE API ---

    public bool CreateSlot()
    {
        if (slotSelections.Count >= maxSlots)
        {
            Debug.LogWarning("⚠️ Max slots reached!");
            return false;
        }

        // Find next available ID (simple auto-increment logic)
        int newSlotId = 0;
        while (slotSelections.ContainsKey(newSlotId)) newSlotId++;

        slotSelections.Add(newSlotId, null);
        Debug.Log($"➕ Slot {newSlotId} created");
        
        OnSlotCreated?.Invoke(newSlotId);
        
        // Auto-focus the new slot? Optional. Let's do it for convenience.
        SetActiveSlot(newSlotId);
        return true;
    }

    public void RemoveSlot(int slotId)
    {
        if (!slotSelections.ContainsKey(slotId)) return;
        if (slotSelections.Count <= 1 && slotId == 0) return; // Don't delete the last slot

        slotSelections.Remove(slotId);
        Debug.Log($"➖ Slot {slotId} removed");
        OnSlotRemoved?.Invoke(slotId);

        // If we deleted the active slot, reset focus to 0
        if (ActiveSlotId == slotId)
            SetActiveSlot(slotSelections.Keys.First());
    }

    // --- ACTIVE SLOT API ---

    public void SetActiveSlot(int slotId)
    {
        if (!slotSelections.ContainsKey(slotId)) return;

        if (ActiveSlotId != slotId)
        {
            ActiveSlotId = slotId;
            Debug.Log($"⭐ Active Slot switched to {slotId}");
            OnActiveSlotChanged?.Invoke(slotId);
        }
    }

    // --- ASSIGNMENT API ---

    // The primary way UI interacts now: "Assign this drone to whatever is active"
    public void AssignDroneToActiveSlot(string droneId)
    {
        SetDroneAtSlot(ActiveSlotId, droneId);
    }

    // Direct assignment (internal use or load/save)
    public void SetDroneAtSlot(int slotId, string droneId)
    {
        if (!slotSelections.ContainsKey(slotId)) return;
        if (slotSelections[slotId] == droneId) return;

        slotSelections[slotId] = droneId;
        Debug.Log($"🎯 System Update: Slot {slotId} = {droneId}");
        OnSlotSelectionChanged?.Invoke(slotId, droneId);
    }

    // --- QUERIES ---

    public string GetDroneAtSlot(int slotId)
    {
        return slotSelections.ContainsKey(slotId) ? slotSelections[slotId] : null;
    }

    public int GetSlotForDrone(string droneId)
    {
        foreach (var kvp in slotSelections)
        {
            if (kvp.Value == droneId) return kvp.Key;
        }
        return -1;
    }
    
    public List<int> GetAllSlots()
    {
        return new List<int>(slotSelections.Keys);
    }
}