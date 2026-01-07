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
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnSlotSelectionChanged += HandleSelection;
            // Initialize
            HandleSelection(SelectionManager.Instance.ActiveSlotId, 
                            SelectionManager.Instance.GetDroneAtSlot(SelectionManager.Instance.ActiveSlotId));
        }

        DroneNetworkClient.OnGlobalTelemetry += HandleTelemetry;

        if (btnArm) btnArm.onClick.AddListener(ToggleArm);
        if (btnTakeoff) btnTakeoff.onClick.AddListener(() => SendFlightCmd("takeoff"));
        if (btnLand) btnLand.onClick.AddListener(() => SendFlightCmd("land"));
        if (btnGoHome) btnGoHome.onClick.AddListener(() => SendFlightCmd("startGoHome"));
        if (btnStop) btnStop.onClick.AddListener(() => SendFlightCmd("stopMission")); 
    }

    void HandleSelection(int slotId, string droneId)
    {
        // Only update if this is the active slot
        if (SelectionManager.Instance != null && slotId != SelectionManager.Instance.ActiveSlotId) return;

        currentDroneId = droneId;

        // 🔥 FIX: Reset state immediately when switching drones/slots
        // This prevents the "Flying" status of Drone A from sticking to Empty Slot B.
        if (string.IsNullOrEmpty(currentDroneId))
        {
            isArmed = false;
            isFlying = false;
        }

        RefreshButtons(); 
    }

    void HandleTelemetry(DroneTelemetryData data)
    {
        if (data.droneId == currentDroneId)
        {
            bool newArmState = data.motorsOn;
            bool newFlyState = data.isFlying;

            if (newArmState != isArmed || newFlyState != isFlying)
            {
                isArmed = newArmState;
                isFlying = newFlyState;
                RefreshButtons();
            }
        }
    }

    void RefreshButtons()
    {
        // "hasDrone" determines if buttons are physically clickable
        bool hasDrone = !string.IsNullOrEmpty(currentDroneId);

        // 1. ARM BUTTON
        if(btnArm) 
        {
            btnArm.interactable = hasDrone && !isFlying;
            UpdateArmVisuals(hasDrone); // Pass 'hasDrone' to fix colors
        }

        // 2. TAKEOFF
        if(btnTakeoff) 
        {
            btnTakeoff.interactable = hasDrone && !isFlying;
        }

        // 3. LAND / RTL
        if(btnLand) btnLand.interactable = hasDrone && isFlying;
        if(btnGoHome) btnGoHome.interactable = hasDrone && isFlying;

        // 4. STOP (Emergency always available if drone exists)
        if(btnStop) btnStop.interactable = hasDrone;
    }

    void UpdateArmVisuals(bool hasDrone)
    {
        if (armText)
        {
            // 🔥 FIX: Explicitly handle "No Drone" look
            if (!hasDrone)
            {
                armText.text = "ARM";
                armText.color = Color.gray; // Make text look disabled
                return;
            }

            // Normal logic
            if (isFlying)
            {
                armText.text = "ARMED";
                armText.color = Color.gray; 
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
        // Use FindFirstObjectByType (newer Unity versions)
        var client = Object.FindFirstObjectByType<DroneNetworkClient>();
        if (client) client.SendUtilityCommand(currentDroneId, "motors", targetState);
    }

    void SendFlightCmd(string cmd)
    {
        if (string.IsNullOrEmpty(currentDroneId)) return;
        var client = Object.FindFirstObjectByType<DroneNetworkClient>();
        if (client) client.SendFlightCommand(currentDroneId, cmd);
    }

    void OnDestroy()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.OnSlotSelectionChanged -= HandleSelection;
        DroneNetworkClient.OnGlobalTelemetry -= HandleTelemetry;
    }
}