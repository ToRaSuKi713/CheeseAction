using System.Collections;
using UnityEngine;

public class VmcRecoilController : MonoBehaviour
{
    public Transform recoilTarget;
    public bool useLocalSpace = true;

    private Vector3 baseLocalPosition;
    private Quaternion baseLocalRotation;
    private Vector3 baseWorldPosition;
    private Quaternion baseWorldRotation;

    private Coroutine recoilRoutine;

    void Awake()
    {
        if (recoilTarget == null)
        {
            recoilTarget = transform;
        }

        CacheBaseTransform();
    }

    void OnEnable()
    {
        CacheBaseTransform();
    }

    public void ApplyEvent(TestEventData eventData)
    {
        if (eventData == null)
            return;

        float duration = Mathf.Max(0.01f, eventData.vmcDurationMs / 1000f);

        Vector3 positionOffset = new Vector3(
            eventData.vmcImpulseX,
            eventData.vmcImpulseY,
            eventData.vmcImpulseZ
        );

        Vector3 rotationOffsetEuler = new Vector3(
            eventData.vmcPitch,
            eventData.vmcYaw,
            eventData.vmcRoll
        );

        if (positionOffset.sqrMagnitude <= 0.000001f &&
            rotationOffsetEuler.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        if (recoilRoutine != null)
        {
            StopCoroutine(recoilRoutine);
            RestoreBaseTransform();
        }

        recoilRoutine = StartCoroutine(PlayRecoil(positionOffset, rotationOffsetEuler, duration));
    }

    void CacheBaseTransform()
    {
        if (recoilTarget == null)
            return;

        baseLocalPosition = recoilTarget.localPosition;
        baseLocalRotation = recoilTarget.localRotation;
        baseWorldPosition = recoilTarget.position;
        baseWorldRotation = recoilTarget.rotation;
    }

    public void SetCurrentPoseAsBase()
    {
        if (recoilTarget == null)
        {
            recoilTarget = transform;
        }

        CacheBaseTransform();
    }

    void RestoreBaseTransform()
    {
        if (recoilTarget == null)
            return;

        if (useLocalSpace)
        {
            recoilTarget.localPosition = baseLocalPosition;
            recoilTarget.localRotation = baseLocalRotation;
        }
        else
        {
            recoilTarget.position = baseWorldPosition;
            recoilTarget.rotation = baseWorldRotation;
        }
    }

    IEnumerator PlayRecoil(Vector3 positionOffset, Vector3 rotationOffsetEuler, float duration)
    {
        if (recoilTarget == null)
            yield break;

        CacheBaseTransform();

        float half = duration * 0.5f;
        Quaternion rotationOffset = Quaternion.Euler(rotationOffsetEuler);

        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / half);

            if (useLocalSpace)
            {
                recoilTarget.localPosition = Vector3.Lerp(baseLocalPosition, baseLocalPosition + positionOffset, lerp);
                recoilTarget.localRotation = Quaternion.Slerp(baseLocalRotation, baseLocalRotation * rotationOffset, lerp);
            }
            else
            {
                recoilTarget.position = Vector3.Lerp(baseWorldPosition, baseWorldPosition + positionOffset, lerp);
                recoilTarget.rotation = Quaternion.Slerp(baseWorldRotation, baseWorldRotation * rotationOffset, lerp);
            }

            yield return null;
        }

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / half);

            if (useLocalSpace)
            {
                recoilTarget.localPosition = Vector3.Lerp(baseLocalPosition + positionOffset, baseLocalPosition, lerp);
                recoilTarget.localRotation = Quaternion.Slerp(baseLocalRotation * rotationOffset, baseLocalRotation, lerp);
            }
            else
            {
                recoilTarget.position = Vector3.Lerp(baseWorldPosition + positionOffset, baseWorldPosition, lerp);
                recoilTarget.rotation = Quaternion.Slerp(baseWorldRotation * rotationOffset, baseWorldRotation, lerp);
            }

            yield return null;
        }

        RestoreBaseTransform();
        recoilRoutine = null;
    }
}
