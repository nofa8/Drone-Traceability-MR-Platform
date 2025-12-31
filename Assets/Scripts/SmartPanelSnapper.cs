using UnityEngine;
using Oculus.Interaction;

public class SmartPanelSnapper : MonoBehaviour
{
    [Header("Snapping Settings")]
    public float detectionRange = 0.2f; // 20cm range
    public float wallOffset = 0.02f;    // Float 2cm off the wall
    public float snapSpeed = 10f;

    [Header("References")]
    public Grabbable grabbable; // Drag your Grabbable component here
    private Rigidbody rb;

    // State
    private bool isSnapping = false;
    private Vector3 targetPos;
    private Quaternion targetRot;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        
        // Subscribe to the Meta Interaction SDK grab events
        if (grabbable != null)
        {
            grabbable.WhenPointerEventRaised += HandlePointerEvent;
        }
    }

    private void HandlePointerEvent(PointerEvent evt)
    {
        // When the user RELEASES the panel (Select = Grab, Unselect = Release)
        if (evt.Type == PointerEventType.Unselect)
        {
            TrySnap();
        }
        // When user GRABS it again, stop snapping
        else if (evt.Type == PointerEventType.Select)
        {
            isSnapping = false;
            rb.isKinematic = false; // Let physics take over (or Grabbable logic)
        }
    }

    void TrySnap()
    {
        // 1. Check for WALLS (Raycast backwards)
        // We use -transform.forward because the UI faces the user
        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit wallHit, detectionRange))
        {
            StartSnap(wallHit.point + (wallHit.normal * wallOffset), Quaternion.LookRotation(wallHit.normal));
            return;
        }

        // 2. Check for TABLES (Raycast down)
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit tableHit, detectionRange))
        {
            // Position: On the table
            Vector3 finalPos = tableHit.point + (Vector3.up * 0.15f); // 15cm up
            
            // Rotation: Tilt back 15 degrees like a picture frame
            // We keep the current Y rotation so it faces the way you placed it
            Quaternion faceForward = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            Quaternion tiltBack = Quaternion.Euler(-15, 0, 0); 
            
            StartSnap(finalPos, faceForward * tiltBack);
        }
    }

    void StartSnap(Vector3 pos, Quaternion rot)
    {
        isSnapping = true;
        rb.isKinematic = true; // Freeze physics so it stays put
        targetPos = pos;
        targetRot = rot;
    }

    void Update()
    {
        if (isSnapping)
        {
            // Smoothly move to the snap target
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * snapSpeed);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * snapSpeed);
        }
    }
}