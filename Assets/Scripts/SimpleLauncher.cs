using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using EVMC4U;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

public class SimpleLauncher : MonoBehaviour
{
    [Header("Ports")]
    [SerializeField] private int vSeeFaceReceivePort = 39541;

    [Header("Projectile")]
    public GameObject projectilePrefab;
    public bool useDownloadedProjectilePool = true;
    public bool avoidSameProjectileTwiceInARow = true;
    public List<GameObject> downloadedProjectilePrefabs = new List<GameObject>();

    [Header("Rain Override")]
    public bool useRainOverrideProjectile = true;
    public GameObject rainOverrideProjectilePrefab;

    [Header("Rain Drop Tuning")]
    public bool rainOverrideUseGravityDrop = true;
    public float rainDropHorizontalRadius = 0.35f;
    public float rainDropMinHeight = 1.4f;
    public float rainDropMaxHeight = 2.0f;
    public bool rainDropRandomYaw = true;

    [Header("Launch Points")]
    public Transform firePoint;
    public Transform rainFirePoint;
    public bool randomlyUseRainFirePointForProjectiles = true;
    public bool aimProjectilesAtAvatarHead = true;
    public bool makeFirePointTrackAvatarHead = true;
    public bool makeRainFirePointTrackAvatarHead = true;

    [Header("Machine Gun")]
    public KeyCode machineGunTestKey = KeyCode.F5;
    [Min(0.1f)] public float machineGunDuration = 1.5f;
    [Min(0.03f)] public float machineGunInterval = 0.12f;
    [Min(0.01f)] public float machineGunPowerMultiplier = 1.0f;

    [Header("Config")]
    public LaunchConfigData defaultConfigData;

    [Header("Feedback")]
    public VmcRecoilController vmcRecoilController;
    public VmcOscSender vmcOscSender;
    public ScreenShakeController screenShakeController;
    public HeadArmPoseController headArmPoseController;
    public HeadStickerPlayer headStickerPlayer;

    [Header("Screen Shake")]
    public bool enableScreenShake = true;
    public float defaultShakePosition = 0.015f;
    public float defaultShakeRotation = 0.4f;
    public float defaultShakeDuration = 0.08f;

    public float donationShakePosition = 0.025f;
    public float donationShakeRotation = 0.8f;
    public float donationShakeDuration = 0.10f;

    public float bigDonationShakePosition = 0.045f;
    public float bigDonationShakeRotation = 1.6f;
    public float bigDonationShakeDuration = 0.14f;

    public float rainShakePosition = 0.02f;
    public float rainShakeRotation = 0.7f;
    public float rainShakeDuration = 0.12f;

    public string LastLaunchLabel { get; private set; } = "None";
    public float LastLaunchTime { get; private set; } = -1f;
    public TestEventData LastEventData { get; private set; }

    public LaunchConfigFile RuntimeConfig
    {
        get { return runtimeConfig; }
    }

    public string VSeeFaceReceiverStatus
    {
        get
        {
            if (vSeeFaceReceiver == null)
                return "Receiver not found";

            return IsVSeeFaceConnected ? "Connected" : "Disconnected";
        }
    }

    public int VSeeFaceReceiverAvailable
    {
        get { return vSeeFaceReceiver != null ? vSeeFaceReceiver.GetAvailable() : 0; }
    }

    public float VSeeFaceReceiverRemoteTime
    {
        get { return vSeeFaceReceiver != null ? vSeeFaceReceiver.GetRemoteTime() : 0f; }
    }

    public bool IsVSeeFaceConnected
    {
        get
        {
            return vSeeFaceReceiver != null
                && vSeeFaceReceiver.GetAvailable() > 0
                && (Time.unscaledTime - lastVSeeFacePacketTime) <= 1.5f;
        }
    }

    public int VSeeFaceReceivePort
    {
        get { return Mathf.Clamp(vSeeFaceReceivePort, 1, 65535); }
    }

    private LaunchConfigFile runtimeConfig;
    private readonly Dictionary<int, float> nextLaunchTimes = new Dictionary<int, float>();
    private readonly List<Collider> activeProjectileColliders = new List<Collider>();
    private int lastProjectilePoolIndex = -1;
    private Transform headBoxAnchorTransform;
    private ExternalReceiver vSeeFaceReceiver;
    private uOSC.uOscServer vSeeFaceReceiverServer;
    private float lastObservedVSeeFaceRemoteTime = float.MinValue;
    private float lastVSeeFacePacketTime = -999f;
    private Coroutine machineGunCoroutine;

    private string ConfigFilePath
    {
        get
        {
            return Path.Combine(Application.persistentDataPath, "launch-config.json");
        }
    }

    void Awake()
    {
        EnsureHeadArmPoseController();
        EnsureHeadBoxAnchorTransform();
        EnsureVSeeFaceReceiver();
        LoadOrCreateConfig();
    }

    void Update()
    {
        TrackVSeeFaceHeartbeat();
        HandleKeyboardInput();
        UpdateLaunchPointTracking();

        if (InputKeyHelper.GetKeyDown(machineGunTestKey))
        {
            LaunchMachineGun();
        }

        if (InputKeyHelper.GetKeyDown(KeyCode.F9))
        {
            LoadConfig();
            Debug.Log("Config reloaded.");
        }
    }

    void UpdateLaunchPointTracking()
    {
        Transform headTransform = ResolveAvatarHeadTransform();
        if (headTransform == null)
            return;

        TrackLaunchPointToHead(firePoint, headTransform, makeFirePointTrackAvatarHead);
        TrackLaunchPointToHead(rainFirePoint, headTransform, makeRainFirePointTrackAvatarHead);
    }

