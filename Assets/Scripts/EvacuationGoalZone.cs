using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the visual Evacuation Goal Point ("¡META!") placed at Sideway 5 prefab (29),
/// animating its concentric ground target rings, floating holographic sign, downward chevron arrow,
/// and triggering the Victory VR panel on PlayerSmokeHealthHUD when the player reaches the goal.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class EvacuationGoalZone : MonoBehaviour
{
    [Header("Goal Detection Settings")]
    [Tooltip("Reference to PlayerSmokeHealthHUD. Auto-detected if null.")]
    [SerializeField]
    private PlayerSmokeHealthHUD playerHud;

    [Tooltip("Reference to the player's VR Camera. Auto-detected if null.")]
    [SerializeField]
    private Camera playerCamera;

    [Tooltip("Horizontal detection radius (meters) around the goal center that triggers Victory when the player arrives.")]
    [Range(0.8f, 4.0f)]
    [SerializeField]
    private float goalTriggerRadius = 1.65f;

    [Header("Visual & Animation References")]
    [Tooltip("Transform of the outer rotating ground ring.")]
    [SerializeField]
    private Transform outerRingTransform;

    [Tooltip("Transform of the inner counter-rotating ground ring.")]
    [SerializeField]
    private Transform innerRingTransform;

    [Tooltip("Transform of the floating '¡META!' World Space Canvas.")]
    [SerializeField]
    private Transform floatingSignTransform;

    [Tooltip("RectTransform of the bouncing downward arrow inside the floating sign.")]
    [SerializeField]
    private RectTransform arrowRectTransform;

    [Tooltip("Point light illuminating the goal zone on the sidewalk.")]
    [SerializeField]
    private Light goalBeaconLight;

    [Tooltip("Particle system emitting rising celebration sparkles around the goal point.")]
    [SerializeField]
    private ParticleSystem goalParticles;

    [Header("Animation Parameters")]
    [SerializeField]
    private float ringRotationSpeed = 38f;

    [SerializeField]
    private float signBobAmplitude = 0.09f;

    [SerializeField]
    private float signBobFrequency = 2.1f;

    [SerializeField]
    private float baseSignHeight = 1.55f;

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnEnable()
    {
        EnsureReferences();
    }

    private void Update()
    {
        EnsureReferences();
        AnimateGoalVisuals();

        if (!Application.isPlaying || playerHud == null)
        {
            return;
        }

        if (playerHud.IsGameOver || playerHud.IsVictory)
        {
            return;
        }

        if (IsPlayerInsideGoalZone())
        {
            playerHud.TriggerVictory();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!Application.isPlaying || playerHud == null || playerHud.IsGameOver || playerHud.IsVictory)
        {
            return;
        }

        if (other.CompareTag("Player") || other.GetComponentInParent<CharacterController>() != null || other.name.Contains("XR"))
        {
            playerHud.TriggerVictory();
        }
    }

    private bool IsPlayerInsideGoalZone()
    {
        if (playerCamera == null)
        {
            return false;
        }

        Vector3 camPos = playerCamera.transform.position;
        Vector3 goalPos = transform.position;
        float dx = camPos.x - goalPos.x;
        float dz = camPos.z - goalPos.z;
        float horizontalDistSq = dx * dx + dz * dz;
        return horizontalDistSq <= (goalTriggerRadius * goalTriggerRadius);
    }

    private void AnimateGoalVisuals()
    {
        float time = Application.isPlaying ? Time.time : (float) (System.DateTime.Now.TimeOfDay.TotalSeconds % 1000.0);
        float dt = Application.isPlaying ? Time.deltaTime : 0.016f;

        if (outerRingTransform != null)
        {
            outerRingTransform.Rotate(0f, 0f, ringRotationSpeed * dt, Space.Self);
        }

        if (innerRingTransform != null)
        {
            innerRingTransform.Rotate(0f, 0f, -ringRotationSpeed * 1.35f * dt, Space.Self);
        }

        if (floatingSignTransform != null)
        {
            float bobOffset = Mathf.Sin(time * signBobFrequency) * signBobAmplitude;
            Vector3 localPos = floatingSignTransform.localPosition;
            localPos.y = baseSignHeight + bobOffset;
            floatingSignTransform.localPosition = localPos;

            // Gently face the player camera horizontally if available
            if (playerCamera != null)
            {
                Vector3 toCam = playerCamera.transform.position - floatingSignTransform.position;
                toCam.y = 0f;
                if (toCam.sqrMagnitude > 0.01f)
                {
                    floatingSignTransform.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
                }
            }
        }

        if (arrowRectTransform != null)
        {
            float arrowBounce = Mathf.Sin(time * 4.2f) * 8.0f;
            arrowRectTransform.anchoredPosition = new Vector2(28f, -22f + arrowBounce);
        }

        if (goalBeaconLight != null)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 3.0f);
            goalBeaconLight.intensity = Mathf.Lerp(1.6f, 2.8f, pulse);
        }
    }

    private void EnsureReferences()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (playerHud == null)
        {
            playerHud = Object.FindAnyObjectByType<PlayerSmokeHealthHUD>();
        }

        if (outerRingTransform == null)
        {
            Transform groundCanvas = transform.Find("GoalGroundRingsCanvas");
            if (groundCanvas != null)
            {
                outerRingTransform = groundCanvas.Find("OuterTargetRing");
                innerRingTransform = groundCanvas.Find("InnerTargetRing");
            }
        }

        if (floatingSignTransform == null)
        {
            floatingSignTransform = transform.Find("GoalFloatingSignCanvas");
        }

        if (arrowRectTransform == null && floatingSignTransform != null)
        {
            Transform arrowTr = floatingSignTransform.Find("DownArrowIcon");
            if (arrowTr != null)
            {
                arrowRectTransform = arrowTr.GetComponent<RectTransform>();
            }
        }

        if (goalBeaconLight == null)
        {
            goalBeaconLight = GetComponentInChildren<Light>(true);
        }

        if (goalParticles == null)
        {
            goalParticles = GetComponentInChildren<ParticleSystem>(true);
        }
    }
}
