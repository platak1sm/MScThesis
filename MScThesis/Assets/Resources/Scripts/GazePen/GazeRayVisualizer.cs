using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class GazeRayVisualizer : MonoBehaviour
{
    public GazeProvider gazeProvider;
    public Camera eyeCamera;
    
    [Tooltip("How far the ray extends if we look at empty space")]
    public float defaultRayLength = 2.0f;
    
    private LineRenderer line;

    void Start()
    {
        line = GetComponent<LineRenderer>();
        line.positionCount = 2;
        
        // Make the line extremely thin natively
        line.startWidth = 0.002f;
        line.endWidth = 0.002f;

        if (gazeProvider == null) gazeProvider = FindFirstObjectByType<GazeProvider>();
        if (eyeCamera == null) eyeCamera = Camera.main;
    }

    void Update()
    {
        if (gazeProvider == null || eyeCamera == null) return;

        // Offset the origin exactly 10cm beneath the headset to simulate cinematic chest projection
        Vector3 originPos = eyeCamera.transform.position + new Vector3(0, -0.1f, 0);
        line.SetPosition(0, originPos);

        if (gazeProvider.DidHit && gazeProvider.Hit.collider != null)
        {
            line.SetPosition(1, gazeProvider.Hit.point);
        }
        else
        {
            line.SetPosition(1, originPos + gazeProvider.GazeDirection * defaultRayLength);
        }
    }
}
