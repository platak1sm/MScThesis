using UnityEngine;

public class ShadowFollower : MonoBehaviour
{
    public Transform targetObject; // The floating cube this shadow belongs to
    public float tableHeight = 0.75f; 

    void Update()
    {
        if (targetObject != null)
        {
            // Always stay exactly under the object, but flat on the table
            transform.position = new Vector3(targetObject.position.x, tableHeight, targetObject.position.z);
            
            // Match rotation (Yaw only)
            transform.rotation = Quaternion.Euler(0, targetObject.eulerAngles.y, 0);
            
            // Match Scale (Dynamic sizing)
            float size = Mathf.Max(targetObject.localScale.x, targetObject.localScale.z);
            transform.localScale = new Vector3(size, 0.01f, size);
        }
        else
        {
            // If the object is deleted, destroy the shadow too
            Destroy(gameObject);
        }
    }
}