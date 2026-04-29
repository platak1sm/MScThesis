using UnityEngine;
using UnityEngine.Events;

public class VRButton : MonoBehaviour
{
    public UnityEvent OnClick;
    private Renderer rend;
    private Color originalColor;

    void Start()
    {
        rend = GetComponent<Renderer>();
        if(rend) originalColor = rend.material.color;
    }

    public void Click()
    {
        OnClick.Invoke();
        if(rend) StartCoroutine(Flash());
    }

    private System.Collections.IEnumerator Flash()
    {
        rend.material.color = Color.green;
        yield return new WaitForSeconds(0.2f);
        rend.material.color = originalColor;
    }
}