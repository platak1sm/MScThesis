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
        pen.activeObject.rotation = pen.virtualPenTip.rotation;
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
            pen.TogglePenScreen(pen.isLookingAtPen);
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
            Vector3 move = new Vector3(scaledDelta.x, 0, scaledDelta.z);
            pen.activeObject.position = pen.startObjPos + move;
            pen.UpdateTopDownCamera();
        }
        else
        {
            Vector3 camRight = pen.eyeCamera.transform.right;
            Vector3 camUp = pen.eyeCamera.transform.up;
            camRight.y = 0; camRight.Normalize();
            camUp = Vector3.up; 
            Vector3 planeMove = (camRight * scaledDelta.x) + (camUp * scaledDelta.z);
            pen.activeObject.position = pen.startObjPos + planeMove;
        }
    }
}
