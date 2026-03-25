using UnityEngine;

public class HybridPenController : MonoBehaviour
{
    public enum IndirectMappingType { VisualAngleGain, OneToOne }
    public enum HeadPosture { Straight, Down }
    public enum MinimapDownMode { FrontFacing_XY, SideFacing_YZ }
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
    
    [Header("Perspective-Dependent Interaction")]
    public float postureDownThreshold = 25.0f;
    public float postureStraightThreshold = 15.0f;
    public MinimapDownMode minimapDownMode = MinimapDownMode.FrontFacing_XY;
    public enum MinimapScaleMode { TrueOptical1To1 }
    [Tooltip("TrueOptical mathematically matches your eye ray scale.")]
    public MinimapScaleMode minimapScaleMode = MinimapScaleMode.TrueOptical1To1;
    [HideInInspector] public HeadPosture currentPosture = HeadPosture.Straight;
    [HideInInspector] public HeadPosture lockedPosture = HeadPosture.Straight;

    [Header("Colors & Visuals")]
    public Color hoverColor = Color.green;       
    public Color indirectGrabColor = Color.blue; 
    public Color directGrabColor = Color.yellow; 

    [Header("Minimap 2D Camera")]
    public GameObject penScreen;         
    public Camera minimapCamera;         
    public float cameraHeight = 5.0f;    
    [Tooltip("Lower number = more zoom.")]
    public float cameraZoom = 1.0f;
    [Tooltip("Speed at which the minimap expands/shrinks.")]
    public float minimapAnimationSpeed = 5f;
    [Tooltip("Size multiplier when not looking at the pen.")]
    public float minimapMinimizedScale = 0.2f;

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
    [HideInInspector] public Quaternion grabRotationOffset;

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
        
        CheckIfLookingAtPen();

        float pitch = 0f;
        if (gazeProvider != null && gazeProvider.GazeDirection.sqrMagnitude > 0.001f)
        {
            pitch = Quaternion.LookRotation(gazeProvider.GazeDirection).eulerAngles.x;
        }
        else
        {
            pitch = eyeCamera.transform.localEulerAngles.x;
        }
        if (pitch > 180f) pitch -= 360f;
        
        if (pitch > postureDownThreshold) currentPosture = HeadPosture.Down;
        else if (pitch < postureStraightThreshold) currentPosture = HeadPosture.Straight;

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
        UpdateMinimapCamera();
    }

    //pen helpers
    public void ReAnchor()
    {
        startPenPos = virtualPenTip.position;
        startObjPos = activeObject.position;
    }

    public void TogglePenScreen(bool enable)
    {
        if (penScreen != null)
        {
            if (enable && !penScreen.activeSelf)
            {
                // Store the prefab scale before manipulating it
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

                penScreen.transform.localScale = initialScreenScale * minimapMinimizedScale;
            }
            penScreen.SetActive(enable);
        }
        if (minimapCamera != null) minimapCamera.gameObject.SetActive(enable);
    }

    public void UpdateMinimapCamera()
    {
        if (penScreen != null)
        {
            penScreen.transform.position = virtualPenTip.position + new Vector3(0, 0.005f, 0); //flat on the table
            float yAngle = eyeCamera != null ? eyeCamera.transform.eulerAngles.y : 0f;
            penScreen.transform.rotation = Quaternion.Euler(90f, yAngle, 0f);

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
        }

        if (minimapCamera != null && activeObject != null)
        {
            float gain = GetVisualGain();

            Vector3 trueCenter = activeObject.position;
            float maxObjExtent = 0.1f;
            
            Collider[] colliders = activeObject.GetComponentsInChildren<Collider>();
            if (colliders.Length > 0)
            {
                Bounds combinedBounds = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++)
                {
                    if (!colliders[i].isTrigger) combinedBounds.Encapsulate(colliders[i].bounds);
                }
                
                trueCenter = combinedBounds.center;
                // find the max width/depth of the combined multi-part prefab
                maxObjExtent = Mathf.Max(combinedBounds.size.x, combinedBounds.size.z);
            }

            // attach to the game object center
            minimapCamera.transform.position = trueCenter;

            // to prevent occlusion: Near Clip = the physical edge of the object (plus tiny 1cm buffer)
            minimapCamera.nearClipPlane = -(maxObjExtent / 2f) - 0.01f;

            if (lockedPosture == HeadPosture.Straight)
            {
                minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            }
            else // HeadPosture.Down
            {
                if (minimapDownMode == MinimapDownMode.FrontFacing_XY)
                {
                    minimapCamera.transform.rotation = Quaternion.Euler(0f, 0f, 0f); // Looks at +Z
                }
                else // SideFacing_YZ
                {
                    minimapCamera.transform.rotation = Quaternion.Euler(0f, -90f, 0f); // Looks at -X
                }
            }

            // Filter out shadows
            int shadowLayer = LayerMask.NameToLayer("Shadow");
            
            int mask = -1; 
            if (shadowLayer != -1) mask &= ~(1 << shadowLayer);
            minimapCamera.cullingMask = mask;

            if (penScreen != null)
            {
                float targetWorldWidth = initialScreenWorldWidth;
                float targetOrthoSize = 1.0f;

                if (minimapScaleMode == MinimapScaleMode.TrueOptical1To1)
                {
                    // True 1:1 Optical Ray Scaling
                    float requiredPhysicalSize = maxObjExtent / gain;
                    float paddedPhysicalSize = requiredPhysicalSize * 3f;

                    // Ensure the physical tablet UI does not scale down into a microscopic sub-pixel point
                    targetWorldWidth = Mathf.Max(initialScreenWorldWidth, paddedPhysicalSize);
                    
                    targetOrthoSize = ((paddedPhysicalSize * gain) / 2f) / Mathf.Max(0.01f, cameraZoom);
                }

                float scaleMultiplier = targetWorldWidth / initialScreenWorldWidth;
                Vector3 expandedScale = initialScreenScale * scaleMultiplier;
                Vector3 targetScale = expandedScale;

                // If not looking at the pen, shrink the minimap
                if (!isLookingAtPen)
                {
                    targetScale = initialScreenScale * minimapMinimizedScale;
                    targetOrthoSize = targetOrthoSize * (targetScale.x / Mathf.Max(0.001f, expandedScale.x));
                }

                // Animate physical tablet scale smoothly
                penScreen.transform.localScale = Vector3.Lerp(
                    penScreen.transform.localScale, 
                    targetScale, 
                    Time.deltaTime * minimapAnimationSpeed
                );

                minimapCamera.orthographic = true; 
                
                // Animate orthographic size smoothly
                minimapCamera.orthographicSize = Mathf.Lerp(
                    minimapCamera.orthographicSize, 
                    targetOrthoSize, 
                    Time.deltaTime * minimapAnimationSpeed
                );
            }
        }
        else if (penScreen != null && activeObject == null)
        {
            if (initialScreenScale != Vector3.zero)
            {
                Vector3 minimizedScale = initialScreenScale * minimapMinimizedScale;
                penScreen.transform.localScale = Vector3.Lerp(
                    penScreen.transform.localScale, 
                    minimizedScale, 
                    Time.deltaTime * minimapAnimationSpeed
                );
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