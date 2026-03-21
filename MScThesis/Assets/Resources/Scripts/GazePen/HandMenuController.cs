using UnityEngine;

public class HandMenuController : MonoBehaviour
{
    [SerializeField] private Transform leftControllerAnchor; 
    [SerializeField] private GameObject menuCanvas;
    [SerializeField] private Vector3 offset = new Vector3(0, 0.15f, 0.05f); 

    private bool isMenuVisible = false;

    void Update()
    {
        if (menuCanvas == null) return;
 
        // Toggle on Left Touch X Button explicitly
        if (OVRInput.GetDown(OVRInput.RawButton.X, OVRInput.Controller.LTouch))
        {
            isMenuVisible = !isMenuVisible;
            menuCanvas.SetActive(isMenuVisible);
        }

        if (isMenuVisible)
        { 
            Vector3 anchorPos = transform.position;
            Quaternion anchorRot = transform.rotation;

            // Follow controller or palm
            if (leftControllerAnchor != null)
            {
                anchorPos = leftControllerAnchor.position;
                anchorRot = leftControllerAnchor.rotation;
            }

            transform.position = anchorPos + (anchorRot * offset);
            
            // Face user
            if (Camera.main != null)
            {
                transform.LookAt(Camera.main.transform);
                transform.Rotate(0, 180, 0); // Flip UI to face camera
            }
        }
    }
}