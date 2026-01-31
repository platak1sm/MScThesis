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
    public Color hoverColor = Color.green;       
    public Color indirectGrabColor = Color.blue; 
    public Color directGrabColor = Color.yellow; 

    // StateMachine
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
    private Transform activeObject = null;  
    
    // Inputs
    private bool isPressingTable = false;
    private bool isTriggerPressed = false;
    private bool isLookingAtPen = false;
    private bool wasLookingAtPen = false;
    
    // Movement Anchors
    private Vector3 startPenPos;
    private Vector3 startObjPos;
    private Vector3 grabOffset; 

    // Visual State Tracking
    private Transform _lastOutlinedObj;
    private Transform _lastOutlinedShadow;

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

        // State Handling
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

    // Movement Logic

    void HandleDirectAir()
    {
        if (activeObject == null) return;
        activeObject.position = virtualPenTip.position + grabOffset;
        activeObject.rotation = virtualPenTip.rotation;
    }

    void HandleDirectTableShadow()
    {
        if (activeObject == null) return;
        Vector3 newShadowPos = virtualPenTip.position + grabOffset;
        activeObject.position = new Vector3(newShadowPos.x, activeObject.position.y, newShadowPos.z);
    }

    void HandleIndirectAir()
    {
        if (activeObject == null) return;
        Vector3 delta = GetScaledDelta();
        activeObject.position = startObjPos + delta;
    }

    void HandleIndirectTableShadow()
    {
        if (activeObject == null) return;
        Vector3 rawDelta = virtualPenTip.position - startPenPos;
        Vector3 scaledDelta = rawDelta * moveSensitivity; 
        Vector3 flatDelta = new Vector3(scaledDelta.x, 0, scaledDelta.z);
        activeObject.position = startObjPos + flatDelta;
    }

    void HandleIndirectTableObject()
    {
        if (activeObject == null) return;
        
        CheckIfLookingAtPen();

        if (isLookingAtPen != wasLookingAtPen)
        {
            ReAnchor();
            wasLookingAtPen = isLookingAtPen;
        }

        Vector3 delta = virtualPenTip.position - startPenPos;
        float scale = moveSensitivity;
        if (mappingType == IndirectMappingType.VisualAngleGain) scale *= GetVisualGain();
        Vector3 scaledDelta = delta * scale;

        if (isLookingAtPen)
        {
            // Mode B: Z-Axis
            Vector3 move = new Vector3(scaledDelta.x, 0, scaledDelta.z);
            activeObject.position = startObjPos + move;
        }
        else
        {
            // Mode A: Y-Axis
            Vector3 camRight = eyeCamera.transform.right;
            Vector3 camUp = eyeCamera.transform.up;
            camRight.y = 0; camRight.Normalize();
            camUp = Vector3.up; 
            Vector3 planeMove = (camRight * scaledDelta.x) + (camUp * scaledDelta.z);
            activeObject.position = startObjPos + planeMove;
        }
    }

    // helper funcs

    void StartDrag(State newState)
    {
        currentState = newState;
        startPenPos = virtualPenTip.position;
        activeObject = GetLinkedObject(focusedTarget);
        
        if (activeObject != null)
        {
            startObjPos = activeObject.position;

            if (newState == State.DirectTableShadow)
            {
                Vector3 shadowPos = new Vector3(activeObject.position.x, virtualPenTip.position.y, activeObject.position.z);
                grabOffset = shadowPos - virtualPenTip.position;
            }
            else if (newState == State.DirectAir)
            {
                grabOffset = activeObject.position - virtualPenTip.position;
            }

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

    void DeterminePotentialTarget()
    {
        focusedTarget = null;
        
        Collider[] hits = Physics.OverlapSphere(virtualPenTip.position, directGrabRadius);
        foreach (var h in hits)
        {
            if (h.CompareTag("Shadow") || h.CompareTag("Interactable"))
            {
                focusedTarget = h.transform;
                return;
            }
        }

        if (gazeProvider != null)
        {
            RaycastHit[] hitsAll = Physics.SphereCastAll(
                gazeProvider.GazeOrigin, 0.05f, gazeProvider.GazeDirection, 100f, 
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide
            );

            float closestDist = Mathf.Infinity;
            Transform bestCandidate = null;

            foreach (RaycastHit hit in hitsAll)
            {
                if (hit.collider.CompareTag("Shadow") || hit.collider.CompareTag("Interactable"))
                {
                    if (hit.distance < closestDist)
                    {
                        closestDist = hit.distance;
                        bestCandidate = hit.collider.transform;
                    }
                }
            }

            if (bestCandidate != null) focusedTarget = bestCandidate;
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
    
    Transform GetShadowForObject(Transform obj)
    {
        if (obj == null) return null;
        ShadowFollower[] allShadows = FindObjectsByType<ShadowFollower>(FindObjectsSortMode.None);
        foreach (var s in allShadows)
        {
            if (s.targetObject == obj) return s.transform;
        }
        return null;
    }

    // visual feedback
    void UpdateOutlines()
    {
        Transform currentObj = null;
        Transform currentShadow = null;
        Color targetColor = Color.white;
        bool shouldOutline = false;

        // 1. Determine Desired State
        if (currentState != State.Idle)
        {
            // Dragging
            shouldOutline = true;
            currentObj = activeObject;
            currentShadow = GetShadowForObject(activeObject);

            if (currentState == State.DirectAir || currentState == State.DirectTableShadow)
                targetColor = directGrabColor; // Yellow
            else
                targetColor = indirectGrabColor; // Blue
        }
        else if (focusedTarget != null)
        {
            // Hovering
            shouldOutline = true;
            targetColor = hoverColor; // Green

            if (focusedTarget.CompareTag("Interactable"))
            {
                currentObj = focusedTarget;
                currentShadow = GetShadowForObject(focusedTarget);
            }
            else if (focusedTarget.CompareTag("Shadow"))
            {
                currentShadow = focusedTarget;
                currentObj = GetLinkedObject(focusedTarget);
            }
        }

        // 2. Handle State Changes
        if (_lastOutlinedObj != null && _lastOutlinedObj != currentObj)
        {
            SetOutline(_lastOutlinedObj, Color.white, false);
        }
        if (_lastOutlinedShadow != null && _lastOutlinedShadow != currentShadow)
        {
            SetOutline(_lastOutlinedShadow, Color.white, false);
        }

        // 3. Apply New State
        if (shouldOutline)
        {
            SetOutline(currentObj, targetColor, true);
            SetOutline(currentShadow, targetColor, true);
        }

        // 4. Update History
        _lastOutlinedObj = currentObj;
        _lastOutlinedShadow = currentShadow;
    }

    void SetOutline(Transform t, Color c, bool enable)
    {
        if (t == null) return;
        var outline = t.GetComponent<Outline>();
        if (outline != null)
        {
            if (enable)
            {
                outline.OutlineColor = c;
                outline.OutlineWidth = 5f; 
                outline.OutlineMode = Outline.Mode.OutlineAll;
                outline.enabled = true;
            }
            else
            {
                outline.enabled = false;
            }
        }
    }
}