using System.Collections;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class MobileARModeController : MonoBehaviour
{
    public enum MobileTrackingMode
    {
        WorldHands,
        FaceSubtitle
    }

    private const string LogTag = "[MobileARMode]";

    [Header("Mode")]
    public MobileTrackingMode startupMode = MobileTrackingMode.WorldHands;
    public bool applyStartupModeOnStart = true;
    public bool restartSessionOnModeChange = true;
    public float sessionResetDelaySeconds = 0.15f;
    public bool fixedSubtitleFallbackWhenFaceMissing = true;

    [Header("References")]
    public GameObject facePrefab;
    public ARSession arSession;
    public XROrigin xrOrigin;
    public ARCameraManager cameraManager;
    public ARFaceManager faceManager;
    public ARPlaneManager planeManager;
    public ARRaycastManager raycastManager;
    public ARCameraHandLandmarkerRunner handLandmarkerRunner;
    public MobileARFaceTrackingRunner faceTrackingRunner;
    public MobileARHeadTracker headTracker;
    public SpeechToTextManager speechToTextManager;
    public TMP_Text statusText;

    [Header("Debug")]
    public bool showDebugStatus = true;
    public float statusRefreshSeconds = 0.5f;

    private GameObject runtimeFacePrefab;
    private MobileTrackingMode currentMode;
    private Coroutine switchRoutine;
    private float nextStatusTime;

    public MobileTrackingMode CurrentMode => currentMode;

    private void Awake()
    {
        AutoBind();
        EnsureFaceManager();
        EnsureFacePrefab();
    }

    private void Start()
    {
        if (applyStartupModeOnStart)
            SetMode(startupMode);
    }

    private void Update()
    {
        if (!showDebugStatus || Time.unscaledTime < nextStatusTime)
            return;

        nextStatusTime = Time.unscaledTime + Mathf.Max(0.1f, statusRefreshSeconds);
        RefreshStatus();
    }

    [ContextMenu("Set World Hands Mode")]
    public void SetWorldHandsMode()
    {
        SetMode(MobileTrackingMode.WorldHands);
    }

    [ContextMenu("Set Face Subtitle Mode")]
    public void SetFaceSubtitleMode()
    {
        SetMode(MobileTrackingMode.FaceSubtitle);
    }

    [ContextMenu("Toggle Mode")]
    public void ToggleMode()
    {
        SetMode(currentMode == MobileTrackingMode.FaceSubtitle
            ? MobileTrackingMode.WorldHands
            : MobileTrackingMode.FaceSubtitle);
    }

    public void SetMode(MobileTrackingMode mode)
    {
        AutoBind();
        EnsureFaceManager();
        EnsureFacePrefab();

        if (switchRoutine != null)
            StopCoroutine(switchRoutine);

        switchRoutine = StartCoroutine(ApplyModeRoutine(mode));
    }

    private IEnumerator ApplyModeRoutine(MobileTrackingMode mode)
    {
        currentMode = mode;
        bool useFaceMode = mode == MobileTrackingMode.FaceSubtitle;

        SetEnabled(faceManager, useFaceMode);
        SetEnabled(handLandmarkerRunner, !useFaceMode);
        SetEnabled(planeManager, !useFaceMode);
        SetEnabled(raycastManager, !useFaceMode);

        if (faceTrackingRunner != null)
        {
            faceTrackingRunner.faceManager = faceManager;
            faceTrackingRunner.trackingCamera = cameraManager != null ? cameraManager.GetComponent<Camera>() : Camera.main;
            faceTrackingRunner.fallbackWhenARFaceMissing = fixedSubtitleFallbackWhenFaceMissing;
            faceTrackingRunner.trackingSource = useFaceMode
                ? MobileARFaceTrackingRunner.TrackingSource.ARFaceManager
                : MobileARFaceTrackingRunner.TrackingSource.ScreenCenterFallback;
        }

        if (headTracker != null)
        {
            headTracker.useFaceTrackingPosition = useFaceMode;
            headTracker.useFixedPositionWhenFaceMissing = fixedSubtitleFallbackWhenFaceMissing;
        }

        if (speechToTextManager != null)
            speechToTextManager.useFaceTrackingSubtitle = useFaceMode;

        if (cameraManager != null)
            cameraManager.requestedFacingDirection = useFaceMode ? CameraFacingDirection.User : CameraFacingDirection.World;

        if (restartSessionOnModeChange && arSession != null)
        {
            arSession.enabled = false;
            float delay = Mathf.Max(0f, sessionResetDelaySeconds);
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);
            else
                yield return null;

            arSession.Reset();
            arSession.enabled = true;
        }

        Debug.Log($"{LogTag} switched to {mode}.", this);
        RefreshStatus();
        switchRoutine = null;
    }

    private void EnsureFaceManager()
    {
        if (faceManager != null)
            return;

        if (xrOrigin == null)
            xrOrigin = FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);

        if (xrOrigin == null)
        {
            Debug.LogWarning($"{LogTag} No XROrigin found; cannot create ARFaceManager.", this);
            return;
        }

        faceManager = xrOrigin.GetComponent<ARFaceManager>();
        if (faceManager == null)
            faceManager = xrOrigin.gameObject.AddComponent<ARFaceManager>();
    }

    private void EnsureFacePrefab()
    {
        if (faceManager == null || faceManager.facePrefab != null)
            return;

        if (facePrefab != null)
        {
            faceManager.facePrefab = facePrefab;
            return;
        }

        runtimeFacePrefab = new GameObject("Runtime Invisible AR Face");
        runtimeFacePrefab.SetActive(false);
        runtimeFacePrefab.hideFlags = HideFlags.HideAndDontSave;
        runtimeFacePrefab.AddComponent<ARFace>();
        faceManager.facePrefab = runtimeFacePrefab;
    }

    private void AutoBind()
    {
        if (arSession == null)
            arSession = FindFirstObjectByType<ARSession>(FindObjectsInactive.Include);

        if (xrOrigin == null)
            xrOrigin = FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);

        if (cameraManager == null)
            cameraManager = FindFirstObjectByType<ARCameraManager>(FindObjectsInactive.Include);

        if (faceManager == null)
            faceManager = FindFirstObjectByType<ARFaceManager>(FindObjectsInactive.Include);

        if (planeManager == null)
            planeManager = FindFirstObjectByType<ARPlaneManager>(FindObjectsInactive.Include);

        if (raycastManager == null)
            raycastManager = FindFirstObjectByType<ARRaycastManager>(FindObjectsInactive.Include);

        if (handLandmarkerRunner == null)
            handLandmarkerRunner = FindFirstObjectByType<ARCameraHandLandmarkerRunner>(FindObjectsInactive.Include);

        if (faceTrackingRunner == null)
            faceTrackingRunner = FindFirstObjectByType<MobileARFaceTrackingRunner>(FindObjectsInactive.Include);

        if (headTracker == null)
            headTracker = FindFirstObjectByType<MobileARHeadTracker>(FindObjectsInactive.Include);

        if (speechToTextManager == null)
            speechToTextManager = FindFirstObjectByType<SpeechToTextManager>(FindObjectsInactive.Include);

        if (statusText == null)
            statusText = FindStatusText();
    }

    private void RefreshStatus()
    {
        string requestedFacing = cameraManager != null ? cameraManager.requestedFacingDirection.ToString() : "None";
        string currentFacing = cameraManager != null ? cameraManager.currentFacingDirection.ToString() : "None";
        bool faceAssigned = faceManager != null;
        bool faceEnabled = faceManager != null && faceManager.enabled;
        int faceCount = faceTrackingRunner != null ? faceTrackingRunner.FaceTrackableCount : 0;
        bool realFace = faceTrackingRunner != null && faceTrackingRunner.HasRealFaceTracking;
        string subtitlePlacement = speechToTextManager != null && speechToTextManager.useFaceTrackingSubtitle ? "Face" : "Fixed";

        string message =
            $"Mode: {currentMode}\n" +
            $"Camera: requested {requestedFacing}, current {currentFacing}\n" +
            $"FaceManager: assigned {YesNo(faceAssigned)}, enabled {YesNo(faceEnabled)}, faces {faceCount}, real {YesNo(realFace)}\n" +
            $"Subtitle: {subtitlePlacement}";

        if (statusText != null)
            statusText.text = message;

        Debug.Log($"{LogTag} {message.Replace('\n', ' ')}", this);
    }

    private static TMP_Text FindStatusText()
    {
        TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (TMP_Text text in texts)
        {
            if (text.name.Contains("Debug") || text.name.Contains("Status"))
                return text;
        }

        return null;
    }

    private static void SetEnabled(Behaviour behaviour, bool enabled)
    {
        if (behaviour != null)
            behaviour.enabled = enabled;
    }

    private static string YesNo(bool value)
    {
        return value ? "yes" : "no";
    }

    private void OnDestroy()
    {
        if (runtimeFacePrefab != null)
            Destroy(runtimeFacePrefab);
    }
}
