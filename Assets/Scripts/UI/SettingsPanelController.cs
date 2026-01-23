using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// VR-friendly settings panel for configuring server IP at runtime.
/// Simplified: Single IP input field.
/// </summary>
public class SettingsPanelController : MonoBehaviour
{
    [Header("Input Fields")]
    public TMP_InputField serverIPInput;

    [Header("Buttons")]
    public Button saveButton;
    public Button cancelButton;

    [Header("Feedback")]
    public TextMeshProUGUI statusText;
    public Color errorColor = Color.red;
    public Color successColor = Color.green;

    private string originalServerIP;

    void OnEnable()
    {
        LoadCurrentConfig();
        
        if (saveButton) saveButton.onClick.AddListener(OnSaveClicked);
        if (cancelButton) cancelButton.onClick.AddListener(OnCancelClicked);
    }

    void OnDisable()
    {
        if (saveButton) saveButton.onClick.RemoveListener(OnSaveClicked);
        if (cancelButton) cancelButton.onClick.RemoveListener(OnCancelClicked);
    }

    void LoadCurrentConfig()
    {
        var config = NetworkConfig.Instance;
        
        originalServerIP = config.serverIP;

        if (serverIPInput) serverIPInput.text = config.serverIP;

        SetStatus("", Color.white);
    }

    void OnSaveClicked()
    {
        var config = NetworkConfig.Instance;

        string ip = serverIPInput?.text?.Trim() ?? "";
        
        if (!config.ValidateIP(ip))
        {
            SetStatus("Invalid IP address", errorColor);
            return;
        }

        config.serverIP = ip;
        config.ApplyAndReconnect();

        SetStatus("✓ Saved! Reconnecting...", successColor);
        
        Invoke(nameof(ClosePanel), 1.5f);
    }

    void OnCancelClicked()
    {
        var config = NetworkConfig.Instance;
        config.serverIP = originalServerIP;

        ClosePanel();
    }

    void ClosePanel()
    {
        gameObject.SetActive(false);
    }

    void SetStatus(string message, Color color)
    {
        if (statusText)
        {
            statusText.text = message;
            statusText.color = color;
        }
    }
}
