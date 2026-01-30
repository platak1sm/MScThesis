using System.Collections;
using UnityEngine;

public class HybridPenController : MonoBehaviour
{
    public enum IndirectMappingType { VisualAngleGain, OneToOne }

    [Header("Core Systems")]
    public GazeProvider gazeProvider;
    public Transform virtualPenTip;
    public Camera eyeCamera; 

    [Header("Settings")]
    public float directGrabRadius = 0.05f; 
    public float moveSensitivity = 1.0f;   
    public float penGazeThreshold = 20.0f; 
    [Range(0.0f, 1.0f)]
    public float pressureThreshold = 0.15f; 

    [Header("Indirect Logic")]
    public IndirectMappingType mappingType = IndirectMappingType.VisualAngleGain;
    
    [Header("Colors")]
    public Color directColor = Color.yellow;
    public Color indirectColor = Color.cyan; 

    // --- STATE MACHINE ---
    private enum State 
    { 
        Idle,
        DirectAir,            
        DirectTableShadow,    
        IndirectAir,          
        IndirectTableShadow,  
        IndirectTableObject   
    }
    private State currentState = State.Idle;
    
    private Transform focusedTarget = null; 
    private Transform activeObject = null;  // The Floating Object
    
    // Inputs
    private bool isPressingTable = false;
    private bool isTriggerPressed = false;
    private bool isLookingAtPen = false;
    private bool wasLookingAtPen = false;
    
    // Movement Anchors
    private Vector3 startPenPos;
    private Vector3 startObjPos;
    private Vector3 grabOffset; 

    void Start()
    {
        if (gazeProvider == null) gazeProvider = FindFirstObjectByType<GazeProvider>();
        if (eyeCamera == null) eyeCamera = Camera.main;
    }

    void Update()
    {
        float pressure = OVRInput.Get(OVRInput.Axis1D.PrimaryStylusForce, OVRInput.Controller.RTouch);
        float trigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
        
        bool wasTrigger = isTriggerPressed;
        bool wasPressing = isPressingTable;
        
        isTriggerPressed = trigger > 0.5f;
        isPressingTable = pressure > pressureThreshold;

        // --- STATE HANDLING ---
        if (currentState != State.Idle)
        {
            switch (currentState)
            {
                case State.DirectAir:
                    HandleDirectAir();
                    if (!isTriggerPressed) EndDrag();
                    break;

                case State.DirectTableShadow:
                    HandleDirectTableShadow();
                    if (!isPressingTable) EndDrag();
                    break;

                case State.IndirectAir:
                    HandleIndirectAir();
                    if (!isTriggerPressed) EndDrag();
                    break;

                case State.IndirectTableShadow:
                    HandleIndirectTableShadow();
                    if (!isPressingTable) EndDrag();
                    break;

                case State.IndirectTableObject:
                    HandleIndirectTableObject();
                    if (!isPressingTable) EndDrag();
                    break;
            }
        }
        else
        {
            DeterminePotentialTarget();

            // PRIORITY 1: TABLE (Pressure)
            if (isPressingTable && !wasPressing) 
            {
                if (IsTouchingShadow()) StartDrag(State.DirectTableShadow);
                else if (IsGazingAtShadow()) StartDrag(State.IndirectTableShadow);
                else if (IsGazingAtObject()) StartDrag(State.IndirectTableObject);
            }

            // PRIORITY 2: AIR (Trigger)
            if (isTriggerPressed && !wasTrigger && !isPressingTable)
            {
                 if (IsTouchingObject()) StartDrag(State.DirectAir);
                 else if (IsGazingAtObject()) StartDrag(State.IndirectAir);
            }
        }

        UpdateOutlines();
    }

    // MOVEMENT LOGIC
    // (1) Direct Air: 3D Grab
    void HandleDirectAir()
    {
        if (activeObject == null) return;
        activeObject.position = virtualPenTip.position + grabOffset;
        activeObject.rotation = virtualPenTip.rotation;
    }

