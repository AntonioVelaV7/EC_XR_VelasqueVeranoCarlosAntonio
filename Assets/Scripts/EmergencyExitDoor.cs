using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Controls the Emergency Exit Door ("SALIDA DE EMERGENCIA") installed in Pared_Norte_Der (3).
/// Supports opening via XR controller ray/direct interaction (Select or Activate on the panic bar or door leaf),
/// physical hand/controller proximity push on the crash bar, and walking into the panic bar from inside the office.
/// Smoothly animates the crash bar depression and outward 96-degree door swing toward the exterior street,
/// and automatically resets to closed on Game Over or Victory retry.
/// </summary>
[DisallowMultipleComponent]
public class EmergencyExitDoor : MonoBehaviour
{
    [Header("Door Pivot & Panic Bar References")]
    [Tooltip("Hinge pivot Transform on the right frame jamb that rotates around local Y to swing the door outward.")]
    [SerializeField]
    private Transform doorLeafPivot;

    [Tooltip("Movable black crash bar Transform that depresses into the side brackets when pushed.")]
    [SerializeField]
    private Transform panicBarPushAssembly;

    [Tooltip("Solid BoxCollider on the door leaf that blocks the doorway when closed and swings away when opened.")]
    [SerializeField]
    private BoxCollider doorLeafCollider;

    [Tooltip("XRSimpleInteractable components on the panic bar and door leaf for VR controller ray/hand activation.")]
    [SerializeField]
    private XRSimpleInteractable[] doorInteractables;

    [Header("Swing & Crash Bar Animation Settings")]
    [Tooltip("Outward opening angle in degrees around the hinge Y axis (positive swings outward toward -Z street).")]
    [Range(70f, 120f)]
    [SerializeField]
    private float openAngle = 96f;

    [Tooltip("Angular speed in degrees per second when the emergency door swings open or closed.")]
    [Range(60f, 360f)]
    [SerializeField]
    private float swingSpeed = 210f;

    [Tooltip("Local Z depression offset (meters) of the black crash bar when pressed inward.")]
    [SerializeField]
    private float barDepressOffsetZ = -0.026f;

    [Tooltip("Speed at which the panic bar depresses and releases.")]
    [SerializeField]
    private float barAnimationSpeed = 14f;

    [Header("VR & Player Push Detection")]
    [Tooltip("If true, walking right up against the crash bar or pushing it with a VR hand automatically unlatches and swings the door outward.")]
    [SerializeField]
    private bool autoOpenOnPlayerOrHandPush = true;

    [Tooltip("Interior Z distance from the door plane (meters) within which the player's body or hand pushes the crash bar open.")]
    [Range(0.45f, 1.35f)]
    [SerializeField]
    private float playerPushDistanceZ = 0.98f;

    [Tooltip("Half-width (meters) around the doorway center X (-4.35) within which pushing triggers the crash bar.")]
    [Range(0.35f, 0.90f)]
    [SerializeField]
    private float doorwayPushHalfWidthX = 0.66f;

    [Header("Runtime State")]
    [Tooltip("True when the emergency exit door is unlatched and open (or opening).")]
    [SerializeField]
    private bool isOpen = false;

    [SerializeField]
    private float currentAngle = 0f;

    private float currentBarHoldTimer = 0f;
    private float currentBarOffsetZ = 0f;
    private Vector3 initialBarLocalPos;
    private bool initialBarPosCached = false;

    private Camera playerCamera;
    private CharacterController playerCharacterController;
    private XRBaseInteractor[] sceneInteractors;
    private AudioSource latchAudioSource;
    private AudioClip latchClickClip;

    private static readonly List<EmergencyExitDoor> AllDoors = new List<EmergencyExitDoor>();

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (!AllDoors.Contains(this))
        {
            AllDoors.Add(this);
        }

