using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Controls a movable debris obstacle in the evacuation zone (EscombrosMover).
/// Preserves the initial posed debris pile until grabbed, enables smooth Near and Far
/// VR grabbing via XRGrabInteractable without pushing the player's CharacterController
/// or snagging on adjacent lodged debris, applies realistic gravity physics upon release,
/// and supports full state restoration on Game Over retry.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(XRGrabInteractable))]
public class EvacuationDebrisItem : MonoBehaviour
{
    [Header("Physics & Grabbing Settings")]
    [Tooltip("Maximum linear speed allowed when releasing/throwing this debris object.")]
    [SerializeField]
    private float maxReleaseLinearSpeed = 6.5f;

    [Tooltip("Maximum angular speed allowed when releasing/throwing this debris object.")]
    [SerializeField]
    private float maxReleaseAngularSpeed = 8.0f;

    [Tooltip("Minimum allowed World Y position to prevent debris from ever clipping below the office floor.")]
    [SerializeField]
    private float minFloorWorldY = -0.15f;

    [Header("Runtime State (Read Only)")]
    [Tooltip("True while the object remains undisturbed in its initial debris pile pose.")]
    [SerializeField]
    private bool isLodgedInInitialPile = true;

    private Rigidbody rb;
    private XRGrabInteractable grabInteractable;
    private Collider[] itemColliders;
    private CharacterController playerCharacterController;

    private Vector3 initialWorldPosition;
    private Quaternion initialWorldRotation;
    private Vector3 initialLocalScale;
    private Transform initialParent;
    private bool initialPoseCached;

    private bool ignoringPlayerCollision;
    private readonly List<Collider> temporarilyIgnoredSiblingColliders = new List<Collider>();

