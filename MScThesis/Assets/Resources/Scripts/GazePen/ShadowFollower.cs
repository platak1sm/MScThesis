using UnityEngine;

public class ShadowFollower : MonoBehaviour
{
    public Transform targetObject; 
    public float tableHeight = 0.75f; 

    void Update()
    {
        if (targetObject != null)
        {
            Collider[] colliders = targetObject.GetComponentsInChildren<Collider>();
            if (colliders.Length > 0)
            {
                Bounds combinedBounds = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++)
                {
                    if (!colliders[i].isTrigger) combinedBounds.Encapsulate(colliders[i].bounds);
                }
                
                // Use true physical world bounds instead of arbitrary Transform scales!
                float size = Mathf.Max(combinedBounds.size.x, combinedBounds.size.z);
                transform.localScale = new Vector3(size, 0.01f, size);
                
                // Keep the shadow directly under the true center of the object, ignoring offset pivots!
                transform.position = new Vector3(combinedBounds.center.x, tableHeight, combinedBounds.center.z);
            }
            else
            {
                // Fallback just in case they have no collider
                float size = Mathf.Max(targetObject.localScale.x, targetObject.localScale.z);
                transform.localScale = new Vector3(size, 0.01f, size);
                transform.position = new Vector3(targetObject.position.x, tableHeight, targetObject.position.z);
            }
            
            // rotation 
            transform.rotation = Quaternion.Euler(0, targetObject.eulerAngles.y, 0);
        }
        else
        {
            // if the object is deleted, destroy the shadow too
            Destroy(gameObject);
        }
    }
}