using UnityEngine;

public class TableAutoCalibrator : MonoBehaviour
{
    [Header("References")]
    public Transform tablePlane;       
    public Transform virtualPenTip;    
    
    [Header("Settings")]
    public float requiredPressure = 0.5f; 
    public float cooldownTime = 1.0f;     
    
    private float lastCalibrateTime = 0;

    void Update()
    {
        // Safety: 'A' Button + Pressure
        bool isHoldingA = OVRInput.Get(OVRInput.Button.One, OVRInput.Controller.RTouch);
        float currentPressure = OVRInput.Get(OVRInput.Axis1D.PrimaryStylusForce, OVRInput.Controller.RTouch);

        if (isHoldingA && currentPressure > requiredPressure && Time.time > lastCalibrateTime + cooldownTime)
        {
            Calibrate();
        }
    }

    void Calibrate()
    {
        float newHeight = virtualPenTip.position.y;
        float oldHeight = tablePlane.position.y;
        float delta = newHeight - oldHeight;

        // 1. Move Table
        Vector3 tablePos = tablePlane.position;
        tablePlane.position = new Vector3(tablePos.x, newHeight, tablePos.z);

        // 2. Move ALL Interactable Objects
        GameObject[] interactables = GameObject.FindGameObjectsWithTag("Interactable");
        foreach (GameObject obj in interactables)
        {
            obj.transform.position += new Vector3(0, delta, 0);
        }

        // 3. Update ALL Shadows (To set their new ground floor)
        ShadowFollower[] shadows = FindObjectsByType<ShadowFollower>(FindObjectsSortMode.None);
        foreach (ShadowFollower shadow in shadows)
        {
            shadow.tableHeight = newHeight;
        }
        
        // 4. Update the Scene Manager (for future spawns)
        ShadowManager manager = FindFirstObjectByType<ShadowManager>();
        if (manager != null)
        {
            manager.globalTableHeight = newHeight + 0.001f;
        }

        // Feedback
        OVRInput.SetControllerVibration(1, 1, OVRInput.Controller.RTouch);
        Invoke("StopVibration", 0.2f);
        lastCalibrateTime = Time.time;
        
        Debug.Log("Calibrated " + interactables.Length + " objects to height: " + newHeight);
    }

    void StopVibration()
    {
        OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
    }
}