    private static readonly List<EvacuationDebrisItem> AllDebrisItems = new List<EvacuationDebrisItem>();
    private static readonly FieldInfo WasKinematicField = typeof(XRGrabInteractable).GetField("m_WasKinematic", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo UsedGravityField = typeof(XRGrabInteractable).GetField("m_UsedGravity", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo DetachInLateUpdateField = typeof(XRGrabInteractable).GetField("m_DetachInLateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

    public bool IsLodgedInInitialPile => isLodgedInInitialPile;

    private void Awake()
    {
        if (!AllDebrisItems.Contains(this))
        {
            AllDebrisItems.Add(this);
        }

        ClearStaticFlagsRecursive(transform);
        CacheComponentsAndInitialPose();
        ApplyLodgedRestingState();
    }

    private void OnEnable()
    {
        if (!AllDebrisItems.Contains(this))
        {
            AllDebrisItems.Add(this);
        }

        if (grabInteractable == null)
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
        }

        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnSelectEntered);
            grabInteractable.selectExited.AddListener(OnSelectExited);
            grabInteractable.hoverEntered.AddListener(OnHoverEntered);
        }
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            grabInteractable.selectExited.RemoveListener(OnSelectExited);
            grabInteractable.hoverEntered.RemoveListener(OnHoverEntered);
        }
    }

    private void OnDestroy()
    {
        AllDebrisItems.Remove(this);
    }

    private static void ClearStaticFlagsRecursive(Transform root)
    {
        if (root == null) return;
        root.gameObject.isStatic = false;
        foreach (Transform child in root)
        {
            ClearStaticFlagsRecursive(child);
        }
    }

    private void CacheComponentsAndInitialPose()
    {
        rb = GetComponent<Rigidbody>();
        grabInteractable = GetComponent<XRGrabInteractable>();

        List<Collider> validCols = new List<Collider>();
        foreach (var col in GetComponentsInChildren<Collider>(true))
        {
            if (col != null && !col.isTrigger)
            {
                validCols.Add(col);
            }
        }
        itemColliders = validCols.ToArray();

        if (!initialPoseCached)
        {
            initialParent = transform.parent;
            initialWorldPosition = transform.position;
            initialWorldRotation = transform.rotation;
            initialLocalScale = transform.localScale;
            initialPoseCached = true;
        }

        if (playerCharacterController == null)
        {
            playerCharacterController = Object.FindAnyObjectByType<CharacterController>();
        }
    }

    private void ApplyLodgedRestingState()
    {
        isLodgedInInitialPile = true;
        if (rb != null)
        {
            rb.useGravity = true;
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        // Keep kinematic while merely hovered so tilted items in the pile stay posed,
        // unless already dislodged by a previous grab.
        if (!isLodgedInInitialPile && rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        isLodgedInInitialPile = false;

        if (grabInteractable != null)
        {
            if (WasKinematicField != null) WasKinematicField.SetValue(grabInteractable, false);
            if (UsedGravityField != null) UsedGravityField.SetValue(grabInteractable, true);
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.WakeUp();
        }

        // Prevent the grabbed furniture from colliding with the player's own CharacterController
        // while being pulled or swung across the player's body.
        SetIgnorePlayerCollision(true);

        // Ignore collisions with still-lodged sibling debris while extracting this piece from the pile.
        SetIgnoreLodgedSiblingsCollision(true);
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        if (grabInteractable != null && grabInteractable.isSelected)
        {
            return;
        }

        isLodgedInInitialPile = false;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            if (rb.linearVelocity.sqrMagnitude > maxReleaseLinearSpeed * maxReleaseLinearSpeed)
            {
                rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, maxReleaseLinearSpeed);
            }

            if (rb.angularVelocity.sqrMagnitude > maxReleaseAngularSpeed * maxReleaseAngularSpeed)
            {
                rb.angularVelocity = Vector3.ClampMagnitude(rb.angularVelocity, maxReleaseAngularSpeed);
            }

            rb.WakeUp();
        }

        // Re-enable collision with sibling debris immediately on release if no longer penetrating them
        RestoreNonOverlappingSiblingCollisions();
    }

    private void FixedUpdate()
    {
        if (isLodgedInInitialPile)
        {
            return;
        }

        bool isHeld = grabInteractable != null && grabInteractable.isSelected;

        if (!isHeld && rb != null)
        {
            // Guarantee XRGrabInteractable never reverts the Rigidbody to kinematic after drop
            if (rb.isKinematic)
            {
                rb.isKinematic = false;
            }
            if (!rb.useGravity)
            {
                rb.useGravity = true;
            }

            // Safety floor guard so no debris item can ever tunnel below the office floor
            if (transform.position.y < minFloorWorldY)
            {
                Vector3 safePos = transform.position;
                safePos.y = 0.25f;
                rb.position = safePos;
                transform.position = safePos;
                if (rb.linearVelocity.y < 0f)
                {
                    Vector3 v = rb.linearVelocity;
                    v.y = 0f;
                    rb.linearVelocity = v;
                }
            }
        }

        if (!isHeld && ignoringPlayerCollision)
        {
            TryRestorePlayerCollisionWhenClear();
        }

        if (temporarilyIgnoredSiblingColliders.Count > 0)
        {
            RestoreNonOverlappingSiblingCollisions();
        }
    }

    private void SetIgnorePlayerCollision(bool ignore)
    {
        if (playerCharacterController == null)
        {
            playerCharacterController = Object.FindAnyObjectByType<CharacterController>();
        }

        if (playerCharacterController == null || itemColliders == null)
        {
            return;
        }

        foreach (var col in itemColliders)
        {
            if (col != null)
            {
                Physics.IgnoreCollision(col, playerCharacterController, ignore);
            }
        }

        ignoringPlayerCollision = ignore;
    }

    private void TryRestorePlayerCollisionWhenClear()
    {
        if (playerCharacterController == null || itemColliders == null)
        {
            ignoringPlayerCollision = false;
            return;
        }

        Bounds playerBounds = playerCharacterController.bounds;
        playerBounds.Expand(0.15f);

        foreach (var col in itemColliders)
        {
            if (col != null && col.bounds.Intersects(playerBounds))
            {
                return;
            }
        }

        SetIgnorePlayerCollision(false);
    }

    private void SetIgnoreLodgedSiblingsCollision(bool ignore)
    {
        if (itemColliders == null) return;

        if (!ignore)
        {
            foreach (var sibCol in temporarilyIgnoredSiblingColliders)
            {
                if (sibCol == null) continue;
                foreach (var myCol in itemColliders)
                {
                    if (myCol != null)
                    {
                        Physics.IgnoreCollision(myCol, sibCol, false);
                    }
                }
            }
            temporarilyIgnoredSiblingColliders.Clear();
            return;
        }

        foreach (var other in AllDebrisItems)
        {
            if (other == null || other == this) continue;
            if (!other.isLodgedInInitialPile || other.itemColliders == null) continue;

            foreach (var sibCol in other.itemColliders)
            {
                if (sibCol == null) continue;
                foreach (var myCol in itemColliders)
                {
                    if (myCol != null)
                    {
                        Physics.IgnoreCollision(myCol, sibCol, true);
                    }
                }
                if (!temporarilyIgnoredSiblingColliders.Contains(sibCol))
                {
                    temporarilyIgnoredSiblingColliders.Add(sibCol);
                }
            }
        }
    }

    private void RestoreNonOverlappingSiblingCollisions()
    {
        if (itemColliders == null || temporarilyIgnoredSiblingColliders.Count == 0)
        {
            return;
        }

        for (int i = temporarilyIgnoredSiblingColliders.Count - 1; i >= 0; i--)
        {
            Collider sibCol = temporarilyIgnoredSiblingColliders[i];
            if (sibCol == null)
            {
                temporarilyIgnoredSiblingColliders.RemoveAt(i);
                continue;
            }

            bool stillOverlapping = false;
            Bounds expandedSib = sibCol.bounds;
            expandedSib.Expand(0.03f);

            foreach (var myCol in itemColliders)
            {
                if (myCol != null && myCol.bounds.Intersects(expandedSib))
                {
                    stillOverlapping = true;
                    break;
                }
            }

            if (!stillOverlapping)
            {
                foreach (var myCol in itemColliders)
                {
                    if (myCol != null)
                    {
                        Physics.IgnoreCollision(myCol, sibCol, false);
                    }
                }
                temporarilyIgnoredSiblingColliders.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Forces release if held and restores this debris item to its exact initial pose and resting state.
    /// </summary>
    public void ResetToInitialPose()
    {
        CacheComponentsAndInitialPose();

        if (grabInteractable != null && grabInteractable.isSelected && grabInteractable.interactionManager != null)
        {
            var interactors = new List<IXRSelectInteractor>(grabInteractable.interactorsSelecting);
            foreach (var interactor in interactors)
            {
                grabInteractable.interactionManager.SelectExit(interactor, grabInteractable);
            }
        }

        if (grabInteractable != null && DetachInLateUpdateField != null)
        {
            DetachInLateUpdateField.SetValue(grabInteractable, false);
        }

        SetIgnorePlayerCollision(false);
        SetIgnoreLodgedSiblingsCollision(false);

        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (initialParent != null && transform.parent != initialParent)
        {
            transform.SetParent(initialParent, true);
        }

        transform.SetPositionAndRotation(initialWorldPosition, initialWorldRotation);
        transform.localScale = initialLocalScale;

        ApplyLodgedRestingState();
    }

    /// <summary>
    /// Resets all EvacuationDebrisItem instances in the active scene back to their initial poses.
    /// </summary>
    public static void ResetAllDebrisItems()
    {
        EvacuationDebrisItem[] items = Object.FindObjectsByType<EvacuationDebrisItem>(FindObjectsInactive.Include);
        if (items == null) return;

        foreach (var item in items)
        {
            if (item != null)
            {
                item.ResetToInitialPose();
            }
        }
    }
}