    // (2) Direct Table: Physical Shadow Push
    void HandleDirectTableShadow()
    {
        if (activeObject == null) return;
        
        // 1. Where is the shadow center now? (Pen + Offset)
        Vector3 newShadowPos = virtualPenTip.position + grabOffset;
        
        // 2. Move Object to that X/Z, but keep its original Y
        activeObject.position = new Vector3(newShadowPos.x, activeObject.position.y, newShadowPos.z);
    }

    // (3) Indirect Air: Remote 3D
    void HandleIndirectAir()
    {
        if (activeObject == null) return;
        Vector3 delta = GetScaledDelta();
        activeObject.position = startObjPos + delta;
    }

    // (4) Indirect Table (Look Shadow): Move X, Z
    void HandleIndirectTableShadow()
    {
        if (activeObject == null) return;
        
        // Use raw movement from start position
        Vector3 rawDelta = virtualPenTip.position - startPenPos;
        Vector3 scaledDelta = rawDelta * moveSensitivity; // Usually 1:1 on table feels best
        
        // Map Hand X/Z -> Object X/Z
        Vector3 flatDelta = new Vector3(scaledDelta.x, 0, scaledDelta.z);
        activeObject.position = startObjPos + flatDelta;
    }

    // (5) Indirect Table (Look Object): Axis Switching
    void HandleIndirectTableObject()
    {
        if (activeObject == null) return;
        
        CheckIfLookingAtPen();

        // RE-ANCHOR: Prevent jumping when switching modes
        if (isLookingAtPen != wasLookingAtPen)
        {
            ReAnchor();
            wasLookingAtPen = isLookingAtPen;
        }

        // Calculate delta from the NEW anchor point
        Vector3 currentHandPos = virtualPenTip.position;
        Vector3 handDelta = currentHandPos - startPenPos;

        // Apply Scaling (Gain or 1:1)
        float scale = moveSensitivity;
        if (mappingType == IndirectMappingType.VisualAngleGain) scale *= GetVisualGain();
        
        Vector3 scaledHandDelta = handDelta * scale;

        if (isLookingAtPen)
        {
            // MODE B: Looking at Pen (Z-AXIS MODE)
            // forward-backward: z axis, left-right: x axis
            
            // Hand Right (X) -> Object Right (X)
            // Hand Fwd (Z)   -> Object Depth (Z)
            
            // Note: On table, Hand Z is forward/back. Hand Y is up/down (pressure).
            // We want Forward/Back to drive depth.
            Vector3 move = new Vector3(scaledHandDelta.x, 0, scaledHandDelta.z);
            activeObject.position = startObjPos + move;
        }
        else
        {
            // MODE A: Looking at Object (Y-AXIS MODE)
            // forward-backward: y axis, left-right: x axis

            // Hand Right (X) -> Camera Right
            // Hand Fwd (Z)   -> Camera Up
            
            Vector3 camRight = eyeCamera.transform.right;
            Vector3 camUp = eyeCamera.transform.up;
            
            // Flatten camera vectors so Up is truly Up
            camRight.y = 0; camRight.Normalize();
            camUp = Vector3.up; 

            // Map:
            // Hand X (Left/Right) -> Cam Right
            // Hand Z (Fwd/Back)   -> Global Up (Y)
            Vector3 planeMove = (camRight * scaledHandDelta.x) + (camUp * scaledHandDelta.z);
            
            activeObject.position = startObjPos + planeMove;
        }
    }

    // Helpers

    void StartDrag(State newState)
    {
        currentState = newState;
        startPenPos = virtualPenTip.position;
        
        // Find the Floating Object (even if we touched a shadow)
        activeObject = GetLinkedObject(focusedTarget);
        
        if (activeObject != null)
        {
            startObjPos = activeObject.position;

            // Calculate Offset based on what we touched
            if (newState == State.DirectTableShadow)
            {
                // Shadow Position (on table) vs Pen Position
                // We fake the shadow pos by using Object X/Z and Pen Y
                Vector3 shadowPos = new Vector3(activeObject.position.x, virtualPenTip.position.y, activeObject.position.z);
                grabOffset = shadowPos - virtualPenTip.position;
            }
            else if (newState == State.DirectAir)
            {
                grabOffset = activeObject.position - virtualPenTip.position;
            }

            // Init Switch Logic
            CheckIfLookingAtPen();
            wasLookingAtPen = isLookingAtPen;
        }
        else
        {
            EndDrag();
        }
    }

