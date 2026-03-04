using UnityEngine;

public class PenInteractionHandler
{
    private HybridPenController pen;
    private PenTargetingHandler targeting;

    public PenInteractionHandler(HybridPenController penController, PenTargetingHandler targetingHandler)
    {
        this.pen = penController;
        this.targeting = targetingHandler;
    }

    public void StartDrag(HybridPenController.State newState)
    {
        pen.currentState = newState;
        pen.startPenPos = pen.virtualPenTip.position;
        pen.activeObject = targeting.GetLinkedObject(pen.focusedTarget);
        
        if (pen.activeObject != null)
        {
            pen.startObjPos = pen.activeObject.position;

            if (newState == HybridPenController.State.DirectTableShadow)
            {
                Vector3 shadowPos = new Vector3(pen.activeObject.position.x, pen.virtualPenTip.position.y, pen.activeObject.position.z);
                pen.grabOffset = shadowPos - pen.virtualPenTip.position;
            }
            else if (newState == HybridPenController.State.DirectAir)
            {
                pen.grabOffset = pen.activeObject.position - pen.virtualPenTip.position;
            }

            pen.CheckIfLookingAtPen();
            pen.wasLookingAtPen = pen.isLookingAtPen;
            
            if (newState == HybridPenController.State.IndirectTableObject && pen.isLookingAtPen)
            {
                pen.TogglePenScreen(true);
                pen.UpdateTopDownCamera();
            }
        }
        else
        {
            EndDrag();
        }
    }

    public void EndDrag()
    {
        pen.currentState = HybridPenController.State.Idle;
        pen.activeObject = null;
        pen.isLookingAtPen = false;
        pen.wasLookingAtPen = false;

        pen.TogglePenScreen(false);
    }
}
