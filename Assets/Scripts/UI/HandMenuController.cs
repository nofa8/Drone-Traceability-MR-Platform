using UnityEngine;

public class HandMenuController : MonoBehaviour
{
    [Header("References")]
    public Transform headCamera;
    public GameObject menuContent;

    [Header("Settings")]
    [Tooltip("How directly you must look to OPEN it (Higher = Harder)")]
    public float openThreshold = 0.80f; 
    
    [Tooltip("How far you can look away before it CLOSES (Lower = Easier to keep open)")]
    public float closeThreshold = 0.55f; 

    void Start()
    {
        if (headCamera == null) headCamera = Camera.main.transform;
    }

    void Update()
    {
        if (headCamera == null || menuContent == null) return;

        // 1. Get directions
        Vector3 dirToHand = (transform.position - headCamera.position).normalized;
        // Check if palm is facing head (Generic check)
        float palmFacingDot = Vector3.Dot(transform.forward, -dirToHand);
        // Check if head is looking at hand
        float lookDot = Vector3.Dot(headCamera.forward, dirToHand);

        bool isLookingAtHand = lookDot > openThreshold;
        bool isLookingNearHand = lookDot > closeThreshold;
        bool isPalmFacing = palmFacingDot > 0.4f; // Generous palm check

        // 2. The "Sticky" Logic
        if (menuContent.activeSelf)
        {
            // If it's ALREADY open, keep it open as long as we are vaguely looking near it
            // OR if the user is interacting with it (optional)
            if (!isLookingNearHand || !isPalmFacing)
            {
                menuContent.SetActive(false);
            }
        }
        else
        {
            // If it's CLOSED, require a direct look to open it
            if (isLookingAtHand && isPalmFacing)
            {
                menuContent.SetActive(true);
            }
        }
    }
}