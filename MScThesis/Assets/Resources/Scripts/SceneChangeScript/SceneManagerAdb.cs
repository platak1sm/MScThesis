using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

/// <summary>
/// Listens for ADB broadcasts and changes scenes accordingly.
/// Broadcast example:
/// adb shell am broadcast -a com.AU.Project_3DUI.CHANGE_SCENE --es sceneName "Technique3"
/// </summary>
public class SceneManagerAdb : MonoBehaviour
{
    private const string SCENE_CHANGE_ACTION = "com.AU.Project_3DUI.CHANGE_SCENE";

    private AndroidJavaObject unityActivity;
    private AndroidJavaObject receiverInstance;

    private static readonly Queue<string> sceneQueue = new Queue<string>();
    private static readonly object queueLock = new object();

    void Start()
    {
        DontDestroyOnLoad(gameObject);

        if (Application.platform == RuntimePlatform.Android)
        {
            try
            {
                // Get Unity activity
                AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

                // Create receiver and intent filter
                receiverInstance = new AndroidJavaObject("com.AU.Project_3DUI.AdbReceiver");
                AndroidJavaObject intentFilter = new AndroidJavaObject("android.content.IntentFilter", SCENE_CHANGE_ACTION);

                // Register the receiver on the main Unity activity
                unityActivity.Call<AndroidJavaObject>("registerReceiver", receiverInstance, intentFilter);

                Debug.Log("[AdbSceneChanger] Broadcast receiver registered successfully.");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[AdbSceneChanger] Registration failed: " + e);
            }
        }
        else
        {
            Debug.Log("[AdbSceneChanger] Not running on Android. Receiver not registered.");
        }
    }

    void Update()
    {
        lock (queueLock)
        {
            if (sceneQueue.Count > 0)
            {
                string nextScene = sceneQueue.Dequeue();
                Debug.Log($"[AdbSceneChanger] Loading scene: {nextScene}");
                SceneManager.LoadScene(nextScene);
            }
        }
    }

    void OnDestroy()
    {
        if (Application.platform == RuntimePlatform.Android && unityActivity != null && receiverInstance != null)
        {
            try
            {
                unityActivity.Call("unregisterReceiver", receiverInstance);
                Debug.Log("[AdbSceneChanger] Broadcast receiver unregistered.");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[AdbSceneChanger] Failed to unregister broadcast receiver: " + e);
            }
        }
    }

    /// <summary>
    /// Called from Java when a broadcast with a valid scene name is received.
    /// </summary>
    public void EnqueueSceneChange(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;

        lock (queueLock)
        {
            sceneQueue.Enqueue(sceneName);
        }
    }
}