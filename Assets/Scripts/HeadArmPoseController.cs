using UnityEngine;

public class HeadArmPoseController : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator driverAnimator;
    [SerializeField] private bool autoFindAnimator = true;

    [Header("Targets")]
    [SerializeField] private Transform headTarget;
    [SerializeField] private Transform leftHandTarget;
    [SerializeField] private Transform rightHandTarget;
    [SerializeField] private Transform leftElbowHint;
    [SerializeField] private Transform rightElbowHint;

    [Header("Head")]
    [SerializeField] private bool controlHead = true;
    [SerializeField] [Range(0f, 1f)] private float neckWeight = 0.35f;
    [SerializeField] [Range(0f, 1f)] private float headWeight = 1.0f;

    [Header("Upper Body")]
    [SerializeField] private bool controlUpperBody = true;
    [SerializeField] [Range(0f, 2f)] private float upperBodyStrength = 0.55f;
    [SerializeField] [Range(0f, 1f)] private float spineWeight = 0.18f;
    [SerializeField] [Range(0f, 1f)] private float chestWeight = 0.34f;
    [SerializeField] [Range(0f, 1f)] private float upperChestWeight = 0.45f;
    [SerializeField] private float upperBodyImpactDuration = 0.18f;
    [SerializeField] private Vector3 impactVelocityToEuler = new Vector3(1.1f, 0.7f, 0.55f);
    [SerializeField] private Vector3 impactEventToEuler = new Vector3(0.75f, 0.5f, 0.35f);
    [SerializeField] private Vector3 maxUpperBodyEuler = new Vector3(12f, 10f, 8f);

    [Header("Arms")]
    [SerializeField] private bool controlLeftArm = true;
    [SerializeField] private bool controlRightArm = true;
    [SerializeField] [Range(0f, 1f)] private float leftArmWeight = 1.0f;
    [SerializeField] [Range(0f, 1f)] private float rightArmWeight = 1.0f;
    [SerializeField] private bool followHandTargetRotation = true;

    [Header("Solver")]
    [SerializeField] private float maxReachScale = 0.999f;
    [SerializeField] private float minReachEpsilon = 0.001f;
    [SerializeField] private bool updateInLateUpdate = true;

    [Header("Debug")]
    [SerializeField] private bool logMissingBones = true;

    private Transform spineBone;
    private Transform chestBone;
    private Transform upperChestBone;
    private Transform neckBone;
    private Transform headBone;

    private Transform leftUpperArmBone;
    private Transform leftLowerArmBone;
    private Transform leftHandBone;

    private Transform rightUpperArmBone;
    private Transform rightLowerArmBone;
    private Transform rightHandBone;

    private Quaternion neckTargetOffset = Quaternion.identity;
    private Quaternion headTargetOffset = Quaternion.identity;
    private Quaternion leftHandTargetOffset = Quaternion.identity;
    private Quaternion rightHandTargetOffset = Quaternion.identity;

    private float leftUpperArmLength;
    private float leftLowerArmLength;
    private float rightUpperArmLength;
    private float rightLowerArmLength;
    private Quaternion spineBaseLocalRotation = Quaternion.identity;
    private Quaternion chestBaseLocalRotation = Quaternion.identity;
    private Quaternion upperChestBaseLocalRotation = Quaternion.identity;
    private Quaternion lastAppliedSpineLocalRotation = Quaternion.identity;
    private Quaternion lastAppliedChestLocalRotation = Quaternion.identity;
    private Quaternion lastAppliedUpperChestLocalRotation = Quaternion.identity;

    private Vector3 currentUpperBodyEuler;
    private Vector3 impactUpperBodyEuler;
    private float impactElapsedSeconds;
    private float impactDurationSeconds;
    private bool impactActive;
    private bool hasAppliedUpperBodyPose;
    private bool isReady;

    public Transform EffectiveHeadTransform
    {
        get
        {
            if (headTarget != null)
                return headTarget;

            return headBone;
        }
    }

    public Transform StickerHeadTransform
    {
        get
        {
            if (headBone != null)
                return headBone;

            return headTarget;
        }
    }

    private void Awake()
    {
        Rebind();
    }

    private void OnEnable()
    {
        if (!isReady)
        {
            Rebind();
        }
    }

    private void LateUpdate()
    {
        if (!updateInLateUpdate)
            return;

        ApplyPose();
    }

    private void Update()
    {
        if (updateInLateUpdate)
            return;

        ApplyPose();
    }

    [ContextMenu("Rebind")]
    public void Rebind()
    {
        if (driverAnimator == null && autoFindAnimator)
        {
            driverAnimator = GetComponent<Animator>();
            if (driverAnimator == null)
            {
                driverAnimator = GetComponentInChildren<Animator>();
            }
        }

        if (driverAnimator == null || !driverAnimator.isHuman)
        {
            if (logMissingBones)
            {
                Debug.LogWarning("[HeadArmPoseController] Humanoid Animator not found or avatar is not humanoid.");
            }

            isReady = false;
            return;
        }

        spineBone = driverAnimator.GetBoneTransform(HumanBodyBones.Spine);
        chestBone = driverAnimator.GetBoneTransform(HumanBodyBones.Chest);
        upperChestBone = driverAnimator.GetBoneTransform(HumanBodyBones.UpperChest);
        neckBone = driverAnimator.GetBoneTransform(HumanBodyBones.Neck);
        headBone = driverAnimator.GetBoneTransform(HumanBodyBones.Head);

        leftUpperArmBone = driverAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        leftLowerArmBone = driverAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        leftHandBone = driverAnimator.GetBoneTransform(HumanBodyBones.LeftHand);

        rightUpperArmBone = driverAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        rightLowerArmBone = driverAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        rightHandBone = driverAnimator.GetBoneTransform(HumanBodyBones.RightHand);

        if (headTarget != null && neckBone != null)
        {
            neckTargetOffset = Quaternion.Inverse(headTarget.rotation) * neckBone.rotation;
        }

        if (headTarget != null && headBone != null)
        {
            headTargetOffset = Quaternion.Inverse(headTarget.rotation) * headBone.rotation;
        }

        if (leftHandTarget != null && leftHandBone != null)
        {
            leftHandTargetOffset = Quaternion.Inverse(leftHandTarget.rotation) * leftHandBone.rotation;
        }

        if (rightHandTarget != null && rightHandBone != null)
        {
            rightHandTargetOffset = Quaternion.Inverse(rightHandTarget.rotation) * rightHandBone.rotation;
        }

        leftUpperArmLength = GetBoneDistance(leftUpperArmBone, leftLowerArmBone);
        leftLowerArmLength = GetBoneDistance(leftLowerArmBone, leftHandBone);
        rightUpperArmLength = GetBoneDistance(rightUpperArmBone, rightLowerArmBone);
        rightLowerArmLength = GetBoneDistance(rightLowerArmBone, rightHandBone);

        currentUpperBodyEuler = Vector3.zero;
        impactUpperBodyEuler = Vector3.zero;
        impactElapsedSeconds = 0f;
        impactDurationSeconds = upperBodyImpactDuration;
        impactActive = false;
        hasAppliedUpperBodyPose = false;
        CacheUpperBodyBasePose();
        lastAppliedSpineLocalRotation = spineBaseLocalRotation;
        lastAppliedChestLocalRotation = chestBaseLocalRotation;
        lastAppliedUpperChestLocalRotation = upperChestBaseLocalRotation;
        isReady = true;
    }

    [ContextMenu("Snap Targets To Current Pose")]
    public void SnapTargetsToCurrentPose()
    {
        if (driverAnimator == null)
        {
            Rebind();
        }

        if (headTarget != null && headBone != null)
        {
            headTarget.position = headBone.position;
            headTarget.rotation = headBone.rotation;
        }

        if (leftHandTarget != null && leftHandBone != null)
        {
            leftHandTarget.position = leftHandBone.position;
            leftHandTarget.rotation = leftHandBone.rotation;
        }

        if (rightHandTarget != null && rightHandBone != null)
        {
            rightHandTarget.position = rightHandBone.position;
            rightHandTarget.rotation = rightHandBone.rotation;
        }

        if (leftElbowHint != null && leftLowerArmBone != null)
        {
            leftElbowHint.position = leftLowerArmBone.position + (leftLowerArmBone.right * -0.15f);
        }

        if (rightElbowHint != null && rightLowerArmBone != null)
        {
            rightElbowHint.position = rightLowerArmBone.position + (rightLowerArmBone.right * 0.15f);
        }

        Rebind();
    }

    public void ApplyPose()
    {
        if (!isReady)
            return;

        if (controlUpperBody)
        {
            CacheUpperBodyBasePose();
            UpdateUpperBodyImpact(Time.deltaTime);
            ApplyUpperBodyPose();
        }

        if (controlHead)
        {
            ApplyHeadPose();
        }

        if (controlLeftArm)
        {
            SolveArmIK(
                leftUpperArmBone,
                leftLowerArmBone,
                leftHandBone,
                leftHandTarget,
                leftElbowHint,
                leftUpperArmLength,
                leftLowerArmLength,
                leftHandTargetOffset,
                leftArmWeight
            );
        }

        if (controlRightArm)
        {
            SolveArmIK(
                rightUpperArmBone,
                rightLowerArmBone,
                rightHandBone,
                rightHandTarget,
                rightElbowHint,
                rightUpperArmLength,
                rightLowerArmLength,
                rightHandTargetOffset,
                rightArmWeight
            );
        }
    }

    public void TriggerUpperBodyImpact(TestEventData eventData, Vector3 worldVelocity)
    {
        if (!controlUpperBody)
            return;

        if (eventData != null &&
            !string.IsNullOrWhiteSpace(eventData.spawnPattern) &&
            eventData.spawnPattern.Trim().ToLowerInvariant() == "rain")
            return;

        Transform referenceRoot = driverAnimator != null ? driverAnimator.transform : transform;
        Vector3 localVelocity = referenceRoot.InverseTransformDirection(worldVelocity);

        float eventPitch = eventData != null ? eventData.vmcPitch : 0f;
        float eventYaw = eventData != null ? eventData.vmcYaw : 0f;
        float eventRoll = eventData != null ? eventData.vmcRoll : 0f;

        Vector3 desiredEuler = new Vector3(
            (-localVelocity.z * impactVelocityToEuler.x) + (-eventPitch * impactEventToEuler.x),
            (localVelocity.x * impactVelocityToEuler.y) + (eventYaw * impactEventToEuler.y),
            (-localVelocity.x * impactVelocityToEuler.z) + (-eventRoll * impactEventToEuler.z)
        );

        float collisionStrength = Mathf.Clamp(worldVelocity.magnitude * 0.18f, 0.85f, 2.4f);
        if (eventData != null &&
            !string.IsNullOrWhiteSpace(eventData.spawnPattern) &&
            eventData.spawnPattern.Trim().ToLowerInvariant() == "burst")
        {
            collisionStrength *= 1.45f;
        }
        desiredEuler *= upperBodyStrength * collisionStrength;

        CacheUpperBodyBasePose();
        impactUpperBodyEuler = ClampUpperBodyEuler(desiredEuler);
        impactDurationSeconds = upperBodyImpactDuration;

        if (eventData != null && eventData.vmcDurationMs > 0)
        {
            impactDurationSeconds = Mathf.Clamp(
                Mathf.Max(upperBodyImpactDuration, eventData.vmcDurationMs / 1000f * 1.25f),
                0.18f,
                0.5f
            );
        }

        impactElapsedSeconds = 0f;
        impactActive = true;
    }

    private void ApplyUpperBodyPose()
    {
        Quaternion torsoRotation = Quaternion.Euler(currentUpperBodyEuler);
        ApplyTorsoBone(spineBone, torsoRotation, spineWeight);
        ApplyTorsoBone(chestBone, torsoRotation, chestWeight);
        ApplyTorsoBone(upperChestBone, torsoRotation, upperChestWeight);
        hasAppliedUpperBodyPose = true;
    }

    private void UpdateUpperBodyImpact(float deltaTime)
    {
        if (!impactActive)
        {
            currentUpperBodyEuler = Vector3.zero;
            return;
        }

        impactElapsedSeconds += deltaTime;
        float t = Mathf.Clamp01(impactElapsedSeconds / Mathf.Max(0.0001f, impactDurationSeconds));

        float weight;
        if (t < 0.3f)
        {
            weight = Mathf.SmoothStep(0f, 1f, t / 0.3f);
        }
        else
        {
            weight = Mathf.SmoothStep(1f, 0f, (t - 0.3f) / 0.7f);
        }

        currentUpperBodyEuler = impactUpperBodyEuler * weight;

        if (t >= 1f)
        {
            impactActive = false;
            currentUpperBodyEuler = Vector3.zero;
            impactUpperBodyEuler = Vector3.zero;
        }
    }

    private void ApplyHeadPose()
    {
        if (headTarget == null)
            return;

        if (neckBone != null && neckWeight > 0f)
        {
            Quaternion desiredNeckRotation = headTarget.rotation * neckTargetOffset;
            neckBone.rotation = Quaternion.Slerp(neckBone.rotation, desiredNeckRotation, neckWeight);
        }

        if (headBone != null && headWeight > 0f)
        {
            Quaternion desiredHeadRotation = headTarget.rotation * headTargetOffset;
            headBone.rotation = Quaternion.Slerp(headBone.rotation, desiredHeadRotation, headWeight);
        }
    }

    private void ApplyTorsoBone(Transform bone, Quaternion torsoRotation, float weight)
    {
        if (bone == null || weight <= 0f)
            return;

        Quaternion weightedRotation = Quaternion.Slerp(Quaternion.identity, torsoRotation, weight);
        if (bone == spineBone)
        {
            bone.localRotation = spineBaseLocalRotation * weightedRotation;
            lastAppliedSpineLocalRotation = bone.localRotation;
            return;
        }

        if (bone == chestBone)
        {
            bone.localRotation = chestBaseLocalRotation * weightedRotation;
            lastAppliedChestLocalRotation = bone.localRotation;
            return;
        }

        if (bone == upperChestBone)
        {
            bone.localRotation = upperChestBaseLocalRotation * weightedRotation;
            lastAppliedUpperChestLocalRotation = bone.localRotation;
        }
    }

    private void CacheUpperBodyBasePose()
    {
        if (spineBone != null)
        {
            spineBaseLocalRotation = CaptureBaseLocalRotation(
                spineBone,
                spineBaseLocalRotation,
                lastAppliedSpineLocalRotation
            );
        }

        if (chestBone != null)
        {
            chestBaseLocalRotation = CaptureBaseLocalRotation(
                chestBone,
                chestBaseLocalRotation,
                lastAppliedChestLocalRotation
            );
        }

        if (upperChestBone != null)
        {
            upperChestBaseLocalRotation = CaptureBaseLocalRotation(
                upperChestBone,
                upperChestBaseLocalRotation,
                lastAppliedUpperChestLocalRotation
            );
        }
    }

    private Quaternion CaptureBaseLocalRotation(Transform bone, Quaternion cachedBaseRotation, Quaternion lastAppliedRotation)
    {
        Quaternion currentLocalRotation = bone.localRotation;

        if (!hasAppliedUpperBodyPose)
            return currentLocalRotation;

        float similarity = Mathf.Abs(Quaternion.Dot(currentLocalRotation, lastAppliedRotation));
        if (similarity < 0.9999f)
            return currentLocalRotation;

        return cachedBaseRotation;
    }

    private void SolveArmIK(
        Transform upperArm,
        Transform lowerArm,
        Transform hand,
        Transform handTarget,
        Transform elbowHint,
        float upperLength,
        float lowerLength,
        Quaternion handRotationOffset,
        float armWeight)
    {
        if (upperArm == null || lowerArm == null || hand == null || handTarget == null)
            return;

        if (armWeight <= 0f)
            return;

        Vector3 rootPosition = upperArm.position;
        Vector3 targetPosition = handTarget.position;
        Vector3 rootToTarget = targetPosition - rootPosition;
        float targetDistance = rootToTarget.magnitude;

        if (targetDistance < 0.0001f)
            return;

        float minReach = Mathf.Abs(upperLength - lowerLength) + minReachEpsilon;
        float maxReach = (upperLength + lowerLength) * Mathf.Max(0.01f, maxReachScale);
        float clampedDistance = Mathf.Clamp(targetDistance, minReach, maxReach);

        Vector3 targetDirection = rootToTarget.normalized;
        Vector3 bendDirection = GetBendDirection(rootPosition, targetDirection, lowerArm.position, elbowHint);

        float upperProjection = ((upperLength * upperLength) - (lowerLength * lowerLength) + (clampedDistance * clampedDistance)) / (2f * clampedDistance);
        float bendHeightSquared = Mathf.Max(0f, (upperLength * upperLength) - (upperProjection * upperProjection));
        float bendHeight = Mathf.Sqrt(bendHeightSquared);
        Vector3 desiredElbowPosition = rootPosition + (targetDirection * upperProjection) + (bendDirection * bendHeight);

        Quaternion upperStartRotation = upperArm.rotation;
        Quaternion lowerStartRotation = lowerArm.rotation;
        Quaternion handStartRotation = hand.rotation;

        Vector3 currentUpperToLower = lowerArm.position - upperArm.position;
        Vector3 desiredUpperToLower = desiredElbowPosition - rootPosition;

        if (currentUpperToLower.sqrMagnitude > 0.000001f && desiredUpperToLower.sqrMagnitude > 0.000001f)
        {
            Quaternion upperRotationDelta = Quaternion.FromToRotation(currentUpperToLower, desiredUpperToLower);
            Quaternion desiredUpperRotation = upperRotationDelta * upperArm.rotation;
            upperArm.rotation = Quaternion.Slerp(upperStartRotation, desiredUpperRotation, armWeight);
        }

        Vector3 currentLowerToHand = hand.position - lowerArm.position;
        Vector3 desiredLowerToHand = targetPosition - lowerArm.position;

        if (currentLowerToHand.sqrMagnitude > 0.000001f && desiredLowerToHand.sqrMagnitude > 0.000001f)
        {
            Quaternion lowerRotationDelta = Quaternion.FromToRotation(currentLowerToHand, desiredLowerToHand);
            Quaternion desiredLowerRotation = lowerRotationDelta * lowerArm.rotation;
            lowerArm.rotation = Quaternion.Slerp(lowerStartRotation, desiredLowerRotation, armWeight);
        }

        if (followHandTargetRotation)
        {
            Quaternion desiredHandRotation = handTarget.rotation * handRotationOffset;
            hand.rotation = Quaternion.Slerp(handStartRotation, desiredHandRotation, armWeight);
        }
    }

    private Vector3 GetBendDirection(
        Vector3 rootPosition,
        Vector3 targetDirection,
        Vector3 currentElbowPosition,
        Transform elbowHint)
    {
        Vector3 bendReference = elbowHint != null
            ? elbowHint.position - rootPosition
            : currentElbowPosition - rootPosition;

        Vector3 projected = Vector3.ProjectOnPlane(bendReference, targetDirection);

        if (projected.sqrMagnitude < 0.000001f)
        {
            projected = Vector3.ProjectOnPlane(transform.up, targetDirection);
        }

        if (projected.sqrMagnitude < 0.000001f)
        {
            projected = Vector3.ProjectOnPlane(transform.right, targetDirection);
        }

        return projected.normalized;
    }

    private Vector3 ClampUpperBodyEuler(Vector3 euler)
    {
        return new Vector3(
            Mathf.Clamp(euler.x, -maxUpperBodyEuler.x, maxUpperBodyEuler.x),
            Mathf.Clamp(euler.y, -maxUpperBodyEuler.y, maxUpperBodyEuler.y),
            Mathf.Clamp(euler.z, -maxUpperBodyEuler.z, maxUpperBodyEuler.z)
        );
    }

    public UpperBodyMotionSettings ExportUpperBodyMotionSettings()
    {
        return new UpperBodyMotionSettings
        {
            strength = upperBodyStrength,
            spineWeight = spineWeight,
            chestWeight = chestWeight,
            upperChestWeight = upperChestWeight,
            impactDuration = upperBodyImpactDuration
        };
    }

    public void ApplyUpperBodyMotionSettings(UpperBodyMotionSettings settings)
    {
        if (settings == null)
            return;

        settings.Clamp();
        upperBodyStrength = settings.strength;
        spineWeight = settings.spineWeight;
        chestWeight = settings.chestWeight;
        upperChestWeight = settings.upperChestWeight;
        upperBodyImpactDuration = settings.impactDuration;
    }

    private float GetBoneDistance(Transform a, Transform b)
    {
        if (a == null || b == null)
            return 0f;

        return Vector3.Distance(a.position, b.position);
    }
}
