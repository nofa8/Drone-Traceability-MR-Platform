using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CommandPanelController : MonoBehaviour
{
    [Header("Target Info")]
    public TextMeshProUGUI targetText;

    [Header("Action Buttons")]
    public Button btnArm;
    public Button btnLand;
    public Button btnRTL;

    private string currentDroneId;

    void Start()
    {
        // Subscribe to selection changes
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnSlotSelectionChanged += HandleSelection;
            // Init
            HandleSelection(SelectionManager.Instance.ActiveSlotId, 
                            SelectionManager.Instance.GetDroneAtSlot(SelectionManager.Instance.ActiveSlotId));
        }

        // Wire up buttons
        if(btnArm) btnArm.onClick.AddListener(() => SendCmd("ARM"));
        if(btnLand) btnLand.onClick.AddListener(() => SendCmd("LAND"));
        if(btnRTL) btnRTL.onClick.AddListener(() => SendCmd("RTL"));
    }

    void HandleSelection(int slotId, string droneId)
    {
        // Only care if it's the active slot
        if (SelectionManager.Instance != null && slotId != SelectionManager.Instance.ActiveSlotId) return;

        currentDroneId = droneId;

        bool hasDrone = !string.IsNullOrEmpty(droneId);

        // Update Text
        if (targetText) targetText.text = hasDrone ? $"COMMAND: {droneId}" : "NO TARGET";

        // Enable/Disable Buttons based on valid target
        if(btnArm) btnArm.interactable = hasDrone;
        if(btnLand) btnLand.interactable = hasDrone;
        if(btnRTL) btnRTL.interactable = hasDrone;
    }

    void SendCmd(string cmd)
    {
        if (string.IsNullOrEmpty(currentDroneId)) return;

        // Visual Feedback (Optional Haptic/Sound here)
        Debug.Log($"🔘 User Pressed: {cmd}");

        // Send to Network
        DroneNetworkClient.SendCommandGlobal(cmd);
    }
    
    void OnDestroy()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.OnSlotSelectionChanged -= HandleSelection;
    }
}