using UnityEngine;

/// <summary>
/// Controls the continuous smoke detector alarm sound and pulsing red emergency beacon light
/// on (Prb)SmokeDetector while the player is trapped inside the dark smoke-filled office.
/// Provides real-time volume reduction controls, automatic stop when the player dies
/// (Game Over at 0% oxygen), and automatic restart on Retry.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class SmokeDetectorAlarm : MonoBehaviour
{
    [Header("Smoke Alarm Audio & Volume Control")]
    [Tooltip("AudioSource playing the looping smoke alarm sound on the ceiling detector.")]
    [SerializeField]
    private AudioSource alarmAudioSource;

    [Tooltip("AudioClip for the smoke alarm (Assets/Audio/SmokeAlarm.wav).")]
    [SerializeField]
    private AudioClip alarmAudioClip;

    [Tooltip("Master volume of the smoke alarm. Defaulted to a moderate level (0.22) so it adds tension without being too loud.")]
    [Range(0f, 1f)]
    [SerializeField]
    private float alarmVolume = 0.22f;

    [Tooltip("Mute toggle to silence the smoke alarm immediately if desired.")]
    [SerializeField]
    private bool muteAlarm = false;

    [Header("3D Spatial & Interior Acoustic Settings")]
    [Tooltip("Spatial blend (0 = 2D, 1 = full 3D). 0.55 keeps 3D ceiling directionality while remaining audible everywhere inside the trapped office.")]
    [Range(0f, 1f)]
    [SerializeField]
    private float spatialBlend = 0.55f;

    [Tooltip("Distance within which the alarm plays at full configured alarmVolume.")]
    [SerializeField]
    private float minDistance = 3.5f;

    [Tooltip("Maximum reach distance of the alarm sound across the building.")]
    [SerializeField]
    private float maxDistance = 24.0f;

    [Tooltip("Volume multiplier applied if the player evacuates outside the smoke-filled office.")]
    [Range(0f, 1f)]
    [SerializeField]
    private float outsideOfficeVolumeMultiplier = 0.40f;

    [Header("Emergency Red Beacon Light")]
    [Tooltip("Point Light under the smoke detector that pulses red while the alarm is active.")]
    [SerializeField]
    private Light emergencyBeaconLight;

    [Tooltip("If true, pulses the red emergency Point Light in the dark office while the alarm sounds.")]
    [SerializeField]
    private bool enableEmergencyLight = true;

    [Tooltip("Red emergency beacon light color.")]
    [SerializeField]
    private Color emergencyLightColor = new Color(1.0f, 0.08f, 0.04f, 1.0f);

    [Tooltip("Peak intensity of the red emergency light pulse.")]
    [Range(0.1f, 5f)]
    [SerializeField]
    private float maxBeaconIntensity = 1.85f;

    [Tooltip("Minimum intensity between pulses.")]
    [Range(0f, 2f)]
    [SerializeField]
    private float minBeaconIntensity = 0.35f;

    [Tooltip("Pulse frequency in Hz synchronized with the emergency alarm cadence.")]
    [Range(0.5f, 6f)]
    [SerializeField]
    private float beaconPulseFrequency = 2.2f;

    [Tooltip("Range in meters of the red ceiling emergency light.")]
    [Range(1f, 20f)]
    [SerializeField]
    private float beaconRange = 12.0f;

    [Header("Runtime State (Read Only)")]
    [SerializeField]
    private bool isAlarmActive = false;

    [SerializeField]
    private bool stoppedByPlayerDeath = false;

    private PlayerSmokeHealthHUD playerHealthHud;

    public float AlarmVolume
    {
        get => alarmVolume;
        set
        {
            alarmVolume = Mathf.Clamp01(value);
            ApplyAudioSettings();
        }
    }

    public bool MuteAlarm
    {
        get => muteAlarm;
        set
        {
            muteAlarm = value;
            ApplyAudioSettings();
        }
    }

    public Light EmergencyBeaconLight => emergencyBeaconLight;

    public bool IsAlarmActive => isAlarmActive && alarmAudioSource != null && alarmAudioSource.isPlaying;

    private void Awake()
    {
        EnsureInitialized();
        ApplyAudioSettings();
    }

    private void Start()
    {
        if (Application.isPlaying)
        {
            StartAlarm();
        }
    }

    private void OnEnable()
    {
        EnsureInitialized();
        ApplyAudioSettings();
    }

    private void OnValidate()
    {
        alarmVolume = Mathf.Clamp01(alarmVolume);
        spatialBlend = Mathf.Clamp01(spatialBlend);
        minDistance = Mathf.Max(0.5f, minDistance);
        maxDistance = Mathf.Max(minDistance + 1f, maxDistance);

        if (alarmAudioSource == null)
        {
            alarmAudioSource = GetComponent<AudioSource>();
        }

        ApplyAudioSettings();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (playerHealthHud == null)
        {
            playerHealthHud = Object.FindAnyObjectByType<PlayerSmokeHealthHUD>();
        }

        if (playerHealthHud != null && playerHealthHud.IsGameOver)
        {
            if (!stoppedByPlayerDeath)
            {
                StopAlarmOnPlayerDeath();
            }
            return;
        }

        if (stoppedByPlayerDeath && playerHealthHud != null && !playerHealthHud.IsGameOver)
        {
            RestartAlarm();
        }

        if (isAlarmActive && alarmAudioSource != null)
        {
            if (!alarmAudioSource.isPlaying && alarmAudioSource.clip != null && !muteAlarm)
            {
                alarmAudioSource.Play();
            }

            ApplyAudioSettings();
            UpdateEmergencyBeaconPulse();
        }
    }

    private void EnsureInitialized()
    {
        if (alarmAudioSource == null)
        {
            alarmAudioSource = GetComponent<AudioSource>();
        }

        if (alarmAudioSource != null)
        {
            if (alarmAudioClip != null && alarmAudioSource.clip != alarmAudioClip)
            {
                alarmAudioSource.clip = alarmAudioClip;
            }
            else if (alarmAudioClip == null && alarmAudioSource.clip != null)
            {
                alarmAudioClip = alarmAudioSource.clip;
            }

            alarmAudioSource.loop = true;
            alarmAudioSource.playOnAwake = false;
        }

        if (emergencyBeaconLight == null)
        {
            Transform lightTr = transform.Find("EmergencyAlarmLight");
            if (lightTr != null)
            {
                emergencyBeaconLight = lightTr.GetComponent<Light>();
            }
            else
            {
                emergencyBeaconLight = GetComponentInChildren<Light>(true);
            }
        }

        if (emergencyBeaconLight != null)
        {
            emergencyBeaconLight.type = LightType.Point;
            emergencyBeaconLight.color = emergencyLightColor;
            emergencyBeaconLight.range = beaconRange;
            emergencyBeaconLight.shadows = LightShadows.None;
        }

        if (playerHealthHud == null)
        {
            playerHealthHud = Object.FindAnyObjectByType<PlayerSmokeHealthHUD>();
        }
    }

    private void UpdateEmergencyBeaconPulse()
    {
        if (emergencyBeaconLight == null)
        {
            return;
        }

        if (!enableEmergencyLight || muteAlarm || stoppedByPlayerDeath)
        {
            if (emergencyBeaconLight.enabled)
            {
                emergencyBeaconLight.intensity = 0f;
                emergencyBeaconLight.enabled = false;
            }
            return;
        }

        if (!emergencyBeaconLight.enabled)
        {
            emergencyBeaconLight.enabled = true;
        }

        emergencyBeaconLight.color = emergencyLightColor;
        emergencyBeaconLight.range = beaconRange;

        float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * beaconPulseFrequency * Mathf.PI * 2f);
        float sharpPulse = Mathf.Pow(wave, 1.6f);
        emergencyBeaconLight.intensity = Mathf.Lerp(minBeaconIntensity, maxBeaconIntensity, sharpPulse);
    }

    /// <summary>
    /// Updates the AudioSource volume, mute state, and spatial parameters in real time.
    /// </summary>
    public void ApplyAudioSettings()
    {
        if (alarmAudioSource == null)
        {
            return;
        }

        if (alarmAudioClip != null && alarmAudioSource.clip != alarmAudioClip)
        {
            alarmAudioSource.clip = alarmAudioClip;
        }

        alarmAudioSource.loop = true;
        alarmAudioSource.spatialBlend = spatialBlend;
        alarmAudioSource.minDistance = minDistance;
        alarmAudioSource.maxDistance = maxDistance;
        alarmAudioSource.rolloffMode = AudioRolloffMode.Linear;
        alarmAudioSource.mute = muteAlarm;

        float effectiveVolume = alarmVolume;
        if (Application.isPlaying && playerHealthHud != null)
        {
            if (playerHealthHud.IsGameOver)
            {
                effectiveVolume = 0f;
            }
            else if (!playerHealthHud.IsPlayerInSmokeZone)
            {
                effectiveVolume *= outsideOfficeVolumeMultiplier;
            }
        }

        alarmAudioSource.volume = Mathf.Clamp01(effectiveVolume);

        if (emergencyBeaconLight != null)
        {
            emergencyBeaconLight.color = emergencyLightColor;
            emergencyBeaconLight.range = beaconRange;
            if (!enableEmergencyLight || muteAlarm || stoppedByPlayerDeath)
            {
                emergencyBeaconLight.intensity = 0f;
                emergencyBeaconLight.enabled = false;
            }
        }
    }

    /// <summary>
    /// Starts or resumes the continuous smoke alarm sound and red emergency beacon light.
    /// </summary>
    public void StartAlarm()
    {
        EnsureInitialized();
        stoppedByPlayerDeath = false;
        isAlarmActive = true;
        ApplyAudioSettings();

        if (emergencyBeaconLight != null && enableEmergencyLight && !muteAlarm)
        {
            emergencyBeaconLight.enabled = true;
            UpdateEmergencyBeaconPulse();
        }

        if (alarmAudioSource != null && alarmAudioSource.clip != null && !alarmAudioSource.isPlaying)
        {
            alarmAudioSource.time = 0f;
            alarmAudioSource.Play();
        }
    }

    /// <summary>
    /// Immediately stops the smoke alarm and turns off the emergency beacon when the player dies (0% oxygen Game Over).
    /// </summary>
    public void StopAlarmOnPlayerDeath()
    {
        stoppedByPlayerDeath = true;
        isAlarmActive = false;

        if (alarmAudioSource != null)
        {
            if (alarmAudioSource.isPlaying)
            {
                alarmAudioSource.Stop();
            }
            alarmAudioSource.volume = 0f;
        }

        if (emergencyBeaconLight != null)
        {
            emergencyBeaconLight.intensity = 0f;
            emergencyBeaconLight.enabled = false;
        }
    }

    /// <summary>
    /// Restarts the smoke alarm and emergency beacon when the player clicks Retry after Game Over.
    /// </summary>
    public void RestartAlarm()
    {
        if (alarmAudioSource != null && alarmAudioSource.isPlaying)
        {
            alarmAudioSource.Stop();
        }

        StartAlarm();
    }

    /// <summary>
    /// Sets the alarm volume (0.0 to 1.0) in real time.
    /// </summary>
    public void SetVolume(float newVolume)
    {
        AlarmVolume = newVolume;
    }

    [ContextMenu("Bajar Volumen Alarma (-10%)")]
    public void LowerVolumeContext()
    {
        AlarmVolume = Mathf.Clamp01(alarmVolume - 0.10f);
        Debug.Log(string.Format("[SmokeDetectorAlarm] Volumen de alarma reducido a {0}%.", Mathf.RoundToInt(alarmVolume * 100f)));
    }

    [ContextMenu("Subir Volumen Alarma (+10%)")]
    public void RaiseVolumeContext()
    {
        AlarmVolume = Mathf.Clamp01(alarmVolume + 0.10f);
        Debug.Log(string.Format("[SmokeDetectorAlarm] Volumen de alarma aumentado a {0}%.", Mathf.RoundToInt(alarmVolume * 100f)));
    }

    [ContextMenu("Volumen Suave (15%)")]
    public void SetLowVolumePreset()
    {
        AlarmVolume = 0.15f;
        Debug.Log("[SmokeDetectorAlarm] Volumen de alarma ajustado a modo suave (15%).");
    }

    [ContextMenu("Volumen Moderado Por Defecto (22%)")]
    public void SetDefaultVolumePreset()
    {
        AlarmVolume = 0.22f;
        Debug.Log("[SmokeDetectorAlarm] Volumen de alarma ajustado a nivel moderado por defecto (22%).");
    }

    [ContextMenu("Silenciar / Activar Alarma (Mute)")]
    public void ToggleMuteContext()
    {
        MuteAlarm = !muteAlarm;
        Debug.Log(string.Format("[SmokeDetectorAlarm] Alarma {0}.", muteAlarm ? "SILENCIADA" : "ACTIVADA"));
    }
}
