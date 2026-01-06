using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class MapRecenterButton : MonoBehaviour
{
    private Button btn;
    
    void Start()
    {
        btn = GetComponent<Button>();
        btn.onClick.AddListener(OnRecenterClicked);

        if (GeoMapContext.Instance != null)
        {
            GeoMapContext.Instance.OnModeChanged += HandleModeChange;
            // Initialize State
            HandleModeChange(GeoMapContext.Instance.currentMode);
        }
    }

    void OnDestroy()
    {
        if (GeoMapContext.Instance != null)
            GeoMapContext.Instance.OnModeChanged -= HandleModeChange;
    }

    void OnRecenterClicked()
    {
        // User wants to return to Follow Mode
        if (GeoMapContext.Instance != null)
            GeoMapContext.Instance.SetFollowMode();
    }

    void HandleModeChange(MapMode mode)
    {
        // Show button ONLY if we are in Free Mode
        bool show = (mode == MapMode.Free);
        gameObject.SetActive(show); 
    }
}