    void TrackLaunchPointToHead(Transform launchPoint, Transform headTransform, bool enabled)
    {
        if (!enabled || launchPoint == null || headTransform == null)
            return;

        Vector3 direction = headTransform.position - launchPoint.position;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        launchPoint.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    void HandleKeyboardInput()
    {
        if (runtimeConfig == null || runtimeConfig.launchConfigs == null)
            return;

        for (int i = 0; i < runtimeConfig.launchConfigs.Length; i++)
        {
            LaunchEventConfig config = runtimeConfig.launchConfigs[i];

            if (config == null)
                continue;

            if (config.triggerKey == KeyCode.None)
                continue;

            if (config.triggerKey == machineGunTestKey)
                continue;

            if (config.triggerKey == KeyCode.Alpha1 ||
                config.triggerKey == KeyCode.Alpha2 ||
                config.triggerKey == KeyCode.Alpha3 ||
                config.triggerKey == KeyCode.Alpha4)
                continue;

            if (InputKeyHelper.GetKeyDown(config.triggerKey))
            {
                int testAmount = 0;

                if (config.label == "싱글샷")
                {
                    testAmount = 1000;
                }
                else if (config.label == "샷건")
                {
                    testAmount = 5000;
                }

                TestEventData testData = new TestEventData
                {
                    eventType = config.label,
                    nickname = "KeyboardUser",
                    message = "Manual test event",
                    amount = testAmount,
                    spawnPattern = "single",
                    direction = "forward",
                    power = 1.0f,
                    count = 1,
                    scale = 1.0f,
                    color = "",
                    projectileEnabled = true,
                    vmcEnabled = true,
                    globalProjectilePowerMultiplier = 1.0f,
                    globalVmcMultiplier = 1.0f,
                    vmcImpulseX = 0f,
                    vmcImpulseY = 0f,
                    vmcImpulseZ = -0.02f,
                    vmcYaw = 0f,
                    vmcPitch = -1f,
                    vmcRoll = 0f,
                    vmcDurationMs = 120
                };

                TryLaunch(i, config, testData);
            }
        }
    }

    public void LaunchByLabel(string label)
    {
        LaunchByLabel(label, null);
    }

    public void LaunchByLabel(string label, TestEventData eventData)
    {
        string normalizedRequestedLabel = NormalizeLaunchLabel(label);

        if (normalizedRequestedLabel == "sticker")
        {
            PlayHeadSticker();
            return;
        }

        if (runtimeConfig == null || runtimeConfig.launchConfigs == null)
            return;

        if (normalizedRequestedLabel == "machinegun")
        {
            LaunchMachineGun(eventData);
            return;
        }

        if (normalizedRequestedLabel == "box")
            return;

        for (int i = 0; i < runtimeConfig.launchConfigs.Length; i++)
        {
            LaunchEventConfig config = runtimeConfig.launchConfigs[i];

            if (config == null)
                continue;

            if (AreLaunchLabelsEquivalent(normalizedRequestedLabel, config.label))
            {
                TryLaunch(i, config, eventData);
                return;
            }
        }

        Debug.LogWarning("No launch config found for label: " + label);
    }

    public void LaunchMachineGun()
    {
        LaunchMachineGun(null);
    }

    public void LaunchMachineGun(TestEventData eventData)
    {
        if (machineGunCoroutine != null)
            StopCoroutine(machineGunCoroutine);

        machineGunCoroutine = StartCoroutine(MachineGunRoutine(eventData));
    }

    public void PlayHeadSticker()
    {
        if (headStickerPlayer == null)
            headStickerPlayer = Object.FindAnyObjectByType<HeadStickerPlayer>();

        if (headStickerPlayer == null)
            headStickerPlayer = gameObject.AddComponent<HeadStickerPlayer>();

        headStickerPlayer.Play(ResolveStickerHeadTransform());
    }

    IEnumerator MachineGunRoutine(TestEventData eventData)
    {
        LaunchEventConfig config = FindLaunchConfig("single");
        if (config == null)
        {
            Debug.LogWarning("No single-shot launch config found for Machine Gun.");
            machineGunCoroutine = null;
            yield break;
        }

        LaunchEventConfig machineGunConfig = config.Clone();
        machineGunConfig.label = "\uAE30\uAD00\uCD1D";

        float endTime = Time.time + Mathf.Max(0.1f, machineGunDuration);
        float interval = Mathf.Max(0.03f, machineGunInterval);

        while (Time.time < endTime)
        {
            Launch(machineGunConfig, BuildMachineGunEventData(eventData));
            yield return new WaitForSeconds(interval);
        }

        machineGunCoroutine = null;
    }

    TestEventData BuildMachineGunEventData(TestEventData source)
    {
        float power = source != null && source.power > 0f ? source.power : machineGunPowerMultiplier;
        float scale = source != null && source.scale > 0f ? source.scale : 1.0f;

        return new TestEventData
        {
            eventType = "\uAE30\uAD00\uCD1D",
            nickname = source != null ? source.nickname : "KeyboardUser",
            message = source != null ? source.message : "Machine gun test event",
            amount = source != null ? source.amount : 0,
            spawnPattern = "single",
            direction = "forward",
            power = power,
            count = 1,
            scale = scale,
            color = source != null ? source.color : "",
            hitSoundIndex = source != null ? source.hitSoundIndex : 0,
            projectileEnabled = source == null || source.projectileEnabled,
            vmcEnabled = source == null || source.vmcEnabled,
            cameraRecoilEnabled = false,
            globalProjectilePowerMultiplier = source != null && source.globalProjectilePowerMultiplier > 0f
                ? source.globalProjectilePowerMultiplier
                : 1.0f,
            globalVmcMultiplier = source != null && source.globalVmcMultiplier > 0f
                ? source.globalVmcMultiplier
                : 1.0f,
            vmcImpulseX = source != null ? source.vmcImpulseX : 0f,
            vmcImpulseY = source != null ? source.vmcImpulseY : 0f,
            vmcImpulseZ = source != null ? source.vmcImpulseZ : -0.02f,
            vmcYaw = source != null ? source.vmcYaw : 0f,
            vmcPitch = source != null ? source.vmcPitch : -1f,
            vmcRoll = source != null ? source.vmcRoll : 0f,
            vmcDurationMs = source != null && source.vmcDurationMs > 0 ? source.vmcDurationMs : 120
        };
    }

    LaunchEventConfig FindLaunchConfig(string label)
    {
        if (runtimeConfig == null || runtimeConfig.launchConfigs == null)
            return null;

        for (int i = 0; i < runtimeConfig.launchConfigs.Length; i++)
        {
            LaunchEventConfig config = runtimeConfig.launchConfigs[i];
            if (config != null && AreLaunchLabelsEquivalent(label, config.label))
                return config;
        }

        return null;
    }

    public void LaunchByIndex(int index)
    {
        if (runtimeConfig == null || runtimeConfig.launchConfigs == null)
            return;

        if (index < 0 || index >= runtimeConfig.launchConfigs.Length)
        {
            Debug.LogWarning("Launch index out of range: " + index);
            return;
        }

        LaunchEventConfig config = runtimeConfig.launchConfigs[index];
        if (config == null)
            return;

        TryLaunch(index, config, null);
    }

    void TryLaunch(int index, LaunchEventConfig config, TestEventData eventData)
    {
        float nextLaunchTime = nextLaunchTimes.TryGetValue(index, out float savedTime)
            ? savedTime
            : 0f;

        if (Time.time < nextLaunchTime)
        {
            Debug.Log(config.label + " is on cooldown.");
            return;
        }

        nextLaunchTimes[index] = Time.time + config.cooldown;
        Launch(config, eventData);
    }

    void Launch(LaunchEventConfig config, TestEventData eventData)
    {
        TestEventData effectiveEventData = BuildEffectiveEventData(eventData);

        LastLaunchLabel = config.label;
        LastLaunchTime = Time.time;
        LastEventData = effectiveEventData != null ? effectiveEventData : eventData;

        bool projectileEnabled = ResolveProjectileEnabled(effectiveEventData);
        bool vmcEnabled = ResolveVmcEnabled(effectiveEventData);

        string spawnPattern = ResolveSpawnPattern(effectiveEventData);
        if (spawnPattern == "rain")
            return;

        Transform launchOrigin = ResolveLaunchOrigin(spawnPattern);
        GameObject selectedProjectilePrefab = ResolveProjectilePrefabForPattern(spawnPattern);

        if (launchOrigin == null)
        {
            Debug.LogWarning("No launch point is assigned.");
            return;
        }

        if (projectileEnabled && selectedProjectilePrefab == null)
        {
            Debug.LogWarning("No projectile prefab is assigned for current spawn pattern.");
            return;
        }

        float powerMultiplier = ResolvePowerMultiplier(effectiveEventData);
        Vector3 launchDirection = ResolveDirection(effectiveEventData, spawnPattern, launchOrigin);
        int count = ResolveCount(effectiveEventData);
        float scaleMultiplier = ResolveScaleMultiplier(effectiveEventData);

        Color overrideColor;
        bool hasColorOverride = TryResolveColorOverride(effectiveEventData, out overrideColor);

        bool useGravityDropOnly = ShouldUseGravityDropOnly(spawnPattern, selectedProjectilePrefab);

        if (projectileEnabled)
        {
            if (spawnPattern == "burst")
            {
                LaunchBurst(
                    selectedProjectilePrefab,
                    launchOrigin,
                    config,
                    count,
                    launchDirection,
                    powerMultiplier,
                    effectiveEventData,
                    scaleMultiplier,
                    hasColorOverride,
                    overrideColor
                );
            }
            else if (spawnPattern == "rain")
            {
                LaunchRain(
                    selectedProjectilePrefab,
                    launchOrigin,
                    config,
                    count,
                    launchDirection,
                    powerMultiplier,
                    effectiveEventData,
                    scaleMultiplier,
                    hasColorOverride,
                    overrideColor,
                    useGravityDropOnly
                );
            }
            else
            {
                LaunchSingle(
                    selectedProjectilePrefab,
                    launchOrigin,
                    config,
                    count,
                    launchDirection,
                    powerMultiplier,
                    effectiveEventData,
                    scaleMultiplier,
                    hasColorOverride,
                    overrideColor
                );
            }
        }
        else
        {
            Debug.Log("Projectile is disabled by global safety switch.");
        }

        if (vmcEnabled)
        {
            if (!projectileEnabled)
            {
                if (vmcOscSender != null)
                {
                    vmcOscSender.ApplyEvent(effectiveEventData);
                }
            }
        }
        else
        {
            Debug.Log("VMC is disabled by global safety switch.");
        }

        if (!projectileEnabled && !vmcEnabled)
        {
            Debug.Log("Both Projectile and VMC are disabled.");
        }

        if (!RuntimeLogSettings.VerboseRealtimeLogs)
            return;

        if (effectiveEventData != null)
        {
            Debug.Log(
                "Launched: " + config.label +
                " | projectileEnabled=" + effectiveEventData.projectileEnabled +
                " | vmcEnabled=" + effectiveEventData.vmcEnabled +
                " | globalProjectilePowerMultiplier=" + effectiveEventData.globalProjectilePowerMultiplier +
                " | globalVmcMultiplier=" + effectiveEventData.globalVmcMultiplier
            );
        }
        else
        {
            Debug.Log("Launched: " + config.label);
        }
    }

    void ApplyScreenShake(string launchLabel, string spawnPattern)
    {
        float positionAmount = defaultShakePosition;
        float rotationAmount = defaultShakeRotation;
        float duration = defaultShakeDuration;

        if (launchLabel == "싱글샷")
        {
            positionAmount = donationShakePosition;
            rotationAmount = donationShakeRotation;
            duration = donationShakeDuration;
        }
        else if (launchLabel == "샷건")
        {
            positionAmount = bigDonationShakePosition;
            rotationAmount = bigDonationShakeRotation;
            duration = bigDonationShakeDuration;
        }

        if (spawnPattern == "rain")
        {
            positionAmount = Mathf.Max(positionAmount, rainShakePosition);
            rotationAmount = Mathf.Max(rotationAmount, rainShakeRotation);
            duration = Mathf.Max(duration, rainShakeDuration);
        }

        screenShakeController.Shake(positionAmount, rotationAmount, duration);
    }

    Transform ResolveLaunchOrigin(string spawnPattern)
    {
        if (spawnPattern != "rain" &&
            randomlyUseRainFirePointForProjectiles &&
            firePoint != null &&
            rainFirePoint != null)
        {
            return Random.value < 0.5f ? firePoint : rainFirePoint;
        }

        return firePoint;
    }

    GameObject ResolveProjectilePrefabForPattern(string spawnPattern)
    {
        if (spawnPattern == "rain" && useRainOverrideProjectile && rainOverrideProjectilePrefab != null)
        {
            return rainOverrideProjectilePrefab;
        }

        return SelectProjectilePrefab();
    }

    bool ShouldUseGravityDropOnly(string spawnPattern, GameObject projectileToSpawn)
    {
        if (spawnPattern != "rain")
            return false;

        if (!useRainOverrideProjectile)
            return false;

        if (!rainOverrideUseGravityDrop)
            return false;

        if (rainOverrideProjectilePrefab == null)
            return false;

        return projectileToSpawn == rainOverrideProjectilePrefab;
    }

    GameObject SelectProjectilePrefab()
    {
        if (!useDownloadedProjectilePool)
            return projectilePrefab;

        List<GameObject> validPrefabs = new List<GameObject>();

        if (downloadedProjectilePrefabs != null)
        {
            for (int i = 0; i < downloadedProjectilePrefabs.Count; i++)
            {
                if (downloadedProjectilePrefabs[i] != null)
                {
                    validPrefabs.Add(downloadedProjectilePrefabs[i]);
                }
            }
        }

        if (validPrefabs.Count == 0)
            return projectilePrefab;

        if (validPrefabs.Count == 1)
        {
            lastProjectilePoolIndex = 0;
            return validPrefabs[0];
        }

        int randomIndex = Random.Range(0, validPrefabs.Count);

        if (avoidSameProjectileTwiceInARow)
        {
            int safety = 0;

            while (randomIndex == lastProjectilePoolIndex && safety < 16)
            {
                randomIndex = Random.Range(0, validPrefabs.Count);
                safety++;
            }
        }

        lastProjectilePoolIndex = randomIndex;
        return validPrefabs[randomIndex];
    }

    TestEventData BuildEffectiveEventData(TestEventData source)
    {
        if (source == null)
            return null;

        TestEventData result = new TestEventData();

        result.eventType = source.eventType;
        result.nickname = source.nickname;
        result.message = source.message;
        result.amount = source.amount;
        result.spawnPattern = source.spawnPattern;
        result.power = source.power * Mathf.Max(0f, source.globalProjectilePowerMultiplier <= 0f && source.globalProjectilePowerMultiplier != 0f ? 1f : source.globalProjectilePowerMultiplier);
        result.direction = source.direction;
        result.count = source.count;
        result.scale = source.scale;
        result.color = source.color;
        result.hitSoundIndex = source.hitSoundIndex;

        result.projectileEnabled = source.projectileEnabled;
        result.vmcEnabled = source.vmcEnabled;
        result.globalProjectilePowerMultiplier = source.globalProjectilePowerMultiplier;
        result.globalVmcMultiplier = source.globalVmcMultiplier;

        float vmcMultiplier = source.globalVmcMultiplier;
        result.vmcImpulseX = source.vmcImpulseX * vmcMultiplier;
        result.vmcImpulseY = source.vmcImpulseY * vmcMultiplier;
        result.vmcImpulseZ = source.vmcImpulseZ * vmcMultiplier;
        result.vmcYaw = source.vmcYaw * vmcMultiplier;
        result.vmcPitch = source.vmcPitch * vmcMultiplier;
        result.vmcRoll = source.vmcRoll * vmcMultiplier;
        result.vmcDurationMs = source.vmcDurationMs;

        return result;
    }

    void LaunchSingle(
        GameObject projectileToSpawn,
        Transform launchOrigin,
        LaunchEventConfig config,
        int count,
        Vector3 direction,
        float powerMultiplier,
        TestEventData eventData,
        float scaleMultiplier,
        bool hasColorOverride,
        Color overrideColor)
    {
        int finalCount = Mathf.Max(1, count);
        float spacing = 0.25f;
        float force = config.force * powerMultiplier;

        for (int i = 0; i < finalCount; i++)
        {
            float xOffset = finalCount == 1
                ? 0f
                : (i - (finalCount - 1) * 0.5f) * spacing;

            Vector3 spawnPosition = launchOrigin.position + (launchOrigin.right * xOffset);

            SpawnProjectile(
                projectileToSpawn,
                launchOrigin,
                config,
                spawnPosition,
                direction,
                force,
                eventData,
                ResolveSpawnPattern(eventData),
                scaleMultiplier,
                hasColorOverride,
                overrideColor,
                true
            );
        }
    }

    void LaunchBurst(
        GameObject projectileToSpawn,
        Transform launchOrigin,
        LaunchEventConfig config,
        int count,
        Vector3 direction,
        float powerMultiplier,
        TestEventData eventData,
        float scaleMultiplier,
        bool hasColorOverride,
        Color overrideColor)
    {
        int finalCount = Mathf.Max(1, count);
        float totalSpread = finalCount <= 1 ? 0f : 30f;
        float force = config.force * powerMultiplier;

        for (int i = 0; i < finalCount; i++)
        {
            float t = finalCount <= 1 ? 0.5f : (float)i / (finalCount - 1);
            float angle = Mathf.Lerp(-totalSpread * 0.5f, totalSpread * 0.5f, t);

            Vector3 rotatedDirection = Quaternion.AngleAxis(angle, launchOrigin.up) * direction;

            SpawnProjectile(
                projectileToSpawn,
                launchOrigin,
                config,
                launchOrigin.position,
                rotatedDirection,
                force,
                eventData,
                ResolveSpawnPattern(eventData),
                scaleMultiplier,
                hasColorOverride,
                overrideColor,
                true
            );
        }
    }

    void LaunchRain(
        GameObject projectileToSpawn,
        Transform launchOrigin,
        LaunchEventConfig config,
        int count,
        Vector3 direction,
        float powerMultiplier,
        TestEventData eventData,
        float scaleMultiplier,
        bool hasColorOverride,
        Color overrideColor,
        bool useGravityDropOnly)
    {
        int finalCount = Mathf.Max(1, count);
        float force = config.force * powerMultiplier;

        for (int i = 0; i < finalCount; i++)
        {
            Vector3 spawnPosition;

            if (useGravityDropOnly)
            {
                float randomX = Random.Range(-rainDropHorizontalRadius, rainDropHorizontalRadius);
                float randomZ = Random.Range(-rainDropHorizontalRadius, rainDropHorizontalRadius);
                float randomY = Random.Range(rainDropMinHeight, rainDropMaxHeight);

                spawnPosition = launchOrigin.position + new Vector3(randomX, randomY, randomZ);
            }
            else
            {
                Vector3 offset = new Vector3(
                    Random.Range(-1.5f, 1.5f),
                    Random.Range(2.0f, 4.0f),
                    Random.Range(-1.5f, 1.5f)
                );

                spawnPosition = launchOrigin.position + offset;
            }

            SpawnProjectile(
                projectileToSpawn,
                launchOrigin,
                config,
                spawnPosition,
                direction,
                force,
                eventData,
                ResolveSpawnPattern(eventData),
                scaleMultiplier,
                hasColorOverride,
                overrideColor,
                !useGravityDropOnly
            );
        }
    }

    void SpawnProjectile(
        GameObject projectileToSpawn,
        Transform launchOrigin,
        LaunchEventConfig config,
        Vector3 spawnPosition,
        Vector3 direction,
        float force,
        TestEventData eventData,
        string spawnPattern,
        float scaleMultiplier,
        bool hasColorOverride,
        Color overrideColor,
        bool applyInitialVelocity)
    {
        if (projectileToSpawn == null)
        {
            Debug.LogWarning("No projectile prefab is assigned.");
            return;
        }

        Quaternion spawnRotation = launchOrigin.rotation;

        if (!applyInitialVelocity && rainDropRandomYaw)
        {
            spawnRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        GameObject projectile = Instantiate(
            projectileToSpawn,
            spawnPosition,
            spawnRotation
        );

        projectile.transform.localScale = config.scale * Mathf.Max(0.01f, scaleMultiplier);

        Renderer rend = projectile.GetComponent<Renderer>();
        if (rend == null)
        {
            rend = projectile.GetComponentInChildren<Renderer>();
        }

        if (rend != null)
        {
            rend.material.color = hasColorOverride ? overrideColor : config.color;
        }

        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = projectile.GetComponentInChildren<Rigidbody>();
        }

        if (rb == null)
        {
            Debug.LogWarning("Projectile has no Rigidbody.");
            return;
        }

        // Small or fast projectiles can tunnel through thin colliders unless continuous collision is enabled.
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        RegisterProjectileCollisionIgnores(projectile);

        AttachImpactReaction(projectile, rb, eventData, spawnPattern);

        if (applyInitialVelocity)
        {
            Vector3 finalDirection = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : launchOrigin.forward;

            rb.linearVelocity = finalDirection * force;
        }
        else
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }

    void RegisterProjectileCollisionIgnores(GameObject projectile)
    {
        if (projectile == null)
            return;

        Collider[] newColliders = projectile.GetComponentsInChildren<Collider>(true);
        if (newColliders == null || newColliders.Length == 0)
            return;

        activeProjectileColliders.RemoveAll(static collider => collider == null);

        for (int i = 0; i < newColliders.Length; i++)
        {
            Collider newCollider = newColliders[i];
            if (newCollider == null)
                continue;

            for (int j = 0; j < activeProjectileColliders.Count; j++)
            {
                Collider existingCollider = activeProjectileColliders[j];
                if (existingCollider == null)
                    continue;

                Physics.IgnoreCollision(newCollider, existingCollider, true);
            }

            activeProjectileColliders.Add(newCollider);
        }
    }

    void TrackVSeeFaceHeartbeat()
    {
        if (vSeeFaceReceiver == null)
            return;

        if (vSeeFaceReceiver.GetAvailable() <= 0)
            return;

        float remoteTime = vSeeFaceReceiver.GetRemoteTime();
        if (!Mathf.Approximately(remoteTime, lastObservedVSeeFaceRemoteTime))
        {
            lastObservedVSeeFaceRemoteTime = remoteTime;
            lastVSeeFacePacketTime = Time.unscaledTime;
        }
    }

    void AttachImpactReaction(GameObject projectile, Rigidbody rb, TestEventData eventData, string spawnPattern)
    {
        GameObject reactionTarget = rb != null ? rb.gameObject : projectile;
        if (reactionTarget == null)
            return;

        ProjectileImpactReaction impactReaction = reactionTarget.GetComponent<ProjectileImpactReaction>();
        if (impactReaction == null)
        {
            impactReaction = reactionTarget.AddComponent<ProjectileImpactReaction>();
        }

        if (headArmPoseController == null)
        {
            headArmPoseController = Object.FindAnyObjectByType<HeadArmPoseController>();
        }

        impactReaction.Setup(
            eventData,
            spawnPattern,
            headArmPoseController,
            vmcRecoilController,
            vmcOscSender
        );
    }

    bool ResolveProjectileEnabled(TestEventData eventData)
    {
        if (eventData == null)
            return true;

        return eventData.projectileEnabled;
    }

    bool ResolveVmcEnabled(TestEventData eventData)
    {
        if (eventData == null)
            return true;

        return eventData.vmcEnabled;
    }

    string ResolveSpawnPattern(TestEventData eventData)
    {
        if (eventData == null || string.IsNullOrWhiteSpace(eventData.spawnPattern))
            return "single";

        string value = eventData.spawnPattern.Trim().ToLowerInvariant();

        if (value == "burst" || value == "rain")
            return value;

        return "single";
    }

    float ResolvePowerMultiplier(TestEventData eventData)
    {
        if (eventData == null)
            return 1f;

        if (eventData.power < 0f)
            return 0f;

        return eventData.power;
    }

    Vector3 ResolveDirection(TestEventData eventData, string spawnPattern, Transform launchOrigin)
    {
        if (launchOrigin == null)
            return Vector3.forward;

        string direction = eventData != null ? eventData.direction : null;

        if (spawnPattern != "rain" && ShouldAimAtAvatarHead(direction))
        {
            Vector3 aimedDirection = ResolveAvatarHeadAimDirection(launchOrigin);
            if (aimedDirection.sqrMagnitude > 0.0001f)
                return aimedDirection;
        }

        if (string.IsNullOrWhiteSpace(direction))
        {
            if (spawnPattern == "rain")
                return -launchOrigin.up;

            return launchOrigin.forward;
        }

        switch (direction.Trim().ToLowerInvariant())
        {
            case "left":
                return -launchOrigin.right;
            case "right":
                return launchOrigin.right;
            case "up":
                return launchOrigin.up;
            case "down":
                return -launchOrigin.up;
            case "backward":
                return -launchOrigin.forward;
            case "forward":
            default:
                if (spawnPattern != "rain" && aimProjectilesAtAvatarHead)
                {
                    Vector3 aimedDirection = ResolveAvatarHeadAimDirection(launchOrigin);
                    if (aimedDirection.sqrMagnitude > 0.0001f)
                        return aimedDirection;
                }

                return launchOrigin.forward;
        }
    }

    bool ShouldAimAtAvatarHead(string direction)
    {
        if (!aimProjectilesAtAvatarHead)
            return false;

        return string.IsNullOrWhiteSpace(direction) ||
            direction.Trim().Equals("forward", StringComparison.OrdinalIgnoreCase);
    }

    Vector3 ResolveAvatarHeadAimDirection(Transform launchOrigin)
    {
        Transform headTransform = ResolveAvatarHeadTransform();
        if (launchOrigin == null || headTransform == null)
            return Vector3.zero;

        Vector3 direction = headTransform.position - launchOrigin.position;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
    }

    Transform ResolveAvatarHeadTransform()
    {
        if (headArmPoseController == null)
            headArmPoseController = Object.FindAnyObjectByType<HeadArmPoseController>();

        if (headArmPoseController != null && headArmPoseController.EffectiveHeadTransform != null)
            return headArmPoseController.EffectiveHeadTransform;

        GameObject modelRoot = ResolveVSeeFaceModelRoot();
        Animator animator = modelRoot != null
            ? modelRoot.GetComponentInChildren<Animator>()
            : Object.FindAnyObjectByType<Animator>();

        return animator != null && animator.isHuman
            ? animator.GetBoneTransform(HumanBodyBones.Head)
            : null;
    }

    Transform ResolveStickerHeadTransform()
    {
        if (headArmPoseController == null)
            headArmPoseController = Object.FindAnyObjectByType<HeadArmPoseController>();

        if (headArmPoseController != null && headArmPoseController.StickerHeadTransform != null)
            return headArmPoseController.StickerHeadTransform;

        return ResolveAvatarHeadTransform();
    }

    int ResolveCount(TestEventData eventData)
    {
        if (eventData == null)
            return 1;

        return Mathf.Max(1, eventData.count);
    }

    float ResolveScaleMultiplier(TestEventData eventData)
    {
        if (eventData == null)
            return 1f;

        if (eventData.scale <= 0f)
            return 1f;

        return eventData.scale;
    }

    bool TryResolveColorOverride(TestEventData eventData, out Color color)
    {
        color = Color.white;

        if (eventData == null || string.IsNullOrWhiteSpace(eventData.color))
            return false;

        string text = eventData.color.Trim();

        if (!text.StartsWith("#"))
        {
            text = "#" + text;
        }

        return ColorUtility.TryParseHtmlString(text, out color);
    }

    void LoadOrCreateConfig()
    {
        if (File.Exists(ConfigFilePath))
        {
            LoadConfig();
            return;
        }

        CreateRuntimeConfigFromDefaults();
        SaveConfig();
    }

    void CreateRuntimeConfigFromDefaults()
    {
        if (defaultConfigData != null && defaultConfigData.launchConfigs != null)
        {
            LaunchConfigFile source = new LaunchConfigFile
            {
                launchConfigs = defaultConfigData.launchConfigs,
                upperBodyMotionSettings = defaultConfigData.upperBodyMotionSettings != null
                    ? defaultConfigData.upperBodyMotionSettings.Clone()
                    : new UpperBodyMotionSettings(),
                headBoxAnchorSettings = defaultConfigData.headBoxAnchorSettings != null
                    ? defaultConfigData.headBoxAnchorSettings.Clone()
                    : HeadBoxAnchorSettings.FromTransform(headBoxAnchorTransform)
            };

            string json = JsonUtility.ToJson(source);
            runtimeConfig = JsonUtility.FromJson<LaunchConfigFile>(json);
        }
        else
        {
            runtimeConfig = new LaunchConfigFile
            {
                launchConfigs = new LaunchEventConfig[0],
                upperBodyMotionSettings = new UpperBodyMotionSettings(),
                headBoxAnchorSettings = HeadBoxAnchorSettings.FromTransform(headBoxAnchorTransform)
            };
        }

        EnsureUpperBodyMotionSettings();
        EnsureHeadBoxAnchorSettings();
        SanitizeLaunchConfigs();
        ApplyUpperBodyMotionSettingsFromConfig();
        ApplyHeadBoxAnchorSettingsFromConfig();
    }

    public void SaveConfig()
    {
        if (runtimeConfig == null)
            return;

        EnsureUpperBodyMotionSettings();
        EnsureHeadBoxAnchorSettings();
        CaptureUpperBodyMotionSettingsToConfig();
        CaptureHeadBoxAnchorSettingsToConfig();
        string json = JsonUtility.ToJson(runtimeConfig, true);
        File.WriteAllText(ConfigFilePath, json);
    }

    public void LoadConfig()
    {
        if (!File.Exists(ConfigFilePath))
        {
            CreateRuntimeConfigFromDefaults();
            return;
        }

        string json = File.ReadAllText(ConfigFilePath);
        runtimeConfig = JsonUtility.FromJson<LaunchConfigFile>(json);

        if (runtimeConfig == null || runtimeConfig.launchConfigs == null)
        {
            CreateRuntimeConfigFromDefaults();
            return;
        }

        EnsureUpperBodyMotionSettings();
        EnsureHeadBoxAnchorSettings();
        SanitizeLaunchConfigs();
        ApplyUpperBodyMotionSettingsFromConfig();
        ApplyHeadBoxAnchorSettingsFromConfig();
        nextLaunchTimes.Clear();
    }

    void SanitizeLaunchConfigs()
    {
        if (runtimeConfig == null || runtimeConfig.launchConfigs == null)
            return;

        List<LaunchEventConfig> sanitized = new List<LaunchEventConfig>();

        for (int i = 0; i < runtimeConfig.launchConfigs.Length; i++)
        {
            LaunchEventConfig config = runtimeConfig.launchConfigs[i];
            if (config == null)
                continue;

            string normalizedLabel = NormalizeLaunchLabel(config.label);
            if (string.IsNullOrWhiteSpace(normalizedLabel))
                continue;

            config.label = normalizedLabel;
            sanitized.Add(config);
        }

        runtimeConfig.launchConfigs = sanitized.ToArray();
    }

    string NormalizeLaunchLabel(string label)
    {
        string value = string.IsNullOrWhiteSpace(label) ? string.Empty : label.Trim();
        if (value.Equals("Chat", StringComparison.OrdinalIgnoreCase))
            return "chat";

        if (value.Equals("Donation", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Big Donation", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("싱글샷", StringComparison.OrdinalIgnoreCase))
            return "single";

        if (value.Equals("Spread", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("샷건", StringComparison.OrdinalIgnoreCase))
            return "shotgun";

        if (value.Equals("Rain", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("박스", StringComparison.OrdinalIgnoreCase))
            return "box";

        if (value.Equals("Machine Gun", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("MachineGun", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("\uAE30\uAD00\uCD1D", StringComparison.OrdinalIgnoreCase))
            return "machinegun";

        if (value.Equals("Sticker", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("\uC2A4\uD2F0\uCEE4 \uBD99\uC774\uAE30", StringComparison.OrdinalIgnoreCase))
            return "sticker";

        return value.ToLowerInvariant();
    }

    bool AreLaunchLabelsEquivalent(string requestedLabel, string configLabel)
    {
        return NormalizeLaunchLabel(requestedLabel) == NormalizeLaunchLabel(configLabel);
    }

    void EnsureHeadArmPoseController()
    {
        if (headArmPoseController == null)
        {
            headArmPoseController = Object.FindAnyObjectByType<HeadArmPoseController>();
        }
    }

    void EnsureVSeeFaceReceiver()
    {
        GameObject modelRoot = ResolveVSeeFaceModelRoot();
        if (modelRoot == null)
            return;

        vSeeFaceReceiver = Object.FindAnyObjectByType<ExternalReceiver>();
        if (vSeeFaceReceiver == null)
        {
            uOSC.uOscServer existingServer = FindExistingVmcServer();
            GameObject receiverObject = existingServer != null
                ? existingServer.gameObject
                : new GameObject("ExternalReceiver");
            receiverObject.SetActive(false);
            vSeeFaceReceiverServer = existingServer != null
                ? existingServer
                : receiverObject.AddComponent<uOSC.uOscServer>();
            vSeeFaceReceiver = receiverObject.AddComponent<ExternalReceiver>();
            receiverObject.SetActive(true);
        }
        else
        {
            vSeeFaceReceiverServer = vSeeFaceReceiver.GetComponent<uOSC.uOscServer>();
            if (vSeeFaceReceiverServer == null)
            {
                vSeeFaceReceiverServer = vSeeFaceReceiver.gameObject.AddComponent<uOSC.uOscServer>();
            }
        }

        DisableDuplicateVmcServers(vSeeFaceReceiverServer);
        vSeeFaceReceiverServer.port = VSeeFaceReceivePort;
        vSeeFaceReceiverServer.autoStart = true;

        vSeeFaceReceiver.Model = modelRoot;
        vSeeFaceReceiver.Freeze = false;
        vSeeFaceReceiver.EnableLateUpdateForOverwriteAnimationResult = false;
        ConfigureReceiverForFullTorsoTracking(vSeeFaceReceiver);

        if (vSeeFaceReceiver.RootPositionTransform == null)
        {
            vSeeFaceReceiver.RootPositionTransform = modelRoot.transform;
        }

        if (vSeeFaceReceiver.RootRotationTransform == null)
        {
            vSeeFaceReceiver.RootRotationTransform = modelRoot.transform;
        }
    }

    uOSC.uOscServer FindExistingVmcServer()
    {
        uOSC.uOscServer[] servers = UnityEngine.Object.FindObjectsByType<uOSC.uOscServer>(FindObjectsSortMode.None);
        for (int i = 0; i < servers.Length; i++)
        {
            if (servers[i] != null && servers[i].port == VSeeFaceReceivePort)
                return servers[i];
        }

        return null;
    }

    void DisableDuplicateVmcServers(uOSC.uOscServer primaryServer)
    {
        uOSC.uOscServer[] servers = UnityEngine.Object.FindObjectsByType<uOSC.uOscServer>(FindObjectsSortMode.None);
        for (int i = 0; i < servers.Length; i++)
        {
            uOSC.uOscServer server = servers[i];
            if (server == null || server == primaryServer || server.port != VSeeFaceReceivePort)
                continue;

            server.autoStart = false;
            server.StopServer();
            server.enabled = false;
        }
    }

    void ConfigureReceiverForFullTorsoTracking(ExternalReceiver receiver)
    {
        if (receiver == null)
            return;

        // Mirror the incoming VSeeFace pose as faithfully as possible on the
        // Unity test avatar.
        receiver.BonePositionSynchronize = true;
        receiver.BonePositionFilterEnable = false;
        receiver.BoneRotationFilterEnable = false;
        receiver.BlendShapeFilterEnable = false;
        receiver.CutBonesEnable = false;
        receiver.CutBoneHips = false;
        receiver.CutBoneSpine = false;
        receiver.CutBoneChest = false;
        receiver.CutBoneUpperChest = false;
    }

    public void SetVSeeFaceReceivePort(int port)
    {
        int nextPort = Mathf.Clamp(port, 1, 65535);
        if (vSeeFaceReceivePort == nextPort && vSeeFaceReceiverServer != null && vSeeFaceReceiverServer.port == nextPort)
            return;

        vSeeFaceReceivePort = nextPort;
        EnsureVSeeFaceReceiver();

        if (vSeeFaceReceiverServer != null)
        {
            vSeeFaceReceiverServer.autoStart = false;
            vSeeFaceReceiverServer.StopServer();
            vSeeFaceReceiverServer.port = nextPort;
            vSeeFaceReceiverServer.autoStart = true;
            vSeeFaceReceiverServer.enabled = false;
            vSeeFaceReceiverServer.enabled = true;
        }
    }

    GameObject ResolveVSeeFaceModelRoot()
    {
        EnsureHeadArmPoseController();

        if (vmcOscSender != null && vmcOscSender.sourceAnimator != null)
            return vmcOscSender.sourceAnimator.gameObject;

        if (headArmPoseController != null)
        {
            Animator controllerAnimator = headArmPoseController.GetComponent<Animator>();
            if (controllerAnimator == null)
            {
                controllerAnimator = headArmPoseController.GetComponentInChildren<Animator>();
            }

            if (controllerAnimator != null)
                return controllerAnimator.gameObject;

            return headArmPoseController.gameObject;
        }

        Animator animator = UnityEngine.Object.FindAnyObjectByType<Animator>();
        return animator != null ? animator.gameObject : null;
    }

    void EnsureUpperBodyMotionSettings()
    {
        if (runtimeConfig == null)
            return;

        if (runtimeConfig.upperBodyMotionSettings == null)
        {
            EnsureHeadArmPoseController();
            runtimeConfig.upperBodyMotionSettings = headArmPoseController != null
                ? headArmPoseController.ExportUpperBodyMotionSettings()
                : new UpperBodyMotionSettings();
        }
    }

    void EnsureHeadBoxAnchorTransform()
    {
        if (headBoxAnchorTransform != null)
            return;

        GameObject anchorObject = GameObject.Find("HeadBoxAnchor");
        if (anchorObject != null)
        {
            headBoxAnchorTransform = anchorObject.transform;
        }
    }

    void EnsureHeadBoxAnchorSettings()
    {
        if (runtimeConfig == null)
            return;

        if (runtimeConfig.headBoxAnchorSettings == null)
        {
            EnsureHeadBoxAnchorTransform();
            runtimeConfig.headBoxAnchorSettings = HeadBoxAnchorSettings.FromTransform(headBoxAnchorTransform);
        }
    }

    void ApplyUpperBodyMotionSettingsFromConfig()
    {
        EnsureHeadArmPoseController();

        if (runtimeConfig == null || runtimeConfig.upperBodyMotionSettings == null || headArmPoseController == null)
            return;

        headArmPoseController.ApplyUpperBodyMotionSettings(runtimeConfig.upperBodyMotionSettings.Clone());
    }

    void ApplyHeadBoxAnchorSettingsFromConfig()
    {
        EnsureHeadBoxAnchorTransform();

        if (runtimeConfig == null || runtimeConfig.headBoxAnchorSettings == null || headBoxAnchorTransform == null)
            return;

        runtimeConfig.headBoxAnchorSettings.Clone().ApplyTo(headBoxAnchorTransform);
    }

    void CaptureUpperBodyMotionSettingsToConfig()
    {
        EnsureHeadArmPoseController();

        if (runtimeConfig == null || headArmPoseController == null)
            return;

        runtimeConfig.upperBodyMotionSettings = headArmPoseController.ExportUpperBodyMotionSettings();
    }

    void CaptureHeadBoxAnchorSettingsToConfig()
    {
        EnsureHeadBoxAnchorTransform();

        if (runtimeConfig == null || headBoxAnchorTransform == null)
            return;

        runtimeConfig.headBoxAnchorSettings = HeadBoxAnchorSettings.FromTransform(headBoxAnchorTransform);
    }

    public UpperBodyMotionSettings GetUpperBodyMotionSettings()
    {
        EnsureHeadArmPoseController();

        if (headArmPoseController != null)
            return headArmPoseController.ExportUpperBodyMotionSettings();

        if (runtimeConfig != null && runtimeConfig.upperBodyMotionSettings != null)
            return runtimeConfig.upperBodyMotionSettings.Clone();

        return new UpperBodyMotionSettings();
    }

    public void SetUpperBodyMotionSettings(UpperBodyMotionSettings settings)
    {
        if (settings == null)
            return;

        settings.Clamp();
        EnsureHeadArmPoseController();

        if (headArmPoseController != null)
        {
            headArmPoseController.ApplyUpperBodyMotionSettings(settings.Clone());
        }

        if (runtimeConfig != null)
        {
            runtimeConfig.upperBodyMotionSettings = settings.Clone();
        }
    }

    public HeadBoxAnchorSettings GetHeadBoxAnchorSettings()
    {
        EnsureHeadBoxAnchorTransform();

        if (headBoxAnchorTransform != null)
            return HeadBoxAnchorSettings.FromTransform(headBoxAnchorTransform);

        if (runtimeConfig != null && runtimeConfig.headBoxAnchorSettings != null)
            return runtimeConfig.headBoxAnchorSettings.Clone();

        return new HeadBoxAnchorSettings();
    }

    public void SetHeadBoxAnchorSettings(HeadBoxAnchorSettings settings)
    {
        if (settings == null)
            return;

        settings.Clamp();
        EnsureHeadBoxAnchorTransform();

        if (headBoxAnchorTransform != null)
        {
            settings.Clone().ApplyTo(headBoxAnchorTransform);
        }

        if (runtimeConfig != null)
        {
            runtimeConfig.headBoxAnchorSettings = settings.Clone();
        }
    }

    public int GetLaunchConfigCount()
    {
        return runtimeConfig != null && runtimeConfig.launchConfigs != null
            ? runtimeConfig.launchConfigs.Length
            : 0;
    }

    public LaunchEventConfig GetLaunchConfig(int index)
    {
        if (runtimeConfig == null || runtimeConfig.launchConfigs == null)
            return null;

        if (index < 0 || index >= runtimeConfig.launchConfigs.Length)
            return null;

        LaunchEventConfig config = runtimeConfig.launchConfigs[index];
        return config != null ? config.Clone() : null;
    }

    public void SetLaunchConfig(int index, LaunchEventConfig config)
    {
        if (config == null || runtimeConfig == null || runtimeConfig.launchConfigs == null)
            return;

        if (index < 0 || index >= runtimeConfig.launchConfigs.Length)
            return;

        config.Clamp();
        runtimeConfig.launchConfigs[index] = config.Clone();
    }
}
