using UnityEngine;

public class ScreenShakeController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform shakeTarget;

    [Header("Defaults")]
    [SerializeField] private bool useUnscaledTime = false;
    [SerializeField] private bool restoreWhenIdle = true;

    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;

    private float remainingTime = 0f;
    private float positionMagnitude = 0f;
    private float rotationMagnitude = 0f;

    private void Awake()
    {
        if (shakeTarget == null)
        {
            shakeTarget = transform;
        }

        originalLocalPosition = shakeTarget.localPosition;
        originalLocalRotation = shakeTarget.localRotation;
    }

    private void OnEnable()
    {
        if (shakeTarget == null)
        {
            shakeTarget = transform;
        }

        originalLocalPosition = shakeTarget.localPosition;
        originalLocalRotation = shakeTarget.localRotation;
    }

    private void LateUpdate()
    {
        if (shakeTarget == null)
            return;

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        if (remainingTime > 0f)
        {
            remainingTime -= deltaTime;

            Vector3 randomPositionOffset = Random.insideUnitSphere * positionMagnitude;
            Vector3 randomRotationOffset = Random.insideUnitSphere * rotationMagnitude;

            shakeTarget.localPosition = originalLocalPosition + randomPositionOffset;
            shakeTarget.localRotation = originalLocalRotation * Quaternion.Euler(randomRotationOffset);
        }
        else if (restoreWhenIdle)
        {
            shakeTarget.localPosition = originalLocalPosition;
            shakeTarget.localRotation = originalLocalRotation;
        }
    }

    public void Shake(float positionAmount, float rotationAmount, float duration)
    {
        if (duration <= 0f)
            return;

        positionMagnitude = Mathf.Max(positionMagnitude, Mathf.Max(0f, positionAmount));
        rotationMagnitude = Mathf.Max(rotationMagnitude, Mathf.Max(0f, rotationAmount));
        remainingTime = Mathf.Max(remainingTime, duration);
    }

    public void StopShake()
    {
        remainingTime = 0f;
        positionMagnitude = 0f;
        rotationMagnitude = 0f;

        if (shakeTarget != null)
        {
            shakeTarget.localPosition = originalLocalPosition;
            shakeTarget.localRotation = originalLocalRotation;
        }
    }

    public void SetCurrentPoseAsOrigin()
    {
        if (shakeTarget == null)
        {
            shakeTarget = transform;
        }

        originalLocalPosition = shakeTarget.localPosition;
        originalLocalRotation = shakeTarget.localRotation;
    }
}