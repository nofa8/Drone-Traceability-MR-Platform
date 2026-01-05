using UnityEngine;
using UnityEngine.UI;

public class MapMarkerUI : MonoBehaviour
{
    private string myDroneId;
    private Button btn;

    public void Setup(string droneId)
    {
        this.myDroneId = droneId;
        
        // 1. Get or Add Button Component
        btn = GetComponent<Button>();
        if (btn == null) {
            Debug.LogWarning($"🗺️ Button Not Found for {droneId}");
            btn = gameObject.AddComponent<Button>();
        }

        ColorBlock cb = btn.colors;
        cb.highlightedColor = Color.red; // Visual feedback
        btn.colors = cb;
        
        // 2. Setup Click Listener
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnMarkerClicked);
    }

    void OnMarkerClicked()
    {
        Debug.LogWarning($"🗺️ Map Clicked: {myDroneId}");

        // 1. Get Intent (Which slot are we filling?)
        int targetSlot = FleetUIManager.Instance.targetSlotId;

        // 2. Update System State
        SelectionManager.Instance.SetDroneAtSlot(targetSlot, myDroneId);

        // 3. Optional: Navigate to Detail View
        FleetUIManager.Instance.ShowDroneDetail();
    }
}