using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls progressive fire hazard extinguishing behavior when sprayed by a fire extinguisher.
/// Integrated with Unity 6 (URP) and XR Interaction Toolkit 3.x.
/// </summary>
[DisallowMultipleComponent]
public class ExtinguishableFire : MonoBehaviour
{
    [Header("Extinguish Parameters")]
    [Tooltip("Maximum health/fire intensity.")]
    [SerializeField]
    private float maxHealth = 100f;

    [Tooltip("Extinguish damage dealt per second of continuous spray contact.")]
    [SerializeField]
    private float extinguishSpeed = 30f;

    [Tooltip("Indicates whether the fire has been completely extinguished.")]
    [SerializeField]
    private bool isExtinguished = false;

    [Header("Current State (Read Only)")]
    [SerializeField]
    private float currentHealth;

    [Header("Visual References")]
    [Tooltip("Point light illuminating the fire. Auto-detected if null.")]
    [SerializeField]
    private Light fireLight;

    [Tooltip("All child particle systems that compose the fire VFX. Auto-cached if empty.")]
    [SerializeField]
    private ParticleSystem[] childParticleSystems;

    [Tooltip("Primary collider for detection. Auto-cached if null.")]
    [SerializeField]
    private Collider fireCollider;

    [Tooltip("Target full intensity of the fire light. Defaults to 1.2f.")]
    [SerializeField]
    private float baseLightIntensity = 1.2f;

    // Initial state cache
    private Vector3 initialScale;
    private readonly Dictionary<ParticleSystem, float> initialEmissionRates = new Dictionary<ParticleSystem, float>();

    #region Properties
    public float MaxHealth
    {
        get => maxHealth;
        set => maxHealth = value;
    }

    public float CurrentHealth => currentHealth;

    public float ExtinguishSpeed
    {
        get => extinguishSpeed;
        set => extinguishSpeed = value;
    }

    public bool IsExtinguished => isExtinguished;

    public float BaseLightIntensity
    {
        get => baseLightIntensity;
        set => baseLightIntensity = value;
    }

    /// <summary>
    /// Gets or sets the target point light reference.
    /// </summary>
    public Light FireLight
    {
        get => fireLight;
        set => fireLight = value;
    }
    #endregion

    private void Awake()
    {
        EnsureInitialized();
    }

    private void EnsureInitialized()
    {
        if (initialScale == Vector3.zero)
        {
            initialScale = transform.localScale == Vector3.zero ? Vector3.one : transform.localScale;
        }

        if (currentHealth <= 0f && !isExtinguished)
        {
            currentHealth = maxHealth;
        }

        // Auto-cache child Particle Systems
        if (childParticleSystems == null || childParticleSystems.Length == 0)
        {
            childParticleSystems = GetComponentsInChildren<ParticleSystem>(true);
        }

        // Cache initial emission rates
        foreach (var ps in childParticleSystems)
        {
            if (ps != null && !initialEmissionRates.ContainsKey(ps))
            {
                initialEmissionRates[ps] = ps.emission.rateOverTimeMultiplier;
            }
        }

        // Auto-cache Light component
        if (fireLight == null)
        {
            fireLight = GetComponentInChildren<Light>(true);
        }

        if (fireLight != null && baseLightIntensity <= 0f)
        {
            baseLightIntensity = fireLight.intensity > 0f ? fireLight.intensity : 1.2f;
        }

        // Auto-cache Collider
        if (fireCollider == null)
        {
            fireCollider = GetComponent<Collider>();
        }
    }

