using UnityEngine;

public class ShadowManager : MonoBehaviour
{
    public GameObject shadowPrefab; 
    public float globalTableHeight = 0.751f;

    void Start()
    {
        // 1. Find all objects tagged "Interactable"
        GameObject[] objects = GameObject.FindGameObjectsWithTag("Interactable");

        foreach (GameObject obj in objects)
        {
            // 2. Spawn a shadow for each one
            GameObject newShadow = Instantiate(shadowPrefab);
            
            // 3. Setup the Shadow Script
            ShadowFollower follower = newShadow.GetComponent<ShadowFollower>();
            if (follower != null)
            {
                follower.targetObject = obj.transform;
                follower.tableHeight = globalTableHeight;
            }
        }
    }
}