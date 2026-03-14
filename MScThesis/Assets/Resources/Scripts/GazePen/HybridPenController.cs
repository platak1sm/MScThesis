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

    [HideInInspector] public Vector3 initialScreenScale = Vector3.zero;
    [HideInInspector] public float initialScreenWorldWidth = -1f;

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
            float gain = GetVisualGain();

            Vector3 trueCenter = activeObject.position; 
            Collider objCollider = activeObject.GetComponent<Collider>();
            float maxObjExtent = 0.1f;
            
            if (objCollider != null)
            {
                trueCenter = objCollider.bounds.center;
                // find the max width/depth of the object
                maxObjExtent = Mathf.Max(objCollider.bounds.size.x, objCollider.bounds.size.z);
            }

            topDownCamera.transform.position = new Vector3(
                trueCenter.x, 
                trueCenter.y + cameraHeight, 
                trueCenter.z
            );
            
            topDownCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // Filter out shadows
            int shadowLayer = LayerMask.NameToLayer("Shadow");
            
            int mask = -1; 
            if (shadowLayer != -1) mask &= ~(1 << shadowLayer);
            topDownCamera.cullingMask = mask;

            if (penScreen != null)
            {
                if (initialScreenScale == Vector3.zero)
                {
                    initialScreenScale = penScreen.transform.localScale;
                    RectTransform rt = penScreen.GetComponent<RectTransform>();
                    if (rt != null) {
                        initialScreenWorldWidth = rt.rect.width * rt.localScale.x;
                    } else {
                        initialScreenWorldWidth = penScreen.transform.localScale.x; 
                    }
                    if (initialScreenWorldWidth <= 0.001f) initialScreenWorldWidth = 0.15f;
                }

                // Physical screen size needed = Object World Size / Gain
                float requiredPhysicalSize = maxObjExtent / gain;
                
                // Add padding so the screen is at least 3 times the size of the object
                float paddedPhysicalSize = requiredPhysicalSize * 3f;

                float targetWorldWidth = Mathf.Max(initialScreenWorldWidth, paddedPhysicalSize);
                float scaleMultiplier = targetWorldWidth / initialScreenWorldWidth;
                penScreen.transform.localScale = initialScreenScale * scaleMultiplier;

                topDownCamera.orthographic = true; 
                topDownCamera.orthographicSize = (targetWorldWidth * gain) / 2f;

                penScreen.transform.position = virtualPenTip.position + new Vector3(0, 0.005f, 0); //flat on the table
                float yAngle = eyeCamera != null ? eyeCamera.transform.eulerAngles.y : 0f;
                penScreen.transform.rotation = Quaternion.Euler(90f, yAngle, 0f);
            }
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

        // A. UI Buttons first to ensure menus can always be pressed
        if (focusedTarget != null)
        {
            VRButton btn = focusedTarget.GetComponent<VRButton>();
            if (btn != null)
            {
                if (fromTrigger || isPressingTable) btn.Click();
                return true; 
            }
        }

        // B. Spawn Tool 
        if (tool == InteractionTool.Spawn)
        {
            if (fromTrigger)
            {
                GameObject prefab = InteractionToolManager.Instance.PrefabToSpawn;
                if (prefab != null)
                {
                    Vector3 spawnPos = virtualPenTip.position + virtualPenTip.TransformDirection(spawnOffset);
                    GameObject newObj = Instantiate(prefab, spawnPos, virtualPenTip.rotation);
                    
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

                        // Make the newly duplicated clone the active target and start dragging it immediately
                        focusedTarget = clone.transform;
                        interactionHandler.StartDrag(targetingHandler.IsPhysicallyClose() ? State.DirectAir : State.IndirectAir);
                        break;

                    case InteractionTool.ColorPicker:
                        Renderer r = target.GetComponent<Renderer>();
                        if (r != null) r.material.color = InteractionToolManager.Instance.ActivePaintColor;
                        break;
                }
            }
            return true;
        }

        return false;
    }
}