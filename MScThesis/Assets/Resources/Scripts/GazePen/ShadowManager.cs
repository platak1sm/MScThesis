using UnityEngine;

public class ShadowManager : MonoBehaviour
{
    public GameObject shadowPrefab; 
    public float globalTableHeight = 0.751f;

    void Start()
    {
        // find all objects tagged "Interactable"
        GameObject[] objects = GameObject.FindGameObjectsWithTag("Interactable");

        foreach (GameObject obj in objects)
        {
            // Skip UI Buttons that happen to be tagged "Interactable"
            if (obj.GetComponent<VRButton>() != null) continue;

            // spawn a shadow for each one
            GameObject newShadow = Instantiate(shadowPrefab);
            
            //setup the shadow script
            ShadowFollower follower = newShadow.GetComponent<ShadowFollower>();
            if (follower != null)
            {
                follower.targetObject = obj.transform;
                follower.tableHeight = globalTableHeight;
            }
        }
    }
}