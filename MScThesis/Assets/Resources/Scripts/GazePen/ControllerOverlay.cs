using UnityEngine;
using UnityEngine.Rendering;

public class ControllerOverlay : MonoBehaviour
{
    private float checkTimer = 0f;

    void Update()
    {
        checkTimer += Time.deltaTime;
        if (checkTimer > 1.0f) 
        {
            checkTimer = 0f;
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                foreach (Material m in r.materials)
                {
                    // ignore the physical table entirely
                    m.SetInt("_ZTest", (int)CompareFunction.Always);
                    // force the rendering engine to paint the controller last
                    m.renderQueue = 5000; 
                }
            }
        }
    }
}
