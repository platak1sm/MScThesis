using UnityEngine;

public class HybridPenController : MonoBehaviour
{
    public enum IndirectMappingType { VisualAngleGain, OneToOne }
    public enum State { Idle, DirectAir, DirectTableShadow, IndirectAir, IndirectTableShadow, IndirectTableObject }

    [Header("Core Systems")]
    public GazeProvider gazeProvider;
    public Transform virtualPenTip;
    public Camera eyeCamera; 

    [Header("Settings")]
    public float directGrabRadius = 0.05f; 
    [Tooltip("How much the object moves relative to the pen")]
    public float moveSensitivity = 1.0f;   
    public float penGazeThreshold = 35.0f; 
    [Range(0.0f, 1.0f)]
    public float pressureThreshold = 0.15f; 
    [Tooltip("Offset for spawning objects on the controller")]
    public Vector3 spawnOffset = new Vector3(0, 0.05f, 0.05f);

    [Header("Indirect Logic")]
    public IndirectMappingType mappingType = IndirectMappingType.VisualAngleGain;
    
    [Header("Colors & Visuals")]
    public Color hoverColor = Color.green;       
    public Color indirectGrabColor = Color.blue; 
    public Color directGrabColor = Color.yellow; 

    [Header("Top-Down 2D")]
    public GameObject penScreen;         
    public Camera topDownCamera;         
    public float cameraHeight = 5.0f;    
    [Tooltip("Lower number = more zoom.")]
    public float cameraZoom = 1.0f;

    [HideInInspector] public State currentState = State.Idle;
    [HideInInspector] public Transform focusedTarget = null; 
    [HideInInspector] public Transform activeObject = null;  
    
    [HideInInspector] public bool isPressingTable = false;
    [HideInInspector] public bool isTriggerPressed = false;
    [HideInInspector] public bool isLookingAtPen = false;
    [HideInInspector] public bool wasLookingAtPen = false;
    
    [HideInInspector] public Vector3 startPenPos;
    [HideInInspector] public Vector3 startObjPos;
    [HideInInspector] public Vector3 grabOffset; 

    [HideInInspector] public Transform lastOutlinedObj;
    [HideInInspector] public Transform lastOutlinedShadow;

    // Logic handlers
    private PenMovementHandler movementHandler;
    private PenTargetingHandler targetingHandler;
    private PenInteractionHandler interactionHandler;

    void Start()
    {
        if (gazeProvider == null) gazeProvider = FindFirstObjectByType<GazeProvider>();
        if (eyeCamera == null) eyeCamera = Camera.main;

        // Cleanup pre-existing outlines
        foreach (var obj in FindObjectsByType<Outline>(FindObjectsSortMode.None))
        {
            if (obj.CompareTag("Interactable") || obj.CompareTag("Shadow"))
            {
                obj.enabled = false;
            }
        }

        movementHandler = new PenMovementHandler(this);
        targetingHandler = new PenTargetingHandler(this);
        interactionHandler = new PenInteractionHandler(this, targetingHandler);

        TogglePenScreen(false);
    }

    void Update()
    {
        float pressure = OVRInput.Get(OVRInput.Axis1D.PrimaryStylusForce, OVRInput.Controller.RTouch);
        float trigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch);
        
        bool wasTrigger = isTriggerPressed;
        bool wasPressing = isPressingTable;
        
        isTriggerPressed = trigger > 0.5f;
        isPressingTable = pressure > pressureThreshold;

        if (currentState != State.Idle)
        {
            switch (currentState)
            {
                case State.DirectAir:
                    movementHandler.HandleDirectAir();
                    if (!isTriggerPressed) interactionHandler.EndDrag();
                    break;

                case State.DirectTableShadow:
                    movementHandler.HandleDirectTableShadow();
                    if (!isPressingTable) interactionHandler.EndDrag();
                    break;

                case State.IndirectAir:
                    movementHandler.HandleIndirectAir();
                    if (!isTriggerPressed) interactionHandler.EndDrag();
                    break;

                case State.IndirectTableShadow:
                    movementHandler.HandleIndirectTableShadow();
                    if (!isPressingTable) interactionHandler.EndDrag();
                    break;

                case State.IndirectTableObject:
                    movementHandler.HandleIndirectTableObject();
                    if (!isPressingTable) interactionHandler.EndDrag();
                    break;
            }
        }
        else
        {
            targetingHandler.DeterminePotentialTarget();

            if (isPressingTable && !wasPressing) 
            {
                if (!HandleToolAction(false))
                {
                    if (targetingHandler.IsTouchingShadow()) interactionHandler.StartDrag(State.DirectTableShadow);
                    else if (targetingHandler.IsGazingAtShadow()) interactionHandler.StartDrag(State.IndirectTableShadow);
                    else if (targetingHandler.IsGazingAtObject()) interactionHandler.StartDrag(State.IndirectTableObject);
                }
            }

            if (isTriggerPressed && !wasTrigger && !isPressingTable)
            {
                if (!HandleToolAction(true))
                {
                     if (targetingHandler.IsTouchingObject()) interactionHandler.StartDrag(State.DirectAir);
                     else if (targetingHandler.IsGazingAtObject()) interactionHandler.StartDrag(State.IndirectAir);
                }
            }
        }

