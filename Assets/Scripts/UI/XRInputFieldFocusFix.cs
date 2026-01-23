using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Fixes Meta Quest system keyboard not appearing on TMP_InputField.
/// Ensures proper focus/selection is triggered on pointer click.
/// Attach this to any TMP_InputField that needs keyboard input in VR.
/// </summary>
[RequireComponent(typeof(TMP_InputField))]
public class XRInputFieldFocusFix : MonoBehaviour, IPointerClickHandler
{
    private TMP_InputField input;

    void Awake()
    {
        input = GetComponent<TMP_InputField>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!input.interactable) return;

        // Force the EventSystem to recognize this as the selected object
        EventSystem.current.SetSelectedGameObject(gameObject);
        Debug.LogWarning("XRInputFieldFocusFix: SetSelectedGameObject");
        // Explicitly activate the input field (triggers OS keyboard on Quest)
        input.ActivateInputField();
    }
}
