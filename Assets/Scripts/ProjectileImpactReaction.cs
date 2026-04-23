using UnityEngine;

public class ProjectileImpactReaction : MonoBehaviour
{
    [SerializeField] private bool useTagFilter = true;
    [SerializeField] private string requiredTag = "Head";
    [SerializeField] private bool matchParentTagToo = true;
    [SerializeField] private bool triggerOnlyOnce = true;
    [SerializeField] private float headHitVelocityDamping = 0.08f;

    private Rigidbody cachedRigidbody;
    private HeadArmPoseController headArmPoseController;
    private VmcRecoilController vmcRecoilController;
    private VmcOscSender vmcOscSender;
    private TestEventData eventData;
    private string spawnPattern;
    private bool allowImpactReaction;
    private bool triggered;

    private void Awake()
    {
        cachedRigidbody = GetComponent<Rigidbody>();
        if (cachedRigidbody == null)
        {
            cachedRigidbody = GetComponentInParent<Rigidbody>();
        }
    }

    public void Setup(
        TestEventData sourceEventData,
        string sourceSpawnPattern,
        HeadArmPoseController sourceHeadArmPoseController,
        VmcRecoilController sourceVmcRecoilController,
        VmcOscSender sourceVmcOscSender)
    {
        eventData = sourceEventData;
        spawnPattern = string.IsNullOrWhiteSpace(sourceSpawnPattern) ? "single" : sourceSpawnPattern.Trim().ToLowerInvariant();
        headArmPoseController = sourceHeadArmPoseController != null
            ? sourceHeadArmPoseController
            : Object.FindAnyObjectByType<HeadArmPoseController>();
        vmcRecoilController = sourceVmcRecoilController;
        vmcOscSender = sourceVmcOscSender;
        allowImpactReaction = spawnPattern != "rain";
        triggered = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (triggerOnlyOnce && triggered)
            return;

        if (!allowImpactReaction)
            return;

        if (!IsValidTarget(collision.collider.transform))
            return;

        Vector3 impactVelocity = collision.relativeVelocity;
        if (impactVelocity.sqrMagnitude < 0.0001f && cachedRigidbody != null)
        {
            impactVelocity = cachedRigidbody.linearVelocity;
        }

        if (headArmPoseController == null)
        {
            headArmPoseController = Object.FindAnyObjectByType<HeadArmPoseController>();
        }

        if (headArmPoseController != null)
        {
            headArmPoseController.TriggerUpperBodyImpact(eventData, impactVelocity);
        }

        Vector3 soundPosition = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        ProjectileHitSoundPlayer.Play(eventData != null ? eventData.hitSoundIndex : 0, soundPosition);

        if (eventData != null && eventData.vmcEnabled)
        {
            if (vmcOscSender != null)
            {
                vmcOscSender.ApplyEvent(BuildImpactVmcEventData(impactVelocity));
            }
        }

        DampenProjectileAfterHeadHit();

        triggered = true;
    }

    private TestEventData BuildImpactVmcEventData(Vector3 worldVelocity)
    {
        TestEventData result = new TestEventData();

        if (eventData != null)
        {
            result.eventType = eventData.eventType;
            result.nickname = eventData.nickname;
            result.message = eventData.message;
            result.amount = eventData.amount;
            result.spawnPattern = eventData.spawnPattern;
            result.power = eventData.power;
            result.direction = eventData.direction;
            result.count = eventData.count;
            result.scale = eventData.scale;
            result.color = eventData.color;
            result.hitSoundIndex = eventData.hitSoundIndex;
            result.projectileEnabled = eventData.projectileEnabled;
            result.vmcEnabled = eventData.vmcEnabled;
            result.cameraRecoilEnabled = eventData.cameraRecoilEnabled;
            result.globalProjectilePowerMultiplier = eventData.globalProjectilePowerMultiplier;
            result.globalVmcMultiplier = eventData.globalVmcMultiplier;
            result.vmcImpulseX = eventData.vmcImpulseX;
            result.vmcImpulseY = eventData.vmcImpulseY;
            result.vmcImpulseZ = eventData.vmcImpulseZ;
            result.vmcYaw = eventData.vmcYaw;
            result.vmcPitch = eventData.vmcPitch;
            result.vmcRoll = eventData.vmcRoll;
            result.vmcDurationMs = eventData.vmcDurationMs;
        }
        else
        {
            result.projectileEnabled = true;
            result.vmcEnabled = true;
            result.globalProjectilePowerMultiplier = 1f;
            result.globalVmcMultiplier = 1f;
            result.vmcDurationMs = 140;
        }

        Transform referenceRoot = null;
        if (vmcOscSender != null && vmcOscSender.sourceAnimator != null)
            referenceRoot = vmcOscSender.sourceAnimator.transform;

        if (referenceRoot == null && headArmPoseController != null)
            referenceRoot = headArmPoseController.transform;

        Vector3 localVelocity = referenceRoot != null
            ? referenceRoot.InverseTransformDirection(worldVelocity)
            : worldVelocity;

        float impactStrength = Mathf.Clamp(worldVelocity.magnitude * 0.08f, 0.12f, 0.9f);
        if (spawnPattern == "burst")
            impactStrength *= 1.75f;

        result.vmcImpulseX += Mathf.Clamp(localVelocity.x * 0.0012f, -0.02f, 0.02f) * impactStrength;
        result.vmcImpulseY += Mathf.Clamp(localVelocity.y * 0.0008f, -0.012f, 0.012f) * impactStrength;
        result.vmcImpulseZ += Mathf.Clamp(-localVelocity.z * 0.0022f, -0.08f, 0.02f) * impactStrength;

        result.vmcPitch += Mathf.Clamp(-localVelocity.z * 0.28f, -10f, 8f) * impactStrength;
        result.vmcYaw += Mathf.Clamp(localVelocity.x * 0.16f, -6f, 6f) * impactStrength;
        result.vmcRoll += Mathf.Clamp(-localVelocity.x * 0.20f, -8f, 8f) * impactStrength;

        if (spawnPattern == "burst")
        {
            result.vmcImpulseZ *= 1.4f;
            result.vmcPitch *= 1.5f;
            result.vmcRoll *= 1.25f;
        }

        result.vmcDurationMs = Mathf.Max(result.vmcDurationMs, 160);
        result.vmcEnabled = true;

        return result;
    }

    private void DampenProjectileAfterHeadHit()
    {
        if (cachedRigidbody == null)
            return;

        cachedRigidbody.linearVelocity *= headHitVelocityDamping;
        cachedRigidbody.angularVelocity *= 0.2f;
    }

    private bool IsValidTarget(Transform hitTransform)
    {
        if (!useTagFilter)
            return true;

        if (hitTransform.CompareTag(requiredTag))
            return true;

        if (!matchParentTagToo)
            return false;

        Transform current = hitTransform.parent;
        while (current != null)
        {
            if (current.CompareTag(requiredTag))
                return true;

            current = current.parent;
        }

        return false;
    }
}
