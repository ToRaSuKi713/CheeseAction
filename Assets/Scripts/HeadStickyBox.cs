using UnityEngine;

public enum HeadStickyFollowMode
{
    PhysicsJoint = 0,
    DirectFollow = 1,
    JiggleAnchor = 2
}

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class HeadStickyBox : MonoBehaviour
{
    [Header("Attach Settings")]
    [SerializeField] private string headTag = "Head";
    [SerializeField] private string headAnchorName = "HeadAnchor";
    [SerializeField] private float attachDelay = 0.02f;
    [SerializeField] private bool disableGravityAfterAttach = false;
    [SerializeField] private HeadStickyFollowMode followMode = HeadStickyFollowMode.PhysicsJoint;

    [Header("Position Spring")]
    [SerializeField] private float positionSpring = 2500f;
    [SerializeField] private float positionDamper = 120f;
    [SerializeField] private float maxPositionForce = 1000f;

    [Header("Rotation Spring")]
    [SerializeField] private float rotationSpring = 300f;
    [SerializeField] private float rotationDamper = 25f;
    [SerializeField] private float maxRotationForce = 300f;

    [Header("Jiggle Limits")]
    [SerializeField] private float linearLimit = 0.03f;
    [SerializeField] private float angularXLimit = 20f;
    [SerializeField] private float angularYLimit = 20f;
    [SerializeField] private float angularZLimit = 20f;

    [Header("Stability")]
    [SerializeField] private float attachedDrag = 2.0f;
    [SerializeField] private float attachedAngularDrag = 6.0f;
    [SerializeField] private bool freezeAfterAttachPosition = false;

    [Header("Direct Follow")]
    [SerializeField] private bool directFollowUseSmoothing = false;
    [SerializeField] private float directFollowPositionLerp = 24f;
    [SerializeField] private float directFollowRotationLerp = 24f;

    [Header("Jiggle Follow")]
    [SerializeField] private float jiggleRotationStrength = 1.8f;
    [SerializeField] private float jigglePositionSpring = 80f;
    [SerializeField] private float jiggleRotationSpring = 55f;
    [SerializeField] private float jiggleDamping = 11f;
    [SerializeField] private float maxJiggleRotationOffset = 10f;

    private Rigidbody rb;
    private ConfigurableJoint joint;
    private WearableRainBox wearableRainBox;
    private Collider[] ownColliders;
    private bool isAttached;
    private Transform attachedAnchor;
    private Vector3 attachedLocalPositionOffset;
    private Quaternion attachedLocalRotationOffset = Quaternion.identity;
    private Vector3 jiggleLocalPosition;
    private Vector3 jiggleLocalPositionVelocity;
    private Vector3 jiggleLocalEuler;
    private Vector3 jiggleLocalEulerVelocity;
    private Vector3 lastAnchorPosition;
    private Quaternion lastAnchorRotation = Quaternion.identity;
    private bool hasAnchorHistory;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        wearableRainBox = GetComponent<WearableRainBox>();
        ownColliders = GetComponentsInChildren<Collider>(true);
    }

    private void LateUpdate()
    {
        if (!isAttached || attachedAnchor == null)
            return;

        if (followMode == HeadStickyFollowMode.DirectFollow)
        {
            UpdateDirectFollow();
            return;
        }

        if (followMode == HeadStickyFollowMode.JiggleAnchor)
        {
            UpdateJiggleFollow();
        }
    }

    private void UpdateDirectFollow()
    {
        Vector3 targetPosition = attachedAnchor.TransformPoint(attachedLocalPositionOffset);
        Quaternion targetRotation = attachedAnchor.rotation * attachedLocalRotationOffset;

        if (directFollowUseSmoothing)
        {
            float posT = 1f - Mathf.Exp(-directFollowPositionLerp * Time.deltaTime);
            float rotT = 1f - Mathf.Exp(-directFollowRotationLerp * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, posT);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotT);
            return;
        }

        transform.position = targetPosition;
        transform.rotation = targetRotation;
    }

    private void UpdateJiggleFollow()
    {
        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);

        if (!hasAnchorHistory)
        {
            lastAnchorPosition = attachedAnchor.position;
            lastAnchorRotation = attachedAnchor.rotation;
            hasAnchorHistory = true;
        }

        Quaternion deltaRotation = attachedAnchor.rotation * Quaternion.Inverse(lastAnchorRotation);
        Vector3 anchorDeltaEuler = NormalizeEuler(deltaRotation.eulerAngles);

        jiggleLocalEuler += -anchorDeltaEuler * jiggleRotationStrength;

        ApplyJiggleSpring(ref jiggleLocalPosition, ref jiggleLocalPositionVelocity, jigglePositionSpring, deltaTime);
        ApplyJiggleSpring(ref jiggleLocalEuler, ref jiggleLocalEulerVelocity, jiggleRotationSpring, deltaTime);

        jiggleLocalPosition = Vector3.zero;
        jiggleLocalPositionVelocity = Vector3.zero;
        jiggleLocalEuler.x = Mathf.Clamp(jiggleLocalEuler.x, -maxJiggleRotationOffset, maxJiggleRotationOffset);
        jiggleLocalEuler.y = Mathf.Clamp(jiggleLocalEuler.y, -maxJiggleRotationOffset, maxJiggleRotationOffset);
        jiggleLocalEuler.z = Mathf.Clamp(jiggleLocalEuler.z, -maxJiggleRotationOffset, maxJiggleRotationOffset);

        Vector3 targetPosition = attachedAnchor.TransformPoint(attachedLocalPositionOffset);
        Quaternion targetRotation = attachedAnchor.rotation * attachedLocalRotationOffset * Quaternion.Euler(jiggleLocalEuler);

        transform.position = targetPosition;
        transform.rotation = targetRotation;

        lastAnchorPosition = attachedAnchor.position;
        lastAnchorRotation = attachedAnchor.rotation;
    }

    private Vector3 NormalizeEuler(Vector3 euler)
    {
        return new Vector3(
            NormalizeAngle(euler.x),
            NormalizeAngle(euler.y),
            NormalizeAngle(euler.z)
        );
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    private void ApplyJiggleSpring(ref Vector3 offset, ref Vector3 velocity, float spring, float deltaTime)
    {
        velocity += (-offset * spring - velocity * jiggleDamping) * deltaTime;
        offset += velocity * deltaTime;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (wearableRainBox != null && wearableRainBox.enabled && wearableRainBox.UsesPhysicsHeadFollow)
            return;

        if (isAttached) return;

        Transform headTransform = FindHeadTransformFromCollision(collision);
        if (headTransform == null) return;

        AttachToHeadTransform(headTransform, headAnchorName, Vector3.zero, Vector3.zero, false);
    }

    private Transform FindHeadTransformFromCollision(Collision collision)
    {
        Transform hit = collision.transform;

        if (hit.CompareTag(headTag))
            return hit;

        Transform parent = hit.parent;
        while (parent != null)
        {
            if (parent.CompareTag(headTag))
                return parent;
            parent = parent.parent;
        }

        return null;
    }

    public bool AttachToHeadTransform(
        Transform headTransform,
        string anchorNameOverride,
        Vector3 localPositionOverride,
        Vector3 localEulerOverride,
        bool useCustomLocalPose)
    {
        if (isAttached || headTransform == null)
            return false;

        string anchorName = string.IsNullOrWhiteSpace(anchorNameOverride) ? headAnchorName : anchorNameOverride;
        Transform anchor = FindOrCreateHeadAnchor(
            headTransform,
            anchorName,
            localPositionOverride,
            localEulerOverride,
            useCustomLocalPose
        );

        if (anchor == null)
            return false;

        attachedLocalPositionOffset = useCustomLocalPose ? localPositionOverride : Vector3.zero;
        attachedLocalRotationOffset = useCustomLocalPose ? Quaternion.Euler(localEulerOverride) : Quaternion.identity;
        AttachToHead(anchor);
        return true;
    }

    private Transform FindOrCreateHeadAnchor(
        Transform headTransform,
        string anchorName,
        Vector3 localPosition,
        Vector3 localEuler,
        bool useCustomLocalPose)
    {
        Transform anchor = headTransform.Find(anchorName);
        if (anchor != null)
        {
            return anchor;
        }

        GameObject anchorObj = new GameObject(anchorName);
        anchor = anchorObj.transform;
        anchor.SetParent(headTransform, false);
        anchor.localPosition = useCustomLocalPose ? localPosition : Vector3.zero;
        anchor.localRotation = useCustomLocalPose ? Quaternion.Euler(localEuler) : Quaternion.identity;

        Rigidbody anchorRb = anchorObj.AddComponent<Rigidbody>();
        anchorRb.isKinematic = true;
        anchorRb.useGravity = false;

        return anchor;
    }

    private void AttachToHead(Transform anchor)
    {
        isAttached = true;
        attachedAnchor = anchor;
        jiggleLocalPosition = Vector3.zero;
        jiggleLocalPositionVelocity = Vector3.zero;
        jiggleLocalEuler = Vector3.zero;
        jiggleLocalEulerVelocity = Vector3.zero;
        lastAnchorPosition = anchor.position;
        lastAnchorRotation = anchor.rotation;
        hasAnchorHistory = true;

        Rigidbody anchorRb = anchor.GetComponent<Rigidbody>();
        if (anchorRb == null)
        {
            anchorRb = anchor.gameObject.AddComponent<Rigidbody>();
            anchorRb.isKinematic = true;
            anchorRb.useGravity = false;
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        Vector3 targetPosition = anchor.TransformPoint(attachedLocalPositionOffset);
        Quaternion targetRotation = anchor.rotation * attachedLocalRotationOffset;

        rb.position = targetPosition;
        rb.rotation = targetRotation;

        rb.linearDamping = attachedDrag;
        rb.angularDamping = attachedAngularDrag;

        if (disableGravityAfterAttach)
            rb.useGravity = false;

        IgnoreCollisionsWithHead(anchor);

        if (followMode == HeadStickyFollowMode.DirectFollow || followMode == HeadStickyFollowMode.JiggleAnchor)
        {
            if (joint != null)
            {
                Destroy(joint);
                joint = null;
            }

            rb.isKinematic = true;
            rb.useGravity = false;
            transform.position = targetPosition;
            transform.rotation = targetRotation;
            return;
        }

        joint = gameObject.AddComponent<ConfigurableJoint>();
        joint.autoConfigureConnectedAnchor = false;
        joint.connectedBody = anchorRb;

        joint.anchor = Vector3.zero;
        joint.connectedAnchor = Vector3.zero;

        if (freezeAfterAttachPosition)
        {
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;
        }
        else
        {
            joint.xMotion = ConfigurableJointMotion.Limited;
            joint.yMotion = ConfigurableJointMotion.Limited;
            joint.zMotion = ConfigurableJointMotion.Limited;

            SoftJointLimit linear = new SoftJointLimit();
            linear.limit = linearLimit;
            joint.linearLimit = linear;
        }

        joint.angularXMotion = ConfigurableJointMotion.Limited;
        joint.angularYMotion = ConfigurableJointMotion.Limited;
        joint.angularZMotion = ConfigurableJointMotion.Limited;

        SoftJointLimit lowX = new SoftJointLimit();
        lowX.limit = angularXLimit;
        joint.lowAngularXLimit = lowX;

        SoftJointLimit highX = new SoftJointLimit();
        highX.limit = angularXLimit;
        joint.highAngularXLimit = highX;

        SoftJointLimit yLimit = new SoftJointLimit();
        yLimit.limit = angularYLimit;
        joint.angularYLimit = yLimit;

        SoftJointLimit zLimit = new SoftJointLimit();
        zLimit.limit = angularZLimit;
        joint.angularZLimit = zLimit;

        JointDrive posDrive = new JointDrive();
        posDrive.positionSpring = positionSpring;
        posDrive.positionDamper = positionDamper;
        posDrive.maximumForce = maxPositionForce;

        joint.xDrive = posDrive;
        joint.yDrive = posDrive;
        joint.zDrive = posDrive;

        JointDrive rotDrive = new JointDrive();
        rotDrive.positionSpring = rotationSpring;
        rotDrive.positionDamper = rotationDamper;
        rotDrive.maximumForce = maxRotationForce;

        joint.rotationDriveMode = RotationDriveMode.Slerp;
        joint.slerpDrive = rotDrive;

        joint.configuredInWorldSpace = false;

        Invoke(nameof(SnapCloserToAnchor), attachDelay);
    }

    private void SnapCloserToAnchor()
    {
        if (joint == null || joint.connectedBody == null) return;

        Transform anchor = joint.connectedBody.transform;
        transform.position = Vector3.Lerp(transform.position, anchor.position, 0.7f);
    }

    private void IgnoreCollisionsWithHead(Transform anchor)
    {
        if (anchor == null || ownColliders == null || ownColliders.Length == 0)
            return;

        Transform headRoot = anchor.parent != null ? anchor.parent : anchor;
        Collider[] headColliders = headRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < ownColliders.Length; i++)
        {
            Collider own = ownColliders[i];
            if (own == null)
                continue;

            for (int j = 0; j < headColliders.Length; j++)
            {
                Collider headCollider = headColliders[j];
                if (headCollider == null)
                    continue;

                Physics.IgnoreCollision(own, headCollider, true);
            }
        }
    }
}
