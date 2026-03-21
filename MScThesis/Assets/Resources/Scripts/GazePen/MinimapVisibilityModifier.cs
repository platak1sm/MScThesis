using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class MinimapVisibilityModifier : MonoBehaviour
{
    void Start()
    {
        MakeAlwaysVisible();
    }

    public void MakeAlwaysVisible()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            foreach (Material m in r.materials)
            {
                m.SetInt("_ZTest", (int)CompareFunction.Always);
            }
        }
        
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        foreach (Graphic g in graphics)
        {
            Material mat = new Material(g.material);
            mat.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            g.material = mat;
        }
    }
}