        EnsureReferences();
        CacheInitialBarPose();
        SetupProceduralLatchAudio();
        ApplyInstantPose(isOpen ? openAngle : 0f, 0f);
    }

    private void OnEnable()
    {
        if (!AllDoors.Contains(this))
        {
            AllDoors.Add(this);
        }

        EnsureReferences();
        RegisterInteractableListeners(true);
    }

    private void OnDisable()
    {
        RegisterInteractableListeners(false);
    }

    private void OnDestroy()
    {
        AllDoors.Remove(this);
    }

    private void RegisterInteractableListeners(bool subscribe)
    {
        if (doorInteractables == null)
        {
            return;
        }

        foreach (var interactable in doorInteractables)
        {
            if (interactable == null) continue;

            interactable.selectEntered.RemoveListener(OnDoorSelectEntered);
            interactable.activated.RemoveListener(OnDoorActivated);

            if (subscribe)
            {
                interactable.selectEntered.AddListener(OnDoorSelectEntered);
                interactable.activated.AddListener(OnDoorActivated);
            }
        }
    }

    private void OnDoorSelectEntered(SelectEnterEventArgs args)
    {
        TriggerPanicBarInteraction();
    }

    private void OnDoorActivated(ActivateEventArgs args)
    {
        TriggerPanicBarInteraction();
    }

    /// <summary>
    /// Triggered when the player clicks/grabs/activates the panic bar or door with a VR controller.
    /// Opens the door if closed, or toggles it if already fully open.
    /// </summary>
    public void TriggerPanicBarInteraction()
    {
        if (!isOpen)
        {
            OpenDoor();
        }
        else if (Mathf.Abs(currentAngle - openAngle) < 5f)
        {
            // Allow closing if clicked again after opening, unless player is standing right inside the doorway threshold
            if (!IsPlayerInsideDoorwayThreshold())
            {
                CloseDoor();
            }
        }
    }

    /// <summary>
    /// Unlatches the panic bar and swings the emergency exit door outward toward the street.
    /// </summary>
    [ContextMenu("Abrir Puerta de Emergencia")]
    public void OpenDoor()
    {
        EnsureReferences();
        CacheInitialBarPose();

        bool wasClosed = !isOpen;
        isOpen = true;
        currentBarHoldTimer = 0.45f;

        if (wasClosed)
        {
            PlayLatchSound();
            SetIgnorePlayerDoorCollision(true);
        }
    }

    /// <summary>
    /// Swings the emergency exit door back to its closed position.
    /// </summary>
    [ContextMenu("Cerrar Puerta de Emergencia")]
    public void CloseDoor()
    {
        EnsureReferences();
        CacheInitialBarPose();

        bool wasOpen = isOpen;
        isOpen = false;
        currentBarHoldTimer = 0.20f;

        if (wasOpen)
        {
            PlayLatchSound();
        }
    }

    /// <summary>
    /// Toggles the emergency door between open and closed states.
    /// </summary>
    [ContextMenu("Alternar Puerta (Abrir/Cerrar)")]
    public void ToggleDoor()
    {
        if (isOpen)
        {
            CloseDoor();
        }
        else
        {
            OpenDoor();
        }
    }

    /// <summary>
    /// Immediately resets the emergency exit door to its closed and latched state (called on Game Over / Victory retry).
    /// </summary>
    public void ResetDoor()
    {
        EnsureReferences();
        CacheInitialBarPose();

        isOpen = false;
        currentAngle = 0f;
        currentBarHoldTimer = 0f;
        currentBarOffsetZ = 0f;

        ApplyInstantPose(0f, 0f);
        SetIgnorePlayerDoorCollision(false);
    }

    /// <summary>
    /// Resets all EmergencyExitDoor instances in the scene to their closed initial state.
    /// </summary>
    public static void ResetAllDoors()
    {
        EmergencyExitDoor[] doors = Object.FindObjectsByType<EmergencyExitDoor>(FindObjectsInactive.Include);
        if (doors == null) return;

        foreach (var d in doors)
        {
            if (d != null)
            {
                d.ResetDoor();
            }
        }
    }

    private void Update()
    {
        EnsureReferences();
        CacheInitialBarPose();

        if (!isOpen && autoOpenOnPlayerOrHandPush)
        {
            if (CheckPlayerOrControllerPushingCrashBar())
            {
                OpenDoor();
            }
        }

        // Update crash bar depression animation
        if (currentBarHoldTimer > 0f)
        {
            currentBarHoldTimer -= Time.deltaTime;
        }

        float targetBarZ = (currentBarHoldTimer > 0f || (isOpen && currentAngle < openAngle * 0.65f))
            ? barDepressOffsetZ
            : 0f;

        currentBarOffsetZ = Mathf.MoveTowards(currentBarOffsetZ, targetBarZ, Time.deltaTime * Mathf.Abs(barDepressOffsetZ) * barAnimationSpeed);

        // Update door leaf outward swing angle
        float targetAngle = isOpen ? openAngle : 0f;
        if (!Mathf.Approximately(currentAngle, targetAngle))
        {
            currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, swingSpeed * Time.deltaTime);
            if (Mathf.Approximately(currentAngle, targetAngle))
            {
                SetIgnorePlayerDoorCollision(false);
            }
        }

        ApplyInstantPose(currentAngle, currentBarOffsetZ);
    }

    private bool CheckPlayerOrControllerPushingCrashBar()
    {
        Vector3 doorCenter = transform.position;

        // 1. Check if player camera / CharacterController walked right up to the interior crash bar
        if (playerCamera != null)
        {
            Vector3 camPos = playerCamera.transform.position;
            float dx = Mathf.Abs(camPos.x - doorCenter.x);
            // Interior is +Z side of doorCenter.z (-11.85). Player pushes from inside when z is in [doorCenter.z - 0.15, doorCenter.z + playerPushDistanceZ]
            float dz = camPos.z - doorCenter.z;
            if (dx <= doorwayPushHalfWidthX && dz >= -0.18f && dz <= playerPushDistanceZ)
            {
                return true;
            }
        }

        if (playerCharacterController != null)
        {
            Vector3 ccPos = playerCharacterController.bounds.center;
            float dx = Mathf.Abs(ccPos.x - doorCenter.x);
            float dz = ccPos.z - doorCenter.z;
            if (dx <= doorwayPushHalfWidthX && dz >= -0.18f && dz <= playerPushDistanceZ)
            {
                return true;
            }
        }

        // 2. Check if any VR hand / controller interactor physically touches or pushes the panic bar area
        if (panicBarPushAssembly != null && sceneInteractors != null)
        {
            Vector3 barWorldPos = panicBarPushAssembly.position;
            foreach (var interactor in sceneInteractors)
            {
                if (interactor == null || !interactor.isActiveAndEnabled) continue;
                Vector3 handPos = interactor.transform.position;
                if (Mathf.Abs(handPos.x - barWorldPos.x) <= 0.58f &&
                    Mathf.Abs(handPos.y - barWorldPos.y) <= 0.28f &&
                    Mathf.Abs(handPos.z - barWorldPos.z) <= 0.30f)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsPlayerInsideDoorwayThreshold()
    {
        if (playerCamera == null) return false;
        Vector3 camPos = playerCamera.transform.position;
        Vector3 doorCenter = transform.position;
        return Mathf.Abs(camPos.x - doorCenter.x) < 0.85f && Mathf.Abs(camPos.z - doorCenter.z) < 0.95f;
    }

    private void ApplyInstantPose(float angleDeg, float barOffsetZ)
    {
        if (doorLeafPivot != null)
        {
            doorLeafPivot.localRotation = Quaternion.Euler(0f, angleDeg, 0f);
        }

        if (panicBarPushAssembly != null && initialBarPosCached)
        {
            Vector3 pos = initialBarLocalPos;
            pos.z += barOffsetZ;
            panicBarPushAssembly.localPosition = pos;
        }
    }

    private void CacheInitialBarPose()
    {
        if (!initialBarPosCached && panicBarPushAssembly != null)
        {
            initialBarLocalPos = panicBarPushAssembly.localPosition;
            initialBarPosCached = true;
        }
    }

    private void SetIgnorePlayerDoorCollision(bool ignore)
    {
        if (playerCharacterController == null)
        {
            playerCharacterController = Object.FindAnyObjectByType<CharacterController>();
        }

        if (playerCharacterController != null && doorLeafCollider != null)
        {
            Physics.IgnoreCollision(doorLeafCollider, playerCharacterController, ignore);
        }
    }

    private void SetupProceduralLatchAudio()
    {
        latchAudioSource = GetComponent<AudioSource>();
        if (latchAudioSource == null)
        {
            latchAudioSource = gameObject.AddComponent<AudioSource>();
        }

        latchAudioSource.playOnAwake = false;
        latchAudioSource.loop = false;
        latchAudioSource.spatialBlend = 0.85f;
        latchAudioSource.minDistance = 1.0f;
        latchAudioSource.maxDistance = 10.0f;
        latchAudioSource.volume = 0.65f;

        if (latchClickClip == null)
        {
            int sampleRate = 22050;
            int sampleCount = Mathf.RoundToInt(sampleRate * 0.14f);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                // Metallic crash-bar clack: sharp initial transient + brief low metallic resonance
                float env1 = Mathf.Exp(-t * 95f);
                float env2 = t > 0.032f ? Mathf.Exp(-(t - 0.032f) * 65f) : 0f;
                float tone = Mathf.Sin(2f * Mathf.PI * 420f * t) * 0.45f +
                             Mathf.Sin(2f * Mathf.PI * 1180f * t) * 0.35f +
                             Mathf.Sin(2f * Mathf.PI * 2450f * t) * 0.20f;
                float noise = (Mathf.PerlinNoise(i * 0.37f, 0.5f) * 2f - 1f) * 0.55f;
                samples[i] = Mathf.Clamp((tone + noise) * (env1 + env2 * 0.8f), -1f, 1f);
            }

            latchClickClip = AudioClip.Create("EmergencyDoorLatchClack", sampleCount, 1, sampleRate, false);
            latchClickClip.SetData(samples, 0);
        }
    }

    private void PlayLatchSound()
    {
        if (latchAudioSource != null && latchClickClip != null && Application.isPlaying)
        {
            latchAudioSource.PlayOneShot(latchClickClip, 0.70f);
        }
    }

    private void EnsureReferences()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (playerCharacterController == null)
        {
            playerCharacterController = Object.FindAnyObjectByType<CharacterController>();
        }

        if (doorLeafPivot == null)
        {
            doorLeafPivot = transform.Find("Pivote_Hoja_Puerta");
        }

        if (doorLeafPivot != null)
        {
            if (panicBarPushAssembly == null)
            {
                panicBarPushAssembly = doorLeafPivot.Find("Hoja_Puerta_Emergencia/Barra_Antipanico/Barra_Antipanico_Movil");
            }

            if (doorLeafCollider == null)
            {
                Transform leafTr = doorLeafPivot.Find("Hoja_Puerta_Emergencia");
                if (leafTr != null)
                {
                    doorLeafCollider = leafTr.GetComponent<BoxCollider>();
                }
            }
        }

        if (doorInteractables == null || doorInteractables.Length == 0)
        {
            doorInteractables = GetComponentsInChildren<XRSimpleInteractable>(true);
        }

        if (sceneInteractors == null || sceneInteractors.Length == 0)
        {
            sceneInteractors = Object.FindObjectsByType<XRBaseInteractor>(FindObjectsInactive.Include);
        }
    }
}
