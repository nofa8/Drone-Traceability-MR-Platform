using UnityEngine;
using System;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance;

    // The State: Which drone is currently active?
    public string SelectedDroneId { get; private set; }
    
    // The Event: "Hey everyone, the selection changed!"
    public event Action<string> OnDroneSelected;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void SelectDrone(string droneId)
    {
        // Don't spam events if nothing changed
        if (SelectedDroneId == droneId) return;

        SelectedDroneId = droneId;
        Debug.Log($"🎯 Selection Changed: {droneId}");

        // Notify all listeners (Dashboard, Map, Hand Menu)
        OnDroneSelected?.Invoke(droneId);
    }

    public void Deselect()
    {
        SelectedDroneId = null;
        OnDroneSelected?.Invoke(null);
    }
}