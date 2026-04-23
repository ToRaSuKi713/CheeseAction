using UnityEngine;

public class WearableRainBox : MonoBehaviour
{
    [Header("Target Filter")]
    [SerializeField] private bool useTagFilter = true;
    [SerializeField] private string requiredTag = "Head";
    [SerializeField] private bool matchParentTagToo = true;

    [Header("Attach Target")]
    [SerializeField] private string preferredAnchorName = "HeadBoxAnchor";
    [SerializeField] private bool usePreferredAnchorName = true;

    [Header("Attach Pose")]
    [SerializeField] private Vector3 fallbackLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 fallbackLocalEuler = Vector3.zero;
    [SerializeField] private Vector3 attachedLocalScale = Vector3.one;

    [Header("Attach Options")]
    [SerializeField] private bool disableColliderAfterAttach = true;
    [SerializeField] private bool makeKinematicAfterAttach = true;
    [SerializeField] private bool clearVelocityAfterAttach = true;
    [SerializeField] private bool attachOnlyOnce = true;
    [SerializeField] private bool preferPhysicsHeadFollow = true;

    [Header("Lifetime")]
    [SerializeField] private bool destroyAfterSeconds = false;
    [SerializeField] private float attachedLifetime = 10f;

    private Rigidbody rb;
    private Collider[] allColliders;
    private HeadStickyBox stickyBox;
    private bool attached = false;

    public bool UsesPhysicsHeadFollow
    {
        get { return preferPhysicsHeadFollow; }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = GetComponentInChildren<Rigidbody>();
        }

        allColliders = GetComponentsInChildren<Collider>(true);
        stickyBox = GetComponent<HeadStickyBox>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (attachOnlyOnce && attached)
            return;

        if (!IsValidTarget(collision.collider.transform))
            return;

        if (preferPhysicsHeadFollow && stickyBox != null)
        {
            Transform headTransform = FindHeadTransform(collision.collider.transform);
            if (headTransform == null)
                return;

            if (stickyBox.AttachToHeadTransform(
                headTransform,
                preferredAnchorName,
                fallbackLocalPosition,
                fallbackLocalEuler,
                true))
            {
                attached = true;
                transform.localScale = attachedLocalScale;
                HandlePostAttachState(false);
            }

            return;
        }

        Transform attachTarget = FindAttachTarget(collision.collider.transform);
        if (attachTarget == null)
            return;

        AttachToTarget(attachTarget);
    }

    private bool IsValidTarget(Transform hitTransform)
    {
        if (!useTagFilter)
            return true;

        return FindHeadTransform(hitTransform) != null;
    }

    private Transform FindHeadTransform(Transform hitTransform)
    {
        if (hitTransform == null)
            return null;

        if (hitTransform.CompareTag(requiredTag))
            return hitTransform;

        if (!matchParentTagToo)
            return null;

        Transform current = hitTransform.parent;
        while (current != null)
        {
            if (current.CompareTag(requiredTag))
                return current;

            current = current.parent;
        }

        return null;
    }

    private Transform FindAttachTarget(Transform hitTransform)
    {
        if (usePreferredAnchorName)
        {
            Transform current = hitTransform;
            while (current != null)
            {
                Transform found = FindDeepChildByName(current, preferredAnchorName);
                if (found != null)
                    return found;

                current = current.parent;
            }
        }

        return hitTransform;
    }

    private Transform FindDeepChildByName(Transform root, string childName)
    {
        if (root == null)
            return null;

        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindDeepChildByName(root.GetChild(i), childName);
            if (result != null)
                return result;
        }

        return null;
    }

    private void AttachToTarget(Transform attachTarget)
    {
        attached = true;

        if (rb != null)
        {
            if (clearVelocityAfterAttach)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        transform.SetParent(attachTarget, false);
        transform.localPosition = fallbackLocalPosition;
        transform.localRotation = Quaternion.Euler(fallbackLocalEuler);
        transform.localScale = attachedLocalScale;

        HandlePostAttachState(true);
    }

    private void HandlePostAttachState(bool allowKinematicChange)
    {
        if (allowKinematicChange && rb != null && makeKinematicAfterAttach)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (disableColliderAfterAttach && allColliders != null)
        {
            for (int i = 0; i < allColliders.Length; i++)
            {
                if (allColliders[i] != null)
                {
                    allColliders[i].enabled = false;
                }
            }
        }

        if (destroyAfterSeconds && attachedLifetime > 0f)
        {
            Destroy(gameObject, attachedLifetime);
        }
    }
}