        targetingHandler.UpdateOutlines();
    }

    //pen helpers
    public void ReAnchor()
    {
        startPenPos = virtualPenTip.position;
        startObjPos = activeObject.position;
    }

    public void TogglePenScreen(bool enable)
    {
        if (penScreen != null) penScreen.SetActive(enable);
        if (topDownCamera != null) topDownCamera.gameObject.SetActive(enable);
    }

    public void UpdateTopDownCamera()
    {
        if (topDownCamera != null && activeObject != null)
        {
            topDownCamera.orthographic = true; 
            topDownCamera.orthographicSize = cameraZoom;

            Vector3 trueCenter = activeObject.position; 
            Collider objCollider = activeObject.GetComponent<Collider>();
            
            if (objCollider != null)
            {
                trueCenter = objCollider.bounds.center;
            }

            topDownCamera.transform.position = new Vector3(
                trueCenter.x, 
                trueCenter.y + cameraHeight, 
                trueCenter.z
            );
            
            topDownCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }

    public Vector3 GetScaledDelta()
    {
        Vector3 rawDelta = virtualPenTip.position - startPenPos;
        if (mappingType == IndirectMappingType.OneToOne) return rawDelta * moveSensitivity;
        float gain = GetVisualGain();
        return rawDelta * moveSensitivity * gain;
    }

    public float GetVisualGain()
    {
        if (activeObject == null) return 1f;
        float eyeHand = Vector3.Distance(eyeCamera.transform.position, virtualPenTip.position);
        float eyeObj = Vector3.Distance(eyeCamera.transform.position, activeObject.position);
        return eyeObj / Mathf.Max(eyeHand, 0.01f);
    }

    public void CheckIfLookingAtPen()
    {
        if (gazeProvider == null) return;
        Vector3 toPen = (virtualPenTip.position - eyeCamera.transform.position).normalized;
        float angle = Vector3.Angle(gazeProvider.GazeDirection, toPen);
        isLookingAtPen = angle < penGazeThreshold;
    }

    private bool HandleToolAction(bool fromTrigger)
    {
        if (InteractionToolManager.Instance == null) return false;

        InteractionTool tool = InteractionToolManager.Instance.CurrentTool;

        // A. UI Buttons FIRST to ensure menus can always be pressed
        if (focusedTarget != null)
        {
            VRButton btn = focusedTarget.GetComponent<VRButton>();
            if (btn != null)
            {
                if (fromTrigger || isPressingTable) btn.Click();
                return true; // We clicked a button, stop further tool processing
            }
        }

        // B. Spawn Tool (No target required)
        if (tool == InteractionTool.Spawn)
        {
            if (fromTrigger)
            {
                GameObject prefab = InteractionToolManager.Instance.PrefabToSpawn;
                if (prefab != null)
                {
                    Vector3 spawnPos = virtualPenTip.position + virtualPenTip.TransformDirection(spawnOffset);
                    GameObject newObj = Instantiate(prefab, spawnPos, virtualPenTip.rotation);
                    
                    // Turn off outline if the prefab had it enabled by default
                    var outline = newObj.GetComponent<Outline>();
                    if (outline != null) outline.enabled = false;
                    
                    // Add a shadow for the new object
                    ShadowManager sm = FindFirstObjectByType<ShadowManager>();
                    if (sm != null && sm.shadowPrefab != null)
                    {
                        GameObject newShadow = Instantiate(sm.shadowPrefab);
                        ShadowFollower follower = newShadow.GetComponent<ShadowFollower>();
                        if (follower != null)
                        {
                            follower.targetObject = newObj.transform;
                            follower.tableHeight = sm.globalTableHeight;
                        }
                    }
                }
            }
            // Always block grabbing logic if Spawn tool is selected
            return true;
        }

        // C. Target-Based Tools
        Transform target = targetingHandler.GetLinkedObject(focusedTarget);
        if (target != null && tool != InteractionTool.Move)
        {
            if (fromTrigger)
            {
                switch (tool)
                {
                    case InteractionTool.Delete:
                        var outline = target.GetComponent<Outline>();
                        if (outline != null) 
                        {
                            outline.enabled = false; 
                            Destroy(outline); 
                        }
                        if (lastOutlinedObj == target) lastOutlinedObj = null;
                        if (lastOutlinedShadow == target) lastOutlinedShadow = null;
                        
                        Destroy(target.gameObject);
                        break;

                    case InteractionTool.Duplicate:
                        GameObject clone = Instantiate(target.gameObject, target.position, target.rotation);
                        
                        // Turn off outline if the cloned object had it enabled
                        var cloneOutline = clone.GetComponent<Outline>();
                        if (cloneOutline != null) cloneOutline.enabled = false;
                        
                        // Add a shadow for the clone
                        ShadowManager sm = FindFirstObjectByType<ShadowManager>();
                        if (sm != null && sm.shadowPrefab != null)
                        {
                            GameObject newShadow = Instantiate(sm.shadowPrefab);
                            ShadowFollower follower = newShadow.GetComponent<ShadowFollower>();
                            if (follower != null)
                            {
                                follower.targetObject = clone.transform;
                                follower.tableHeight = sm.globalTableHeight;
                            }
                        }

                        // Make the newly duplicated clone our active target and start dragging it immediately
                        focusedTarget = clone.transform;
                        interactionHandler.StartDrag(targetingHandler.IsPhysicallyClose() ? State.DirectAir : State.IndirectAir);
                        break;

                    case InteractionTool.ColorPicker:
                        Renderer r = target.GetComponent<Renderer>();
                        if (r != null) r.material.color = InteractionToolManager.Instance.ActivePaintColor;
                        break;
                }
            }
            // Block normal grab (return true) if ANY modifier tool (Delete, Duplicate, Color) is selected and aimed at an object
            return true;
        }

        // If move tool, or no target, let normal logic run
        return false;
    }
}