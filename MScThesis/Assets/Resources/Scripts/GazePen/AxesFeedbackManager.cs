using UnityEngine;

public class AxesFeedbackManager : MonoBehaviour
{
    public HybridPenController penController;
    private GameObject axesRoot;
    private GameObject axisX; // Red
    private GameObject axisY; // Green
    private GameObject axisZ; // Blue

    void Start()
    {
        if (penController == null) penController = GetComponent<HybridPenController>();
        CreateAxes();
    }

    void CreateAxes()
    {
        axesRoot = new GameObject("AxesFeedbackRoot");
        axesRoot.SetActive(false);

        axisX = CreateArrow("ArrowX", Color.red, Vector3.right);
        axisY = CreateArrow("ArrowY", Color.green, Vector3.up);
        axisZ = CreateArrow("ArrowZ", Color.blue, Vector3.forward);

        axisX.transform.SetParent(axesRoot.transform, false);
        axisY.transform.SetParent(axesRoot.transform, false);
        axisZ.transform.SetParent(axesRoot.transform, false);
    }

    GameObject CreateArrow(string name, Color color, Vector3 direction)
    {
        GameObject arrow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        arrow.name = name;
        Destroy(arrow.GetComponent<Collider>());
        
        arrow.transform.localScale = new Vector3(0.005f, 0.05f, 0.005f);
        arrow.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
        arrow.transform.localPosition = direction * 0.05f;

        Renderer r = arrow.GetComponent<Renderer>();
        r.material = new Material(Shader.Find("Unlit/Color"));
        r.material.color = color;

        r.material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);

        GameObject holder = new GameObject(name + "_Holder");
        arrow.transform.SetParent(holder.transform, false);
        return holder;
    }

    void Update()
    {
        if (penController == null || penController.currentState == HybridPenController.State.Idle || penController.activeObject == null)
        {
            if (axesRoot.activeSelf) axesRoot.SetActive(false);
            return;
        }

        if (!axesRoot.activeSelf) axesRoot.SetActive(true);

        axesRoot.transform.position = penController.activeObject.position;
        
        bool showX = false, showY = false, showZ = false;

        if (penController.currentState == HybridPenController.State.DirectAir)
        {
            showX = showY = showZ = true;
            axesRoot.transform.rotation = Quaternion.identity; // World space
        }
        else if (penController.currentState == HybridPenController.State.IndirectTableObject)
        {
            Vector3 camRight = penController.eyeCamera.transform.right;
            camRight.y = 0; if (camRight.sqrMagnitude > 0.001f) camRight.Normalize(); else camRight = Vector3.right;

            Vector3 camForward = penController.eyeCamera.transform.forward;
            camForward.y = 0; if (camForward.sqrMagnitude > 0.001f) camForward.Normalize(); else camForward = Vector3.forward;

            if (penController.isLookingAtPen)
            {
                axesRoot.transform.rotation = Quaternion.identity; // World space
                if (penController.lockedPosture == HybridPenController.HeadPosture.Straight)
                {
                    showX = showZ = true; // Top-Down
                }
                else // Down
                {
                    if (penController.minimapDownMode == HybridPenController.MinimapDownMode.FrontFacing_XY)
                        showX = showY = true;
                    else
                        showZ = showY = true;
                }
            }
            else // Main View
            {
                axesRoot.transform.rotation = Quaternion.LookRotation(camForward, Vector3.up); // Align X/Z to camera view

                if (penController.lockedPosture == HybridPenController.HeadPosture.Straight)
                {
                    showX = showY = true;
                }
                else // Down
                {
                    showX = showZ = true;
                }
            }
        }
        else if (penController.currentState == HybridPenController.State.DirectTableShadow)
        {
            showX = showZ = true;
            axesRoot.transform.rotation = Quaternion.identity; //XZ plane 
        }
        else 
        {
            // Fallback for IndirectAir/Shadow if applicable
            showX = showY = showZ = true;
            axesRoot.transform.rotation = Quaternion.identity;
        }

        axisX.SetActive(showX);
        axisY.SetActive(showY);
        axisZ.SetActive(showZ);
    }
}
