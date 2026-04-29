using UnityEngine;

public class EyeTrackingInitializer : MonoBehaviour
{
    void Start()
    {
        OVRManager.eyeTrackedFoveatedRenderingEnabled = true;
    }
}