using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Controls the player's top-left World Space Life/Oxygen HUD, dynamically drains
/// life/oxygen while inside the smoke-filled office with active fire, progressively
/// closes the player's eyes (fade-to-black blackout) when oxygen drops to <= 3%,
/// triggers the VR World Space GAME OVER panel in complete darkness at 0% oxygen,
/// and performs a full reset to initial spawn on retry.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class PlayerSmokeHealthHUD : MonoBehaviour
{
    [Header("Life / Oxygen Parameters")]
    [Tooltip("Maximum life/oxygen capacity (100%).")]
    [SerializeField]
    private float maxOxygen = 300f;

    [Tooltip("Current remaining life/oxygen.")]
    [SerializeField]
    private float currentOxygen = 300f;

    [Tooltip("Oxygen/life units lost per second at full smoke density.")]
    [SerializeField]
    private float baseDrainRate = 4.0f;

    [Tooltip("Oxygen/life units recovered per second when the player leaves the smoke-filled office.")]
    [SerializeField]
    private float outsideRecoveryRate = 4.0f;

    [Header("Smoke Cough Audio (Every 25% Life Lost)")]
    [Tooltip("AudioSource on Main Camera playing the player's cough sound (non-looping).")]
    [SerializeField]
    private AudioSource coughAudioSource;

    [Tooltip("AudioClip for the male cough sound (Assets/Audio/TosHombre.wav).")]
    [SerializeField]
    private AudioClip coughAudioClip;

    [Tooltip("Volume of the cough sound when triggered at each 25% milestone.")]
    [Range(0.1f, 1f)]
    [SerializeField]
    private float coughVolume = 0.90f;

    [Tooltip("Normalized life/oxygen thresholds (75%, 50%, 25%) where the player coughs once.")]
    [SerializeField]
    private float[] coughThresholds = new float[] { 0.75f, 0.50f, 0.25f };

    [Header("Dying Eye-Close / Blackout Effect (<= 3% Oxygen)")]
    [Tooltip("Normalized oxygen threshold (0.03 = 3%) below which the player's eyes progressively close to black.")]
    [Range(0.01f, 0.20f)]
    [SerializeField]
    private float blackoutStartThreshold = 0.03f;

    [Tooltip("World Space Canvas covering the camera view for the closing-eyes blackout transition.")]
    [SerializeField]
    private Canvas blackoutCanvas;

    [Tooltip("Full-screen black Image that darkens the player's vision as oxygen approaches 0%.")]
    [SerializeField]
    private Image darknessVeilImage;

    [Tooltip("Top black eyelid RectTransform that descends toward the center of vision.")]
    [SerializeField]
    private RectTransform topEyelidRect;

    [Tooltip("Bottom black eyelid RectTransform that rises toward the center of vision.")]
    [SerializeField]
    private RectTransform bottomEyelidRect;

    [Header("Hazard & Environment References")]
    [Tooltip("Trigger BoxCollider defining the smoke-filled office interior.")]
    [SerializeField]
    private BoxCollider officeSmokeBounds;

    [Tooltip("Root transform containing all ambient office smoke ParticleSystems.")]
    [SerializeField]
    private Transform officeSmokeRoot;

    [Tooltip("Reference to the office fire hazard(s). Auto-detected if empty.")]
    [SerializeField]
    private ExtinguishableFire[] officeFires;

    [Tooltip("Reference to the fire extinguisher controller. Auto-detected if empty.")]
    [SerializeField]
    private ExtinguisherController extinguisher;

    [Tooltip("Reference to the player's XR Origin root transform. Auto-detected if empty.")]
    [SerializeField]
    private Transform xrOriginTransform;

    [Tooltip("Player camera used to check position inside the office and anchor the top-left HUD.")]
    [SerializeField]
    private Camera playerCamera;

    [Header("World Space Top-Left HUD References")]
    [Tooltip("World Space Canvas attached to Main Camera for the top-left life/oxygen bar.")]
    [SerializeField]
    private Canvas hudCanvas;

    [Tooltip("Outer border Image of the life/oxygen HUD.")]
    [SerializeField]
    private Image borderImage;

    [Tooltip("Filled UI Image representing remaining life/oxygen.")]
    [SerializeField]
    private Image oxygenFillImage;

    [Tooltip("TextMeshPro label showing life/oxygen percentage and state.")]
    [SerializeField]
    private TMP_Text oxygenStatusText;

    [Header("Top-Left Viewport Placement (World Space)")]
    [Tooltip("Distance in meters in front of the VR camera.")]
    [SerializeField]
    private float hudDistance = 0.85f;

    [Tooltip("Normalized viewport X (0 = left edge, 1 = right edge).")]
    [Range(0.01f, 0.5f)]
    [SerializeField]
    private float viewportX = 0.03f;

    [Tooltip("Normalized viewport Y (0 = bottom edge, 1 = top edge).")]
    [Range(0.5f, 0.99f)]
    [SerializeField]
    private float viewportY = 0.94f;

    [Header("Game Over VR Panel References")]
    [Tooltip("World Space Canvas displayed in front of the player when oxygen reaches 0%.")]
    [SerializeField]
    private Canvas gameOverCanvas;

    [Tooltip("UI Button to retry/restart the experience.")]
    [SerializeField]
    private Button retryButton;

    [Tooltip("XRSimpleInteractable on the retry button for direct VR ray/poke activation.")]
    [SerializeField]
    private XRSimpleInteractable retryXrInteractable;

    [Tooltip("Distance in meters in front of the player camera where the Game Over panel appears.")]
    [SerializeField]
    private float gameOverPanelDistance = 1.10f;

    [Tooltip("If true, reloads the Unity scene via SceneManager after resetting XR simulator pose.")]
    [SerializeField]
    private bool reloadSceneOnRetry = false;

    [Header("HUD Colors")]
    [SerializeField]
    private Color healthyColor = new Color(0.12f, 0.78f, 0.98f, 1.0f);

    [SerializeField]
    private Color cautionColor = new Color(1.0f, 0.65f, 0.08f, 1.0f);

    [SerializeField]
    private Color criticalColor = new Color(0.95f, 0.18f, 0.18f, 1.0f);

    [Header("Runtime State (Read Only)")]
    [SerializeField]
    private bool isPlayerInSmokeZone = true;

    [SerializeField]
    private float currentSmokeIntensity = 1.0f;

    [SerializeField]
    private bool isGameOver = false;

    [SerializeField]
    private float currentBlackoutAmount = 0f;

    // Cached initial transform & XRDeviceSimulator states for exact spawn restoration
    private bool initialTransformsCached;
    private Vector3 initialPlayerPos = new Vector3(-3.50f, 0.00f, 2.80f);
    private Quaternion initialPlayerRot = Quaternion.Euler(0f, 180f, 0f);
    private Vector3 initialCameraOffsetLocalPos = Vector3.zero;
    private Quaternion initialCameraOffsetLocalRot = Quaternion.identity;
    private Vector3 initialCameraLocalPos = new Vector3(0f, 1.36144f, 0f);
    private Quaternion initialCameraLocalRot = Quaternion.identity;
    private Vector3 initialLeftCtrlLocalPos = new Vector3(-0.1f, 1.05f, 0.3f);
    private Quaternion initialLeftCtrlLocalRot = Quaternion.identity;
    private Vector3 initialRightCtrlLocalPos = new Vector3(0.1f, 1.05f, 0.3f);
    private Quaternion initialRightCtrlLocalRot = Quaternion.identity;
    private Vector3 initialExtinguisherPos = new Vector3(-4.798f, 1.027f, 3.850f);
    private Quaternion initialExtinguisherRot = Quaternion.Euler(270f, 180f, 0f);

    // XRDeviceSimulator reflection cache
    private Component xrSimulatorComp;
    private Component xrLifecycleComp;
    private FieldInfo hmdStateField;
    private FieldInfo leftCtrlStateField;
    private FieldInfo rightCtrlStateField;
    private FieldInfo centerEyeEulerField;
    private FieldInfo leftCtrlEulerField;
    private FieldInfo rightCtrlEulerField;
    private MethodInfo applyHmdStateMethod;
    private MethodInfo applyCtrlStateMethod;
    private object initialHmdState;
    private object initialLeftCtrlState;
    private object initialRightCtrlState;
    private Vector3 initialCenterEyeEuler;
    private Vector3 initialLeftCtrlEuler;
    private Vector3 initialRightCtrlEuler;

    private readonly Dictionary<ParticleSystem, float> initialSmokeRates = new Dictionary<ParticleSystem, float>();
    private ParticleSystem[] smokeParticleSystems;
    private bool smokeStoppedAfterExtinction;
    private bool[] coughThresholdTriggered;

    public float MaxOxygen => maxOxygen;
    public float CurrentOxygen => currentOxygen;
    public float OxygenNormalized => maxOxygen > 0f ? Mathf.Clamp01(currentOxygen / maxOxygen) : 0f;
    public bool IsPlayerInSmokeZone => isPlayerInSmokeZone;
    public float CurrentSmokeIntensity => currentSmokeIntensity;
    public bool IsGameOver => isGameOver;
    public float CurrentBlackoutAmount => currentBlackoutAmount;

    private void Awake()
    {
        EnsureReferences();
        if (Application.isPlaying)
        {
            officeFires = Object.FindObjectsByType<ExtinguishableFire>(FindObjectsInactive.Include);
            currentOxygen = maxOxygen;
            isGameOver = false;
            ResetCoughThresholds();
            SetGameOverPanelVisible(false);
            CacheSmokeParticleBaselines();
        }
        UpdateHudPlacement();
        UpdateHudVisuals();
        UpdateBlackoutEyesEffect();
    }

    private void Start()
    {
        if (Application.isPlaying)
        {
            CacheInitialSpawnTransforms();
        }
    }

    private void OnEnable()
    {
        EnsureReferences();
        BindRetryEvents();
        UpdateHudPlacement();
        UpdateHudVisuals();
        UpdateBlackoutEyesEffect();
    }

    private void OnDisable()
    {
        UnbindRetryEvents();
    }

    private void BindRetryEvents()
    {
        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(RestartGame);
            retryButton.onClick.AddListener(RestartGame);
        }

        if (retryXrInteractable != null)
        {
            retryXrInteractable.selectEntered.RemoveListener(OnRetryXrSelected);
            retryXrInteractable.selectEntered.AddListener(OnRetryXrSelected);
            retryXrInteractable.activated.RemoveListener(OnRetryXrActivated);
            retryXrInteractable.activated.AddListener(OnRetryXrActivated);
        }
    }

    private void UnbindRetryEvents()
    {
        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(RestartGame);
        }

        if (retryXrInteractable != null)
        {
            retryXrInteractable.selectEntered.RemoveListener(OnRetryXrSelected);
            retryXrInteractable.activated.RemoveListener(OnRetryXrActivated);
        }
    }

    private void OnRetryXrSelected(SelectEnterEventArgs args)
    {
        RestartGame();
    }

    private void OnRetryXrActivated(ActivateEventArgs args)
    {
        RestartGame();
    }

    private void CacheInitialSpawnTransforms()
    {
        if (initialTransformsCached)
        {
            return;
        }

        if (xrOriginTransform != null)
        {
            initialPlayerPos = xrOriginTransform.position;
            initialPlayerRot = xrOriginTransform.rotation;
            Transform camOffset = xrOriginTransform.Find("Camera Offset");
            if (camOffset != null)
            {
                initialCameraOffsetLocalPos = camOffset.localPosition;
                initialCameraOffsetLocalRot = camOffset.localRotation;

                Transform leftCtrl = camOffset.Find("Left Controller");
                if (leftCtrl != null)
                {
                    initialLeftCtrlLocalPos = leftCtrl.localPosition;
                    initialLeftCtrlLocalRot = leftCtrl.localRotation;
                }

                Transform rightCtrl = camOffset.Find("Right Controller");
                if (rightCtrl != null)
                {
                    initialRightCtrlLocalPos = rightCtrl.localPosition;
                    initialRightCtrlLocalRot = rightCtrl.localRotation;
                }
            }
        }

        if (playerCamera != null)
        {
            initialCameraLocalPos = playerCamera.transform.localPosition;
            initialCameraLocalRot = playerCamera.transform.localRotation;
        }

        if (extinguisher != null)
        {
            initialExtinguisherPos = extinguisher.transform.position;
            initialExtinguisherRot = extinguisher.transform.rotation;
        }

        GameObject simObj = GameObject.Find("XR Device Simulator");
        if (simObj != null)
        {
            xrSimulatorComp = simObj.GetComponent("XRDeviceSimulator");
            xrLifecycleComp = simObj.GetComponent("SimulatedDeviceLifecycleManager");

            if (xrSimulatorComp != null)
            {
                System.Type simType = xrSimulatorComp.GetType();
                BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

                hmdStateField = simType.GetField("m_HMDState", flags);
                leftCtrlStateField = simType.GetField("m_LeftControllerState", flags);
                rightCtrlStateField = simType.GetField("m_RightControllerState", flags);
                centerEyeEulerField = simType.GetField("m_CenterEyeEuler", flags);
                leftCtrlEulerField = simType.GetField("m_LeftControllerEuler", flags);
                rightCtrlEulerField = simType.GetField("m_RightControllerEuler", flags);

                if (hmdStateField != null) initialHmdState = hmdStateField.GetValue(xrSimulatorComp);
                if (leftCtrlStateField != null) initialLeftCtrlState = leftCtrlStateField.GetValue(xrSimulatorComp);
                if (rightCtrlStateField != null) initialRightCtrlState = rightCtrlStateField.GetValue(xrSimulatorComp);
                if (centerEyeEulerField != null) initialCenterEyeEuler = (Vector3)centerEyeEulerField.GetValue(xrSimulatorComp);
                if (leftCtrlEulerField != null) initialLeftCtrlEuler = (Vector3)leftCtrlEulerField.GetValue(xrSimulatorComp);
                if (rightCtrlEulerField != null) initialRightCtrlEuler = (Vector3)rightCtrlEulerField.GetValue(xrSimulatorComp);
            }

            if (xrLifecycleComp != null)
            {
                System.Type lcType = xrLifecycleComp.GetType();
                BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                applyHmdStateMethod = lcType.GetMethod("ApplyHMDState", flags);
                applyCtrlStateMethod = lcType.GetMethod("ApplyControllerState", flags);
            }
        }

        initialTransformsCached = true;
    }

    private void ResetXRSimulatorAndPlayerToSpawn()
    {
        if (xrSimulatorComp != null)
        {
            if (hmdStateField != null && initialHmdState != null)
                hmdStateField.SetValue(xrSimulatorComp, initialHmdState);
            if (leftCtrlStateField != null && initialLeftCtrlState != null)
                leftCtrlStateField.SetValue(xrSimulatorComp, initialLeftCtrlState);
            if (rightCtrlStateField != null && initialRightCtrlState != null)
                rightCtrlStateField.SetValue(xrSimulatorComp, initialRightCtrlState);

            if (centerEyeEulerField != null)
                centerEyeEulerField.SetValue(xrSimulatorComp, initialCenterEyeEuler);
            if (leftCtrlEulerField != null)
                leftCtrlEulerField.SetValue(xrSimulatorComp, initialLeftCtrlEuler);
            if (rightCtrlEulerField != null)
                rightCtrlEulerField.SetValue(xrSimulatorComp, initialRightCtrlEuler);

            if (xrLifecycleComp != null)
            {
                if (applyHmdStateMethod != null && initialHmdState != null)
                {
                    applyHmdStateMethod.Invoke(xrLifecycleComp, new object[] { initialHmdState });
                }
                if (applyCtrlStateMethod != null && initialLeftCtrlState != null && initialRightCtrlState != null)
                {
                    applyCtrlStateMethod.Invoke(xrLifecycleComp, new object[] { initialLeftCtrlState, initialRightCtrlState });
                }
            }
        }

        if (xrOriginTransform != null)
        {
            CharacterController cc = xrOriginTransform.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            xrOriginTransform.SetPositionAndRotation(initialPlayerPos, initialPlayerRot);

            Transform camOffset = xrOriginTransform.Find("Camera Offset");
            if (camOffset != null)
            {
                camOffset.localPosition = initialCameraOffsetLocalPos;
                camOffset.localRotation = initialCameraOffsetLocalRot;

                Transform leftCtrl = camOffset.Find("Left Controller");
                if (leftCtrl != null)
                {
                    leftCtrl.localPosition = initialLeftCtrlLocalPos;
                    leftCtrl.localRotation = initialLeftCtrlLocalRot;
                }

                Transform rightCtrl = camOffset.Find("Right Controller");
                if (rightCtrl != null)
                {
                    rightCtrl.localPosition = initialRightCtrlLocalPos;
                    rightCtrl.localRotation = initialRightCtrlLocalRot;
                }
            }

            if (playerCamera != null)
            {
                playerCamera.transform.localPosition = initialCameraLocalPos;
                playerCamera.transform.localRotation = initialCameraLocalRot;
            }

            if (cc != null) cc.enabled = true;
        }
    }

    private void Update()
    {
        EnsureReferences();

        if (!Application.isPlaying)
        {
            UpdateHudPlacement();
            UpdateHudVisuals();
            UpdateBlackoutEyesEffect();
            return;
        }

        if (isGameOver)
        {
            UpdateBlackoutEyesEffect();
            UpdateGameOverPanelFollow();
            return;
        }

        // 1. Evaluate remaining fire intensity (1 = full fire, 0 = all fires extinguished)
        float fireRatio = EvaluateFireIntensity();

        // 2. Dynamically modulate office smoke density based on fire state
        UpdateOfficeSmokeEmission(fireRatio);

        // 3. Check if player is inside the office smoke volume
        isPlayerInSmokeZone = CheckPlayerInsideOfficeSmoke();

        // 4. Dynamically drain or recover oxygen/life
        if (fireRatio > 0.001f && isPlayerInSmokeZone)
        {
            float dynamicMultiplier = Mathf.Lerp(0.35f, 1.0f, fireRatio);
            currentOxygen -= baseDrainRate * dynamicMultiplier * Time.deltaTime;
            if (currentOxygen <= 0f)
            {
                currentOxygen = 0f;
                UpdateHudVisuals();
                TriggerGameOver();
                return;
            }

            CheckAndTriggerCoughMilestones();
        }
        else if ((!isPlayerInSmokeZone || fireRatio <= 0.001f) && currentOxygen < maxOxygen && currentOxygen > 0f)
        {
            currentOxygen = Mathf.Min(maxOxygen, currentOxygen + outsideRecoveryRate * Time.deltaTime);
            RefreshRecoveredCoughMilestones();
        }

        UpdateHudVisuals();
        UpdateBlackoutEyesEffect();
    }

    private void LateUpdate()
    {
        UpdateHudPlacement();
        UpdateBlackoutEyesEffect();
        if (Application.isPlaying && isGameOver)
        {
            UpdateGameOverPanelFollow();
        }
    }

    /// <summary>
    /// Progressively closes the player's top and bottom eyelids and darkens the view to 100% pitch black
    /// when oxygen falls to or below blackoutStartThreshold (default 3% = 0.03).
    /// </summary>
    public void UpdateBlackoutEyesEffect()
    {
        if (blackoutCanvas == null)
        {
            return;
        }

        float ratio = OxygenNormalized;
        float threshold = Mathf.Max(0.001f, blackoutStartThreshold);

        if (isGameOver || ratio <= 0f)
        {
            currentBlackoutAmount = 1f;
        }
        else if (ratio <= threshold)
        {
            // Normalized dying progress: 0 at 3% oxygen -> 1 at 0% oxygen
            float rawProgress = 1f - Mathf.Clamp01(ratio / threshold);
            // Smooth closing curve with a subtle heavy-eyelid pulse as consciousness fades
            float smooth = Mathf.SmoothStep(0f, 1f, rawProgress);
            float heavyBlink = Mathf.Sin(rawProgress * Mathf.PI * 2.5f) * 0.10f * (1f - rawProgress);
            currentBlackoutAmount = Mathf.Clamp01(smooth + Mathf.Max(0f, heavyBlink));
        }
        else
        {
            currentBlackoutAmount = 0f;
        }

        bool shouldEnableBlackout = currentBlackoutAmount > 0.0001f;
        if (blackoutCanvas.enabled != shouldEnableBlackout)
        {
            blackoutCanvas.enabled = shouldEnableBlackout;
        }

        if (!shouldEnableBlackout)
        {
            return;
        }

        // 1. Darken the entire vision veil up to 100% pitch black at 0% oxygen
        if (darknessVeilImage != null)
        {
            float veilAlpha = Mathf.Clamp01(Mathf.Pow(currentBlackoutAmount, 0.75f));
            if (currentBlackoutAmount >= 0.99f)
            {
                veilAlpha = 1f;
            }
            darknessVeilImage.color = new Color(0f, 0f, 0f, veilAlpha);
        }

        // 2. Close Top Eyelid downward from top edge (1.0) to center (0.48)
        if (topEyelidRect != null)
        {
            float topBottomEdge = Mathf.Lerp(1f, 0.48f, currentBlackoutAmount);
            topEyelidRect.anchorMin = new Vector2(0f, topBottomEdge);
            topEyelidRect.anchorMax = new Vector2(1f, 1f);
            topEyelidRect.offsetMin = Vector2.zero;
            topEyelidRect.offsetMax = Vector2.zero;
        }

        // 3. Close Bottom Eyelid upward from bottom edge (0.0) to center (0.52)
        if (bottomEyelidRect != null)
        {
            float bottomTopEdge = Mathf.Lerp(0f, 0.52f, currentBlackoutAmount);
            bottomEyelidRect.anchorMin = new Vector2(0f, 0f);
            bottomEyelidRect.anchorMax = new Vector2(1f, bottomTopEdge);
            bottomEyelidRect.offsetMin = Vector2.zero;
            bottomEyelidRect.offsetMax = Vector2.zero;
        }
    }

    private void ResetCoughThresholds()
    {
        if (coughThresholds == null || coughThresholds.Length == 0)
        {
            coughThresholds = new float[] { 0.75f, 0.50f, 0.25f };
        }

        if (coughThresholdTriggered == null || coughThresholdTriggered.Length != coughThresholds.Length)
        {
            coughThresholdTriggered = new bool[coughThresholds.Length];
        }

        for (int i = 0; i < coughThresholdTriggered.Length; i++)
        {
            coughThresholdTriggered[i] = false;
        }

        if (coughAudioSource != null && coughAudioSource.isPlaying)
        {
            coughAudioSource.Stop();
        }
    }

    private void CheckAndTriggerCoughMilestones()
    {
        if (coughThresholds == null || coughThresholds.Length == 0)
        {
            return;
        }

        if (coughThresholdTriggered == null || coughThresholdTriggered.Length != coughThresholds.Length)
        {
            coughThresholdTriggered = new bool[coughThresholds.Length];
        }

        float ratio = OxygenNormalized;
        for (int i = 0; i < coughThresholds.Length; i++)
        {
            if (!coughThresholdTriggered[i] && ratio <= coughThresholds[i])
            {
                coughThresholdTriggered[i] = true;
                PlayCoughOnce(Mathf.RoundToInt(coughThresholds[i] * 100f));
                break;
            }
        }
    }

    private void RefreshRecoveredCoughMilestones()
    {
        if (coughThresholds == null || coughThresholdTriggered == null)
        {
            return;
        }

        float ratio = OxygenNormalized;
        int len = Mathf.Min(coughThresholds.Length, coughThresholdTriggered.Length);
        for (int i = 0; i < len; i++)
        {
            if (coughThresholdTriggered[i] && ratio > coughThresholds[i] + 0.05f)
            {
                coughThresholdTriggered[i] = false;
            }
        }
    }

    private void PlayCoughOnce(int milestonePercent)
    {
        if (coughAudioSource == null)
        {
            return;
        }

        if (coughAudioClip != null && coughAudioSource.clip != coughAudioClip)
        {
            coughAudioSource.clip = coughAudioClip;
        }

        if (coughAudioSource.clip != null)
        {
            coughAudioSource.loop = false;
            coughAudioSource.volume = coughVolume;
            coughAudioSource.Stop();
            coughAudioSource.Play();
            Debug.Log(string.Format("[PlayerSmokeHealthHUD] Tos activada al bajar al {0}% de vida/oxígeno.", milestonePercent));
        }
    }

    /// <summary>
    /// Displays the World Space GAME OVER panel in front of the player when oxygen reaches 0%,
    /// with the player's eyes completely closed in pitch-black darkness behind the panel.
    /// </summary>
    [ContextMenu("Simulate Game Over (0% Oxygen)")]
    public void TriggerGameOver()
    {
        currentOxygen = 0f;
        isGameOver = true;
        if (coughAudioSource != null && coughAudioSource.isPlaying)
        {
            coughAudioSource.Stop();
        }
        UpdateHudVisuals();
        UpdateBlackoutEyesEffect();

        if (extinguisher != null)
        {
            extinguisher.StopSpraying();
        }

        SetGameOverPanelVisible(true);
        PositionGameOverPanelInFrontOfCamera(true);
        Debug.Log("[PlayerSmokeHealthHUD] ¡GAME OVER! El jugador cerró los ojos por falta de oxígeno.");
    }

    /// <summary>
    /// Restarts the entire scenario: opens the player's eyes, resets XRDeviceSimulator and teleports
    /// player back to initial spawn, refills and returns the extinguisher, resets all fires and office
    /// smoke, restores oxygen to 100%, and hides the Game Over panel.
    /// </summary>
    [ContextMenu("Restart Game (Retry)")]
    public void RestartGame()
    {
        EnsureReferences();
        CacheInitialSpawnTransforms();

        ResetXRSimulatorAndPlayerToSpawn();

        if (reloadSceneOnRetry && Application.isPlaying)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.name);
            return;
        }

        isGameOver = false;
        SetGameOverPanelVisible(false);

        if (extinguisher != null)
        {
            extinguisher.StopSpraying();
            extinguisher.RefillExtinguisher();

            XRGrabInteractable grab = extinguisher.GrabInteractable;
            if (grab != null && grab.isSelected && grab.interactionManager != null)
            {
                var interactors = new List<UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor>(grab.interactorsSelecting);
                foreach (var interactor in interactors)
                {
                    grab.interactionManager.SelectExit(interactor, grab);
                }
            }

            Rigidbody rb = extinguisher.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            extinguisher.transform.SetPositionAndRotation(initialExtinguisherPos, initialExtinguisherRot);
            extinguisher.UpdateHudVisuals();
        }

        officeFires = Object.FindObjectsByType<ExtinguishableFire>(FindObjectsInactive.Include);
        if (officeFires != null)
        {
            foreach (var fire in officeFires)
            {
                if (fire != null)
                {
                    fire.gameObject.SetActive(true);
                    fire.ResetFire();
                }
            }
        }

        smokeStoppedAfterExtinction = false;
        currentSmokeIntensity = 1.0f;
        if (smokeParticleSystems == null || smokeParticleSystems.Length == 0)
        {
            CacheSmokeParticleBaselines();
        }
        if (smokeParticleSystems != null)
        {
            foreach (var ps in smokeParticleSystems)
            {
                if (ps == null) continue;
                if (initialSmokeRates.TryGetValue(ps, out float baseRate))
                {
                    var emission = ps.emission;
                    emission.rateOverTimeMultiplier = baseRate;
                }
                ps.Clear(true);
                ps.Simulate(2.0f, true, true);
                ps.Play(true);
            }
        }

        currentOxygen = maxOxygen;
        isPlayerInSmokeZone = true;
        ResetCoughThresholds();
        UpdateHudPlacement();
        UpdateHudVisuals();
        UpdateBlackoutEyesEffect();

        Debug.Log("[PlayerSmokeHealthHUD] ¡Juego reiniciado por completo! Vista restaurada y jugador de vuelta en el spawn inicial.");
    }

    private void SetGameOverPanelVisible(bool visible)
    {
        if (gameOverCanvas == null)
        {
            return;
        }

        gameOverCanvas.enabled = visible;
        if (retryXrInteractable != null)
        {
            Collider col = retryXrInteractable.GetComponent<Collider>();
            if (col != null) col.enabled = visible;
        }
    }

    private void PositionGameOverPanelInFrontOfCamera(bool immediate)
    {
        if (gameOverCanvas == null || playerCamera == null)
        {
            return;
        }

        Vector3 camPos = playerCamera.transform.position;
        Vector3 forward = playerCamera.transform.forward;
        Vector3 flatForward = new Vector3(forward.x, 0f, forward.z);
        if (flatForward.sqrMagnitude < 0.001f)
        {
            flatForward = forward;
        }
        flatForward.Normalize();

        Vector3 targetPos = camPos + flatForward * gameOverPanelDistance;
        targetPos.y = camPos.y - 0.04f;
        Quaternion targetRot = Quaternion.LookRotation(flatForward, Vector3.up);

        if (immediate)
        {
            gameOverCanvas.transform.SetPositionAndRotation(targetPos, targetRot);
        }
        else
        {
            gameOverCanvas.transform.position = Vector3.Lerp(gameOverCanvas.transform.position, targetPos, Time.deltaTime * 4f);
            gameOverCanvas.transform.rotation = Quaternion.Slerp(gameOverCanvas.transform.rotation, targetRot, Time.deltaTime * 4f);
        }
    }

    private void UpdateGameOverPanelFollow()
    {
        if (gameOverCanvas == null || !gameOverCanvas.enabled || playerCamera == null)
        {
            return;
        }

        Vector3 toPanel = gameOverCanvas.transform.position - playerCamera.transform.position;
        float dist = toPanel.magnitude;
        float angle = Vector3.Angle(playerCamera.transform.forward, toPanel);

        if (angle > 38f || Mathf.Abs(dist - gameOverPanelDistance) > 0.35f)
        {
            PositionGameOverPanelInFrontOfCamera(false);
        }
    }

    private void EnsureReferences()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (coughAudioSource == null && playerCamera != null)
        {
            coughAudioSource = playerCamera.GetComponent<AudioSource>();
        }

        if (xrOriginTransform == null)
        {
            GameObject xrObj = GameObject.Find("XR Origin Hands (XR Rig)");
            if (xrObj != null) xrOriginTransform = xrObj.transform;
        }

        if (extinguisher == null)
        {
            extinguisher = Object.FindAnyObjectByType<ExtinguisherController>();
        }

        if (officeSmokeRoot == null)
        {
            GameObject zoneObj = GameObject.Find("OfficeSmokeZone");
            if (zoneObj != null)
            {
                officeSmokeRoot = zoneObj.transform;
            }
        }

        if (officeSmokeBounds == null && officeSmokeRoot != null)
        {
            officeSmokeBounds = officeSmokeRoot.GetComponent<BoxCollider>();
        }

        if (officeFires == null || officeFires.Length == 0)
        {
            officeFires = Object.FindObjectsByType<ExtinguishableFire>(FindObjectsInactive.Include);
        }

        if (hudCanvas == null && playerCamera != null)
        {
            Transform canvasTr = playerCamera.transform.Find("PlayerOxygenHUDCanvas");
            if (canvasTr != null)
            {
                hudCanvas = canvasTr.GetComponent<Canvas>();
            }
        }

        if (hudCanvas != null)
        {
            if (borderImage == null)
            {
                Transform bTr = hudCanvas.transform.Find("BorderFrame");
                if (bTr != null) borderImage = bTr.GetComponent<Image>();
            }
            if (oxygenFillImage == null)
            {
                Transform fTr = hudCanvas.transform.Find("BorderFrame/InnerBackground/OxygenFillBar");
                if (fTr != null) oxygenFillImage = fTr.GetComponent<Image>();
            }
            if (oxygenStatusText == null)
            {
                oxygenStatusText = hudCanvas.GetComponentInChildren<TMP_Text>(true);
            }
        }

        if (blackoutCanvas == null && playerCamera != null)
        {
            Transform blkTr = playerCamera.transform.Find("BlackoutEyesCanvas");
            if (blkTr != null)
            {
                blackoutCanvas = blkTr.GetComponent<Canvas>();
            }
        }

        if (blackoutCanvas != null)
        {
            if (darknessVeilImage == null)
            {
                Transform veilTr = blackoutCanvas.transform.Find("DarknessVeil");
                if (veilTr != null) darknessVeilImage = veilTr.GetComponent<Image>();
            }
            if (topEyelidRect == null)
            {
                Transform topTr = blackoutCanvas.transform.Find("TopEyelid");
                if (topTr != null) topEyelidRect = topTr.GetComponent<RectTransform>();
            }
            if (bottomEyelidRect == null)
            {
                Transform botTr = blackoutCanvas.transform.Find("BottomEyelid");
                if (botTr != null) bottomEyelidRect = botTr.GetComponent<RectTransform>();
            }
        }

        if (gameOverCanvas == null)
        {
            GameObject goCanvasObj = GameObject.Find("GameOverVRCanvas");
            if (goCanvasObj != null)
            {
                gameOverCanvas = goCanvasObj.GetComponent<Canvas>();
            }
        }

        if (gameOverCanvas != null)
        {
            if (retryButton == null)
            {
                retryButton = gameOverCanvas.GetComponentInChildren<Button>(true);
            }
            if (retryXrInteractable == null)
            {
                retryXrInteractable = gameOverCanvas.GetComponentInChildren<XRSimpleInteractable>(true);
            }
        }
    }

    private void CacheSmokeParticleBaselines()
    {
        if (officeSmokeRoot == null)
        {
            return;
        }

        smokeParticleSystems = officeSmokeRoot.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in smokeParticleSystems)
        {
            if (ps != null && !initialSmokeRates.ContainsKey(ps))
            {
                initialSmokeRates[ps] = ps.emission.rateOverTimeMultiplier;
            }
        }
    }

    private float EvaluateFireIntensity()
    {
        if (officeFires == null || officeFires.Length == 0)
        {
            currentSmokeIntensity = 1f;
            return 1f;
        }

        float totalMax = 0f;
        float totalCurrent = 0f;
        bool anyActiveFire = false;

        foreach (var fire in officeFires)
        {
            if (fire == null) continue;
            totalMax += Mathf.Max(1f, fire.MaxHealth);
            if (!fire.IsExtinguished)
            {
                anyActiveFire = true;
                totalCurrent += Mathf.Max(0f, fire.CurrentHealth);
            }
        }

        if (!anyActiveFire || totalMax <= 0f)
        {
            currentSmokeIntensity = 0f;
            return 0f;
        }

        currentSmokeIntensity = Mathf.Clamp01(totalCurrent / totalMax);
        return currentSmokeIntensity;
    }

    private void UpdateOfficeSmokeEmission(float fireRatio)
    {
        if (officeSmokeRoot == null)
        {
            return;
        }

        if (smokeParticleSystems == null || smokeParticleSystems.Length == 0)
        {
            CacheSmokeParticleBaselines();
        }

        if (fireRatio <= 0.001f)
        {
            if (!smokeStoppedAfterExtinction && smokeParticleSystems != null)
            {
                foreach (var ps in smokeParticleSystems)
                {
                    if (ps != null && ps.isPlaying)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }
                smokeStoppedAfterExtinction = true;
            }
            return;
        }

        smokeStoppedAfterExtinction = false;
        if (smokeParticleSystems != null)
        {
            foreach (var ps in smokeParticleSystems)
            {
                if (ps != null && initialSmokeRates.TryGetValue(ps, out float baseRate))
                {
                    var emission = ps.emission;
                    emission.rateOverTimeMultiplier = baseRate * Mathf.Lerp(0.15f, 1.0f, fireRatio);
                }
            }
        }
    }

    private bool CheckPlayerInsideOfficeSmoke()
    {
        if (officeSmokeBounds == null || playerCamera == null)
        {
            return true;
        }

        Vector3 camPos = playerCamera.transform.position;
        Vector3 checkPos = new Vector3(camPos.x, Mathf.Clamp(camPos.y, 0.5f, 2.5f), camPos.z);
        return officeSmokeBounds.bounds.Contains(checkPos);
    }

    /// <summary>
    /// Pins the World Space Canvas to the top-left of the player's camera view.
    /// </summary>
    public void UpdateHudPlacement()
    {
        if (hudCanvas == null || playerCamera == null)
        {
            return;
        }

        RectTransform rt = hudCanvas.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.pivot = new Vector2(0f, 1f);
        }

        Vector3 targetWorldPos = playerCamera.ViewportToWorldPoint(new Vector3(viewportX, viewportY, hudDistance));
        hudCanvas.transform.position = targetWorldPos;
        hudCanvas.transform.rotation = playerCamera.transform.rotation;
    }

    /// <summary>
    /// Updates the fill bar, color gradient, border color, and status text.
    /// </summary>
    public void UpdateHudVisuals()
    {
        float ratio = OxygenNormalized;

        Color activeColor;
        if (ratio > 0.5f)
        {
            float t = (ratio - 0.5f) / 0.5f;
            activeColor = Color.Lerp(cautionColor, healthyColor, t);
        }
        else
        {
            float t = Mathf.Clamp01(ratio / 0.5f);
            activeColor = Color.Lerp(criticalColor, cautionColor, t);
        }

        if (oxygenFillImage != null)
        {
            oxygenFillImage.fillAmount = ratio;
            oxygenFillImage.color = activeColor;
        }

        if (borderImage != null)
        {
            borderImage.color = new Color(activeColor.r, activeColor.g, activeColor.b, 0.95f);
        }

        if (oxygenStatusText != null)
        {
            int pct = Mathf.CeilToInt(ratio * 100f);
            if (ratio <= 0f)
            {
                oxygenStatusText.text = "¡SIN OXÍGENO! 0%";
            }
            else if (currentSmokeIntensity <= 0.001f)
            {
                oxygenStatusText.text = string.Format("HUMO DISIPADO · VIDA {0}%", pct);
            }
            else if (!isPlayerInSmokeZone)
            {
                oxygenStatusText.text = string.Format("FUERA DE OFICINA · VIDA {0}%", pct);
            }
            else if (ratio <= 0.25f)
            {
                oxygenStatusText.text = string.Format("¡ASFIXIA POR HUMO! {0}%", pct);
            }
            else
            {
                oxygenStatusText.text = string.Format("VIDA / OXÍGENO {0}%", pct);
            }
        }
    }
}
