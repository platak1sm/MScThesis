using UnityEngine;
using UnityEngine.Rendering;

public class ControllerShadowGrid : MonoBehaviour
{
    [Tooltip("Max size of the shadow when far from the floor.")]
    public float baseSize = 0.15f;
    [Tooltip("The vertical height range over which the shadow scales dynamically.")]
    public float maxHoverDistance = 1.0f;
    
    private GameObject activeShadow;
    private Material shadowMaterial;
    private ShadowManager runtimeShadowManager;

    void Start()
    {
        activeShadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
        activeShadow.name = "ControllerFloorIndicator";
        Destroy(activeShadow.GetComponent<Collider>());
        
        Renderer r = activeShadow.GetComponent<Renderer>();
        // Using UI/Default allows mainTexture coloration dynamically on 3D objects
        shadowMaterial = new Material(Shader.Find("UI/Default"));
        
        // Procedurally generate a soft black circle texture native to Unity without external images
        int res = 128;
        Texture2D circleTex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        circleTex.wrapMode = TextureWrapMode.Clamp;
        Vector2 center = new Vector2(res / 2f, res / 2f);
        float radius = res / 2f;
        Color transp = new Color(0, 0, 0, 0);

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                // 2-pixel anti-aliasing edge so the circle is perfectly smooth
                float alpha = Mathf.Clamp01((radius - dist) / 2f);
                if (dist < radius)
                    circleTex.SetPixel(x, y, new Color(0, 0, 0, alpha));
                else
                    circleTex.SetPixel(x, y, transp);
            }
        }
        circleTex.Apply();

        shadowMaterial.mainTexture = circleTex;
        shadowMaterial.color = new Color(0.0f, 0.0f, 0.0f, 0.65f); // Solid semi-transparent black
        
        r.material = shadowMaterial;
        // Avoid Z-Fighting natively by boosting rendering priority
        r.material.renderQueue = 3000;
        
        runtimeShadowManager = FindFirstObjectByType<ShadowManager>();
    }

    void Update()
    {
        if (runtimeShadowManager == null) 
        {
            runtimeShadowManager = FindFirstObjectByType<ShadowManager>();
            if (runtimeShadowManager == null) return;
        }

        if (!activeShadow.activeSelf) activeShadow.SetActive(true);

        // Anchor it EXACTLY 1cm above the global table height at all times
        float floorY = runtimeShadowManager.globalTableHeight;
        float hoverAltitude = transform.position.y - floorY;

        if (hoverAltitude < 0) hoverAltitude = 0.001f;

        activeShadow.transform.position = new Vector3(transform.position.x, floorY + 0.01f, transform.position.z);
        activeShadow.transform.rotation = Quaternion.Euler(90, 0, 0); // Flat
        
        // Scale dynamically: The shadow is large when the pen is in the air, and SHRINKS down to roughly 0 as you touch the table!
        float depthRatio = Mathf.Clamp01(hoverAltitude / maxHoverDistance);
        
        // When depthRatio is 0 (touching table), multiplier is 0.02f (tiny point)
        float scaleMultiplier = Mathf.Max(0.02f, depthRatio);
        activeShadow.transform.localScale = new Vector3(baseSize * scaleMultiplier, baseSize * scaleMultiplier, 1f);
    }
}
