using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OVR;

public class GazeProvider : MonoBehaviour
{
    [SerializeField, Tooltip("The LayerMasks used for gaze raycasting.")]
    public LayerMask RaycastLayerMask = Physics.DefaultRaycastLayers;

    [SerializeField, Tooltip("Optional LineRenderer to visualize the gaze ray.")]
    public LineRenderer GazeRayVisualizer;

    [SerializeField, Tooltip("How long (in ms) to keep gaze history for lookback.")]
    public long HistoryDurationMs = 300;

    [Header("Filtering")]
    public bool UseFiltering = true;
    public float FilterMinCutoff = 0.05f;
    public float FilterBeta = 10f;
    public float FilterDCutoff = 1f;

    [Header("Gaze Correction")]
    public bool UseCorrection = false;
    public int FrameOffset = 7;

    private OVREyeGaze leftEye;
    private OVREyeGaze rightEye;

    public Vector3 GazeOrigin { get; private set; }
    public Vector3 GazeDirection { get; private set; }
    public bool DidHit { get; private set; }
    public RaycastHit Hit { get; private set; }
    public GameObject GazeTarget { get; private set; }

    private readonly Queue<GazeHistoryEntry> gazeHistory = new Queue<GazeHistoryEntry>();

    private OneEuroFilterVector3 dirFilter;
    private OneEuroFilterVector3 posFilter;

    private List<Quaternion> headRotationBuffer = new List<Quaternion>();

    [System.Serializable]
    public class GazeHistoryEntry
    {
        public long timestamp;
        public GameObject gazeTarget;
        public RaycastHit hitInfo;

        public GazeHistoryEntry(long ts, GameObject target, RaycastHit hit)
        {
            timestamp = ts;
            gazeTarget = target;
            hitInfo = hit;
        }
    }

    void Start()
    {
        OVREyeGaze[] eyes = FindObjectsByType<OVREyeGaze>(FindObjectsSortMode.None);
        foreach (var eye in eyes)
        {
            if (eye.Eye == OVREyeGaze.EyeId.Left) leftEye = eye;
            else if (eye.Eye == OVREyeGaze.EyeId.Right) rightEye = eye;
        }

        if (leftEye == null || rightEye == null)
        {
            Debug.LogError("GazeProvider: Missing OVREyeGaze components for left or right eye. Ensure they are set up in the scene.");
            enabled = false;
            return;
        }

        dirFilter = new OneEuroFilterVector3(FilterMinCutoff, FilterBeta, FilterDCutoff);
        posFilter = new OneEuroFilterVector3(FilterMinCutoff, FilterBeta, FilterDCutoff);

        if (GazeRayVisualizer != null)
        {
            GazeRayVisualizer.positionCount = 2;
            GazeRayVisualizer.startWidth = 0.01f;
            GazeRayVisualizer.endWidth = 0.01f;
        }
    }

    void Update()
    {
        if (leftEye == null || rightEye == null) return;

        OVRPlugin.EyeGazesState eyeGazesState = new OVRPlugin.EyeGazesState();
        if (OVRPlugin.GetEyeGazesState(OVRPlugin.Step.Render, -1, ref eyeGazesState))
        {
            OVRPlugin.EyeGazeState leftGaze = eyeGazesState.EyeGazes[(int)OVRPlugin.Eye.Left];
            OVRPlugin.EyeGazeState rightGaze = eyeGazesState.EyeGazes[(int)OVRPlugin.Eye.Right];

            if (leftGaze.Confidence >= leftEye.ConfidenceThreshold && rightGaze.Confidence >= rightEye.ConfidenceThreshold)
            {
                // Convert to Unity poses (OVRPose to Unity)
                OVRPose leftPose = leftGaze.Pose.ToOVRPose();
                OVRPose rightPose = rightGaze.Pose.ToOVRPose();

                GazeOrigin = Vector3.Lerp(leftPose.position, rightPose.position, 0.5f);
                Quaternion combinedRot = Quaternion.Slerp(leftPose.orientation, rightPose.orientation, 0.5f).normalized;
                GazeDirection = combinedRot * Vector3.forward;
            }
            else
            {
                // Fallback to head direction if low confidence
                GazeOrigin = Camera.main.transform.position;
                GazeDirection = Camera.main.transform.forward;
                Debug.LogWarning("Low eye gaze confidence, falling back to head direction.");
            }
        }
        else
        {
            Debug.LogError("Failed to get eye gazes state from OVRPlugin.");
            // Fallback
            GazeOrigin = Camera.main.transform.position;
            GazeDirection = Camera.main.transform.forward;
        }

        float rate = 1f / Time.deltaTime;

        // Apply filtering if enabled
        if (UseFiltering)
        {
            GazeDirection = dirFilter.Filter(GazeDirection, rate);
            GazeOrigin = posFilter.Filter(GazeOrigin, rate);
        }

        // Apply gaze correction if enabled
        if (UseCorrection && headRotationBuffer.Count == FrameOffset)
        {
            Quaternion oldHeadRot = headRotationBuffer[0];
            Quaternion newHeadRot = headRotationBuffer[headRotationBuffer.Count - 1];
            Quaternion headRotOffset = oldHeadRot * Quaternion.Inverse(newHeadRot);
            GazeDirection = headRotOffset * GazeDirection;
        }

        // Update head rotation buffer
        Quaternion currentHeadRot = Camera.main.transform.rotation;
        headRotationBuffer.Add(currentHeadRot);
        if (headRotationBuffer.Count > FrameOffset)
        {
            headRotationBuffer.RemoveAt(0);
        }

        // Perform raycast
        Ray gazeRay = new Ray(GazeOrigin, GazeDirection);
        DidHit = Physics.Raycast(gazeRay, out RaycastHit hit, Mathf.Infinity, RaycastLayerMask);
        Hit = hit;
        GazeTarget = DidHit ? hit.collider.gameObject : null;

        // Record history
        long currentTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        gazeHistory.Enqueue(new GazeHistoryEntry(currentTime, GazeTarget, Hit));

        while (gazeHistory.Count > 0 && currentTime - gazeHistory.Peek().timestamp > HistoryDurationMs)
        {
            gazeHistory.Dequeue();
        }

        // Update visualizer if present
        if (GazeRayVisualizer != null)
        {
            GazeRayVisualizer.SetPosition(0, GazeOrigin);
            Vector3 endPoint = DidHit ? Hit.point : GazeOrigin + GazeDirection * 5f;
            GazeRayVisualizer.SetPosition(1, endPoint);
        }
    }

    public GazeHistoryEntry GetGazeHistoryEntry(long lookbackMs)
    {
        if (gazeHistory.Count == 0) return null;

        long currentTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long targetTime = currentTime - lookbackMs;

        GazeHistoryEntry bestMatch = null;
        foreach (var entry in gazeHistory)
        {
            if (entry.timestamp <= targetTime)
            {
                bestMatch = entry;
            }
            else
            {
                break;
            }
        }

        return bestMatch ?? gazeHistory.Peek();
    }
}