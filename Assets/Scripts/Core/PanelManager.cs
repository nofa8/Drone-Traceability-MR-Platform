using UnityEngine;
using System.Collections.Generic;

public class PanelManager : MonoBehaviour
{
    public static PanelManager Instance;

    [Header("Panels")]
    public GameObject dashboardPanel; // Assign your Txt_Speed parent here
    public GameObject mapPanel;       // Placeholder for Phase 2
    public GameObject povPanel;       // Placeholder for Phase 3
    
    // Dictionary to manage panels by string name
    private Dictionary<string, GameObject> panels = new Dictionary<string, GameObject>();

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        else Instance = this;

        // Register panels if they are assigned
        if(dashboardPanel) panels.Add("Dashboard", dashboardPanel);
        if(mapPanel) panels.Add("Map", mapPanel);
        if(povPanel) panels.Add("POV", povPanel);
        
        // Start clean
        CloseAll();
    }

    public void TogglePanel(string panelName)
    {
        if (panels.ContainsKey(panelName))
        {
            bool isActive = panels[panelName].activeSelf;
            // Close others if you want only one open at a time:
            // CloseAll(); 
            
            panels[panelName].SetActive(!isActive);
            
            if (!isActive) SnapToUserView(panels[panelName]);
        }
        else
        {
            Debug.LogWarning($"Panel {panelName} not found in PanelManager!");
        }
    }

    public void CloseAll()
    {
        foreach(var panel in panels.Values) panel.SetActive(false);
    }

    // Brings the panel 50cm in front of the user's face
    private void SnapToUserView(GameObject panel)
    {
        // Using your specific Camera "CenterEyeAnchor"
        Transform cameraTransform = Camera.main ? Camera.main.transform : GameObject.Find("CenterEyeAnchor").transform;
        
        if (cameraTransform != null)
        {
            panel.transform.position = cameraTransform.position + (cameraTransform.forward * 0.5f);
            
            // Billboard effect: Face the user but keep upright (Y-axis only)
            Vector3 lookPos = new Vector3(cameraTransform.position.x, panel.transform.position.y, cameraTransform.position.z);
            panel.transform.LookAt(2 * panel.transform.position - lookPos);
        }
    }
}