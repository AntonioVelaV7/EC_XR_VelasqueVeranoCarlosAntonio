using UnityEngine;

/// <summary>
/// Forwards particle collision events from child colliders to the parent ExtinguishableFire component.
/// </summary>
public class ParticleCollisionForwarder : MonoBehaviour
{
    private ExtinguishableFire targetFire;

    private void Awake()
    {
        targetFire = GetComponentInParent<ExtinguishableFire>();
    }

    private void OnParticleCollision(GameObject other)
    {
        if (targetFire != null)
        {
            targetFire.SendMessage("OnParticleCollision", other, SendMessageOptions.DontRequireReceiver);
        }
    }
}
