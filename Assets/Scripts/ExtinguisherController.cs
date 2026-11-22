using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Controls the pressurized smoke/chemical spray emission, 3D spatial spray audio,
/// chemical tank depletion, low-pressure spray force/audio reduction, and World Space HUD
/// for a fire extinguisher integrated with Unity 6 (URP) and XR Interaction Toolkit 3.x.
/// </summary>
[DisallowMultipleComponent]
public class ExtinguisherController : MonoBehaviour
{
    [Header("Particle System")]
    [Tooltip("Reference to the StylizedSmokeVFX ParticleSystem component.")]
    [SerializeField]
    private ParticleSystem smokeParticles;

    [Header("Spray Detection Trigger (Optional)")]
    [Tooltip("Optional trigger collider along the nozzle spray cone. Activated while spraying.")]
    [SerializeField]
    private Collider sprayTrigger;

    [Header("XR Interaction Toolkit (Optional)")]
    [Tooltip("Reference to the XRGrabInteractable on this object. Auto-cached if left unassigned.")]
    [SerializeField]
    private XRGrabInteractable grabInteractable;

    [Header("Spray Audio (3D Sound)")]
    [Tooltip("AudioSource playing the pressurized spray sound while the extinguisher is active.")]
    [SerializeField]
    private AudioSource sprayAudioSource;

    [Tooltip("AudioClip for the extinguisher spray (Assets/Audio/ExtintorAudio.wav).")]
    [SerializeField]
    private AudioClip sprayAudioClip;

    [Tooltip("Base audio volume at normal extinguisher pressure.")]
    [Range(0.1f, 1f)]
    [SerializeField]
    private float baseAudioVolume = 0.90f;

    [Tooltip("Minimum audio volume when chemical pressure is critically low.")]
    [Range(0.05f, 1f)]
    [SerializeField]
    private float minLowPressureVolume = 0.35f;

    [Tooltip("Minimum audio pitch when chemical pressure is critically low.")]
    [Range(0.5f, 1f)]
    [SerializeField]
    private float minLowPressurePitch = 0.78f;

    [Tooltip("Offset in seconds to skip initial silence at the very beginning of the audio clip.")]
    [SerializeField]
    private float audioStartOffset = 0.14f;

    [Tooltip("Loop sustain offset in seconds so continuous spraying loops seamlessly without silent gaps.")]
    [SerializeField]
    private float audioLoopSustainOffset = 0.30f;

    [Header("Chemical Tank & Depletion")]
    [Tooltip("Maximum chemical charge of the extinguisher.")]
    [SerializeField]
    private float maxChemical = 250f;

    [Tooltip("Current remaining chemical charge.")]
    [SerializeField]
    private float currentChemical = 250f;

    [Tooltip("Chemical units consumed per second of continuous spraying.")]
    [SerializeField]
    private float consumptionRate = 6.5f;

    [Tooltip("True while the extinguisher is actively spraying chemical.")]
    [SerializeField]
    private bool isSpraying = false;

    [Header("Low-Chemical Force Reduction")]
    [Tooltip("Normalized threshold (0-1) below which the spray progressively loses force and reach.")]
    [Range(0.05f, 0.5f)]
    [SerializeField]
    private float lowChemicalThreshold = 0.30f;

    [Tooltip("Minimum particle speed multiplier when chemical is critically low.")]
    [Range(0.1f, 1f)]
    [SerializeField]
    private float minSpeedMultiplier = 0.25f;

    [Tooltip("Minimum particle emission rate multiplier when chemical is critically low.")]
    [Range(0.05f, 1f)]
    [SerializeField]
    private float minEmissionMultiplier = 0.18f;

    [Tooltip("Minimum particle size multiplier when chemical is critically low.")]
    [Range(0.2f, 1f)]
    [SerializeField]
    private float minSizeMultiplier = 0.55f;

    [Tooltip("Minimum spray trigger distance/reach multiplier when chemical is critically low.")]
    [Range(0.2f, 1f)]
    [SerializeField]
    private float minRangeMultiplier = 0.35f;

    [Header("World Space HUD")]
    [Tooltip("World Space Canvas displaying the remaining chemical bar above the extinguisher.")]
    [SerializeField]
    private Canvas hudCanvas;

    [Tooltip("Filled UI Image representing the orange chemical bar.")]
    [SerializeField]
    private Image chemicalFillImage;

    [Tooltip("Optional TextMeshPro label displaying percentage or low/empty status.")]
    [SerializeField]
    private TMP_Text chemicalStatusText;

    [Tooltip("If true, the HUD only shows while the extinguisher is grabbed or actively spraying.")]
    [SerializeField]
    private bool showHudOnlyWhenGrabbed = true;

