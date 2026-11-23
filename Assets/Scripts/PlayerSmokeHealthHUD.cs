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

    [Header("Smoke Detector Alarm Audio ((Prb)SmokeDetector)")]
    [Tooltip("Reference to the ceiling SmokeDetectorAlarm component. Auto-detected if empty.")]
    [SerializeField]
    private SmokeDetectorAlarm smokeDetectorAlarm;

    [Tooltip("Volume control for the continuous smoke detector alarm (0 = silent, 0.22 = moderate default).")]
    [Range(0f, 1f)]
    [SerializeField]
    private float smokeAlarmVolume = 0.22f;

    [Tooltip("If true, mutes the smoke detector alarm.")]
    [SerializeField]
    private bool muteSmokeAlarm = false;

    [Header("Indoor Fire Blackout & Outdoor Daylight Separation")]
    [Tooltip("Main Directional Light (Rendering Layer 1) dimmed while the player is inside the dark smoke-filled office.")]
    [SerializeField]
    private Light directionalLight;

    [Tooltip("Dedicated Directional Sun Light (Rendering Layer 2) that keeps the exterior city in bright daytime permanently.")]
    [SerializeField]
    private Light outdoorDaylightSun;

    [Tooltip("Dedicated Sky Fill Directional Light (Rendering Layer 2) that illuminates shaded exterior building facades in daytime.")]
    [SerializeField]
    private Light outdoorDaylightSkyFill;

    [Tooltip("Root transform of the exterior city environment (Entorno_Ciudad_Exterior) isolated on Rendering Layer 2.")]
    [SerializeField]
    private Transform exteriorCityRoot;

    [Tooltip("Root transform of the indoor office environment (Entorno_Oficina) isolated on Rendering Layer 1.")]
    [SerializeField]
    private Transform indoorOfficeRoot;

    [Tooltip("Dim charcoal ambient color inside the power-outage office (dark enough for emergency atmosphere, bright enough to navigate).")]
    [SerializeField]
    private Color indoorDarkAmbientColor = new Color(0.20f, 0.19f, 0.22f, 1.0f);

    [Tooltip("Low skybox reflection intensity inside the dark office.")]
    [Range(0f, 1f)]
    [SerializeField]
    private float indoorReflectionIntensity = 0.25f;

    [Tooltip("Directional light intensity on Layer 1 (Office Interior) while inside the dark office.")]
    [Range(0f, 1f)]
    [SerializeField]
    private float indoorDirectionalLightIntensity = 0.24f;

    [Tooltip("Directional light intensity on Layer 1 (for player hands/props) when the player evacuates outside to the street.")]
    [Range(0.1f, 2f)]
    [SerializeField]
    private float outdoorDirectionalLightIntensity = 1.25f;

    [Tooltip("Permanent daytime sun intensity on Layer 2 (Entorno_Ciudad_Exterior).")]
    [Range(0.5f, 3f)]
    [SerializeField]
    private float exteriorSunIntensity = 1.45f;

    [Tooltip("Permanent daytime sky fill intensity on Layer 2 (Entorno_Ciudad_Exterior shaded facades).")]
    [Range(0.2f, 2f)]
    [SerializeField]
    private float exteriorSkyFillIntensity = 0.95f;

    [Header("Progressive Smoke Thinning & Residual Particles")]
    [Tooltip("Fraction of initial smoke emission rate that remains as scattered residual smoke after fires are extinguished (0.14 = 14%).")]
    [Range(0.05f, 0.40f)]
    [SerializeField]
    private float residualSmokeEmissionRatio = 0.14f;

    [Tooltip("Fraction of initial smoke particle opacity (alpha) that remains on scattered residual smoke after fires are extinguished.")]
    [Range(0.20f, 0.80f)]
    [SerializeField]
    private float residualSmokeOpacityRatio = 0.42f;

    [Tooltip("Speed at which smoke intensity progressively decreases as fires are sprayed and extinguished.")]
    [Range(0.15f, 2.0f)]
    [SerializeField]
    private float smokeThinningSpeed = 0.65f;

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

    [Header("Top-Right Evacuation Stopwatch / Cronómetro (World Space)")]
    [Tooltip("World Space Canvas attached to Main Camera for the top-right digital evacuation stopwatch.")]
    [SerializeField]
    private Canvas timerHudCanvas;

    [Tooltip("Normalized viewport X for the top-right stopwatch (0.975 = right edge).")]
    [Range(0.5f, 0.995f)]
    [SerializeField]
    private float timerViewportX = 0.975f;

    [Tooltip("Normalized viewport Y for the top-right stopwatch (0.96 = top edge).")]
    [Range(0.5f, 0.995f)]
    [SerializeField]
    private float timerViewportY = 0.96f;

    [Tooltip("Glowing digital amber-orange color for active 7-segment LED digits (matches 01:06.49 display).")]
    [SerializeField]
    private Color timerActiveSegmentColor = new Color(1.0f, 0.56f, 0.02f, 1.0f);

    [Tooltip("Faint ghost color for unlit 7-segment LED segments.")]
    [SerializeField]
    private Color timerInactiveSegmentColor = new Color(1.0f, 0.56f, 0.02f, 0.055f);

    [Tooltip("Optional TMP_Text storing the formatted MM:SS.cc stopwatch string.")]
    [SerializeField]
    private TMP_Text timerReadoutText;

    [Tooltip("Elapsed evacuation time in seconds since start or last retry.")]
    [SerializeField]
    private float elapsedEvacuationTime = 0f;

    [Tooltip("True while the evacuation stopwatch is actively counting up.")]
    [SerializeField]
    private bool isEvacuationTimerRunning = true;

    [Tooltip("True once the player has successfully evacuated outside the office, locking the final escape time.")]
    [SerializeField]
    private bool hasEvacuatedOutside = false;

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

    [Tooltip("Distance in meters in front of the player camera where the Game Over / Victory panel appears.")]
    [SerializeField]
    private float gameOverPanelDistance = 1.10f;

    [Tooltip("If true, reloads the Unity scene via SceneManager after resetting XR simulator pose.")]
    [SerializeField]
    private bool reloadSceneOnRetry = false;

    [Header("Victory / Evacuation Goal VR Panel References")]
    [Tooltip("World Space Canvas displayed in front of the player when reaching the evacuation goal point (¡META!).")]
    [SerializeField]
    private Canvas victoryCanvas;

    [Tooltip("UI Button on the Victory panel to retry/restart the experience.")]
    [SerializeField]
    private Button victoryRetryButton;

    [Tooltip("XRSimpleInteractable on the Victory retry button for direct VR ray/poke activation.")]
    [SerializeField]
    private XRSimpleInteractable victoryRetryXrInteractable;

    [Tooltip("TMP_Text on the Victory panel displaying the final stopwatch evacuation time (MM:SS.cc).")]
    [SerializeField]
    private TMP_Text victoryTimeText;

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
    private bool isVictory = false;

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
    private readonly Dictionary<ParticleSystem, float> initialSmokeSizes = new Dictionary<ParticleSystem, float>();
    private readonly Dictionary<ParticleSystem, ParticleSystem.MinMaxGradient> initialSmokeColors = new Dictionary<ParticleSystem, ParticleSystem.MinMaxGradient>();
    private readonly Dictionary<ParticleSystem, float> smoothedEmitterRatios = new Dictionary<ParticleSystem, float>();
    private ParticleSystem.Particle[] liveParticleBuffer = new ParticleSystem.Particle[600];
    private ParticleSystem[] smokeParticleSystems;
    private bool smokeStoppedAfterExtinction;
    private bool[] coughThresholdTriggered;

    // 7-Segment LED digit cache: 6 digits (M1, M0, S1, S0, C1, C0) x 7 segments (A..G)
    private static readonly byte[] SevenSegmentDigitMasks = new byte[10]
    {
        0x3F, // 0: A B C D E F
        0x06, // 1: B C
        0x5B, // 2: A B D E G
        0x4F, // 3: A B C D G
        0x66, // 4: B C F G
        0x6D, // 5: A C D F G
        0x7D, // 6: A C D E F G
        0x07, // 7: A B C
        0x7F, // 8: A B C D E F G
        0x6F  // 9: A B C D F G
    };
    private Image[][] timerDigitSegments;
    private Image[] timerPunctuationImages;
    private int lastDisplayedCentiseconds = -1;

    public float MaxOxygen => maxOxygen;
    public float CurrentOxygen => currentOxygen;
    public float OxygenNormalized => maxOxygen > 0f ? Mathf.Clamp01(currentOxygen / maxOxygen) : 0f;
    public bool IsPlayerInSmokeZone => isPlayerInSmokeZone;
    public float CurrentSmokeIntensity => currentSmokeIntensity;
    public bool IsGameOver => isGameOver;
    public bool IsVictory => isVictory;
    public float CurrentBlackoutAmount => currentBlackoutAmount;
    public float ElapsedEvacuationTime => elapsedEvacuationTime;
    public bool IsEvacuationTimerRunning => isEvacuationTimerRunning;
    public bool HasEvacuatedOutside => hasEvacuatedOutside;
    public string FormattedEvacuationTime => FormatEvacuationTime(elapsedEvacuationTime);

    private void Awake()
    {
        EnsureReferences();
        if (Application.isPlaying)
        {
            officeFires = Object.FindObjectsByType<ExtinguishableFire>(FindObjectsInactive.Include);
            currentOxygen = maxOxygen;
            isGameOver = false;
            isVictory = false;
            ResetEvacuationTimer();
            ResetCoughThresholds();
            SetGameOverPanelVisible(false);
            SetVictoryPanelVisible(false);
            CacheSmokeParticleBaselines();
            UpdateIndoorDarknessAndSmokeFog(true);
        }
        UpdateHudPlacement();
        UpdateHudVisuals();
        UpdateEvacuationTimerVisuals(true);
        UpdateBlackoutEyesEffect();
        UpdateIndoorDarknessAndSmokeFog(false);
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
        UpdateEvacuationTimerVisuals(true);
        UpdateBlackoutEyesEffect();
        UpdateIndoorDarknessAndSmokeFog(false);
    }

    private void OnDisable()
    {
        UnbindRetryEvents();
    }

    private void OnValidate()
    {
        smokeAlarmVolume = Mathf.Clamp01(smokeAlarmVolume);
        if (smokeDetectorAlarm != null)
        {
            smokeDetectorAlarm.AlarmVolume = smokeAlarmVolume;
            smokeDetectorAlarm.MuteAlarm = muteSmokeAlarm;
        }
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

        if (victoryRetryButton != null)
        {
            victoryRetryButton.onClick.RemoveListener(RestartGame);
            victoryRetryButton.onClick.AddListener(RestartGame);
        }

        if (victoryRetryXrInteractable != null)
        {
            victoryRetryXrInteractable.selectEntered.RemoveListener(OnRetryXrSelected);
            victoryRetryXrInteractable.selectEntered.AddListener(OnRetryXrSelected);
            victoryRetryXrInteractable.activated.RemoveListener(OnRetryXrActivated);
            victoryRetryXrInteractable.activated.AddListener(OnRetryXrActivated);
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

        if (victoryRetryButton != null)
        {
            victoryRetryButton.onClick.RemoveListener(RestartGame);
        }

        if (victoryRetryXrInteractable != null)
        {
            victoryRetryXrInteractable.selectEntered.RemoveListener(OnRetryXrSelected);
            victoryRetryXrInteractable.activated.RemoveListener(OnRetryXrActivated);
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
            UpdateEvacuationTimerVisuals(false);
            UpdateBlackoutEyesEffect();
            return;
        }

        if (isGameOver)
        {
            UpdateBlackoutEyesEffect();
            UpdateGameOverPanelFollow();
            return;
        }

        if (isVictory)
        {
            UpdateVictoryPanelFollow();
            UpdateIndoorDarknessAndSmokeFog(false);
            return;
        }

        // 1. Evaluate remaining fire intensity (1 = full fire, 0 = all fires extinguished)
        float fireRatio = EvaluateFireIntensity();

        // 2. Dynamically modulate office smoke density based on fire state
        UpdateOfficeSmokeEmission(fireRatio);

        // 3. Check if player is inside the office smoke volume
        isPlayerInSmokeZone = CheckPlayerInsideOfficeSmoke();
        if (!isPlayerInSmokeZone && !hasEvacuatedOutside)
        {
            hasEvacuatedOutside = true;
        }

        // 4. Advance the evacuation stopwatch until the player reaches the final Goal Point (¡META!)
        if (isEvacuationTimerRunning)
        {
            elapsedEvacuationTime += Time.deltaTime;
        }

        // 5. Dynamically drain or recover oxygen/life
        if (fireRatio > 0.001f && isPlayerInSmokeZone)
        {
            float dynamicMultiplier = Mathf.Lerp(0.35f, 1.0f, fireRatio);
            currentOxygen -= baseDrainRate * dynamicMultiplier * Time.deltaTime;
            if (currentOxygen <= 0f)
            {
                currentOxygen = 0f;
                UpdateHudVisuals();
                UpdateEvacuationTimerVisuals(false);
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
        UpdateEvacuationTimerVisuals(false);
        UpdateBlackoutEyesEffect();
        UpdateIndoorDarknessAndSmokeFog(false);
    }

    private void LateUpdate()
    {
        UpdateHudPlacement();
        UpdateBlackoutEyesEffect();
        if (Application.isPlaying)
        {
            if (isGameOver)
            {
                UpdateGameOverPanelFollow();
            }
            else if (isVictory)
            {
                UpdateVictoryPanelFollow();
            }
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
        isEvacuationTimerRunning = false;
        if (coughAudioSource != null && coughAudioSource.isPlaying)
        {
            coughAudioSource.Stop();
        }
        if (smokeDetectorAlarm != null)
        {
            smokeDetectorAlarm.StopAlarmOnPlayerDeath();
        }
        UpdateHudVisuals();
        UpdateEvacuationTimerVisuals(true);
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
    /// Displays the World Space VICTORY VR panel in front of the player when they reach the evacuation
    /// goal point (¡META!), freezing the stopwatch, showing the final time on the panel, and offering Retry.
    /// </summary>
    [ContextMenu("Simulate Victory (Reach Goal Point)")]
    public void TriggerVictory()
    {
        if (isGameOver || isVictory)
        {
            return;
        }

        isVictory = true;
        hasEvacuatedOutside = true;
        isEvacuationTimerRunning = false;

        if (coughAudioSource != null && coughAudioSource.isPlaying)
        {
            coughAudioSource.Stop();
        }

        if (extinguisher != null)
        {
            extinguisher.StopSpraying();
        }

        UpdateHudVisuals();
        UpdateEvacuationTimerVisuals(true);
        UpdateBlackoutEyesEffect();

        if (victoryTimeText != null)
        {
            victoryTimeText.text = string.Format("TIEMPO DE EVACUACIÓN: {0}", FormattedEvacuationTime);
        }

        SetVictoryPanelVisible(true);
        PositionVictoryPanelInFrontOfCamera(true);
        Debug.Log(string.Format("[PlayerSmokeHealthHUD] ¡META ALCANZADA! Evacuación completada en {0}.", FormattedEvacuationTime));
    }

    /// <summary>
    /// Restarts the entire scenario: opens the player's eyes, resets XRDeviceSimulator and teleports
    /// player back to initial spawn, refills and returns the extinguisher, resets all fires and office
    /// smoke, restores oxygen to 100%, resets stopwatch to 00:00.00, and hides Game Over / Victory panels.
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
        isVictory = false;
        SetGameOverPanelVisible(false);
        SetVictoryPanelVisible(false);

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

        EvacuationDebrisItem.ResetAllDebrisItems();
        EmergencyExitDoor.ResetAllDoors();

        if (smokeDetectorAlarm != null)
        {
            smokeDetectorAlarm.RestartAlarm();
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
                smoothedEmitterRatios[ps] = 1.0f;
                if (initialSmokeRates.TryGetValue(ps, out float baseRate))
                {
                    var emission = ps.emission;
                    emission.rateOverTimeMultiplier = baseRate;
                }
                if (initialSmokeSizes.TryGetValue(ps, out float baseSize))
                {
                    var main = ps.main;
                    main.startSizeMultiplier = baseSize;
                }
                if (initialSmokeColors.TryGetValue(ps, out ParticleSystem.MinMaxGradient baseCol))
                {
                    var main = ps.main;
                    main.startColor = baseCol;
                }
                ps.Clear(true);
                ps.Simulate(2.0f, true, true);
                ps.Play(true);
            }
        }

        currentOxygen = maxOxygen;
        isPlayerInSmokeZone = true;
        ResetEvacuationTimer();
        ResetCoughThresholds();
        UpdateIndoorDarknessAndSmokeFog(true);
        UpdateHudPlacement();
        UpdateHudVisuals();
        UpdateEvacuationTimerVisuals(true);
        UpdateBlackoutEyesEffect();

        Debug.Log("[PlayerSmokeHealthHUD] ¡Juego reiniciado por completo! Cronómetro en 00:00.00, vista restaurada y jugador de vuelta en el spawn inicial.");
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

    private void SetVictoryPanelVisible(bool visible)
    {
        if (victoryCanvas == null)
        {
            return;
        }

        victoryCanvas.enabled = visible;
        if (victoryRetryXrInteractable != null)
        {
            Collider col = victoryRetryXrInteractable.GetComponent<Collider>();
            if (col != null) col.enabled = visible;
        }
    }

    private void PositionVictoryPanelInFrontOfCamera(bool immediate)
    {
        if (victoryCanvas == null || playerCamera == null)
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
            victoryCanvas.transform.SetPositionAndRotation(targetPos, targetRot);
        }
        else
        {
            victoryCanvas.transform.position = Vector3.Lerp(victoryCanvas.transform.position, targetPos, Time.deltaTime * 4f);
            victoryCanvas.transform.rotation = Quaternion.Slerp(victoryCanvas.transform.rotation, targetRot, Time.deltaTime * 4f);
        }
    }

    private void UpdateVictoryPanelFollow()
    {
        if (victoryCanvas == null || !victoryCanvas.enabled || playerCamera == null)
        {
            return;
        }

        Vector3 toPanel = victoryCanvas.transform.position - playerCamera.transform.position;
        float dist = toPanel.magnitude;
        float angle = Vector3.Angle(playerCamera.transform.forward, toPanel);

        if (angle > 38f || Mathf.Abs(dist - gameOverPanelDistance) > 0.35f)
        {
            PositionVictoryPanelInFrontOfCamera(false);
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

        if (smokeDetectorAlarm == null)
        {
            smokeDetectorAlarm = Object.FindAnyObjectByType<SmokeDetectorAlarm>();
        }

        if (directionalLight == null)
        {
            GameObject dirLightObj = GameObject.Find("Directional Light");
            if (dirLightObj != null)
            {
                directionalLight = dirLightObj.GetComponent<Light>();
            }
        }

        if (outdoorDaylightSun == null)
        {
            GameObject sunObj = GameObject.Find("Outdoor_Daylight_Sun");
            if (sunObj != null)
            {
                outdoorDaylightSun = sunObj.GetComponent<Light>();
            }
        }

        if (outdoorDaylightSkyFill == null)
        {
            GameObject fillObj = GameObject.Find("Outdoor_Daylight_SkyFill");
            if (fillObj != null)
            {
                outdoorDaylightSkyFill = fillObj.GetComponent<Light>();
            }
        }

        if (exteriorCityRoot == null)
        {
            GameObject cityObj = GameObject.Find("Entorno_Ciudad_Exterior");
            if (cityObj != null)
            {
                exteriorCityRoot = cityObj.transform;
            }
        }

        if (indoorOfficeRoot == null)
        {
            GameObject officeObj = GameObject.Find("Entorno_Oficina");
            if (officeObj != null)
            {
                indoorOfficeRoot = officeObj.transform;
            }
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

        if (timerHudCanvas == null && playerCamera != null)
        {
            Transform tCanvasTr = playerCamera.transform.Find("PlayerTimerHUDCanvas");
            if (tCanvasTr != null)
            {
                timerHudCanvas = tCanvasTr.GetComponent<Canvas>();
            }
        }

        if (timerHudCanvas != null)
        {
            if (timerReadoutText == null)
            {
                timerReadoutText = timerHudCanvas.GetComponentInChildren<TMP_Text>(true);
            }
            CacheSevenSegmentReferences();
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

        if (victoryCanvas == null)
        {
            GameObject vicCanvasObj = GameObject.Find("VictoryVRCanvas");
            if (vicCanvasObj != null)
            {
                victoryCanvas = vicCanvasObj.GetComponent<Canvas>();
            }
        }

        if (victoryCanvas != null)
        {
            if (victoryRetryButton == null)
            {
                victoryRetryButton = victoryCanvas.GetComponentInChildren<Button>(true);
            }
            if (victoryRetryXrInteractable == null)
            {
                victoryRetryXrInteractable = victoryCanvas.GetComponentInChildren<XRSimpleInteractable>(true);
            }
            if (victoryTimeText == null)
            {
                Transform timeTr = victoryCanvas.transform.Find("VictoryBorder/VictoryPanel/VictoryTimeBadge/VictoryTimeText");
                if (timeTr != null)
                {
                    victoryTimeText = timeTr.GetComponent<TMP_Text>();
                }
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
            if (ps == null) continue;

            if (!initialSmokeRates.ContainsKey(ps))
            {
                initialSmokeRates[ps] = ps.emission.rateOverTimeMultiplier;
            }
            if (!initialSmokeSizes.ContainsKey(ps))
            {
                initialSmokeSizes[ps] = ps.main.startSizeMultiplier;
            }
            if (!initialSmokeColors.ContainsKey(ps))
            {
                initialSmokeColors[ps] = ps.main.startColor;
            }
            if (!smoothedEmitterRatios.ContainsKey(ps))
            {
                smoothedEmitterRatios[ps] = 1.0f;
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

        float targetRatio = (!anyActiveFire || totalMax <= 0f) ? 0f : Mathf.Clamp01(totalCurrent / totalMax);
        if (Application.isPlaying)
        {
            currentSmokeIntensity = Mathf.MoveTowards(currentSmokeIntensity, targetRatio, Time.deltaTime * smokeThinningSpeed);
        }
        else
        {
            currentSmokeIntensity = targetRatio;
        }

        return currentSmokeIntensity;
    }

    private float EvaluateLocalFireRatioForEmitter(Vector3 emitterPos, float globalFireRatio)
    {
        if (officeFires == null || officeFires.Length == 0)
        {
            return globalFireRatio;
        }

        float weightedCurrent = 0f;
        float weightedMax = 0f;

        foreach (var fire in officeFires)
        {
            if (fire == null) continue;
            float dist = Vector3.Distance(emitterPos, fire.transform.position);
            // Fires within 5m of this smoke emitter have strong local influence
            float weight = 1f / (1f + dist * dist * 0.12f);
            float maxH = Mathf.Max(1f, fire.MaxHealth);
            float curH = fire.IsExtinguished ? 0f : Mathf.Max(0f, fire.CurrentHealth);
            weightedMax += maxH * weight;
            weightedCurrent += curH * weight;
        }

        if (weightedMax <= 0.0001f)
        {
            return globalFireRatio;
        }

        float localRatio = Mathf.Clamp01(weightedCurrent / weightedMax);
        return Mathf.Lerp(globalFireRatio, localRatio, 0.65f);
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

        if (smokeParticleSystems == null)
        {
            return;
        }

        smokeStoppedAfterExtinction = fireRatio <= 0.001f;

        foreach (var ps in smokeParticleSystems)
        {
            if (ps == null) continue;

            bool isAshes = ps.name.Contains("Ashes");
            if (isAshes)
            {
                if (fireRatio <= 0.001f)
                {
                    if (ps.isPlaying)
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    }
                }
                else if (initialSmokeRates.TryGetValue(ps, out float ashBaseRate))
                {
                    var ashEmission = ps.emission;
                    ashEmission.rateOverTimeMultiplier = ashBaseRate * fireRatio;
                    if (!ps.isPlaying) ps.Play(true);
                }
                continue;
            }

            // Compute target ratio for this specific smoke emitter (blending local + global fire progress)
            float targetEmitterRatio = EvaluateLocalFireRatioForEmitter(ps.transform.position, fireRatio);
            float prevSmoothed = smoothedEmitterRatios.TryGetValue(ps, out float curVal) ? curVal : 1.0f;
            float smoothedRatio = Mathf.MoveTowards(prevSmoothed, targetEmitterRatio, Time.deltaTime * smokeThinningSpeed);
            smoothedEmitterRatios[ps] = smoothedRatio;

            // Keep emitter playing so scattered residual smoke particles remain even when fireRatio == 0
            if (!ps.isPlaying)
            {
                ps.Play(true);
            }

            if (initialSmokeRates.TryGetValue(ps, out float baseRate))
            {
                var emission = ps.emission;
                emission.rateOverTimeMultiplier = baseRate * Mathf.Lerp(residualSmokeEmissionRatio, 1.0f, smoothedRatio);
            }

            if (initialSmokeSizes.TryGetValue(ps, out float baseSize))
            {
                var main = ps.main;
                main.startSizeMultiplier = baseSize * Mathf.Lerp(0.75f, 1.0f, smoothedRatio);
            }

            if (initialSmokeColors.TryGetValue(ps, out ParticleSystem.MinMaxGradient baseColorGrad))
            {
                var main = ps.main;
                float alphaMult = Mathf.Lerp(residualSmokeOpacityRatio, 1.0f, smoothedRatio);
                if (baseColorGrad.mode == ParticleSystemGradientMode.TwoColors)
                {
                    Color cMin = baseColorGrad.colorMin;
                    Color cMax = baseColorGrad.colorMax;
                    cMin.a *= alphaMult;
                    cMax.a *= alphaMult;
                    main.startColor = new ParticleSystem.MinMaxGradient(cMin, cMax);
                }
                else if (baseColorGrad.mode == ParticleSystemGradientMode.Color)
                {
                    Color c = baseColorGrad.color;
                    c.a *= alphaMult;
                    main.startColor = new ParticleSystem.MinMaxGradient(c);
                }
            }

            // Gently attenuate already-living particles when smoke intensity drops so the player sees immediate thinning
            if (prevSmoothed - smoothedRatio > 0.0005f)
            {
                int maxCount = ps.main.maxParticles;
                if (liveParticleBuffer == null || liveParticleBuffer.Length < maxCount)
                {
                    liveParticleBuffer = new ParticleSystem.Particle[Mathf.Max(600, maxCount)];
                }

                int numAlive = ps.GetParticles(liveParticleBuffer);
                if (numAlive > 0)
                {
                    float fadeFactor = Mathf.Clamp01(1f - (prevSmoothed - smoothedRatio) * 0.85f);
                    byte minAllowedAlpha = (byte)Mathf.RoundToInt(Mathf.Lerp(55f, 255f, smoothedRatio));
                    for (int i = 0; i < numAlive; i++)
                    {
                        Color32 c32 = liveParticleBuffer[i].startColor;
                        if (c32.a > minAllowedAlpha)
                        {
                            c32.a = (byte)Mathf.Max(minAllowedAlpha, Mathf.RoundToInt(c32.a * fadeFactor));
                            liveParticleBuffer[i].startColor = c32;
                        }
                    }
                    ps.SetParticles(liveParticleBuffer, numAlive);
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
    /// Isolates the office interior on URP Rendering Layer 1 (dimmed emergency lighting + 3D smoke particles)
    /// and the exterior city on URP Rendering Layer 2 (permanent bright daytime sun and sky fill without global dark fog).
    /// </summary>
    public void UpdateIndoorDarknessAndSmokeFog(bool immediate)
    {
        float lerpFactor = immediate ? 1f : Mathf.Clamp01(Time.deltaTime * 3.5f);

        // Keep global distance fog disabled so distant outdoor city buildings and streets are never blackened.
        // Indoor smoke is rendered volumetrically inside OfficeSmokeZone by the 3D particle emitters.
        if (RenderSettings.fog)
        {
            RenderSettings.fog = false;
        }

        // Permanent bright daytime lighting on Rendering Layer 2 (Entorno_Ciudad_Exterior)
        if (outdoorDaylightSun != null)
        {
            if (!outdoorDaylightSun.enabled) outdoorDaylightSun.enabled = true;
            outdoorDaylightSun.intensity = exteriorSunIntensity;
        }

        if (outdoorDaylightSkyFill != null)
        {
            if (!outdoorDaylightSkyFill.enabled) outdoorDaylightSkyFill.enabled = true;
            outdoorDaylightSkyFill.intensity = exteriorSkyFillIntensity;
        }

        if (isPlayerInSmokeZone)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = immediate
                ? indoorDarkAmbientColor
                : Color.Lerp(RenderSettings.ambientLight, indoorDarkAmbientColor, lerpFactor);
            RenderSettings.reflectionIntensity = immediate
                ? indoorReflectionIntensity
                : Mathf.Lerp(RenderSettings.reflectionIntensity, indoorReflectionIntensity, lerpFactor);

            if (directionalLight != null)
            {
                directionalLight.intensity = immediate
                    ? indoorDirectionalLightIntensity
                    : Mathf.Lerp(directionalLight.intensity, indoorDirectionalLightIntensity, lerpFactor);
            }
        }
        else
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = immediate
                ? 1.0f
                : Mathf.Lerp(RenderSettings.ambientIntensity, 1.0f, lerpFactor);
            RenderSettings.reflectionIntensity = immediate
                ? 1.0f
                : Mathf.Lerp(RenderSettings.reflectionIntensity, 1.0f, lerpFactor);

            if (directionalLight != null)
            {
                directionalLight.intensity = immediate
                    ? outdoorDirectionalLightIntensity
                    : Mathf.Lerp(directionalLight.intensity, outdoorDirectionalLightIntensity, lerpFactor);
            }
        }
    }

    /// <summary>
    /// Pins the World Space Canvases (top-left Life/Oxygen bar and top-right Evacuation Stopwatch)
    /// to the player's camera view.
    /// </summary>
    public void UpdateHudPlacement()
    {
        if (playerCamera == null)
        {
            return;
        }

        if (hudCanvas != null)
        {
            RectTransform rt = hudCanvas.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.pivot = new Vector2(0f, 1f);
            }

            Vector3 targetWorldPos = playerCamera.ViewportToWorldPoint(new Vector3(viewportX, viewportY, hudDistance));
            hudCanvas.transform.position = targetWorldPos;
            hudCanvas.transform.rotation = playerCamera.transform.rotation;
        }

        if (timerHudCanvas != null)
        {
            RectTransform trt = timerHudCanvas.GetComponent<RectTransform>();
            if (trt != null)
            {
                trt.pivot = new Vector2(1f, 1f);
            }

            Vector3 timerWorldPos = playerCamera.ViewportToWorldPoint(new Vector3(timerViewportX, timerViewportY, hudDistance));
            timerHudCanvas.transform.position = timerWorldPos;
            timerHudCanvas.transform.rotation = playerCamera.transform.rotation;
        }
    }

    /// <summary>
    /// Resets the evacuation stopwatch to 00:00.00 and starts counting immediately.
    /// </summary>
    [ContextMenu("Reset Evacuation Stopwatch (00:00.00)")]
    public void ResetEvacuationTimer()
    {
        elapsedEvacuationTime = 0f;
        hasEvacuatedOutside = false;
        isEvacuationTimerRunning = true;
        lastDisplayedCentiseconds = -1;
        UpdateEvacuationTimerVisuals(true);
    }

    /// <summary>
    /// Formats a duration in seconds as MM:SS.cc (e.g., 01:06.49).
    /// </summary>
    public static string FormatEvacuationTime(float timeSeconds)
    {
        float clamped = Mathf.Max(0f, timeSeconds);
        int totalCentiseconds = Mathf.FloorToInt(clamped * 100f);
        int centiseconds = totalCentiseconds % 100;
        int totalSeconds = totalCentiseconds / 100;
        int seconds = totalSeconds % 60;
        int minutes = Mathf.Min(99, totalSeconds / 60);
        return string.Format("{0:00}:{1:00}.{2:00}", minutes, seconds, centiseconds);
    }

    private void CacheSevenSegmentReferences()
    {
        if (timerHudCanvas == null)
        {
            return;
        }

        if (timerDigitSegments != null && timerDigitSegments.Length == 6 && timerDigitSegments[0] != null && timerDigitSegments[0][0] != null)
        {
            return;
        }

        Transform innerBg = timerHudCanvas.transform.Find("TimerBorderFrame/TimerInnerBackground");
        if (innerBg == null)
        {
            return;
        }

        string[] digitNames = new string[6] { "Digit_M1", "Digit_M0", "Digit_S1", "Digit_S0", "Digit_C1", "Digit_C0" };
        timerDigitSegments = new Image[6][];
        for (int d = 0; d < 6; d++)
        {
            timerDigitSegments[d] = new Image[7];
            Transform digitTr = innerBg.Find(digitNames[d]);
            if (digitTr != null)
            {
                for (int s = 0; s < 7; s++)
                {
                    Transform segTr = digitTr.Find("Seg_" + s);
                    if (segTr != null)
                    {
                        timerDigitSegments[d][s] = segTr.GetComponent<Image>();
                    }
                }
            }
        }

        string[] punctNames = new string[3] { "Colon_Top", "Colon_Bottom", "Decimal_Dot" };
        timerPunctuationImages = new Image[3];
        for (int p = 0; p < 3; p++)
        {
            Transform pTr = innerBg.Find(punctNames[p]);
            if (pTr != null)
            {
                timerPunctuationImages[p] = pTr.GetComponent<Image>();
            }
        }
    }

    /// <summary>
    /// Updates the 7-segment digital stopwatch display in the top-right corner (MM:SS.cc).
    /// </summary>
    public void UpdateEvacuationTimerVisuals(bool forceRefresh)
    {
        if (timerHudCanvas == null)
        {
            return;
        }

        CacheSevenSegmentReferences();

        float clamped = Mathf.Max(0f, elapsedEvacuationTime);
        int totalCentiseconds = Mathf.FloorToInt(clamped * 100f);
        if (!forceRefresh && totalCentiseconds == lastDisplayedCentiseconds)
        {
            return;
        }
        lastDisplayedCentiseconds = totalCentiseconds;

        int centiseconds = totalCentiseconds % 100;
        int totalSeconds = totalCentiseconds / 100;
        int seconds = totalSeconds % 60;
        int minutes = Mathf.Min(99, totalSeconds / 60);

        int[] digits = new int[6]
        {
            minutes / 10,
            minutes % 10,
            seconds / 10,
            seconds % 10,
            centiseconds / 10,
            centiseconds % 10
        };

        if (timerDigitSegments != null)
        {
            for (int d = 0; d < 6; d++)
            {
                Image[] segs = timerDigitSegments[d];
                if (segs == null) continue;
                byte mask = SevenSegmentDigitMasks[Mathf.Clamp(digits[d], 0, 9)];
                for (int s = 0; s < 7; s++)
                {
                    if (segs[s] != null)
                    {
                        bool isLit = (mask & (1 << s)) != 0;
                        segs[s].color = isLit ? timerActiveSegmentColor : timerInactiveSegmentColor;
                    }
                }
            }
        }

        if (timerPunctuationImages != null)
        {
            for (int p = 0; p < timerPunctuationImages.Length; p++)
            {
                if (timerPunctuationImages[p] != null)
                {
                    timerPunctuationImages[p].color = timerActiveSegmentColor;
                }
            }
        }

        if (timerReadoutText != null)
        {
            timerReadoutText.text = string.Format("{0:00}:{1:00}.{2:00}", minutes, seconds, centiseconds);
        }
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
