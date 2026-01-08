using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class DroneCardUI : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI idText;
    public TextMeshProUGUI modelText;
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI batteryText; 
    public Image batteryFill;
    public Image statusIcon; 
    public Image selectionBorder; 

    [Header("Visual Settings")]
    public Color slot0Color = Color.cyan;
    public Color slot1Color = new Color(1f, 0.5f, 0f); // Orange
    public Color offlineColor = Color.gray;
    public Color staleColor = Color.yellow; // Shows when data is old/snapshot
    public Color defaultBorderColor = new Color(0, 0, 0, 0); 

    public string droneId { get; private set; }

    // --- LIFECYCLE ---
    void Start()
    {
        // 1. Listen for Selection Changes (Border Highlight)
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnSlotSelectionChanged += HandleAnySelectionChange;
            RefreshSelectionVisuals();
        }

        // 2. Listen to the Repository (Automatic Data Updates)
        if (DroneStateRepository.Instance != null)
        {
            DroneStateRepository.Instance.OnDroneStateUpdated += HandleStateUpdate;
            
            // Initial Fetch: Pull data immediately so the card isn't empty on spawn
            if (!string.IsNullOrEmpty(droneId))
            {
                var state = DroneStateRepository.Instance.GetState(droneId);
                UpdateVisuals(state);
            }
        }
    }

    void OnDestroy()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.OnSlotSelectionChanged -= HandleAnySelectionChange;

        if (DroneStateRepository.Instance != null)
            DroneStateRepository.Instance.OnDroneStateUpdated -= HandleStateUpdate;
    }

    // --- SETUP & INTERACTION ---
    public void Setup(string id)
    {
        this.droneId = id;
        if (idText) idText.text = id;
        
        Button btn = GetComponent<Button>();
        if (btn)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnClick);
        }
    }

    void OnClick()
    {
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.AssignDroneToActiveSlot(this.droneId);
            if (FleetUIManager.Instance) FleetUIManager.Instance.ShowDroneDetail(); 
        }
    }

    // --- REPOSITORY EVENT HANDLER ---
    private void HandleStateUpdate(string updatedId, DroneState state)
    {
        // Only update if this event is about ME
        if (updatedId == this.droneId)
        {
            UpdateVisuals(state);
        }
    }

    // --- COMPATIBILITY ADAPTERS ---
    // These ensure FleetUIManager doesn't break, but we defer logic to the Repository
    
    public void UpdateFromSnapshot(DroneSnapshotModel snap)
    {
        // We only set static text here. We let the Repository event trigger the full visual update.
        if (modelText) modelText.text = snap.model;
    }

    public void UpdateFromLive(DroneTelemetryData data)
    {
        // No-op: The Repository event will handle this automatically.
    }

    // --- CORE VISUALIZATION LOGIC ---

    private void UpdateVisuals(DroneState state)
    {
        if (state == null) return;
        DroneTelemetryData data = state.data;

        // 1. Model Text
        if (modelText && !string.IsNullOrEmpty(data.model)) modelText.text = data.model;

        // 2. Battery Visuals
        float batVal = (float)data.batteryLevel;
        
        if (batteryText) batteryText.text = $"{Mathf.RoundToInt(batVal)}%";

        if (batteryFill)
        {
            batteryFill.fillAmount = batVal / 100f;
            if (batVal < 20) batteryFill.color = Color.red;
            else if (batVal < 50) batteryFill.color = Color.yellow;
            else batteryFill.color = Color.green;
        }

        // 3. Status Logic (Hierarchy of States)
        if (statusText && statusIcon)
        {
            // 🔥 THE FIX: Prioritize "Stale" check over "Flying"
            if (state.IsStale)
            {
                SetStatus("STALE", staleColor);
            }
            else if (!state.isConnected) 
            { 
                SetStatus("OFFLINE", offlineColor);
            }
            else if (data.isFlying) 
            { 
                SetStatus("FLYING", Color.cyan);
            }
            else if (data.motorsOn)
            {
                SetStatus("ARMED", new Color(1f, 0.5f, 0f)); // Orange
            }
            else 
            { 
                SetStatus("READY", Color.green);
            }
        }
    }

    private void SetStatus(string text, Color color)
    {
        statusText.text = text;
        statusIcon.color = color;
    }

    // --- SELECTION HIGHLIGHT LOGIC ---
    private void HandleAnySelectionChange(int slotId, string selectedId)
    {
        RefreshSelectionVisuals();
    }

    private void RefreshSelectionVisuals()
    {
        if (!selectionBorder || SelectionManager.Instance == null) return;

        int mySlot = SelectionManager.Instance.GetSlotForDrone(this.droneId);

        if (mySlot == 0) selectionBorder.color = slot0Color;      
        else if (mySlot == 1) selectionBorder.color = slot1Color; 
        else selectionBorder.color = defaultBorderColor;          
    }
}