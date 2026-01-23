using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HandMenuController : MonoBehaviour
{
    [Header("References")]
    public Transform headCamera;
    public GameObject menuContent;
    public TextMeshProUGUI activeDroneText;
    
    [Header("Main Buttons")]
    public Button dashboardBtn; // The "Smart" Button

    [Header("Slot Controls")]
    public Button addSlotBtn;
    // We use an array now for easy expansion (Slot 0, 1, 2, 3)
    public Button[] slotButtons; 

    [Header("Settings")]
    public float openThreshold = 0.80f; 
    public float closeThreshold = 0.55f;
    
    [Header("Settings Panel")]
    public Button settingsBtn;
    public GameObject settingsPanel; 

    void Start()
    {
        if (headCamera == null && Camera.main != null) headCamera = Camera.main.transform;

        // 1. Subscribe to state changes (to update text)
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.OnActiveSlotChanged += UpdateUI;
            SelectionManager.Instance.OnSlotSelectionChanged += (slot, drone) => UpdateUI(SelectionManager.Instance.ActiveSlotId);
            SelectionManager.Instance.OnSlotCreated += (slot) => UpdateSlotButtons();
        }

        // 2. Wire up "Add Slot"
        if (addSlotBtn)
             addSlotBtn.onClick.AddListener(() => SelectionManager.Instance.CreateSlot());

        // 3. Wire up Slot Buttons (0, 1, 2, 3)
        for (int i = 0; i < slotButtons.Length; i++)
        {
            int slotIndex = i; // Capture index for lambda
            if (slotButtons[i] != null)
            {
                slotButtons[i].onClick.AddListener(() => SelectionManager.Instance.SetActiveSlot(slotIndex));
            }
        }

        // 4. Wire up "Smart Dashboard" Button
        if(dashboardBtn) 
        {
            dashboardBtn.onClick.AddListener(OnDashboardClicked);
        }

        // Init UI
        UpdateSlotButtons();
        if (SelectionManager.Instance != null) UpdateUI(SelectionManager.Instance.ActiveSlotId);

        // 5. Wire up "Settings" Button
        if (settingsBtn && settingsPanel)
        {
            settingsPanel.SetActive(false); // Start hidden
            settingsBtn.onClick.AddListener(OnSettingsClicked);
        }
    }

    void OnDestroy()
    {
        if (SelectionManager.Instance == null) return;

        SelectionManager.Instance.OnActiveSlotChanged -= UpdateUI;
        SelectionManager.Instance.OnSlotCreated -= (slot) => UpdateSlotButtons();
    }

    // 🔥 THE SMART LOGIC
    void OnDashboardClicked()
    {
        if (SelectionManager.Instance == null) Debug.LogError("❌ Missing: SelectionManager (Check 'System' object)");
        if (PanelManager.Instance == null) Debug.LogError("❌ Missing: PanelManager (Do you have this script?)");
        if (FleetUIManager.Instance == null) Debug.LogError("❌ Missing: FleetUIManager (Is Dashboard disabled?)");

        PanelManager.Instance.TogglePanel("Dashboard");

        int activeSlot = SelectionManager.Instance.ActiveSlotId;
        string assignedDrone = SelectionManager.Instance.GetDroneAtSlot(activeSlot);

        if (string.IsNullOrEmpty(assignedDrone))
            FleetUIManager.Instance.ShowFleetView();
        else
            FleetUIManager.Instance.ShowDroneDetail();
    }

    void OnSettingsClicked()
    {
        if (settingsPanel)
            settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    void UpdateSlotButtons()
    {
        // Only show buttons for slots that actually exist
        if (SelectionManager.Instance == null) return;
        
        var allSlots = SelectionManager.Instance.GetAllSlots();
        for (int i = 0; i < slotButtons.Length; i++)
        {
            if (slotButtons[i] == null) continue;
            // Active if slot 'i' exists in the system
            slotButtons[i].gameObject.SetActive(allSlots.Contains(i));
        }
    }

    void UpdateUI(int activeSlotId)
    {
        string droneId = SelectionManager.Instance.GetDroneAtSlot(activeSlotId);
        
        if (activeDroneText)
        {
            activeDroneText.text = $"SLOT {activeSlotId}: {(string.IsNullOrEmpty(droneId) ? "EMPTY" : droneId)}";
        }
    }

    // ... (Keep existing Update() for palm detection) ...
    void Update()
    {
        if (headCamera == null || menuContent == null) return;

        Vector3 dirToHand = (transform.position - headCamera.position).normalized;
        float palmFacingDot = Vector3.Dot(transform.forward, -dirToHand);
        float lookDot = Vector3.Dot(headCamera.forward, dirToHand);

        bool isLookingNearHand = lookDot > closeThreshold;
        bool isPalmFacing = palmFacingDot > 0.4f; 

        if (menuContent.activeSelf)
        {
            if (!isLookingNearHand || !isPalmFacing) menuContent.SetActive(false);
            else SmoothLookAt();
        }
        else
        {
            if (isLookingNearHand && isPalmFacing && lookDot > openThreshold) menuContent.SetActive(true);
        }
    }

    void SmoothLookAt()
    {
        Quaternion targetRot = Quaternion.LookRotation(menuContent.transform.position - headCamera.position);
        menuContent.transform.rotation = Quaternion.Slerp(menuContent.transform.rotation, targetRot, Time.deltaTime * 10f);
    }
}