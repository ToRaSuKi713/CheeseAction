using UnityEngine;

public class RandomBounceScatter : MonoBehaviour
{
    [Header("Target Filter")]
    [SerializeField] private bool useTagFilter = true;
    [SerializeField] private string requiredTag = "Head";
    [SerializeField] private bool matchParentTagToo = true;

    [Header("Bounce Randomness")]
    [SerializeField] private float randomAngleMin = 8f;
    [SerializeField] private float randomAngleMax = 28f;
    [SerializeField] private float normalPush = 0.2f;
    [SerializeField] private float upwardBias = 0.15f;
    [SerializeField] private float speedMultiplierMin = 0.9f;
    [SerializeField] private float speedMultiplierMax = 1.15f;

    [Header("Spin")]
    [SerializeField] private bool addRandomSpin = true;
    [SerializeField] private float randomSpinStrength = 6f;

    [Header("Limits")]
    [SerializeField] private float minIncomingSpeed = 0.5f;
    [SerializeField] private bool applyOnlyOnce = false;

    private Rigidbody rb;
    private bool alreadyApplied = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            rb = GetComponentInChildren<Rigidbody>();
        }

        if (rb == null)
        {
            Debug.LogWarning("[RandomBounceScatter] Rigidbody를 찾지 못했습니다.");
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (rb == null)
            return;

        if (applyOnlyOnce && alreadyApplied)
            return;

        if (!IsValidTarget(collision))
            return;

        Vector3 currentVelocity = rb.linearVelocity;
        float currentSpeed = currentVelocity.magnitude;

        if (currentSpeed < minIncomingSpeed)
            return;

        Vector3 hitNormal = collision.contacts.Length > 0
            ? collision.contacts[0].normal
            : -transform.forward;

        Vector3 reflectedDirection = Vector3.Reflect(currentVelocity.normalized, hitNormal);

        Vector3 randomAxis = Random.onUnitSphere;
        if (Vector3.Dot(randomAxis.normalized, reflectedDirection.normalized) > 0.95f)
        {
            randomAxis = Vector3.Cross(reflectedDirection, Vector3.up);
            if (randomAxis.sqrMagnitude < 0.0001f)
            {
                randomAxis = Vector3.Cross(reflectedDirection, Vector3.right);
            }
        }

        float randomAngle = Random.Range(randomAngleMin, randomAngleMax);
        Vector3 randomizedDirection =
            Quaternion.AngleAxis(randomAngle, randomAxis.normalized) * reflectedDirection;

        randomizedDirection += hitNormal * normalPush;
        randomizedDirection += Vector3.up * upwardBias;
        randomizedDirection.Normalize();

        float speedMultiplier = Random.Range(speedMultiplierMin, speedMultiplierMax);
        rb.linearVelocity = randomizedDirection * currentSpeed * speedMultiplier;

        if (addRandomSpin)
        {
            rb.angularVelocity = Vector3.zero;
            rb.AddTorque(Random.onUnitSphere * randomSpinStrength, ForceMode.VelocityChange);
        }

        alreadyApplied = true;
    }

    private bool IsValidTarget(Collision collision)
    {
        if (!useTagFilter)
            return true;

        if (collision.collider.CompareTag(requiredTag))
            return true;

        if (matchParentTagToo)
        {
            Transform current = collision.collider.transform.parent;

            while (current != null)
            {
                if (current.CompareTag(requiredTag))
                    return true;

                current = current.parent;
            }
        }

        return false;
    }
}