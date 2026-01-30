package com.AU.Project_3DUI;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.util.Log;
import com.unity3d.player.UnityPlayer;

public class AdbReceiver extends BroadcastReceiver {
    private static final String TAG = "AdbReceiver";
    private static final String ACTION = "com.AU.Project_3DUI.CHANGE_SCENE";

    @Override
    public void onReceive(Context context, Intent intent) {
        if (intent == null) return;

        String action = intent.getAction();
        if (ACTION.equals(action)) {
            String sceneName = intent.getStringExtra("sceneName");
            Log.d(TAG, "Received scene change request: " + sceneName);

            if (sceneName != null && !sceneName.isEmpty()) {
                // Call into Unity C# static method
                UnityPlayer.UnitySendMessage("SceneManagerAdb", "EnqueueSceneChange", sceneName);
            }
        }
    }
}