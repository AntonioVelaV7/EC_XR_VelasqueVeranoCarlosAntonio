using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Controls the pressurized smoke/chemical spray emission for a fire extinguisher
/// integrated with Unity 6 (URP) and XR Interaction Toolkit 3.x.
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

    /// <summary>
    /// Gets or sets the target ParticleSystem reference.
    /// </summary>
    public ParticleSystem SmokeParticles
    {
        get => smokeParticles;
        set => smokeParticles = value;
    }

    /// <summary>
    /// Backwards compatibility property for previous references.
    /// </summary>
    public ParticleSystem FoamParticles
    {
        get => smokeParticles;
        set => smokeParticles = value;
    }

    /// <summary>
    /// Gets or sets the target XRGrabInteractable reference.
    /// </summary>
    public XRGrabInteractable GrabInteractable
    {
        get => grabInteractable;
        set => grabInteractable = value;
    }

    /// <summary>
    /// Gets or sets the optional spray trigger collider.
    /// </summary>
    public Collider SprayTrigger
    {
        get => sprayTrigger;
        set => sprayTrigger = value;
    }

    private void Awake()
    {
        // Auto-cache grab interactable if not assigned in Inspector
        if (grabInteractable == null)
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
        }

        // Auto-find child ParticleSystem if unassigned
        if (smokeParticles == null)
        {
            smokeParticles = GetComponentInChildren<ParticleSystem>(true);
        }

        // Auto-find child spray trigger if unassigned
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

        // Safety check on awake to ensure emission is stopped initially
        if (smokeParticles != null && smokeParticles.isPlaying)
        {
            smokeParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    /// <summary>
    /// Starts spraying pressurized smoke particles.
    /// Validates null references before executing any calls.
    /// </summary>
    [ContextMenu("Start Spraying")]
    public void StartSpraying()
    {
        if (smokeParticles == null)
        {
            Debug.LogWarning($"[{nameof(ExtinguisherController)}] Cannot start spraying: ParticleSystem is not assigned on '{gameObject.name}'.", this);
            return;
        }

        if (!smokeParticles.isPlaying)
        {
            smokeParticles.Play();
        }

        if (sprayTrigger != null)
        {
            sprayTrigger.enabled = true;
        }
    }

    /// <summary>
    /// Stops spraying pressurized smoke particles.
    /// Validates null references before executing any calls.
    /// </summary>
    [ContextMenu("Stop Spraying")]
    public void StopSpraying()
    {
        if (smokeParticles != null && smokeParticles.isPlaying)
        {
            smokeParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        if (sprayTrigger != null)
        {
            sprayTrigger.enabled = false;
        }
    }

    #region XR Interaction Toolkit Event Overloads
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
    }
    #endregion
}
