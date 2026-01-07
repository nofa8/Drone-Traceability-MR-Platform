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
    private bool isFlying = false; // New State Tracker

    void Start()
    {
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnSlotSelectionChanged += HandleSelection;
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
        if (SelectionManager.Instance != null && slotId != SelectionManager.Instance.ActiveSlotId) return;
        currentDroneId = droneId;
        RefreshButtons(); // Re-evaluate locks when we switch drones
    }

    void HandleTelemetry(DroneTelemetryData data)
    {
        if (data.droneId == currentDroneId)
        {
            // Update States
            bool newArmState = data.motorsOn; // or data.areMotorsOn based on your model
            bool newFlyState = data.isFlying;

            if (newArmState != isArmed || newFlyState != isFlying)
            {
                isArmed = newArmState;
                isFlying = newFlyState;
                RefreshButtons(); // Refresh UI only on change
            }
        }
    }

    // --- SAFETY LOGIC ---

    void RefreshButtons()
    {
        bool hasDrone = !string.IsNullOrEmpty(currentDroneId);

        // 1. ARM BUTTON: Only available if ON GROUND
        // Prevents accidental mid-air disarm (Crash).
        if(btnArm) 
        {
            btnArm.interactable = hasDrone && !isFlying;
            UpdateArmVisuals();
        }

        // 2. TAKEOFF: Only available if ON GROUND
        if(btnTakeoff) 
        {
            // Optional: require isArmed == true before allowing takeoff?
            // For now, we just say "If not flying, you can try to takeoff".
            btnTakeoff.interactable = hasDrone && !isFlying;
        }

        // 3. LAND / RTL: Only available if IN AIR (or at least Armed)
        if(btnLand) btnLand.interactable = hasDrone && isFlying;
        if(btnGoHome) btnGoHome.interactable = hasDrone && isFlying;

        // 4. STOP: ALWAYS Available (Emergency)
        if(btnStop) btnStop.interactable = hasDrone;
    }

    void ToggleArm()
    {
        if (string.IsNullOrEmpty(currentDroneId)) return;
        
        // Safety Check: Double confirm we are not flying
        if (isFlying) 
        {
            Debug.LogWarning("⚠️ Safety Block: Cannot Disarm while flying!");
            return;
        }

        bool targetState = !isArmed;
        var client = FindFirstObjectByType<DroneNetworkClient>();
        if (client) client.SendUtilityCommand(currentDroneId, "motors", targetState);
    }

    void SendFlightCmd(string cmd)
    {
        if (string.IsNullOrEmpty(currentDroneId)) return;
        var client = FindFirstObjectByType<DroneNetworkClient>();
        if (client) client.SendFlightCommand(currentDroneId, cmd);
    }

    void UpdateArmVisuals()
    {
        if (armText)
        {
            // If flying, maybe gray out the text or change it to "ARMED (LOCKED)"
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

    void OnDestroy()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.OnSlotSelectionChanged -= HandleSelection;
        DroneNetworkClient.OnGlobalTelemetry -= HandleTelemetry;
    }
}