    void ReAnchor()
    {
        startPenPos = virtualPenTip.position;
        startObjPos = activeObject.position;
    }

    void EndDrag()
    {
        currentState = State.Idle;
        activeObject = null;
        isLookingAtPen = false;
    }

    Vector3 GetScaledDelta()
    {
        Vector3 rawDelta = virtualPenTip.position - startPenPos;
        if (mappingType == IndirectMappingType.OneToOne) return rawDelta * moveSensitivity;
        
        float gain = GetVisualGain();
        return rawDelta * moveSensitivity * gain;
    }

    float GetVisualGain()
    {
        if (activeObject == null) return 1f;
        float eyeHand = Vector3.Distance(eyeCamera.transform.position, virtualPenTip.position);
        float eyeObj = Vector3.Distance(eyeCamera.transform.position, activeObject.position);
        return eyeObj / Mathf.Max(eyeHand, 0.01f);
    }

    void CheckIfLookingAtPen()
    {
        if (gazeProvider == null) return;
        Vector3 toPen = (virtualPenTip.position - eyeCamera.transform.position).normalized;
        float angle = Vector3.Angle(gazeProvider.GazeDirection, toPen);
        isLookingAtPen = angle < penGazeThreshold;
    }

    // detection

    void DeterminePotentialTarget()
    {
        focusedTarget = null;
        
        // 1. Direct Touch
        Collider[] hits = Physics.OverlapSphere(virtualPenTip.position, directGrabRadius);
        foreach (var h in hits)
        {
            if (h.CompareTag("Shadow") || h.CompareTag("Interactable"))
            {
                focusedTarget = h.transform;
                return;
            }
        }

        // 2. Gaze
        if (gazeProvider != null)
        {
            RaycastHit hit;
            if (Physics.Raycast(gazeProvider.GazeOrigin, gazeProvider.GazeDirection, out hit))
            {
                if (hit.collider.CompareTag("Shadow") || hit.collider.CompareTag("Interactable"))
                {
                    focusedTarget = hit.collider.transform;
                }
            }
        }
    }

    bool IsTouchingShadow() => focusedTarget != null && focusedTarget.CompareTag("Shadow") && IsPhysicallyClose();
    bool IsTouchingObject() => focusedTarget != null && focusedTarget.CompareTag("Interactable") && IsPhysicallyClose();
    
    bool IsPhysicallyClose() => Vector3.Distance(virtualPenTip.position, focusedTarget.position) <= directGrabRadius;

    bool IsGazingAtShadow() => focusedTarget != null && focusedTarget.CompareTag("Shadow");
    bool IsGazingAtObject() => focusedTarget != null && focusedTarget.CompareTag("Interactable");

    Transform GetLinkedObject(Transform target)
    {
        if (target == null) return null;
        if (target.CompareTag("Interactable")) return target;
        
        if (target.CompareTag("Shadow"))
        {
            var follower = target.GetComponent<ShadowFollower>();
            if (follower != null) return follower.targetObject;
        }
        return null;
    }

    // visuals

    void UpdateOutlines()
    {
        Color targetColor = indirectColor;
        
        if (currentState == State.DirectAir || currentState == State.DirectTableShadow) targetColor = directColor;
        else if (currentState != State.Idle) targetColor = indirectColor;
        else
        {
            if (IsTouchingShadow() || IsTouchingObject()) targetColor = directColor;
            else targetColor = indirectColor;
        }

        if (focusedTarget != null)
        {
            Transform pair = GetLinkedObject(focusedTarget);
            SetOutline(focusedTarget, targetColor, true);
            if (pair != null && pair != focusedTarget) SetOutline(pair, targetColor, true);
        }
    }

    void SetOutline(Transform t, Color c, bool enable)
    {
        if (t == null) return;
        var outline = t.GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = enable;
            outline.OutlineColor = c;
            outline.OutlineWidth = 5f;
        }
    }
}

