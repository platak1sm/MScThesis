using UnityEngine;

public class PenTargetingHandler
{
    private HybridPenController pen;

    public PenTargetingHandler(HybridPenController penController)
    {
        this.pen = penController;
    }

    public void DeterminePotentialTarget()
    {
        pen.focusedTarget = null;
        
        Collider[] hits = Physics.OverlapSphere(pen.virtualPenTip.position, pen.directGrabRadius);
        Transform bestOverlapShadow = null;
        Transform bestOverlapInteractable = null;

        foreach (var h in hits)
        {
            if (h.CompareTag("Interactable") && bestOverlapInteractable == null)
            {
                bestOverlapInteractable = h.transform;
            }
            else if (h.CompareTag("Shadow") && bestOverlapShadow == null)
            {
                bestOverlapShadow = h.transform;
            }
        }

        // If the pen is touching both the BoxCollider and the Shadow at the exact same time
        if (bestOverlapInteractable != null && bestOverlapShadow != null)
        {
            if (pen.isPressingTable)
            {
                // The pen is being pressed down into the desk.
                // Prioritize the shadow so DirectTableShadow drag is triggered.
                pen.focusedTarget = bestOverlapShadow;
            }
            else
            {
                // The pen is not being pressed down into the desk
                // Prioritize the physical box collider
                pen.focusedTarget = bestOverlapInteractable;
            }
            return;
        }

        // If they are only touching one or the other, cleanly grab mathematically whichever one they are touching
        if (bestOverlapInteractable != null)
        {
            pen.focusedTarget = bestOverlapInteractable;
            return;
        }
        if (bestOverlapShadow != null)
        {
            pen.focusedTarget = bestOverlapShadow;
            return;
        }

        if (pen.gazeProvider != null)
        {
            RaycastHit[] hitsAll = Physics.SphereCastAll(
                pen.gazeProvider.GazeOrigin, 0.05f, pen.gazeProvider.GazeDirection, 100f, 
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide
            );

            float closestInteractableDist = Mathf.Infinity;
            Transform bestInteractable = null;

            float closestShadowDist = Mathf.Infinity;
            Transform bestShadow = null;

            foreach (RaycastHit hit in hitsAll)
            {
                if (hit.collider.CompareTag("Interactable"))
                {
                    if (hit.distance < closestInteractableDist)
                    {
                        closestInteractableDist = hit.distance;
                        bestInteractable = hit.collider.transform;
                    }
                }
                else if (hit.collider.CompareTag("Shadow"))
                {
                    if (hit.distance < closestShadowDist)
                    {
                        closestShadowDist = hit.distance;
                        bestShadow = hit.collider.transform;
                    }
                }
            }

            if (bestInteractable != null)
            {
                pen.focusedTarget = bestInteractable;
            }
            else if (bestShadow != null)
            {
                pen.focusedTarget = bestShadow;
            }
        }
    }

    public bool IsTouchingShadow()
    {
        return pen.focusedTarget != null && pen.focusedTarget.CompareTag("Shadow") && IsPhysicallyClose();
    }

    public bool IsTouchingObject()
    {
        return pen.focusedTarget != null && pen.focusedTarget.CompareTag("Interactable") && IsPhysicallyClose();
    }

    public bool IsPhysicallyClose()
    {
        return Vector3.Distance(pen.virtualPenTip.position, pen.focusedTarget.position) <= pen.directGrabRadius;
    }

    public bool IsGazingAtShadow()
    {
        return pen.focusedTarget != null && pen.focusedTarget.CompareTag("Shadow");
    }

    public bool IsGazingAtObject()
    {
        return pen.focusedTarget != null && pen.focusedTarget.CompareTag("Interactable");
    }

    public Transform GetLinkedObject(Transform target)
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
    
    public Transform GetShadowForObject(Transform obj)
    {
        if (obj == null) return null;
        ShadowFollower[] allShadows = UnityEngine.Object.FindObjectsByType<ShadowFollower>(UnityEngine.FindObjectsSortMode.None);
        foreach (var s in allShadows)
        {
            if (s.targetObject == obj) return s.transform;
        }
        return null;
    }

    public void UpdateOutlines()
    {
        Transform currentObj = null;
        Transform currentShadow = null;
        Color targetColor = Color.white;
        bool shouldOutline = false;

        if (pen.currentState != HybridPenController.State.Idle)
        {
            shouldOutline = true;
            currentObj = pen.activeObject;
            currentShadow = GetShadowForObject(pen.activeObject);

            if (pen.currentState == HybridPenController.State.DirectAir || pen.currentState == HybridPenController.State.DirectTableShadow)
                targetColor = pen.directGrabColor; 
            else
                targetColor = pen.indirectGrabColor; 
        }
        else if (pen.focusedTarget != null)
        {
            shouldOutline = true;
            targetColor = pen.hoverColor; 

            if (pen.focusedTarget.CompareTag("Interactable"))
            {
                currentObj = pen.focusedTarget;
                currentShadow = GetShadowForObject(pen.focusedTarget);
            }
            else if (pen.focusedTarget.CompareTag("Shadow"))
            {
                currentShadow = pen.focusedTarget;
                currentObj = GetLinkedObject(pen.focusedTarget);
            }
        }

        if (pen.lastOutlinedObj != null && pen.lastOutlinedObj != currentObj)
        {
            SetOutline(pen.lastOutlinedObj, Color.white, false);
        }
        if (pen.lastOutlinedShadow != null && pen.lastOutlinedShadow != currentShadow)
        {
            SetOutline(pen.lastOutlinedShadow, Color.white, false);
        }

        if (shouldOutline)
        {
            SetOutline(currentObj, targetColor, true);
            SetOutline(currentShadow, targetColor, true);
        }

        pen.lastOutlinedObj = currentObj;
        pen.lastOutlinedShadow = currentShadow;
    }

    public void SetOutline(Transform t, Color c, bool enable)
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
