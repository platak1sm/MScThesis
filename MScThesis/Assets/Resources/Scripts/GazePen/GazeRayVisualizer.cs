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

        if (gazeProvider == null) gazeProvider = FindFirstObjectByType<GazeProvider>();
        if (eyeCamera == null) eyeCamera = Camera.main;
    }

    void Update()
    {
        if (gazeProvider == null || eyeCamera == null) return;

        line.SetPosition(0, eyeCamera.transform.position);

        if (gazeProvider.DidHit && gazeProvider.Hit.collider != null)
        {
            line.SetPosition(1, gazeProvider.Hit.point);
        }
        else
        {
            line.SetPosition(1, eyeCamera.transform.position + gazeProvider.GazeDirection * defaultRayLength);
        }
    }
}
