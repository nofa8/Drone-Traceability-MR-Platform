using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DroneCommandButtons : MonoBehaviour
{
    [Header("Flight Control Buttons")]
    public Button btnArm;      
    public Button btnTakeoff;
    public Button btnLand;
    public Button btnGoHome;
    public Button btnStop;     

    [Header("Visual Feedback")]
    public TextMeshProUGUI armText; 

    // State
    private string currentDroneId;
    private bool isArmed = false;
    private bool isFlying = false;

    void Start()
    {
        // 1. Setup Selection (The "Pull" Trigger)
        if (SelectionManager.Instance != null)
        {
            // Listen for BOTH slot assignment and slot switching
            SelectionManager.Instance.OnSlotSelectionChanged += OnSlotAssignmentChanged;
            SelectionManager.Instance.OnActiveSlotChanged += OnActiveSlotChanged;
            
            // Initial Refresh
            RefreshTarget();
        }

        // 2. Listen to Repository (The "Push" Update)
        if (DroneStateRepository.Instance != null)
        {
            DroneStateRepository.Instance.OnDroneStateUpdated += HandleStateUpdate;
        }

        // 3. Setup Button Listeners
        if (btnArm) btnArm.onClick.AddListener(ToggleArm);
        if (btnTakeoff) btnTakeoff.onClick.AddListener(() => SendFlightCmd("takeoff"));
        if (btnLand) btnLand.onClick.AddListener(() => SendFlightCmd("land"));
        if (btnGoHome) btnGoHome.onClick.AddListener(() => SendFlightCmd("startGoHome"));
        if (btnStop) btnStop.onClick.AddListener(() => SendFlightCmd("stopMission")); 
    }

    void OnDestroy()
    {
        if (SelectionManager.Instance != null) {
            SelectionManager.Instance.OnSlotSelectionChanged -= OnSlotAssignmentChanged;
            SelectionManager.Instance.OnActiveSlotChanged -= OnActiveSlotChanged;
        }
        if (DroneStateRepository.Instance != null)
            DroneStateRepository.Instance.OnDroneStateUpdated -= HandleStateUpdate;
    }

    // --- NAVIGATION LOGIC ---

    void OnActiveSlotChanged(int newSlotId)
    {
        RefreshTarget();
    }

    void OnSlotAssignmentChanged(int slotId, string newDroneId)
    {
        // Only refresh if the change happened in the slot we are currently viewing
        if (SelectionManager.Instance != null && slotId == SelectionManager.Instance.ActiveSlotId)
        {
            RefreshTarget();
        }
    }

    void RefreshTarget()
    {
        if (SelectionManager.Instance == null) return;
        
        // Update ID based on current active slot
        currentDroneId = SelectionManager.Instance.GetDroneAtSlot(SelectionManager.Instance.ActiveSlotId);

        if (string.IsNullOrEmpty(currentDroneId))
        {
            // Reset to "No Drone" state (Disable All)
            DisableAllButtons();
            return;
        }

        // 🔥 Fetch state IMMEDIATELY (Fixes race condition)
        if (DroneStateRepository.Instance != null)
        {
            DroneState savedState = DroneStateRepository.Instance.GetState(currentDroneId);
            UpdateButtons(savedState);
        }
    }

    // --- DATA HANDLING ---

    void HandleStateUpdate(string droneId, DroneState state)
    {
        // Only update buttons if the event matches our selected drone
        if (droneId == currentDroneId) UpdateButtons(state);
    }

    void UpdateButtons(DroneState state)
    {
        // 🔒 SAFETY CHECK:
        // We can only Command the drone if:
        // 1. It exists (state != null)
        // 2. It is connected (isConnected)
        // 3. The data is FRESH (!IsStale). We don't want to command a ghost!
        bool isSafe = state != null && state.isConnected && !state.IsStale;
        
        // Extract State
        isArmed = state != null && state.data.motorsOn;
        isFlying = state != null && state.data.isFlying;

        // 1. ARM BUTTON
        if (btnArm)
        {
            // Can only arm if connected, safe, and NOT flying
            btnArm.interactable = isSafe && !isFlying;
            UpdateArmVisuals(isSafe);
        }

        // 2. TAKEOFF
        if (btnTakeoff)
        {
            // Can take off if safe and not already flying
            btnTakeoff.interactable = isSafe && !isFlying;
        }

        // 3. LAND / RTL
        // Can only land if we are actually flying
        if (btnLand) btnLand.interactable = isSafe && isFlying;
        if (btnGoHome) btnGoHome.interactable = isSafe && isFlying;

        // 4. STOP 
        // Emergency Stop is usually allowed as long as we have *any* connection
        if (btnStop) btnStop.interactable = state != null && state.isConnected; 
    }

    void DisableAllButtons()
    {
        isArmed = false;
        isFlying = false;

        if (btnArm) btnArm.interactable = false;
        if (btnTakeoff) btnTakeoff.interactable = false;
        if (btnLand) btnLand.interactable = false;
        if (btnGoHome) btnGoHome.interactable = false;
        if (btnStop) btnStop.interactable = false;

        UpdateArmVisuals(false);
    }

    void UpdateArmVisuals(bool canInteract)
    {
        if (armText)
        {
            if (!canInteract)
            {
                armText.text = "ARM";
                armText.color = Color.gray; // Disabled look
                return;
            }

            if (isFlying)
            {
                armText.text = "ARMED";
                armText.color = Color.gray; // Locked while flying
            }
            else
            {
                armText.text = isArmed ? "DISARM" : "ARM";
                armText.color = isArmed ? Color.red : Color.green;
            }
        }
    }

    // --- ACTIONS ---

    void ToggleArm()
    {
        if (string.IsNullOrEmpty(currentDroneId)) return;
        
        if (isFlying) 
        {
            Debug.LogWarning("⚠️ Safety Block: Cannot Disarm while flying!");
            return;
        }

        bool targetState = !isArmed;
        // Use newer Unity search, or fallback
        var client = FindObjectOfType<DroneNetworkClient>();
        if (client) client.SendUtilityCommand(currentDroneId, "motors", targetState);
    }

    void SendFlightCmd(string cmd)
    {
        if (string.IsNullOrEmpty(currentDroneId)) return;
        var client = FindObjectOfType<DroneNetworkClient>();
        if (client) client.SendFlightCommand(currentDroneId, cmd);
    }
}