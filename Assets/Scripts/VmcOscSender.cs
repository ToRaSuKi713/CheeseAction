using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class VmcOscSender : MonoBehaviour
{
    [Header("VMC Target")]
    public bool sendEnabled = true;
    public string targetHost = "127.0.0.1";
    public int targetPort = 39540;

    [Header("Source Avatar")]
    public Animator sourceAnimator;
    public bool autoFindSourceAnimator = true;
    public bool requireHumanoid = true;

    [Header("Send Rate")]
    [Range(10f, 120f)]
    public float sendRate = 60f;
    public bool sendOnlyWhileRecoiling = true;
    public int resetSendFrames = 45;

    [Header("Root Recoil Weights")]
    [Range(0f, 2f)] public float rootPositionWeight = 0.0f;
    [Range(0f, 2f)] public float rootRotationWeight = 0.0f;

    [Header("Spine / Head Recoil Weights")]
    [Range(0f, 2f)] public float neckPositionWeight = 0.20f;
    [Range(0f, 2f)] public float neckRotationWeight = 0.45f;

    [Range(0f, 2f)] public float headPositionWeight = 1.0f;
    [Range(0f, 2f)] public float headRotationWeight = 1.0f;

    [Range(0f, 2f)] public float upperChestPositionWeight = 0.28f;
    [Range(0f, 2f)] public float upperChestRotationWeight = 1.35f;

    [Range(0f, 2f)] public float chestPositionWeight = 0.2f;
    [Range(0f, 2f)] public float chestRotationWeight = 1.0f;

    [Range(0f, 2f)] public float spinePositionWeight = 0.12f;
    [Range(0f, 2f)] public float spineRotationWeight = 0.52f;

    [Header("Arm Recoil Weights")]
    [Range(0f, 2f)] public float leftUpperArmRotationWeight = 0.10f;
    [Range(0f, 2f)] public float leftLowerArmRotationWeight = 0.05f;
    [Range(0f, 2f)] public float leftHandRotationWeight = 0.05f;

    [Range(0f, 2f)] public float rightUpperArmRotationWeight = 0.10f;
    [Range(0f, 2f)] public float rightLowerArmRotationWeight = 0.05f;
    [Range(0f, 2f)] public float rightHandRotationWeight = 0.05f;

    [Header("Runtime")]
    public string LastStatus { get; private set; } = "Idle";
    public float LastSendTime { get; private set; } = -1f;

    public string LastTarget
    {
        get { return targetHost + ":" + targetPort; }
    }

    public int CurrentTargetPort
    {
        get { return Mathf.Clamp(targetPort, 1, 65535); }
    }

    private class BoneEntry
    {
        public HumanBodyBones bone;
        public Transform transform;
        public string boneName;
    }

    private struct BonePoseSnapshot
    {
        public Vector3 localPosition;
        public Quaternion localRotation;
    }

    private UdpClient udpClient;
    private float sendAccumulator = 0f;
    private readonly List<BoneEntry> boneEntries = new List<BoneEntry>(64);
    private readonly Dictionary<HumanBodyBones, BonePoseSnapshot> baselineBoneSnapshots = new Dictionary<HumanBodyBones, BonePoseSnapshot>(16);
    private readonly Dictionary<HumanBodyBones, BonePoseSnapshot> defaultBoneSnapshots = new Dictionary<HumanBodyBones, BonePoseSnapshot>(16);
    private Transform assistantHeadBone;
    private Vector3 baselineRootPosition;
    private Quaternion baselineRootRotation = Quaternion.identity;
    private Vector3 baselineHeadPosition;
    private Quaternion baselineHeadRotation = Quaternion.identity;
    private bool hasBaselineSnapshot = false;

    private Vector3 recoilBasePosition;
    private Vector3 recoilBaseEuler;
    private Vector3 currentRecoilPosition;
    private Vector3 currentRecoilEuler;
    private float recoilDurationSeconds = 0f;
    private float recoilElapsedSeconds = 0f;
    private bool recoilActive = false;
    private int pendingResetFrames = 0;

    void Awake()
    {
        TryResolveAnimator();
        RebuildBoneCache();
        CreateClient();
    }

    void OnEnable()
    {
        TryResolveAnimator();
        RebuildBoneCache();
        CreateClient();
    }

    void OnDisable()
    {
        CloseClient();
    }

    void OnApplicationQuit()
    {
        CloseClient();
    }

    [ContextMenu("Rebuild Bone Cache")]
    public void RebuildBoneCache()
    {
        boneEntries.Clear();
        defaultBoneSnapshots.Clear();
        assistantHeadBone = null;

        if (sourceAnimator == null)
            return;

        if (requireHumanoid && !sourceAnimator.isHuman)
            return;

        for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
        {
            HumanBodyBones bone = (HumanBodyBones)i;
            Transform boneTransform = sourceAnimator.GetBoneTransform(bone);

            if (boneTransform == null)
                continue;

            if (bone == HumanBodyBones.Head)
            {
                assistantHeadBone = boneTransform;
            }

            boneEntries.Add(new BoneEntry
            {
                bone = bone,
                transform = boneTransform,
                boneName = bone.ToString()
            });
        }

        CaptureDefaultSnapshot();
    }

    void LateUpdate()
    {
        if (!sendEnabled)
        {
            LastStatus = "Disabled";
            return;
        }

        if (!TryResolveAnimator())
        {
            LastStatus = "Source Animator is not assigned";
            return;
        }

        if (requireHumanoid && !sourceAnimator.isHuman)
        {
            LastStatus = "Source Animator is not Humanoid";
            return;
        }

        if (boneEntries.Count == 0)
        {
            RebuildBoneCache();
        }

        UpdateRecoil(Time.deltaTime);

        if (sendOnlyWhileRecoiling && !recoilActive && pendingResetFrames <= 0)
        {
            LastStatus = "Idle";
            return;
        }

        float interval = 1f / Mathf.Max(1f, sendRate);
        sendAccumulator += Time.deltaTime;

        while (sendAccumulator >= interval)
        {
            sendAccumulator -= interval;
            SendCurrentPose();

            if (pendingResetFrames > 0)
            {
                pendingResetFrames--;
            }
        }
    }

    bool TryResolveAnimator()
    {
        if (sourceAnimator != null)
            return true;

        if (!autoFindSourceAnimator)
            return false;

        sourceAnimator = GetComponent<Animator>();

        if (sourceAnimator == null)
        {
            sourceAnimator = GetComponentInChildren<Animator>();
        }

        return sourceAnimator != null;
    }

    public void ApplyEvent(TestEventData eventData)
    {
        if (!sendEnabled || eventData == null)
            return;

        if (!TryResolveAnimator())
            return;

        if (boneEntries.Count == 0)
        {
            RebuildBoneCache();
        }

        CaptureBaselineSnapshot();

        recoilBasePosition = new Vector3(
            eventData.vmcImpulseX,
            eventData.vmcImpulseY,
            eventData.vmcImpulseZ
        );

        recoilBaseEuler = new Vector3(
            eventData.vmcPitch,
            eventData.vmcYaw,
            eventData.vmcRoll
        );

        recoilDurationSeconds = Mathf.Max(0.01f, eventData.vmcDurationMs / 1000f);
        recoilElapsedSeconds = 0f;
        recoilActive = true;
        pendingResetFrames = 0;

        LastStatus = "Recoil overlay queued";
    }

    void UpdateRecoil(float deltaTime)
    {
        if (!recoilActive)
        {
            currentRecoilPosition = Vector3.zero;
            currentRecoilEuler = Vector3.zero;
            return;
        }

        recoilElapsedSeconds += deltaTime;
        float t = Mathf.Clamp01(recoilElapsedSeconds / recoilDurationSeconds);

        float weight;
        if (t < 0.5f)
        {
            weight = Mathf.SmoothStep(0f, 1f, t / 0.5f);
        }
        else
        {
            weight = Mathf.SmoothStep(1f, 0f, (t - 0.5f) / 0.5f);
        }

        currentRecoilPosition = recoilBasePosition * weight;
        currentRecoilEuler = recoilBaseEuler * weight;

        if (t >= 1f)
        {
            recoilActive = false;
            currentRecoilPosition = Vector3.zero;
            currentRecoilEuler = Vector3.zero;
            pendingResetFrames = Mathf.Max(1, resetSendFrames);
        }
    }

    void CaptureBaselineSnapshot()
    {
        if (sourceAnimator == null)
            return;

        baselineRootPosition = sourceAnimator.transform.position;
        baselineRootRotation = sourceAnimator.transform.rotation;
        baselineHeadPosition = assistantHeadBone != null ? assistantHeadBone.position : baselineRootPosition;
        baselineHeadRotation = assistantHeadBone != null ? assistantHeadBone.rotation : baselineRootRotation;
        baselineBoneSnapshots.Clear();

        for (int i = 0; i < boneEntries.Count; i++)
        {
            BoneEntry entry = boneEntries[i];
            if (entry == null || entry.transform == null)
                continue;

            if (!ShouldSendBoneToVSeeFace(entry.bone))
                continue;

            baselineBoneSnapshots[entry.bone] = new BonePoseSnapshot
            {
                localPosition = entry.transform.localPosition,
                localRotation = entry.transform.localRotation
            };
        }

        hasBaselineSnapshot = true;
    }

    void SendCurrentPose()
    {
        CreateClient();

        if (udpClient == null || sourceAnimator == null)
            return;

        try
        {
            for (int i = 0; i < boneEntries.Count; i++)
            {
                BoneEntry entry = boneEntries[i];
                if (entry == null || entry.transform == null)
                    continue;

                if (!ShouldSendBoneToVSeeFace(entry.bone))
                    continue;

                Vector3 localPosition;
                Quaternion localRotation;
                GetBonePoseForSend(entry, out localPosition, out localRotation);

                ApplyRecoilToBone(entry.bone, ref localPosition, ref localRotation);
                SendBonePose(entry.boneName, localPosition, localRotation);
            }

            LastSendTime = Time.time;
            LastStatus = "Streaming recoil pose to " + LastTarget;

            if (!recoilActive && pendingResetFrames <= 0)
            {
                hasBaselineSnapshot = false;
                baselineBoneSnapshots.Clear();
            }
        }
        catch (Exception ex)
        {
            LastStatus = "Send failed: " + ex.Message;
            Debug.LogWarning(LastStatus);
        }
    }

    void GetBonePoseForSend(BoneEntry entry, out Vector3 localPosition, out Quaternion localRotation)
    {
        if (defaultBoneSnapshots.TryGetValue(entry.bone, out BonePoseSnapshot defaultSnapshot))
        {
            localPosition = defaultSnapshot.localPosition;
            localRotation = defaultSnapshot.localRotation;
            return;
        }

        if (hasBaselineSnapshot && baselineBoneSnapshots.TryGetValue(entry.bone, out BonePoseSnapshot snapshot))
        {
            localPosition = snapshot.localPosition;
            localRotation = snapshot.localRotation;
            return;
        }

        localPosition = entry.transform.localPosition;
        localRotation = entry.transform.localRotation;
    }

    bool ShouldSendBoneToVSeeFace(HumanBodyBones bone)
    {
        switch (bone)
        {
            case HumanBodyBones.Spine:
            case HumanBodyBones.Chest:
            case HumanBodyBones.UpperChest:
                return true;

            default:
                return false;
        }
    }

    void SendRootPose()
    {
    }

    void CaptureDefaultSnapshot()
    {
        if (sourceAnimator == null)
            return;

        defaultBoneSnapshots.Clear();

        for (int i = 0; i < boneEntries.Count; i++)
        {
            BoneEntry entry = boneEntries[i];
            if (entry == null || entry.transform == null)
                continue;

            if (!ShouldSendBoneToVSeeFace(entry.bone))
                continue;

            defaultBoneSnapshots[entry.bone] = new BonePoseSnapshot
            {
                localPosition = entry.transform.localPosition,
                localRotation = entry.transform.localRotation
            };
        }

    }

    void SendBonePose(string boneName, Vector3 localPosition, Quaternion localRotation)
    {
        byte[] packet = BuildOscMessage(
            "/VMC/Ext/Bone/Pos",
            new object[]
            {
                boneName,
                localPosition.x, localPosition.y, localPosition.z,
                localRotation.x, localRotation.y, localRotation.z, localRotation.w
            }
        );

        udpClient.Send(packet, packet.Length, targetHost, targetPort);
    }

    void ApplyRecoilToRoot(ref Vector3 rootPosition, ref Quaternion rootRotation)
    {
        if (rootPositionWeight > 0f)
        {
            rootPosition += currentRecoilPosition * rootPositionWeight;
        }

        if (rootRotationWeight > 0f)
        {
            rootRotation = rootRotation * Quaternion.Euler(currentRecoilEuler * rootRotationWeight);
        }
    }

    void ApplyRecoilToBone(HumanBodyBones bone, ref Vector3 localPosition, ref Quaternion localRotation)
    {
        float positionWeight = 0f;
        float rotationWeight = 0f;
        bool applyPosition = false;
        bool applyRotation = false;

        switch (bone)
        {
            case HumanBodyBones.Neck:
                positionWeight = neckPositionWeight;
                rotationWeight = neckRotationWeight;
                applyPosition = positionWeight != 0f;
                applyRotation = rotationWeight != 0f;
                break;

            case HumanBodyBones.Head:
                positionWeight = headPositionWeight;
                rotationWeight = headRotationWeight;
                applyPosition = positionWeight != 0f;
                applyRotation = rotationWeight != 0f;
                break;

            case HumanBodyBones.UpperChest:
                positionWeight = upperChestPositionWeight;
                rotationWeight = upperChestRotationWeight;
                applyPosition = positionWeight != 0f;
                applyRotation = rotationWeight != 0f;
                break;

            case HumanBodyBones.Chest:
                positionWeight = chestPositionWeight;
                rotationWeight = chestRotationWeight;
                applyPosition = positionWeight != 0f;
                applyRotation = rotationWeight != 0f;
                break;

            case HumanBodyBones.Spine:
                positionWeight = spinePositionWeight;
                rotationWeight = spineRotationWeight;
                applyPosition = positionWeight != 0f;
                applyRotation = rotationWeight != 0f;
                break;

            case HumanBodyBones.LeftUpperArm:
                rotationWeight = leftUpperArmRotationWeight;
                applyRotation = rotationWeight != 0f;
                break;

            case HumanBodyBones.LeftLowerArm:
                rotationWeight = leftLowerArmRotationWeight;
                applyRotation = rotationWeight != 0f;
                break;

            case HumanBodyBones.LeftHand:
                rotationWeight = leftHandRotationWeight;
                applyRotation = rotationWeight != 0f;
                break;

            case HumanBodyBones.RightUpperArm:
                rotationWeight = rightUpperArmRotationWeight;
                applyRotation = rotationWeight != 0f;
                break;

            case HumanBodyBones.RightLowerArm:
                rotationWeight = rightLowerArmRotationWeight;
                applyRotation = rotationWeight != 0f;
                break;

            case HumanBodyBones.RightHand:
                rotationWeight = rightHandRotationWeight;
                applyRotation = rotationWeight != 0f;
                break;

            default:
                return;
        }

        if (applyPosition)
        {
            localPosition += currentRecoilPosition * positionWeight;
        }

        if (applyRotation)
        {
            localRotation = localRotation * Quaternion.Euler(currentRecoilEuler * rotationWeight);
        }
    }

    void CreateClient()
    {
        if (udpClient != null)
            return;

        try
        {
            udpClient = new UdpClient();
        }
        catch (Exception ex)
        {
            udpClient = null;
            LastStatus = "UDP client create failed: " + ex.Message;
            Debug.LogWarning(LastStatus);
        }
    }

    void CloseClient()
    {
        if (udpClient == null)
            return;

        try
        {
            udpClient.Close();
        }
        catch
        {
        }

        udpClient = null;
    }

    public void SetTargetPort(int port)
    {
        targetPort = Mathf.Clamp(port, 1, 65535);
        CloseClient();
        CreateClient();
    }

    byte[] BuildOscMessage(string address, object[] args)
    {
        List<byte> bytes = new List<byte>(256);

        AppendPaddedString(bytes, address);

        StringBuilder typeTags = new StringBuilder(",");
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] is string)
                typeTags.Append("s");
            else if (args[i] is int)
                typeTags.Append("i");
            else if (args[i] is float || args[i] is double)
                typeTags.Append("f");
            else
                throw new InvalidOperationException("Unsupported OSC arg type: " + args[i].GetType().Name);
        }

        AppendPaddedString(bytes, typeTags.ToString());

        for (int i = 0; i < args.Length; i++)
        {
            object arg = args[i];

            if (arg is string)
            {
                AppendPaddedString(bytes, (string)arg);
            }
            else if (arg is int)
            {
                AppendInt(bytes, (int)arg);
            }
            else if (arg is float)
            {
                AppendFloat(bytes, (float)arg);
            }
            else if (arg is double)
            {
                AppendFloat(bytes, (float)(double)arg);
            }
        }

        return bytes.ToArray();
    }

    void AppendPaddedString(List<byte> bytes, string text)
    {
        byte[] strBytes = Encoding.UTF8.GetBytes(text);
        bytes.AddRange(strBytes);
        bytes.Add(0);

        while (bytes.Count % 4 != 0)
        {
            bytes.Add(0);
        }
    }

    void AppendInt(List<byte> bytes, int value)
    {
        byte[] intBytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(intBytes);
        }

        bytes.AddRange(intBytes);
    }

    void AppendFloat(List<byte> bytes, float value)
    {
        byte[] floatBytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(floatBytes);
        }

        bytes.AddRange(floatBytes);
    }
}