    [Tooltip("If true, the HUD rotates smoothly to face the player's Main Camera.")]
    [SerializeField]
    private bool billboardHudToCamera = true;

    [Tooltip("Local anchor point on the extinguisher where the HUD floats.")]
    [SerializeField]
    private Vector3 hudLocalAnchor = new Vector3(0f, 0f, 0.47f);

    [Tooltip("Small offset in meters toward the camera to prevent clipping into nearby walls or the handle.")]
    [SerializeField]
    private float hudCameraPullOffset = 0.15f;

    [Tooltip("Bar color when chemical charge is normal (> lowChemicalThreshold).")]
    [SerializeField]
    private Color fullChemicalColor = new Color(1.0f, 0.53f, 0.05f, 1.0f);

    [Tooltip("Bar color when chemical charge is critically low.")]
    [SerializeField]
    private Color lowChemicalColor = new Color(0.95f, 0.18f, 0.05f, 1.0f);

    // Cached initial particle and trigger baselines for force modulation
    private bool baselinesCached;
    private float baseSpeedMin = 4f;
    private float baseSpeedMax = 6f;
    private ParticleSystemCurveMode baseSpeedMode = ParticleSystemCurveMode.TwoConstants;
    private float baseSizeMin = 1.4f;
    private float baseSizeMax = 1.8f;
    private ParticleSystemCurveMode baseSizeMode = ParticleSystemCurveMode.TwoConstants;
    private float baseEmissionRate = 110f;
    private Vector3 baseTriggerLocalPos = new Vector3(0f, 0f, 1.2f);
    private Vector3 baseTriggerLocalScale = Vector3.one;
    private static Sprite fallbackWhiteSprite;

    #region Properties
    public ParticleSystem SmokeParticles
    {
        get => smokeParticles;
        set => smokeParticles = value;
    }

    public ParticleSystem FoamParticles
    {
        get => smokeParticles;
        set => smokeParticles = value;
    }

    public XRGrabInteractable GrabInteractable
    {
        get => grabInteractable;
        set => grabInteractable = value;
    }

    public Collider SprayTrigger
    {
        get => sprayTrigger;
        set => sprayTrigger = value;
    }

    public AudioSource SprayAudioSource
    {
        get => sprayAudioSource;
        set => sprayAudioSource = value;
    }

    public float MaxChemical
    {
        get => maxChemical;
        set => maxChemical = Mathf.Max(0.01f, value);
    }

    public float CurrentChemical => currentChemical;

    public float ChemicalNormalized => maxChemical > 0f ? Mathf.Clamp01(currentChemical / maxChemical) : 0f;

    public bool IsDepleted => currentChemical <= 0f;

    public bool IsSpraying => isSpraying;

    public float CurrentSprayForceMultiplier { get; private set; } = 1f;
    #endregion

    private void Awake()
    {
        if (grabInteractable == null)
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
        }

        if (smokeParticles == null)
        {
            smokeParticles = GetComponentInChildren<ParticleSystem>(true);
        }

        if (sprayTrigger == null)
        {
            var triggers = GetComponentsInChildren<Collider>(true);
            foreach (var col in triggers)
            {
                if (col.isTrigger && col.gameObject != gameObject)
                {
                    sprayTrigger = col;
                    break;
                }
            }
        }

        if (sprayTrigger != null)
        {
            sprayTrigger.enabled = false;
        }

        if (smokeParticles != null && smokeParticles.isPlaying)
        {
            smokeParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (sprayAudioSource == null)
        {
            sprayAudioSource = GetComponentInChildren<AudioSource>(true);
        }

        if (sprayAudioSource != null)
        {
            sprayAudioSource.playOnAwake = false;
            sprayAudioSource.loop = true;
            if (sprayAudioClip != null && sprayAudioSource.clip == null)
            {
                sprayAudioSource.clip = sprayAudioClip;
            }
            if (sprayAudioSource.isPlaying)
            {
                sprayAudioSource.Stop();
            }
        }

        if (maxChemical <= 0f)
        {
            maxChemical = 250f;
        }
        currentChemical = Mathf.Clamp(currentChemical <= 0f ? maxChemical : currentChemical, 0f, maxChemical);

        CacheBaselines();
        AutoBindHudReferences();
        EnsureFillImageSprite();
        UpdateSprayForce();
        UpdateHudVisuals();
        UpdateHudVisibility();
    }

