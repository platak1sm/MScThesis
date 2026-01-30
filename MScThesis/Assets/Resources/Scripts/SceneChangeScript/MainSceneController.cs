using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class MainSceneController : MonoBehaviour
{
    public Image fadePanel;
    public float fadeDuration = 1f;

    void Start()
    {
        if (!fadePanel) Debug.LogError("Fade Panel missing!");
        StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        float elapsedTime = 0f;
        Color startColor = fadePanel.color; // Black, Alpha 1
        Color endColor = new Color(0, 0, 0, 0); // Transparent

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            fadePanel.color = Color.Lerp(startColor, endColor, elapsedTime / fadeDuration);
            yield return null;
        }

        // Optional: Deactivate canvas to save performance
        fadePanel.gameObject.SetActive(false);
        Debug.Log("Fade in complete");
    }
}