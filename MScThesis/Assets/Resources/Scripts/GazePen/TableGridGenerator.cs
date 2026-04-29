using UnityEngine;

public class TableGridGenerator : MonoBehaviour
{
    [Tooltip("Higher multiplier = more grids (smaller squares).")]
    public float gridDensityMultiplier = 40f; 
    public Color gridLineColor = new Color(0, 0, 0, 0.45f);

    void Start()
    {
        Renderer parentRenderer = GetComponent<Renderer>();
        if (parentRenderer == null) parentRenderer = GetComponentInChildren<Renderer>();
        if (parentRenderer == null) return;

        // Create a completely autonomous visual mesh so we don't rely on submesh arrays!
        GameObject gridObj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        gridObj.name = "TableGrid_Overlay";
        Destroy(gridObj.GetComponent<Collider>()); // Strip physics
        
        gridObj.transform.SetParent(parentRenderer.transform, false);
        // Face UP natively relative to the parent surface natively
        gridObj.transform.localRotation = Quaternion.Euler(90, 0, 0); 
        
        MeshFilter mf = parentRenderer.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Bounds b = mf.sharedMesh.bounds;
            // Snaps exactly to the physical top face of the native mesh
            gridObj.transform.localScale = new Vector3(b.size.x, b.size.z, 1f); 
            
            // Calculate a perfect mathematically guaranteed global 1mm offset above the bounds
            float pushUp = 0.002f;
            if (Mathf.Abs(parentRenderer.transform.lossyScale.y) > 0.0001f)
                pushUp = 0.002f / parentRenderer.transform.lossyScale.y;

            gridObj.transform.localPosition = new Vector3(b.center.x, b.center.y + b.extents.y + pushUp, b.center.z);
        }
        else 
        {
            gridObj.transform.localPosition = new Vector3(0, 0.002f, 0); 
        }

        int res = 128;
        Texture2D gridTex = new Texture2D(res, res, TextureFormat.RGBA32, true);
        gridTex.wrapMode = TextureWrapMode.Repeat;
        gridTex.filterMode = FilterMode.Trilinear;
        Color transp = new Color(0, 0, 0, 0);

        int thickness = 2; // thinner crisp lines
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                if (x < thickness || y < thickness || x > res - thickness || y > res - thickness)
                    gridTex.SetPixel(x, y, gridLineColor);
                else
                    gridTex.SetPixel(x, y, transp);
            }
        }
        gridTex.Apply();

        Material gridMat = new Material(Shader.Find("UI/Default"));
        gridMat.mainTexture = gridTex;
        
        Vector3 globalScale = transform.lossyScale;
        gridMat.mainTextureScale = new Vector2(globalScale.x * gridDensityMultiplier, globalScale.z * gridDensityMultiplier); 

        gridObj.GetComponent<Renderer>().material = gridMat;
        gridObj.GetComponent<Renderer>().material.renderQueue = 3000;
    }
}
