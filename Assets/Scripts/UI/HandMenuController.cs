using UnityEngine;
using UnityEngine.UI;
using TMPro; // Needed for text updates

public class HandMenuController : MonoBehaviour
{
    [Header("References")]
    public Transform headCamera;
    public GameObject menuContent;
    public TextMeshProUGUI activeDroneText; // Optional: To show "Monitoring: RD001"
    
    [Header("Buttons")]
    public Button dashboardBtn; // ✅ Added missing field
    // public Button mapBtn;

    [Header("Context")]
    public int contextSlotId = 0; // Which slot does this menu control?

    [Header("Settings")]
    public float openThreshold = 0.80f; 
    public float closeThreshold = 0.55f; 

    void Start()
    {
        if (headCamera == null && Camera.main != null) headCamera = Camera.main.transform;

        // Subscribe to state changes
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.OnSlotSelectionChanged += HandleSlotChanged;

        // Wire up the button
        if(dashboardBtn) 
        {
            dashboardBtn.onClick.AddListener(() => {
                // 1. Open the Panel
                PanelManager.Instance.TogglePanel("Dashboard");
                
                // 2. Set Intent: We are looking at Slot 0
                if (FleetUIManager.Instance != null)
                {
                    FleetUIManager.Instance.targetSlotId = contextSlotId;
                }
            });
        }
    }

    void OnDestroy()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.OnSlotSelectionChanged -= HandleSlotChanged;
    }

    // Update the menu text when the drone changes
    void HandleSlotChanged(int slotId, string droneId)
    {
        // Only update if this event is for OUR slot
        if (slotId != contextSlotId) return;

        if (activeDroneText)
        {
            activeDroneText.text = string.IsNullOrEmpty(droneId)
                ? "No Drone Selected"
                : $"Monitoring: {droneId}";
        }
    }

    void Update()
    {
        if (headCamera == null || menuContent == null) return;

        Vector3 dirToHand = (transform.position - headCamera.position).normalized;
        float palmFacingDot = Vector3.Dot(transform.forward, -dirToHand);
        float lookDot = Vector3.Dot(headCamera.forward, dirToHand);

        bool isLookingAtHand = lookDot > openThreshold;
        bool isLookingNearHand = lookDot > closeThreshold;
        bool isPalmFacing = palmFacingDot > 0.4f; 

        if (menuContent.activeSelf)
        {
            if (!isLookingNearHand || !isPalmFacing) menuContent.SetActive(false);
        }
        else
        {
            if (isLookingAtHand && isPalmFacing) menuContent.SetActive(true);
        }
    }
}