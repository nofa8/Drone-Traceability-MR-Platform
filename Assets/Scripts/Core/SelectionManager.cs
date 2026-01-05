using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance;

    [Header("Slot Configuration")]
    public int maxSlots = 4;

    // --- STATE ---
    // Mapping: Slot ID -> Drone ID
    private Dictionary<int, string> slotSelections = new Dictionary<int, string>();
    
    // NEW: Explicit Order List (Fixes non-deterministic dictionary behavior)
    private List<int> slotOrder = new List<int>();

    // The "Focus"
    public int ActiveSlotId { get; private set; } = 0;

    // --- EVENTS ---
    public event Action<int, string> OnSlotSelectionChanged; 
    public event Action<int> OnActiveSlotChanged;            
    public event Action<int> OnSlotCreated;                  
    public event Action<int> OnSlotRemoved;                  

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Always start with Slot 0
        CreateSlot(true); 
    }

    // --- LIFECYCLE API ---

    // Updated: Added autoFocus parameter for future safety
    public bool CreateSlot(bool autoFocus = true)
    {
        if (slotSelections.Count >= maxSlots)
        {
            Debug.LogWarning("⚠️ Max slots reached!");
            return false;
        }

        // Find next ID
        int newSlotId = 0;
        while (slotSelections.ContainsKey(newSlotId)) newSlotId++;

        slotSelections.Add(newSlotId, null);
        slotOrder.Add(newSlotId); // Track order

        Debug.Log($"➕ Slot {newSlotId} created");
        OnSlotCreated?.Invoke(newSlotId);
        
        if (autoFocus)
            SetActiveSlot(newSlotId);
            
        return true;
    }

    public void RemoveSlot(int slotId)
    {
        if (!slotSelections.ContainsKey(slotId)) return;
        
        // Don't remove if it's the only one left (Fallback to first created)
        if (slotSelections.Count <= 1 && slotOrder.Count > 0 && slotId == slotOrder[0]) return;

        // 🔥 FIX 1: Cleanup drone assignment BEFORE removing slot
        // This ensures Map/Trails turn "White" (Unassigned) instead of staying Cyan forever
        string assignedDrone = slotSelections[slotId];
        if (!string.IsNullOrEmpty(assignedDrone))
        {
             OnSlotSelectionChanged?.Invoke(slotId, null);
        }

        slotSelections.Remove(slotId);
        slotOrder.Remove(slotId); // Keep order clean

        Debug.Log($"➖ Slot {slotId} removed");
        OnSlotRemoved?.Invoke(slotId);

        // 🔥 FIX 4: Deterministic Fallback
        // If we deleted the active slot, reset focus to the first available slot
        if (ActiveSlotId == slotId)
        {
            if (slotOrder.Count > 0)
                SetActiveSlot(slotOrder[0]);
        }
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

    public void AssignDroneToActiveSlot(string droneId)
    {
        SetDroneAtSlot(ActiveSlotId, droneId);
    }

    // NEW: Explicit Clear API (Cleaner than passing null)
    public void ClearSlot(int slotId)
    {
         if (!slotSelections.ContainsKey(slotId)) return;
         SetDroneAtSlot(slotId, null);
    }

    public void SetDroneAtSlot(int slotId, string droneId)
    {
        if (!slotSelections.ContainsKey(slotId)) return;
        if (slotSelections[slotId] == droneId) return;

        // 🔥 FIX 2: Enforce Uniqueness (One Drone = One Slot)
        // If we are trying to assign a REAL drone (not clearing)...
        if (!string.IsNullOrEmpty(droneId))
        {
            int existingSlot = GetSlotForDrone(droneId);
            if (existingSlot != -1 && existingSlot != slotId)
            {
                Debug.LogWarning($"⚠️ Drone {droneId} is already in Slot {existingSlot}. Assignment blocked.");
                // Option: You could "steal" it here by calling ClearSlot(existingSlot) first.
                // For now, blocking is safer.
                return; 
            }
        }

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
        return new List<int>(slotOrder); // Return a safe copy
    }
}