    private void OnEnable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnSelectEntered);
            grabInteractable.selectExited.AddListener(OnSelectExited);
        }
        UpdateHudVisibility();
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            grabInteractable.selectExited.RemoveListener(OnSelectExited);
        }

        if (sprayAudioSource != null && sprayAudioSource.isPlaying)
        {
            sprayAudioSource.Stop();
        }
    }

    private void Update()
    {
        if (isSpraying)
        {
            if (currentChemical > 0f)
            {
                currentChemical -= consumptionRate * Time.deltaTime;
                if (currentChemical <= 0f)
                {
                    currentChemical = 0f;
                    UpdateSprayForce();
                    UpdateHudVisuals();
                    StopSpraying();
                    Debug.Log("[ExtinguisherController] ¡El químico del extintor se ha agotado por completo! Sonido y partículas detenidos.");
                }
                else
                {
                    UpdateSprayForce();
                    UpdateHudVisuals();
                    MaintainSeamlessSprayAudioLoop();
                }
            }
            else
            {
                StopSpraying();
            }
        }
        else if (sprayAudioSource != null && sprayAudioSource.isPlaying)
        {
            sprayAudioSource.Stop();
        }

        UpdateHudVisibility();
    }

    private void LateUpdate()
    {
        if (!billboardHudToCamera || hudCanvas == null || !hudCanvas.enabled)
        {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        Vector3 anchorWorld = transform.TransformPoint(hudLocalAnchor);
        Vector3 dirFromCamera = anchorWorld - cam.transform.position;
        float dist = dirFromCamera.magnitude;
        if (dist > 0.001f)
        {
            Vector3 forwardDir = dirFromCamera / dist;
            hudCanvas.transform.position = anchorWorld - forwardDir * Mathf.Min(hudCameraPullOffset, dist * 0.4f);

            Vector3 flatForward = new Vector3(forwardDir.x, 0f, forwardDir.z);
            if (flatForward.sqrMagnitude > 0.0001f)
            {
                hudCanvas.transform.rotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
            }
            else
            {
                hudCanvas.transform.rotation = Quaternion.LookRotation(forwardDir, Vector3.up);
            }
        }
    }

    private void MaintainSeamlessSprayAudioLoop()
    {
        if (sprayAudioSource == null)
        {
            return;
        }

        if (sprayAudioClip != null && sprayAudioSource.clip != sprayAudioClip)
        {
            sprayAudioSource.clip = sprayAudioClip;
        }

        AudioClip clip = sprayAudioSource.clip;
        if (clip == null)
        {
            return;
        }

        float sustainStart = Mathf.Clamp(audioLoopSustainOffset, 0f, Mathf.Max(0f, clip.length - 0.1f));
        if (!sprayAudioSource.isPlaying)
        {
            sprayAudioSource.time = sustainStart;
            sprayAudioSource.Play();
        }
        else if (sprayAudioSource.time >= clip.length - 0.06f)
        {
            // Jump back into the active spray body to avoid the 0.14s initial silent lead-in on loop
            sprayAudioSource.time = sustainStart;
        }
    }

    private void CacheBaselines()
    {
        if (baselinesCached)
        {
            return;
        }

        if (smokeParticles != null)
        {
            var main = smokeParticles.main;
            baseSpeedMode = main.startSpeed.mode;
            if (baseSpeedMode == ParticleSystemCurveMode.TwoConstants)
            {
                baseSpeedMin = main.startSpeed.constantMin;
                baseSpeedMax = main.startSpeed.constantMax;
            }
            else
            {
                baseSpeedMin = main.startSpeed.constant;
                baseSpeedMax = main.startSpeed.constant;
            }

            baseSizeMode = main.startSize.mode;
            if (baseSizeMode == ParticleSystemCurveMode.TwoConstants)
            {
                baseSizeMin = main.startSize.constantMin;
                baseSizeMax = main.startSize.constantMax;
            }
            else
            {
                baseSizeMin = main.startSize.constant;
                baseSizeMax = main.startSize.constant;
            }

            var emission = smokeParticles.emission;
            baseEmissionRate = emission.rateOverTimeMultiplier > 0f ? emission.rateOverTimeMultiplier : 110f;
        }

        if (sprayTrigger != null)
        {
            baseTriggerLocalPos = sprayTrigger.transform.localPosition;
            baseTriggerLocalScale = sprayTrigger.transform.localScale;
        }

        baselinesCached = true;
    }

    private void AutoBindHudReferences()
    {
        if (hudCanvas == null)
        {
            Transform hudTr = transform.Find("ExtinguisherHUDCanvas");
            if (hudTr != null)
            {
                hudCanvas = hudTr.GetComponent<Canvas>();
            }
            if (hudCanvas == null)
            {
                hudCanvas = GetComponentInChildren<Canvas>(true);
            }
        }

        if (hudCanvas != null)
        {
            if (chemicalFillImage == null)
            {
                Transform fillTr = hudCanvas.transform.Find("BorderFrame/InnerBackground/ChemicalFillBar");
                if (fillTr != null)
                {
                    chemicalFillImage = fillTr.GetComponent<Image>();
                }
            }

            if (chemicalStatusText == null)
            {
                chemicalStatusText = hudCanvas.GetComponentInChildren<TMP_Text>(true);
            }
        }
    }

    private void EnsureFillImageSprite()
    {
        if (chemicalFillImage == null)
        {
            return;
        }

        if (chemicalFillImage.sprite == null)
        {
            if (fallbackWhiteSprite == null)
            {
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                Color[] cols = new Color[] { Color.white, Color.white, Color.white, Color.white };
                tex.SetPixels(cols);
                tex.Apply();
                fallbackWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
                fallbackWhiteSprite.name = "ProceduralWhiteFillSprite";
            }
            chemicalFillImage.sprite = fallbackWhiteSprite;
        }

        chemicalFillImage.type = Image.Type.Filled;
        chemicalFillImage.fillMethod = Image.FillMethod.Horizontal;
        chemicalFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
    }

    /// <summary>
    /// Modulates particle speed, size, emission density, spray reach, and audio volume/pitch when chemical is low.
    /// </summary>
    private void UpdateSprayForce()
    {
        CacheBaselines();

        float ratio = ChemicalNormalized;
        float lowFactor = 1f;

        if (ratio <= 0f)
        {
            lowFactor = 0f;
            CurrentSprayForceMultiplier = 0f;
        }
        else if (ratio < lowChemicalThreshold && lowChemicalThreshold > 0.0001f)
        {
            lowFactor = Mathf.Clamp01(ratio / lowChemicalThreshold);
            CurrentSprayForceMultiplier = Mathf.Lerp(minSpeedMultiplier, 1f, lowFactor);
        }
        else
        {
            lowFactor = 1f;
            CurrentSprayForceMultiplier = 1f;
        }

        if (smokeParticles != null)
        {
            float speedMult = Mathf.Lerp(minSpeedMultiplier, 1f, lowFactor);
            float sizeMult = Mathf.Lerp(minSizeMultiplier, 1f, lowFactor);
            float emissionMult = Mathf.Lerp(minEmissionMultiplier, 1f, lowFactor);

            var main = smokeParticles.main;
            if (baseSpeedMode == ParticleSystemCurveMode.TwoConstants)
            {
                main.startSpeed = new ParticleSystem.MinMaxCurve(baseSpeedMin * speedMult, baseSpeedMax * speedMult);
            }
            else
            {
                main.startSpeed = new ParticleSystem.MinMaxCurve(baseSpeedMax * speedMult);
            }

            if (baseSizeMode == ParticleSystemCurveMode.TwoConstants)
            {
                main.startSize = new ParticleSystem.MinMaxCurve(baseSizeMin * sizeMult, baseSizeMax * sizeMult);
            }
            else
            {
                main.startSize = new ParticleSystem.MinMaxCurve(baseSizeMax * sizeMult);
            }

            var emission = smokeParticles.emission;
            emission.rateOverTimeMultiplier = baseEmissionRate * emissionMult;
        }

        if (sprayTrigger != null)
        {
            float rangeMult = Mathf.Lerp(minRangeMultiplier, 1f, lowFactor);
            sprayTrigger.transform.localPosition = new Vector3(
                baseTriggerLocalPos.x,
                baseTriggerLocalPos.y,
                baseTriggerLocalPos.z * rangeMult
            );
            sprayTrigger.transform.localScale = new Vector3(
                baseTriggerLocalScale.x,
                baseTriggerLocalScale.y,
                baseTriggerLocalScale.z * rangeMult
            );
        }

        if (sprayAudioSource != null)
        {
            if (ratio <= 0f)
            {
                sprayAudioSource.volume = 0f;
            }
            else
            {
                sprayAudioSource.volume = Mathf.Lerp(minLowPressureVolume, baseAudioVolume, lowFactor);
                sprayAudioSource.pitch = Mathf.Lerp(minLowPressurePitch, 1f, lowFactor);
            }
        }
    }

    /// <summary>
    /// Updates the orange fill bar amount, color transition, and percentage/status text.
    /// </summary>
    public void UpdateHudVisuals()
    {
        float ratio = ChemicalNormalized;

        if (chemicalFillImage != null)
        {
            chemicalFillImage.fillAmount = ratio;

            if (ratio <= lowChemicalThreshold && lowChemicalThreshold > 0.0001f)
            {
                float t = Mathf.Clamp01(ratio / lowChemicalThreshold);
                chemicalFillImage.color = Color.Lerp(lowChemicalColor, fullChemicalColor, t);
            }
            else
            {
                chemicalFillImage.color = fullChemicalColor;
            }
        }

        if (chemicalStatusText != null)
        {
            int pct = Mathf.CeilToInt(ratio * 100f);
            if (ratio <= 0f)
            {
                chemicalStatusText.text = "VACÍO (0%)";
            }
            else if (ratio <= lowChemicalThreshold)
            {
                chemicalStatusText.text = string.Format("BAJA PRESIÓN {0}%", pct);
            }
            else
            {
                chemicalStatusText.text = string.Format("QUÍMICO {0}%", pct);
            }
        }
    }

    private void UpdateHudVisibility()
    {
        if (hudCanvas == null)
        {
            return;
        }

        if (!Application.isPlaying)
        {
            hudCanvas.enabled = true;
            return;
        }

        bool isGrabbed = grabInteractable != null && grabInteractable.isSelected;
        bool shouldShow = !showHudOnlyWhenGrabbed || isGrabbed || isSpraying;

        if (hudCanvas.enabled != shouldShow)
        {
            hudCanvas.enabled = shouldShow;
        }
    }

    /// <summary>
    /// Starts spraying pressurized smoke particles and plays the 3D spray sound if chemical remains.
    /// When chemical is 0%, it refuses to start and ensures sound is stopped.
    /// </summary>
    [ContextMenu("Start Spraying")]
    public void StartSpraying()
    {
        if (IsDepleted)
        {
            StopSpraying();
            UpdateHudVisuals();
            return;
        }

        if (smokeParticles == null)
        {
            Debug.LogWarning($"[{nameof(ExtinguisherController)}] Cannot start spraying: ParticleSystem is not assigned on '{gameObject.name}'.", this);
            return;
        }

        isSpraying = true;
        UpdateSprayForce();

        if (!smokeParticles.isPlaying)
        {
            smokeParticles.Play();
        }

        if (sprayTrigger != null)
        {
            sprayTrigger.enabled = true;
        }

        if (sprayAudioSource != null)
        {
            if (sprayAudioClip != null && sprayAudioSource.clip != sprayAudioClip)
            {
                sprayAudioSource.clip = sprayAudioClip;
            }

            if (sprayAudioSource.clip != null && !sprayAudioSource.isPlaying)
            {
                float startT = Mathf.Clamp(audioStartOffset, 0f, Mathf.Max(0f, sprayAudioSource.clip.length - 0.1f));
                sprayAudioSource.time = startT;
                sprayAudioSource.Play();
            }
        }

        UpdateHudVisibility();
    }

    /// <summary>
    /// Stops spraying pressurized smoke particles and stops the spray sound immediately.
    /// </summary>
    [ContextMenu("Stop Spraying")]
    public void StopSpraying()
    {
        isSpraying = false;

        if (smokeParticles != null && smokeParticles.isPlaying)
        {
            smokeParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        if (sprayTrigger != null)
        {
            sprayTrigger.enabled = false;
        }

        if (sprayAudioSource != null && sprayAudioSource.isPlaying)
        {
            sprayAudioSource.Stop();
        }

        UpdateHudVisibility();
    }

    /// <summary>
    /// Refills the extinguisher back to 100% chemical capacity.
    /// </summary>
    [ContextMenu("Refill Extinguisher")]
    public void RefillExtinguisher()
    {
        currentChemical = maxChemical;
        UpdateSprayForce();
        UpdateHudVisuals();
    }

    #region XR Interaction Toolkit Event Overloads
    /// <summary>
    /// Invoked when the interactable is grabbed by an XR interactor.
    /// </summary>
    public void OnSelectEntered(SelectEnterEventArgs args)
    {
        UpdateHudVisuals();
        UpdateHudVisibility();
    }

    /// <summary>
    /// Invoked when the XR activate interaction occurs (trigger pressed while grabbed).
    /// </summary>
    public void OnActivated(ActivateEventArgs args)
    {
        StartSpraying();
    }

    /// <summary>
    /// Invoked when the XR deactivate interaction occurs (trigger released).
    /// </summary>
    public void OnDeactivated(DeactivateEventArgs args)
    {
        StopSpraying();
    }

    /// <summary>
    /// Invoked when the interactable is dropped or thrown.
    /// </summary>
    public void OnSelectExited(SelectExitEventArgs args)
    {
        StopSpraying();
        UpdateHudVisibility();
    }
    #endregion
}
