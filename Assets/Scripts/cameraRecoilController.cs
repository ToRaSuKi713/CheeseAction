using UnityEngine;

public class CameraRecoilController : MonoBehaviour
{
    [Header("Strong Broadcast Preset")]
    public float strongPitch = 4f;
    public float strongYaw = 1.25f;
    public float strongRoll = 0f;

    [Header("Motion")]
    public float returnSpeed = 8f;
    public float snappiness = 16f;

    [Header("Debug")]
    public string lastAppliedPreset = "None";

    private Vector3 currentRotation;
    private Vector3 targetRotation;
    private Quaternion baseLocalRotation;

    private void Awake()
    {
        CacheBaseRotation();
    }

    private void OnEnable()
    {
        CacheBaseRotation();
    }

    private void Update()
    {
        bool recoilActive = targetRotation.sqrMagnitude > 0.000001f || currentRotation.sqrMagnitude > 0.000001f;
        if (!recoilActive)
        {
            baseLocalRotation = transform.localRotation;
            return;
        }

        targetRotation = Vector3.Lerp(targetRotation, Vector3.zero, returnSpeed * Time.deltaTime);
        currentRotation = Vector3.Lerp(currentRotation, targetRotation, snappiness * Time.deltaTime);

        if (targetRotation.sqrMagnitude < 0.000001f && currentRotation.sqrMagnitude < 0.000001f)
        {
            targetRotation = Vector3.zero;
            currentRotation = Vector3.zero;
            transform.localRotation = baseLocalRotation;
            return;
        }

        transform.localRotation = baseLocalRotation * Quaternion.Euler(currentRotation);
    }

    public void PlayWeakBroadcastPreset()
    {
        lastAppliedPreset = "Weak (No Camera Recoil)";
    }

    public void PlayStrongBroadcastPreset()
    {
        lastAppliedPreset = "Strong";
        AddRecoil(strongPitch, Random.Range(-strongYaw, strongYaw), strongRoll);
    }

    public void AddRecoil(float pitch, float yaw, float roll)
    {
        CacheBaseRotation();
        targetRotation += new Vector3(-Mathf.Abs(pitch), yaw, roll);
    }

    public void ResetRecoilInstant()
    {
        currentRotation = Vector3.zero;
        targetRotation = Vector3.zero;
        transform.localRotation = baseLocalRotation;
    }

    public void SetCurrentPoseAsBase()
    {
        currentRotation = Vector3.zero;
        targetRotation = Vector3.zero;
        CacheBaseRotation();
    }

    void CacheBaseRotation()
    {
        baseLocalRotation = transform.localRotation;
    }
}
