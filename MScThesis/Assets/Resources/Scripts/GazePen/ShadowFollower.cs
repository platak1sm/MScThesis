using UnityEngine;

public class ShadowFollower : MonoBehaviour
{
    public Transform targetObject; 
    public float tableHeight = 0.75f; 

    void Update()
    {
        if (targetObject != null)
        {
            // under the object
            transform.position = new Vector3(targetObject.position.x, tableHeight, targetObject.position.z);
            
            // rotation 
            transform.rotation = Quaternion.Euler(0, targetObject.eulerAngles.y, 0);
            
            // scale 
            float size = Mathf.Max(targetObject.localScale.x, targetObject.localScale.z);
            transform.localScale = new Vector3(size, 0.01f, size);
        }
        else
        {
            // if the object is deleted, destroy the shadow too
            Destroy(gameObject);
        }
    }
}