    /// <summary>
    /// Reduces fire health and updates progressive visual indicators.
    /// </summary>
    /// <param name="deltaTime">Time slice of impact.</param>
    public void Extinguish(float deltaTime)
    {
        EnsureInitialized();

        if (isExtinguished)
        {
            return;
        }

        currentHealth -= extinguishSpeed * deltaTime;
        currentHealth = Mathf.Max(0f, currentHealth);

        float healthPercent = Mathf.Clamp01(currentHealth / maxHealth);

        // 1. Proportional local scale reduction
        transform.localScale = initialScale * healthPercent;

        // 2. Proportional light dimming
        if (fireLight != null)
        {
            fireLight.intensity = baseLightIntensity * healthPercent;
        }

        // 3. Proportional particle emission modulation
        foreach (var ps in childParticleSystems)
        {
            if (ps != null && initialEmissionRates.TryGetValue(ps, out float baseRate))
            {
                var emission = ps.emission;
                emission.rateOverTimeMultiplier = baseRate * healthPercent;
            }
        }

        // 4. Complete extinction trigger
        if (currentHealth <= 0f)
        {
            ExtinguishComplete();
        }
    }

    /// <summary>
    /// Handles final extinction state, stops particles, disables light and colliders.
    /// </summary>
    private void ExtinguishComplete()
    {
        if (isExtinguished)
        {
            return;
        }

        isExtinguished = true;
        currentHealth = 0f;
        transform.localScale = Vector3.zero;

        // Stop all fire particle systems
        if (childParticleSystems != null)
        {
            foreach (var ps in childParticleSystems)
            {
                if (ps != null)
                {
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        // Turn off light
        if (fireLight != null)
        {
            fireLight.intensity = 0f;
            fireLight.enabled = false;
        }

        // Disable colliders so no further collision events occur
        if (fireCollider != null)
        {
            fireCollider.enabled = false;
        }

        var allColliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in allColliders)
        {
            col.enabled = false;
        }

        Debug.Log("¡Fuego extinguido exitosamente!");
    }

    /// <summary>
    /// Called when particles from a ParticleSystem collide with this object's collider.
    /// </summary>
    private void OnParticleCollision(GameObject other)
    {
        if (isExtinguished)
        {
            return;
        }

        if (IsExtinguisherSource(other))
        {
            Extinguish(Time.deltaTime);
        }
    }

    /// <summary>
    /// Called when a trigger collider (such as a spray trigger volume) stays within the fire trigger.
    /// </summary>
    private void OnTriggerStay(Collider other)
    {
        if (isExtinguished)
        {
            return;
        }

        if (IsExtinguisherSource(other.gameObject))
        {
            Extinguish(Time.deltaTime);
        }
    }

    /// <summary>
    /// Validates whether the colliding GameObject originates from the extinguisher or its spray.
    /// </summary>
    private bool IsExtinguisherSource(GameObject obj)
    {
        if (obj == null) return false;

        if (obj.CompareTag("Player"))
        {
            return true;
        }

        string lowerName = obj.name.ToLowerInvariant();
        if (lowerName.Contains("smoke") || lowerName.Contains("nozzle") || lowerName.Contains("extinguisher"))
        {
            return true;
        }

        if (obj.GetComponentInParent<ExtinguisherController>() != null)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Resets the fire hazard back to full intensity (useful for testing or level reloads).
    /// </summary>
    [ContextMenu("Reset Fire")]
    public void ResetFire()
    {
        EnsureInitialized();
        if (initialScale == Vector3.zero)
        {
            initialScale = Vector3.one;
        }

        isExtinguished = false;
        currentHealth = maxHealth;
        transform.localScale = initialScale;

        if (fireLight != null)
        {
            fireLight.enabled = true;
            fireLight.intensity = baseLightIntensity;
        }

        if (childParticleSystems != null)
        {
            foreach (var ps in childParticleSystems)
            {
                if (ps != null)
                {
                    if (initialEmissionRates.TryGetValue(ps, out float baseRate))
                    {
                        var emission = ps.emission;
                        emission.rateOverTimeMultiplier = baseRate;
                    }
                    ps.Play(true);
                }
            }
        }

        var allColliders = GetComponentsInChildren<Collider>(true);
        foreach (var col in allColliders)
        {
            col.enabled = true;
        }
    }
}
