using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HandMenuController : MonoBehaviour
{
    [Header("References")]
    public Transform headCamera;
    public GameObject menuContent;
    public TextMeshProUGUI activeDroneText; 
    
    [Header("Buttons")]
    public Button dashboardBtn; 

    [Header("Slot Controls")]
    public Button addSlotBtn;
    public Button slot0Btn; 
    public Button slot1Btn;

    [Header("Context")]
    public int contextSlotId = 0; // The slot this menu represents

    [Header("Settings")]
    public float openThreshold = 0.80f; 
    public float closeThreshold = 0.55f; 

    void Start()
    {
        if (headCamera == null && Camera.main != null) headCamera = Camera.main.transform;

        // Subscribe to state changes
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.OnSlotSelectionChanged += HandleSlotChanged;

        // Wire up the Dashboard Button
        if(dashboardBtn) 
        {
            dashboardBtn.onClick.AddListener(() => {
                // 1. Open the Panel
                PanelManager.Instance.TogglePanel("Dashboard");
                
                // 2. Set Intent: Set the System Focus to this menu's slot
                if (SelectionManager.Instance != null)
                {
                    // FIX: Use SetActiveSlot instead of the removed targetSlotId
                    SelectionManager.Instance.SetActiveSlot(contextSlotId);
                }
            }); // FIX: Removed the extra }); here
        }
        
        // Wire up Slice Controls (For testing)
        if (addSlotBtn) 
            addSlotBtn.onClick.AddListener(() => SelectionManager.Instance.CreateSlot());

        if (slot0Btn) 
            slot0Btn.onClick.AddListener(() => SelectionManager.Instance.SetActiveSlot(0));

        if (slot1Btn) 
            slot1Btn.onClick.AddListener(() => SelectionManager.Instance.SetActiveSlot(1));
    }

    void OnDestroy()
    {
        if (SelectionManager.Instance != null)
            SelectionManager.Instance.OnSlotSelectionChanged -= HandleSlotChanged;
    }

    void HandleSlotChanged(int slotId, string droneId)
    {
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