using UnityEngine;

public class PenMovementHandler
{
    private HybridPenController pen;

    public PenMovementHandler(HybridPenController penController)
    {
        this.pen = penController;
    }

    public void HandleDirectAir()
    {
        if (pen.activeObject == null) return;
        pen.activeObject.position = pen.virtualPenTip.position + pen.grabOffset;
        
        // Add the pen's current rotation to the originally captured angle difference!
        pen.activeObject.rotation = pen.virtualPenTip.rotation * pen.grabRotationOffset;
    }

    public void HandleDirectTableShadow()
    {
        if (pen.activeObject == null) return;
        Vector3 newShadowPos = pen.virtualPenTip.position + pen.grabOffset;
        pen.activeObject.position = new Vector3(newShadowPos.x, pen.activeObject.position.y, newShadowPos.z);
    }

    public void HandleIndirectAir()
    {
        if (pen.activeObject == null) return;
        Vector3 delta = pen.GetScaledDelta();
        pen.activeObject.position = pen.startObjPos + delta;
    }

    public void HandleIndirectTableShadow()
    {
        if (pen.activeObject == null) return;
        Vector3 rawDelta = pen.virtualPenTip.position - pen.startPenPos;
        Vector3 scaledDelta = rawDelta * pen.moveSensitivity; 
        Vector3 flatDelta = new Vector3(scaledDelta.x, 0, scaledDelta.z);
        pen.activeObject.position = pen.startObjPos + flatDelta;
    }

    public void HandleIndirectTableObject()
    {
        if (pen.activeObject == null) return;
        
        pen.CheckIfLookingAtPen();

        if (pen.isLookingAtPen != pen.wasLookingAtPen)
        {
            pen.ReAnchor();
            pen.wasLookingAtPen = pen.isLookingAtPen;
        }

        Vector3 delta = pen.virtualPenTip.position - pen.startPenPos;
        float scale = pen.moveSensitivity;
        if (pen.mappingType == HybridPenController.IndirectMappingType.VisualAngleGain) 
        {
            scale *= pen.GetVisualGain();
        }
        
        Vector3 scaledDelta = delta * scale;

        if (pen.isLookingAtPen)
        {
            Vector3 move;
            if (pen.lockedPosture == HybridPenController.HeadPosture.Straight)
            {
                move = new Vector3(scaledDelta.x, 0, scaledDelta.z);
            }
            else // HeadPosture.Down
            {
                if (pen.minimapDownMode == HybridPenController.MinimapDownMode.FrontFacing_XY)
                {
                    move = new Vector3(scaledDelta.x, scaledDelta.z, 0); // Front-Facing Minimap (XY)
                }
                else // SideFacing_YZ
                {
                    move = new Vector3(0, scaledDelta.z, scaledDelta.x); // Side-Facing Minimap (YZ)
                }
            }
            pen.activeObject.position = pen.startObjPos + move;
        }
        else
        {
            Vector3 camRight = pen.eyeCamera.transform.right;
            camRight.y = 0; 
            if (camRight.sqrMagnitude > 0.001f) camRight.Normalize(); else camRight = Vector3.right;
            
            Vector3 planeMove;
            if (pen.lockedPosture == HybridPenController.HeadPosture.Straight)
            {
                Vector3 camUp = Vector3.up; 
                planeMove = (camRight * scaledDelta.x) + (camUp * scaledDelta.z); // Wall mapping (XY)
            }
            else // HeadPosture.Down
            {
                Vector3 camForward = pen.eyeCamera.transform.forward;
                camForward.y = 0;
                if (camForward.sqrMagnitude > 0.001f) camForward.Normalize(); else camForward = Vector3.forward;
                planeMove = (camRight * scaledDelta.x) + (camForward * scaledDelta.z); // Floor mapping (XZ)
            }
            
            pen.activeObject.position = pen.startObjPos + planeMove;
        }

        pen.UpdateMinimapCamera();
    }
}
