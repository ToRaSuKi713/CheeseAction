using UnityEngine;
using Object = UnityEngine.Object;
using Klak.Spout;

public class LauncherDebugUI : MonoBehaviour
{
    [System.Serializable]
    private class CommandRuleRow
    {
        public string chatText = string.Empty;
        public string donationText = "0";
        public int eventIndex;
        public int soundIndex;
    }

    [System.Serializable]
    private class CommandRuleCollection
    {
        public CommandRuleRow[] rows;
    }

    private enum StreamerUiMode
    {
        Setup,
        Preview,
        Broadcast
    }

    private enum BroadcastPreset
    {
        Weak,
        Strong
    }

    private enum SettingsTab
    {
        Ports,
        Chzzk,
        Sound,
        Commands,
        Sticker,
        Avatar
    }

    public SimpleLauncher launcher;
    public UdpEventReceiver udpReceiver;
    public ChatOrDonationRouter chatRouter;
    public ChzzkLoginManager loginManager;

    [Header("Shortcuts")]
    [SerializeField] private KeyCode backgroundToggleKey = KeyCode.F10;
    [SerializeField] private KeyCode debugUiToggleKey = KeyCode.F9;
    [SerializeField] private KeyCode profileSpriteToggleKey = KeyCode.F11;
    [SerializeField] private KeyCode weakPresetKey = KeyCode.F1;
    [SerializeField] private KeyCode strongPresetKey = KeyCode.F2;

    [Header("Presentation")]
    [SerializeField] private Color opaqueBackgroundColor = new Color(0.10f, 0.12f, 0.16f, 1f);
    [SerializeField] private Color transparentBackgroundColor = new Color(0f, 0f, 0f, 0f);
    [SerializeField] private bool useProfileSprite = true;
    [SerializeField] private GameObject profileSpriteObject;
    [SerializeField] private bool followProfileSpriteHead = true;
    [SerializeField] private bool enableSpoutOutput = true;
    [SerializeField] private string spoutSenderName = "RangE_VMCBridge";
    [SerializeField] private float cameraDepthOffsetMin = -2.0f;
    [SerializeField] private float cameraDepthOffsetMax = 0.98f;

    private readonly Color panelColor = new Color(0.07f, 0.09f, 0.12f, 0.92f);
    private readonly Color softPanelColor = new Color(0.10f, 0.12f, 0.16f, 0.86f);
    private readonly Color accentColor = new Color(0.15f, 0.78f, 0.68f, 1f);
    private readonly Color successColor = new Color(0.30f, 0.86f, 0.52f, 1f);
    private readonly Color errorColor = new Color(0.93f, 0.32f, 0.36f, 1f);
    private readonly Color neutralColor = new Color(0.76f, 0.80f, 0.88f, 1f);

    private Texture2D solidTexture;
    private Texture2D settingsButtonTexture;
    private Texture2D portStatusCheckTexture;
    private Texture2D portStatusXTexture;
    private GUIStyle titleStyle;
    private GUIStyle sectionStyle;
    private GUIStyle bodyStyle;
    private GUIStyle mutedStyle;
    private GUIStyle badgeStyle;
    private GUIStyle primaryButtonStyle;
    private GUIStyle secondaryButtonStyle;
    private GUIStyle subtleButtonStyle;
    private GUIStyle centeredTitleStyle;

    private Camera targetCamera;
    private SpoutSender spoutSender;
    private SpoutResources spoutResources;
    private StreamerUiMode currentMode = StreamerUiMode.Setup;
    private BroadcastPreset currentPreset = BroadcastPreset.Weak;
    private bool transparentBackground;
    private bool controlPanelVisible = true;
    private bool profileSpriteVisible = true;
    private bool guideVisible = true;
    private bool settingsWindowVisible;
    private bool logoutConfirmVisible;
    private bool presetDialogVisible;
    private bool pendingPresetOverwrite;
    private int pendingPresetSlot;
    private SettingsTab currentSettingsTab = SettingsTab.Ports;
    private float previewStrengthValue = 0.62f;
    private float projectileScaleMultiplier = 1.00f;
    private float lastSavedPreviewStrength = float.MinValue;
    private float lastSavedCameraDepthOffset = float.MinValue;
    private float lastSavedProjectileScaleMultiplier = -1f;
    private string vmcReceivePortText = "39541";
    private string vmcSendPortText = "39540";
    private string eventReceivePortText = "9000";
    private string chzzkChannelText = string.Empty;
    private string chzzkTokenText = string.Empty;
    private string chzzkBackendUrlText = string.Empty;
    private float masterVolume = 1f;
    private float effectVolume = 1f;
    private float stickerScale = 0.55f;
    private float stickerOffsetX = 0f;
    private float stickerOffsetY = 0.02f;
    private const int GuideOffsetStorageVersion = 2;
    private const float GuideOffsetLimit = 1.0f;
    private const float GuideOffsetWorldScale = 0.25f;
    private float avatarOffsetX;
    private float avatarOffsetY;
    private Vector2 commandRulesScroll;
    private readonly System.Collections.Generic.List<CommandRuleRow> commandRuleRows = new System.Collections.Generic.List<CommandRuleRow>();
    private bool commandRowsInitialized;
    private float cameraDepthOffset;
    private Vector3[] projectileScaleBaselines;
    private Transform profileSpriteHeadTarget;
    private Vector3 profileSpriteHeadOffset;
    private bool profileSpriteOffsetInitialized;
    private Vector3 initialCameraPosition;
    private Vector3 initialCameraForward;
    private bool cameraPositionInitialized;
    private Transform avatarRootTransform;
    private Vector3 avatarBasePosition;
    private bool avatarBasePositionInitialized;

    void Awake()
    {
        ConfigureObsFriendlyWindow();
        EnsureCamera();
        EnsureChatRouter();
        EnsureLoginManager();
        CacheCameraBaselineIfNeeded();
        EnsureSpoutSender();
        LoadUiState();
        float savedPreviewStrength = previewStrengthValue;
        ApplyPreset(currentPreset);
        previewStrengthValue = savedPreviewStrength;
        LoadSettingsData();
        SyncPortTexts();
        ApplyBackgroundState();
        ApplyPreviewStrength();
        ApplyCameraDepthOffset();
        ApplyProjectileScaleMultiplier();
        ApplyCommandSettings();
        ApplySoundSettings();
        ApplyStickerSettings();
        ApplyAvatarPositionOffset();
    }

    void OnEnable()
    {
        EnsureCamera();
        EnsureChatRouter();
        EnsureLoginManager();
        CacheCameraBaselineIfNeeded();
        EnsureSpoutSender();
        SyncPortTexts();
        ApplyBackgroundState();
        ApplyStickerSettings();
        ApplyAvatarPositionOffset();
    }

    void Update()
    {
        UpdateProfileSpriteTracking();

        if (InputKeyHelper.GetKeyDown(backgroundToggleKey))
            ToggleBackgroundTransparency();

        if (InputKeyHelper.GetKeyDown(debugUiToggleKey))
        {
            controlPanelVisible = !controlPanelVisible;
            SaveUiState();
        }

        if (InputKeyHelper.GetKeyDown(profileSpriteToggleKey))
        {
            profileSpriteVisible = !profileSpriteVisible;
            SetProfileSpriteVisible(profileSpriteVisible);
            SaveUiState();
        }

        if (InputKeyHelper.GetKeyDown(weakPresetKey))
        {
            ApplyPreset(BroadcastPreset.Weak);
            SaveUiState();
        }

        if (InputKeyHelper.GetKeyDown(strongPresetKey))
        {
            ApplyPreset(BroadcastPreset.Strong);
            SaveUiState();
        }
    }

    void LateUpdate()
    {
        ApplyAvatarPositionOffset();
    }

    void OnApplicationQuit()
    {
        SaveUiState();
        SaveSettingsData();
    }

    void OnGUI()
    {
        EnsureStyles();
        DrawTopStatusBar();
        DrawSetupGuideOverlay();

        if (!controlPanelVisible)
            return;

        switch (currentMode)
        {
            case StreamerUiMode.Setup:
                DrawSetupMode();
                break;
            case StreamerUiMode.Preview:
                DrawPreviewMode();
                break;
            case StreamerUiMode.Broadcast:
                DrawBroadcastMode();
                break;
        }

        DrawSettingsWindow();
        DrawPresetDialog();
        DrawFlowBar();
    }

    void ConfigureObsFriendlyWindow()
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        Screen.fullScreenMode = FullScreenMode.Windowed;
        Resolution resolution = Screen.currentResolution;
        Screen.SetResolution(Mathf.Min(1920, resolution.width), Mathf.Min(1080, resolution.height), FullScreenMode.Windowed);
#endif
    }

    void EnsureCamera()
    {
        if (targetCamera != null)
            return;

        targetCamera = Camera.main;
        if (targetCamera == null)
            targetCamera = Object.FindAnyObjectByType<Camera>();
    }

    void EnsureChatRouter()
    {
        if (chatRouter == null)
            chatRouter = Object.FindAnyObjectByType<ChatOrDonationRouter>();
    }

    void EnsureLoginManager()
    {
        if (loginManager == null)
            loginManager = Object.FindAnyObjectByType<ChzzkLoginManager>();

        if (loginManager == null)
        {
            GameObject loginManagerObject = new GameObject("ChzzkLoginManager");
            loginManager = loginManagerObject.AddComponent<ChzzkLoginManager>();
        }

        if (string.IsNullOrWhiteSpace(chzzkBackendUrlText))
            chzzkBackendUrlText = loginManager.BackendBaseUrl;
    }

    HeadStickerPlayer EnsureHeadStickerPlayer()
    {
        if (launcher == null)
            launcher = Object.FindAnyObjectByType<SimpleLauncher>();

        HeadStickerPlayer player = launcher != null ? launcher.headStickerPlayer : null;
        if (player == null)
            player = Object.FindAnyObjectByType<HeadStickerPlayer>();
        if (player == null && launcher != null)
            player = launcher.gameObject.AddComponent<HeadStickerPlayer>();
        if (launcher != null && launcher.headStickerPlayer == null)
            launcher.headStickerPlayer = player;

        return player;
    }

    Transform EnsureAvatarRootTransform()
    {
        if (avatarRootTransform != null)
            return avatarRootTransform;

        if (launcher == null)
            launcher = Object.FindAnyObjectByType<SimpleLauncher>();

        Animator animator = null;
        if (launcher != null && launcher.vmcOscSender != null)
            animator = launcher.vmcOscSender.sourceAnimator;
        if (animator == null && launcher != null && launcher.headArmPoseController != null)
            animator = launcher.headArmPoseController.GetComponentInChildren<Animator>();
        if (animator == null)
            animator = Object.FindAnyObjectByType<Animator>();

        avatarRootTransform = animator != null ? animator.transform : null;
        if (avatarRootTransform != null && !avatarBasePositionInitialized)
        {
            avatarBasePosition = avatarRootTransform.position;
            avatarBasePositionInitialized = true;
        }

        return avatarRootTransform;
    }

    void EnsureSpoutSender()
    {
        if (!enableSpoutOutput)
            return;

        EnsureCamera();
        if (targetCamera == null)
            return;

        if (spoutResources == null)
        {
            Shader blitShader = Shader.Find("Hidden/Klak/Spout/Blit");
            if (blitShader == null)
            {
                Debug.LogWarning("Spout blit shader not found. Spout output disabled.");
                if (spoutSender != null)
                    spoutSender.enabled = false;
                return;
            }

            spoutResources = ScriptableObject.CreateInstance<SpoutResources>();
            spoutResources.hideFlags = HideFlags.HideAndDontSave;
            spoutResources.blitShader = blitShader;
        }

        spoutSender = targetCamera.GetComponent<SpoutSender>();
        if (spoutSender == null)
            spoutSender = targetCamera.gameObject.AddComponent<SpoutSender>();

        spoutSender.SetResources(spoutResources);
        spoutSender.captureMethod = CaptureMethod.Camera;
        spoutSender.sourceCamera = targetCamera;
        spoutSender.keepAlpha = true;
        spoutSender.spoutName = spoutSenderName;
        spoutSender.enabled = true;
    }

    void ApplyBackgroundState()
    {
        EnsureCamera();
        CacheCameraBaselineIfNeeded();

        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null)
                continue;

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = transparentBackground ? transparentBackgroundColor : opaqueBackgroundColor;
            camera.allowHDR = false;
        }

        SetProfileSpriteVisible(profileSpriteVisible);
        ApplyCameraDepthOffset();
    }

    void ToggleBackgroundTransparency()
    {
        transparentBackground = !transparentBackground;
        ApplyBackgroundState();
        SaveUiState();
    }

    void CacheCameraBaselineIfNeeded()
    {
        EnsureCamera();
        if (targetCamera == null || cameraPositionInitialized)
            return;

        initialCameraPosition = targetCamera.transform.position;
        initialCameraForward = targetCamera.transform.forward.normalized;
        cameraPositionInitialized = true;
    }

    void ApplyCameraDepthOffset()
    {
        EnsureCamera();
        CacheCameraBaselineIfNeeded();
        if (targetCamera == null || !cameraPositionInitialized)
            return;

        targetCamera.transform.position = initialCameraPosition + (initialCameraForward * cameraDepthOffset);
        SaveUiState();
    }

    void UpdateProfileSpriteTracking()
    {
        if (!useProfileSprite || profileSpriteObject == null || !followProfileSpriteHead)
            return;

        if (profileSpriteHeadTarget == null)
        {
            profileSpriteHeadTarget = FindHeadTarget();
            profileSpriteOffsetInitialized = false;
        }

        if (profileSpriteHeadTarget == null)
            return;

        if (!profileSpriteOffsetInitialized)
        {
            profileSpriteHeadOffset = profileSpriteObject.transform.position - profileSpriteHeadTarget.position;
            profileSpriteOffsetInitialized = true;
        }

        profileSpriteObject.transform.position = profileSpriteHeadTarget.position + profileSpriteHeadOffset;
    }

    Transform FindHeadTarget()
    {
        Animator animator = null;

        if (launcher != null && launcher.vmcOscSender != null)
            animator = launcher.vmcOscSender.sourceAnimator;

        if (animator == null)
            animator = Object.FindAnyObjectByType<Animator>();

        if (animator == null || !animator.isHuman)
            return null;

        return animator.GetBoneTransform(HumanBodyBones.Head);
    }

    void SetProfileSpriteVisible(bool visible)
    {
        if (!useProfileSprite || profileSpriteObject == null)
            return;

        profileSpriteObject.SetActive(visible);
    }

    void EnsureStyles()
    {
        if (solidTexture == null)
        {
            solidTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            solidTexture.SetPixel(0, 0, Color.white);
            solidTexture.Apply();
            solidTexture.hideFlags = HideFlags.HideAndDontSave;
        }

        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 20;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.clipping = TextClipping.Overflow;
            titleStyle.normal.textColor = Color.white;
        }

        if (sectionStyle == null)
        {
            sectionStyle = new GUIStyle(GUI.skin.label);
            sectionStyle.fontSize = 16;
            sectionStyle.fontStyle = FontStyle.Bold;
            sectionStyle.clipping = TextClipping.Overflow;
            sectionStyle.normal.textColor = Color.white;
        }

        if (bodyStyle == null)
        {
            bodyStyle = new GUIStyle(GUI.skin.label);
            bodyStyle.fontSize = 13;
            bodyStyle.wordWrap = true;
            bodyStyle.clipping = TextClipping.Overflow;
            bodyStyle.normal.textColor = Color.white;
        }

        if (mutedStyle == null)
        {
            mutedStyle = new GUIStyle(bodyStyle);
            mutedStyle.normal.textColor = neutralColor;
        }

        if (badgeStyle == null)
        {
            badgeStyle = new GUIStyle(GUI.skin.label);
            badgeStyle.fontSize = 12;
            badgeStyle.fontStyle = FontStyle.Bold;
            badgeStyle.alignment = TextAnchor.MiddleCenter;
        }

        if (primaryButtonStyle == null)
        {
            primaryButtonStyle = new GUIStyle(GUI.skin.button);
            primaryButtonStyle.fontSize = 14;
            primaryButtonStyle.fontStyle = FontStyle.Bold;
        }

        if (secondaryButtonStyle == null)
        {
            secondaryButtonStyle = new GUIStyle(GUI.skin.button);
            secondaryButtonStyle.fontSize = 13;
        }

        if (subtleButtonStyle == null)
        {
            subtleButtonStyle = new GUIStyle(GUI.skin.button);
            subtleButtonStyle.fontSize = 12;
        }

        if (centeredTitleStyle == null)
        {
            centeredTitleStyle = new GUIStyle(titleStyle);
            centeredTitleStyle.alignment = TextAnchor.MiddleCenter;
        }
    }

    void DrawTopStatusBar()
    {
        DrawBadge(new Rect(20f, 18f, 148f, 30f), GetConnectionLabel(), GetConnectionColor());

        Rect settingsRect = new Rect(Screen.width - 58f, 18f, 38f, 30f);
        if (GUI.Button(settingsRect, GUIContent.none, subtleButtonStyle))
        {
            settingsWindowVisible = !settingsWindowVisible;
            if (settingsWindowVisible)
                SyncPortTexts();
            SaveUiState();
        }

        if (settingsButtonTexture == null)
            settingsButtonTexture = Resources.Load<Texture2D>("UI/SettingsButton");
        if (settingsButtonTexture != null)
            GUI.DrawTexture(new Rect(settingsRect.x + 5f, settingsRect.y + 2f, 28f, 26f), settingsButtonTexture, ScaleMode.ScaleToFit, true);
    }

    void DrawSetupGuideOverlay()
    {
        if (currentMode != StreamerUiMode.Setup || !guideVisible)
            return;

        Rect hintRect = new Rect((Screen.width * 0.5f) - 260f, 72f, 520f, 62f);
        DrawPanel(hintRect, softPanelColor);
        GUI.Label(new Rect(hintRect.x + 18f, hintRect.y + 18f, hintRect.width - 36f, 28f), "아바타의 머리를 가이드에 대충 맞추시오", centeredTitleStyle);
    }

    void DrawSetupMode()
    {
        Rect panel = new Rect(40f, Screen.height - 220f, 520f, 150f);
        DrawPanel(panel, panelColor);

        GUI.Label(new Rect(panel.x + 18f, panel.y + 16f, 320f, 30f), "위치 맞추기", titleStyle);

        if (GUI.Button(new Rect(panel.x + 18f, panel.y + 82f, 148f, 38f), guideVisible ? "가이드 숨기기" : "가이드 다시 보기", primaryButtonStyle))
        {
            guideVisible = !guideVisible;
            profileSpriteVisible = guideVisible;
            SetProfileSpriteVisible(profileSpriteVisible);
            SaveUiState();
        }

        if (GUI.Button(new Rect(panel.x + 178f, panel.y + 82f, 148f, 38f), "기본 설정", primaryButtonStyle))
        {
            currentMode = StreamerUiMode.Preview;
            SaveUiState();
        }

        if (GUI.Button(new Rect(panel.x + 338f, panel.y + 82f, 148f, 38f), "배경 투명화", primaryButtonStyle))
            ToggleBackgroundTransparency();
    }

    void DrawPreviewMode()
    {
        Rect header = new Rect(40f, 84f, 720f, 120f);
        Rect left = new Rect(40f, 220f, 340f, 500f);
        Rect right = new Rect(400f, 220f, 360f, 180f);

        DrawPanel(header, panelColor);
        DrawPanel(left, panelColor);
        DrawPanel(right, panelColor);

        GUI.Label(new Rect(header.x + 18f, header.y + 14f, 260f, 30f), "미리보기", titleStyle);
        GUI.Label(new Rect(header.x + 18f, header.y + 54f, 220f, 22f), "프리셋 불러오기", bodyStyle);

        if (GUI.Button(new Rect(header.x + 18f, header.y + 82f, 100f, 26f), "프리셋 1", subtleButtonStyle))
            LoadQuickPreset(1);
        if (GUI.Button(new Rect(header.x + 128f, header.y + 82f, 100f, 26f), "프리셋 2", subtleButtonStyle))
            LoadQuickPreset(2);
        if (GUI.Button(new Rect(header.x + 238f, header.y + 82f, 100f, 26f), "프리셋 3", subtleButtonStyle))
            LoadQuickPreset(3);

        GUI.Label(new Rect(left.x + 18f, left.y + 16f, 220f, 28f), "현재 상태", sectionStyle);
        DrawInfoLine(left.x + 18f, left.y + 56f, "VSeeFace", GetConnectionLabel());
        DrawInfoLine(left.x + 18f, left.y + 82f, "이벤트 수신", GetEventLabel());
        DrawInfoLine(left.x + 18f, left.y + 108f, "가이드", guideVisible ? "표시중" : "숨김");
        if (GUI.Button(new Rect(left.x + 250f, left.y + 106f, 72f, 24f), guideVisible ? "숨기기" : "보이기", subtleButtonStyle))
        {
            guideVisible = !guideVisible;
            profileSpriteVisible = guideVisible;
            SetProfileSpriteVisible(profileSpriteVisible);
            SaveUiState();
        }

        GUI.Label(new Rect(left.x + 18f, left.y + 156f, 280f, 22f), "반응 강도", bodyStyle);
        previewStrengthValue = GUI.HorizontalSlider(new Rect(left.x + 18f, left.y + 184f, 260f, 18f), previewStrengthValue, 0.30f, 4.50f);
        GUI.Label(new Rect(left.x + 288f, left.y + 176f, 40f, 24f), previewStrengthValue.ToString("F2"), bodyStyle);
        ApplyPreviewStrength();

        GUI.Label(new Rect(left.x + 18f, left.y + 210f, 280f, 22f), "카메라 앞뒤", bodyStyle);
        cameraDepthOffset = GUI.HorizontalSlider(new Rect(left.x + 18f, left.y + 238f, 260f, 18f), cameraDepthOffset, cameraDepthOffsetMin, cameraDepthOffsetMax);
        GUI.Label(new Rect(left.x + 288f, left.y + 230f, 40f, 24f), cameraDepthOffset.ToString("F2"), bodyStyle);
        ApplyCameraDepthOffset();

        GUI.Label(new Rect(left.x + 18f, left.y + 264f, 280f, 22f), "발사체 크기", bodyStyle);
        projectileScaleMultiplier = GUI.HorizontalSlider(new Rect(left.x + 18f, left.y + 292f, 260f, 18f), projectileScaleMultiplier, 0.10f, 3.00f);
        GUI.Label(new Rect(left.x + 288f, left.y + 284f, 40f, 24f), projectileScaleMultiplier.ToString("F2"), bodyStyle);
        ApplyProjectileScaleMultiplier();

        GUI.Label(new Rect(left.x + 18f, left.y + 332f, 220f, 22f), "프리셋 저장", bodyStyle);
        if (GUI.Button(new Rect(left.x + 18f, left.y + 362f, 92f, 30f), "1", subtleButtonStyle))
            OpenPresetSaveDialog(1);
        if (GUI.Button(new Rect(left.x + 124f, left.y + 362f, 92f, 30f), "2", subtleButtonStyle))
            OpenPresetSaveDialog(2);
        if (GUI.Button(new Rect(left.x + 230f, left.y + 362f, 92f, 30f), "3", subtleButtonStyle))
            OpenPresetSaveDialog(3);

        GUI.Label(new Rect(right.x + 18f, right.y + 16f, 260f, 28f), "빠른 작업", sectionStyle);
        GUI.Label(new Rect(right.x + 18f, right.y + 46f, 300f, 22f), "F5/F6/F7/F8 을 눌러 발사 테스트", mutedStyle);

        if (GUI.Button(new Rect(right.x + 18f, right.y + 86f, 324f, 44f), "배경 투명화", primaryButtonStyle))
            ToggleBackgroundTransparency();

        if (GUI.Button(new Rect(right.x + 18f, right.y + 142f, 324f, 24f), "위치 맞추기로 돌아가기", secondaryButtonStyle))
        {
            currentMode = StreamerUiMode.Setup;
            SaveUiState();
        }
    }

    void DrawSettingsWindow()
    {
        if (!settingsWindowVisible)
            return;

        Rect panel = new Rect((Screen.width - 1040f) * 0.5f, (Screen.height - 520f) * 0.5f, 1040f, 520f);
        DrawPanel(panel, panelColor);

        GUI.Label(new Rect(panel.x + 20f, panel.y + 16f, 200f, 28f), "설정창", sectionStyle);
        if (GUI.Button(new Rect(panel.x + panel.width - 54f, panel.y + 14f, 34f, 28f), "X", subtleButtonStyle))
        {
            settingsWindowVisible = false;
            SaveUiState();
            return;
        }

        if (GUI.Button(new Rect(panel.x + 20f, panel.y + 56f, 130f, 30f), "포트 수정", currentSettingsTab == SettingsTab.Ports ? primaryButtonStyle : subtleButtonStyle))
        {
            currentSettingsTab = SettingsTab.Ports;
            SaveUiState();
        }
        if (GUI.Button(new Rect(panel.x + 160f, panel.y + 56f, 140f, 30f), "치지직 로그인", currentSettingsTab == SettingsTab.Chzzk ? primaryButtonStyle : subtleButtonStyle))
        {
            currentSettingsTab = SettingsTab.Chzzk;
            SaveUiState();
        }
        if (GUI.Button(new Rect(panel.x + 310f, panel.y + 56f, 110f, 30f), "소리 설정", currentSettingsTab == SettingsTab.Sound ? primaryButtonStyle : subtleButtonStyle))
        {
            currentSettingsTab = SettingsTab.Sound;
            SaveUiState();
        }
        if (GUI.Button(new Rect(panel.x + 430f, panel.y + 56f, 180f, 30f), "명령어 커스텀", currentSettingsTab == SettingsTab.Commands ? primaryButtonStyle : subtleButtonStyle))
        {
            currentSettingsTab = SettingsTab.Commands;
            SaveUiState();
        }
        if (GUI.Button(new Rect(panel.x + 620f, panel.y + 56f, 190f, 30f), "\uC2A4\uD2F0\uCEE4 \uD06C\uAE30, \uC704\uCE58", currentSettingsTab == SettingsTab.Sticker ? primaryButtonStyle : subtleButtonStyle))
        {
            currentSettingsTab = SettingsTab.Sticker;
            SaveUiState();
        }
        if (GUI.Button(new Rect(panel.x + 820f, panel.y + 56f, 190f, 30f), "\uAC00\uC774\uB4DC \uC704\uCE58 \uC870\uC808", currentSettingsTab == SettingsTab.Avatar ? primaryButtonStyle : subtleButtonStyle))
        {
            currentSettingsTab = SettingsTab.Avatar;
            SaveUiState();
        }

        switch (currentSettingsTab)
        {
            case SettingsTab.Ports:
                DrawPortsTab(panel);
                break;
            case SettingsTab.Chzzk:
                DrawChzzkLoginTab(panel);
                break;
            case SettingsTab.Sound:
                DrawSoundTab(panel);
                break;
            case SettingsTab.Commands:
                DrawCommandsTab(panel);
                break;
            case SettingsTab.Sticker:
                DrawStickerTab(panel);
                break;
            case SettingsTab.Avatar:
                DrawAvatarPositionTab(panel);
                break;
        }
    }

    void DrawPortsTab(Rect panel)
    {
        float x = panel.x + 24f;
        float y = panel.y + 108f;

        GUI.Label(new Rect(x, y, 220f, 22f), "VSeeFace -> Unity", bodyStyle);
        vmcReceivePortText = GUI.TextField(new Rect(x, y + 28f, 260f, 28f), vmcReceivePortText);
        GUI.Label(new Rect(x, y + 60f, 220f, 20f), "기본 포트: 39541", mutedStyle);
        DrawPortStatusIcon(new Rect(x + 228f, y + 56f, 26f, 26f), IsReceivePortReady());

        GUI.Label(new Rect(x, y + 98f, 220f, 22f), "Unity -> VSeeFace", bodyStyle);
        vmcSendPortText = GUI.TextField(new Rect(x, y + 126f, 260f, 28f), vmcSendPortText);
        GUI.Label(new Rect(x, y + 158f, 220f, 20f), "기본 포트: 39540", mutedStyle);
        DrawPortStatusIcon(new Rect(x + 228f, y + 154f, 26f, 26f), IsSendPortReady());

        GUI.Label(new Rect(x, y + 196f, 220f, 22f), "이벤트 수신", bodyStyle);
        eventReceivePortText = GUI.TextField(new Rect(x, y + 224f, 260f, 28f), eventReceivePortText);
        GUI.Label(new Rect(x, y + 256f, 220f, 20f), "기본 포트: 9000", mutedStyle);
        DrawPortStatusIcon(new Rect(x + 228f, y + 252f, 26f, 26f), IsEventPortReady());

        if (GUI.Button(new Rect(x, y + 300f, 260f, 34f), "포트 적용", primaryButtonStyle))
            ApplyPortSettings();
    }

    void DrawChzzkTab(Rect panel)
    {
        float x = panel.x + 24f;
        float y = panel.y + 108f;

        GUI.Label(new Rect(x, y, 280f, 22f), "치지직 채널 ID", bodyStyle);
        string nextChannel = GUI.TextField(new Rect(x, y + 28f, 360f, 28f), chzzkChannelText);
        if (nextChannel != chzzkChannelText)
        {
            chzzkChannelText = nextChannel;
            SaveSettingsData();
        }

        GUI.Label(new Rect(x, y + 78f, 280f, 22f), "치지직 토큰", bodyStyle);
        string nextToken = GUI.TextField(new Rect(x, y + 106f, 360f, 28f), chzzkTokenText);
        if (nextToken != chzzkTokenText)
        {
            chzzkTokenText = nextToken;
            SaveSettingsData();
        }

        GUI.Label(new Rect(x, y + 156f, 520f, 22f), "로그인 기능은 다음 단계에서 실제 연동을 붙일 수 있게 준비중입니다.", mutedStyle);
    }

    void DrawChzzkLoginTab(Rect panel)
    {
        float x = panel.x + 24f;
        float y = panel.y + 108f;

        EnsureLoginManager();

        GUI.Label(new Rect(x, y, 560f, 22f), "상태: " + (loginManager != null ? loginManager.StatusText : "로그인 매니저 없음"), bodyStyle);

        if (loginManager != null && !string.IsNullOrWhiteSpace(loginManager.ChannelId))
            GUI.Label(new Rect(x, y + 28f, 560f, 20f), "채널 ID: " + loginManager.ChannelId, mutedStyle);

        bool oldEnabled = GUI.enabled;
        GUI.enabled = loginManager != null && !loginManager.IsBusy;
        if (GUI.Button(new Rect(x, y + 72f, 180f, 36f), "치지직 로그인", primaryButtonStyle))
        {
            logoutConfirmVisible = false;
            loginManager.BeginLogin();
        }
        if (GUI.Button(new Rect(x + 196f, y + 72f, 140f, 36f), "로그아웃", secondaryButtonStyle))
            logoutConfirmVisible = true;
        GUI.enabled = oldEnabled;

        if (logoutConfirmVisible)
        {
            DrawPanel(new Rect(x, y + 126f, 360f, 102f), softPanelColor);
            GUI.Label(new Rect(x + 16f, y + 142f, 320f, 24f), "정말 하시겠습니까?", bodyStyle);

            bool previousEnabled = GUI.enabled;
            GUI.enabled = loginManager != null && !loginManager.IsBusy;
            if (GUI.Button(new Rect(x + 16f, y + 178f, 150f, 32f), "예", primaryButtonStyle))
            {
                logoutConfirmVisible = false;
                loginManager.BeginLogout();
            }
            if (GUI.Button(new Rect(x + 182f, y + 178f, 150f, 32f), "아니오", secondaryButtonStyle))
                logoutConfirmVisible = false;
            GUI.enabled = previousEnabled;
        }
    }

    void DrawSoundTab(Rect panel)
    {
        float x = panel.x + 24f;
        float y = panel.y + 108f;

        GUI.Label(new Rect(x, y, 220f, 22f), "마스터 볼륨", bodyStyle);
        float nextMaster = GUI.HorizontalSlider(new Rect(x, y + 30f, 260f, 18f), masterVolume, 0f, 1f);
        GUI.Label(new Rect(x + 276f, y + 22f, 48f, 24f), nextMaster.ToString("F2"), bodyStyle);
        if (!Mathf.Approximately(nextMaster, masterVolume))
        {
            masterVolume = nextMaster;
            ApplySoundSettings();
            SaveSettingsData();
        }
        GUI.Label(new Rect(x, y + 76f, 520f, 22f), "마스터 볼륨만 사용합니다.", mutedStyle);
    }

    void DrawStickerTab(Rect panel)
    {
        float x = panel.x + 24f;
        float y = panel.y + 108f;

        GUI.Label(new Rect(x, y, 420f, 24f), "\uC2A4\uD2F0\uCEE4 \uD06C\uAE30, \uC704\uCE58 \uC870\uC808", sectionStyle);
        GUI.Label(new Rect(x, y + 30f, 620f, 22f), "\uC2A4\uD2F0\uCEE4 \uBD99\uC774\uAE30\uAC00 \uBA38\uB9AC\uC5D0 \uBD99\uC744 \uB54C\uC758 \uD06C\uAE30\uC640 \uC138\uBD80 \uC704\uCE58\uB97C \uC870\uC808\uD569\uB2C8\uB2E4.", mutedStyle);

        float nextScale = DrawStickerSlider(x, y + 72f, "\uD06C\uAE30", stickerScale, 0.10f, 2.50f);
        float nextX = DrawStickerSlider(x, y + 126f, "\uC624\uB978\uCABD / \uC67C\uCABD", stickerOffsetX, -0.60f, 0.60f);
        float nextY = DrawStickerSlider(x, y + 180f, "\uC544\uB798 / \uC704", stickerOffsetY, -0.60f, 0.60f);

        if (!Mathf.Approximately(nextScale, stickerScale) || !Mathf.Approximately(nextX, stickerOffsetX) || !Mathf.Approximately(nextY, stickerOffsetY))
        {
            stickerScale = nextScale;
            stickerOffsetX = nextX;
            stickerOffsetY = nextY;
            ApplyStickerSettings();
            PreviewStickerEvent();
            SaveSettingsData();
        }

        if (GUI.Button(new Rect(x, y + 244f, 180f, 32f), "\uCD08\uAE30\uD654", secondaryButtonStyle))
        {
            stickerScale = 0.55f;
            stickerOffsetX = 0f;
            stickerOffsetY = 0.02f;
            ApplyStickerSettings();
            PreviewStickerEvent();
            SaveSettingsData();
        }
    }

    float DrawStickerSlider(float x, float y, string label, float value, float min, float max)
    {
        GUI.Label(new Rect(x, y, 220f, 22f), label, bodyStyle);
        float next = GUI.HorizontalSlider(new Rect(x, y + 28f, 360f, 18f), value, min, max);
        GUI.Label(new Rect(x + 376f, y + 20f, 64f, 24f), next.ToString("F2"), bodyStyle);
        return next;
    }

    void DrawAvatarPositionTab(Rect panel)
    {
        float x = panel.x + 24f;
        float y = panel.y + 108f;

        float nextX = DrawStickerSlider(x, y + 18f, "\uC624\uB978\uCABD / \uC67C\uCABD", avatarOffsetX, -GuideOffsetLimit, GuideOffsetLimit);
        float nextY = DrawStickerSlider(x, y + 72f, "\uC544\uB798 / \uC704", avatarOffsetY, -GuideOffsetLimit, GuideOffsetLimit);

        if (!Mathf.Approximately(nextX, avatarOffsetX) || !Mathf.Approximately(nextY, avatarOffsetY))
        {
            avatarOffsetX = nextX;
            avatarOffsetY = nextY;
            ApplyAvatarPositionOffset();
            SaveSettingsData();
            SaveUiState();
        }

        if (GUI.Button(new Rect(x, y + 142f, 180f, 32f), "\uCD08\uAE30\uD654", secondaryButtonStyle))
        {
            avatarOffsetX = 0f;
            avatarOffsetY = 0f;
            ApplyAvatarPositionOffset();
            SaveSettingsData();
            SaveUiState();
        }
    }

    void DrawCommandsTabReadable(Rect panel)
    {
        float x = panel.x + 24f;
        float y = panel.y + 108f;

        EnsureDefaultCommandRows();
        GUI.Label(new Rect(x, y, 520f, 22f), "\uCC44\uD305 / \uB3C4\uB124\uC774\uC158 / \uC801\uC6A9 \uC774\uBCA4\uD2B8", bodyStyle);

        Rect viewRect = new Rect(x, y + 30f, 970f, 326f);
        Rect contentRect = new Rect(0f, 0f, 950f, Mathf.Max(326f, commandRuleRows.Count * 126f + 12f));
        commandRulesScroll = GUI.BeginScrollView(viewRect, commandRulesScroll, contentRect);

        string[] eventOptions =
        {
            "\uC2F1\uAE00\uC0F7",
            "\uC0F7\uAC74",
            "\uAE30\uAD00\uCD1D",
            "\uC2A4\uD2F0\uCEE4"
        };
        string[] soundOptions =
        {
            "\uB7EC\uBC84 \uB355",
            "\uD788\uD2B8"
        };

        for (int i = 0; i < commandRuleRows.Count; i++)
        {
            CommandRuleRow row = commandRuleRows[i];
            if (row == null)
                continue;

            float rowY = 8f + (i * 126f);
            DrawPanel(new Rect(0f, rowY, 932f, 110f), softPanelColor);

            GUI.Label(new Rect(14f, rowY + 12f, 220f, 20f), "\uC774\uBCA4\uD2B8 \uD638\uCD9C \uCC44\uD305", mutedStyle);
            string nextChat = DrawImeTextField("command_chat_readable_" + i, new Rect(14f, rowY + 36f, 390f, 30f), row.chatText ?? string.Empty);

            GUI.Label(new Rect(424f, rowY + 12f, 220f, 20f), "\uC774\uBCA4\uD2B8 \uD638\uCD9C \uB3C4\uB124\uC774\uC158 \uC561\uC218", mutedStyle);
            string nextDonation = GUI.TextField(new Rect(424f, rowY + 36f, 150f, 30f), row.donationText ?? "0");

            GUI.Label(new Rect(14f, rowY + 74f, 120f, 20f), "\uC801\uC6A9 \uC774\uBCA4\uD2B8", mutedStyle);
            int nextEventIndex = GUI.Toolbar(new Rect(124f, rowY + 72f, 360f, 30f), Mathf.Clamp(row.eventIndex, 0, eventOptions.Length - 1), eventOptions);

            GUI.Label(new Rect(514f, rowY + 74f, 90f, 20f), "\uD6A8\uACFC\uC74C", mutedStyle);
            int nextSoundIndex = GUI.Toolbar(new Rect(604f, rowY + 72f, 240f, 30f), Mathf.Clamp(row.soundIndex, 0, soundOptions.Length - 1), soundOptions);

            if (nextChat != row.chatText || nextDonation != row.donationText || nextEventIndex != row.eventIndex || nextSoundIndex != row.soundIndex)
            {
                row.chatText = nextChat;
                row.donationText = nextDonation;
                row.eventIndex = nextEventIndex;
                row.soundIndex = nextSoundIndex;
                SaveSettingsData();
                ApplyCommandSettings();
            }
        }

        GUI.EndScrollView();

        if (GUI.Button(new Rect(x, y + 368f, 220f, 34f), "\uC0C8 \uADDC\uCE59 \uCD94\uAC00", primaryButtonStyle))
        {
            commandRuleRows.Add(new CommandRuleRow());
            SaveSettingsData();
            ApplyCommandSettings();
        }

        GUI.Label(new Rect(x, y + 412f, 900f, 22f), "\uC608\uC2DC: \uB54C\uB824 / 0 / \uC2F1\uAE00\uC0F7  |  \uCC44\uD305 \uC5C6\uC74C / 1000 / \uC0F7\uAC74", mutedStyle);
    }

    void DrawCommandsTabClean(Rect panel)
    {
        float x = panel.x + 24f;
        float y = panel.y + 108f;

        EnsureDefaultCommandRows();
        GUI.Label(new Rect(x, y, 520f, 24f), "\uBA85\uB839\uC5B4 \uCEE4\uC2A4\uD140", sectionStyle);
        GUI.Label(new Rect(x, y + 28f, 820f, 20f), "\uC870\uAC74\uC744 \uC785\uB825\uD558\uACE0 \uC2E4\uD589\uD560 \uC774\uBCA4\uD2B8\uC640 \uD6A8\uACFC\uC74C\uC744 \uC120\uD0DD\uD558\uC138\uC694.", mutedStyle);

        Rect viewRect = new Rect(x, y + 62f, 972f, 310f);
        Rect contentRect = new Rect(0f, 0f, 952f, Mathf.Max(310f, commandRuleRows.Count * 92f + 10f));
        commandRulesScroll = GUI.BeginScrollView(viewRect, commandRulesScroll, contentRect);

        string[] eventOptions =
        {
            "\uC2F1\uAE00\uC0F7",
            "\uC0F7\uAC74",
            "\uAE30\uAD00\uCD1D",
            "\uC2A4\uD2F0\uCEE4"
        };
        string[] soundOptions =
        {
            "\uB7EC\uBC84 \uB355",
            "\uD788\uD2B8"
        };

        for (int i = 0; i < commandRuleRows.Count; i++)
        {
            CommandRuleRow row = commandRuleRows[i];
            if (row == null)
                continue;

            float rowY = 8f + (i * 92f);
            DrawPanel(new Rect(0f, rowY, 936f, 78f), softPanelColor);
            GUI.Label(new Rect(14f, rowY + 28f, 72f, 22f), "\uADDC\uCE59 " + (i + 1), bodyStyle);

            GUI.Label(new Rect(96f, rowY + 10f, 130f, 18f), "\uCC44\uD305 \uC870\uAC74", mutedStyle);
            string nextChat = DrawImeTextField("command_chat_clean_" + i, new Rect(96f, rowY + 34f, 250f, 30f), row.chatText ?? string.Empty);

            GUI.Label(new Rect(360f, rowY + 10f, 110f, 18f), "\uD6C4\uC6D0 \uAE08\uC561", mutedStyle);
            string nextDonation = GUI.TextField(new Rect(360f, rowY + 34f, 92f, 30f), row.donationText ?? "0");

            GUI.Label(new Rect(468f, rowY + 10f, 130f, 18f), "\uC2E4\uD589 \uC774\uBCA4\uD2B8", mutedStyle);
            int nextEventIndex = GUI.Toolbar(new Rect(468f, rowY + 34f, 268f, 30f), Mathf.Clamp(row.eventIndex, 0, eventOptions.Length - 1), eventOptions);

            GUI.Label(new Rect(746f, rowY + 10f, 80f, 18f), "\uD6A8\uACFC\uC74C", mutedStyle);
            int nextSoundIndex = GUI.Toolbar(new Rect(746f, rowY + 34f, 132f, 30f), Mathf.Clamp(row.soundIndex, 0, soundOptions.Length - 1), soundOptions);

            if (GUI.Button(new Rect(888f, rowY + 34f, 42f, 30f), "\uC0AD\uC81C", subtleButtonStyle))
            {
                commandRuleRows.RemoveAt(i);
                SaveSettingsData();
                ApplyCommandSettings();
                GUIUtility.ExitGUI();
            }

            if (nextChat != row.chatText || nextDonation != row.donationText || nextEventIndex != row.eventIndex || nextSoundIndex != row.soundIndex)
            {
                row.chatText = nextChat;
                row.donationText = nextDonation;
                row.eventIndex = nextEventIndex;
                row.soundIndex = nextSoundIndex;
                SaveSettingsData();
                ApplyCommandSettings();
            }
        }

        GUI.EndScrollView();

        if (GUI.Button(new Rect(x, y + 388f, 180f, 34f), "\uC0C8 \uADDC\uCE59 \uCD94\uAC00", primaryButtonStyle))
        {
            commandRuleRows.Add(new CommandRuleRow());
            SaveSettingsData();
            ApplyCommandSettings();
        }

    }

    void DrawCommandsTab(Rect panel)
    {
        DrawCommandsTabClean(panel);
        return;
    }

#if false
        float x = panel.x + 24f;
        float y = panel.y + 108f;

        EnsureDefaultCommandRows();
        GUI.Label(new Rect(x, y, 520f, 22f), "채팅 / 도네이션 / 적용 이벤트", bodyStyle);

        Rect viewRect = new Rect(x, y + 30f, 970f, 326f);
        Rect contentRect = new Rect(0f, 0f, 950f, Mathf.Max(326f, commandRuleRows.Count * 126f + 12f));
        commandRulesScroll = GUI.BeginScrollView(viewRect, commandRulesScroll, contentRect);

        string[] eventOptions =
        {
            "\uC2F1\uAE00\uC0F7",
            "\uC0F7\uAC74",
            "\uAE30\uAD00\uCD1D",
            "\uC2A4\uD2F0\uCEE4 \uBD99\uC774\uAE30"
        };
        string[] soundOptions =
        {
            "\uB7EC\uBC84 \uB355",
            "\uD788\uD2B8"
        };
        for (int i = 0; i < commandRuleRows.Count; i++)
        {
            CommandRuleRow row = commandRuleRows[i];
            if (row == null)
                continue;

            float rowY = 8f + (i * 126f);
            DrawPanel(new Rect(0f, rowY, 932f, 110f), softPanelColor);
            GUI.Label(new Rect(0f, rowY, 160f, 20f), "이벤트 호출 채팅", mutedStyle);
            string nextChat = DrawImeTextField("command_chat_" + i, new Rect(0f, rowY + 24f, 180f, 26f), row.chatText ?? string.Empty);

            GUI.Label(new Rect(192f, rowY, 110f, 20f), "이벤트 호출 도네이션", mutedStyle);
            string nextDonation = GUI.TextField(new Rect(192f, rowY + 24f, 100f, 26f), row.donationText ?? "0");

            GUI.Label(new Rect(304f, rowY, 140f, 20f), "적용시킬 이벤트", mutedStyle);
            int nextEventIndex = GUI.Toolbar(new Rect(304f, rowY + 24f, 200f, 26f), Mathf.Clamp(row.eventIndex, 0, eventOptions.Length - 1), eventOptions);

            GUI.Label(new Rect(520f, rowY, 120f, 20f), "효과음", mutedStyle);
            int nextSoundIndex = GUI.Toolbar(new Rect(520f, rowY + 24f, 170f, 26f), Mathf.Clamp(row.soundIndex, 0, soundOptions.Length - 1), soundOptions);

            if (nextChat != row.chatText || nextDonation != row.donationText || nextEventIndex != row.eventIndex || nextSoundIndex != row.soundIndex)
            {
                row.chatText = nextChat;
                row.donationText = nextDonation;
                row.eventIndex = nextEventIndex;
                row.soundIndex = nextSoundIndex;
                SaveSettingsData();
                ApplyCommandSettings();
            }
        }

        GUI.EndScrollView();

        if (GUI.Button(new Rect(x, y + 292f, 220f, 32f), "새 규칙 추가", primaryButtonStyle))
        {
            commandRuleRows.Add(new CommandRuleRow());
            SaveSettingsData();
            ApplyCommandSettings();
        }

        GUI.Label(new Rect(x, y + 334f, 720f, 22f), "예시: 때려 / 0 / 싱글샷 / 러버 덕, 채팅 없음 / 1000 / 샷건 / 히트", mutedStyle);
    }

#endif

    string DrawImeTextField(string controlName, Rect rect, string value)
    {
        GUI.SetNextControlName(controlName);
        string before = value ?? string.Empty;
        string next = GUI.TextField(rect, before);

        if (GUI.GetNameOfFocusedControl() != controlName)
            return next;

        Input.imeCompositionMode = IMECompositionMode.On;
        Input.compositionCursorPos = new Vector2(rect.xMax, rect.yMax);

        Event current = Event.current;
        if (current == null || current.type != EventType.KeyDown)
            return next;

        char typed = current.character;
        if (typed == '\0' || char.IsControl(typed))
            return next;

        bool unityHandledInput = next != before;
        if (unityHandledInput)
            return next;

        next = before + typed;
        current.Use();
        return next;
    }

    void DrawPresetDialog()
    {
        if (!presetDialogVisible)
            return;

        Rect panel = new Rect((Screen.width - 320f) * 0.5f, (Screen.height - 150f) * 0.5f, 320f, 150f);
        DrawPanel(panel, panelColor);

        string message = pendingPresetOverwrite ? "덮어쓰시겠습니까?" : "저장하시겠습니까?";
        GUI.Label(new Rect(panel.x + 18f, panel.y + 20f, 280f, 28f), "프리셋 " + pendingPresetSlot, sectionStyle);
        GUI.Label(new Rect(panel.x + 18f, panel.y + 58f, 280f, 24f), message, bodyStyle);

        if (GUI.Button(new Rect(panel.x + 18f, panel.y + 100f, 132f, 30f), "확인", primaryButtonStyle))
            ConfirmPresetSave();

        if (GUI.Button(new Rect(panel.x + 170f, panel.y + 100f, 132f, 30f), "취소", secondaryButtonStyle))
            presetDialogVisible = false;
    }

    void DrawBroadcastMode()
    {
        Rect badge = new Rect(Screen.width - 290f, 64f, 250f, 106f);
        DrawPanel(badge, panelColor);

        GUI.Label(new Rect(badge.x + 18f, badge.y + 14f, 200f, 28f), "3. 방송 모드", titleStyle);
        GUI.Label(new Rect(badge.x + 18f, badge.y + 46f, 200f, 22f), "프리셋 " + GetPresetLabel(), bodyStyle);
        GUI.Label(new Rect(badge.x + 18f, badge.y + 68f, 220f, 22f), GetConnectionLabel() + " / " + GetEventLabel(), mutedStyle);
    }

    void DrawFlowBar()
    {
        return;
    }

    void DrawInfoLine(float x, float y, string label, string value)
    {
        GUI.Label(new Rect(x, y, 110f, 22f), label, mutedStyle);
        GUI.Label(new Rect(x + 118f, y, 180f, 22f), value, bodyStyle);
    }

    void DrawBadge(Rect rect, string text, Color color)
    {
        Color old = GUI.color;
        GUI.color = new Color(color.r, color.g, color.b, 0.22f);
        GUI.DrawTexture(rect, solidTexture);
        GUI.color = old;

        Color textOld = GUI.contentColor;
        GUI.contentColor = color;
        GUI.Label(rect, text, badgeStyle);
        GUI.contentColor = textOld;
    }

    void DrawPanel(Rect rect, Color color)
    {
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, solidTexture);
        GUI.color = old;
    }

    string GetPresetLabel()
    {
        return currentPreset == BroadcastPreset.Weak ? "방송용 약한" : "방송용 강한";
    }

    string GetConnectionLabel()
    {
        if (launcher == null)
            return "VSeeFace 대기중";

        return launcher.IsVSeeFaceConnected ? "VSeeFace 연결됨" : "VSeeFace 끊김";
    }

    Color GetConnectionColor()
    {
        if (launcher == null)
            return errorColor;

        return launcher.IsVSeeFaceConnected ? successColor : errorColor;
    }

    string GetEventLabel()
    {
        if (udpReceiver == null)
            return "이벤트 대기중";

        string status = udpReceiver.LastStatus ?? string.Empty;
        return status.StartsWith("Received") ? "이벤트 수신중" : "이벤트 준비";
    }

    void ApplyPreviewStrength()
    {
        if (launcher == null)
            return;

        UpperBodyMotionSettings settings = launcher.GetUpperBodyMotionSettings();
        if (settings == null)
            return;

        settings.strength = previewStrengthValue;
        launcher.SetUpperBodyMotionSettings(settings);

        ApplyVSeeFacePreviewStrength();
        SaveUiState();
    }

    void ApplyProjectileScaleMultiplier()
    {
        if (launcher == null)
            return;

        int count = launcher.GetLaunchConfigCount();
        if (count <= 0)
            return;

        if (projectileScaleBaselines == null || projectileScaleBaselines.Length != count)
        {
            projectileScaleBaselines = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                LaunchEventConfig config = launcher.GetLaunchConfig(i);
                projectileScaleBaselines[i] = config != null ? config.scale : Vector3.one;
            }
        }

        for (int i = 0; i < count; i++)
        {
            LaunchEventConfig config = launcher.GetLaunchConfig(i);
            if (config == null)
                continue;

            string normalizedLabel = string.IsNullOrWhiteSpace(config.label) ? string.Empty : config.label.Trim().ToLowerInvariant();
            bool affectsProjectileScale =
                normalizedLabel == "single" ||
                normalizedLabel == "shotgun" ||
                normalizedLabel == "donation" ||
                normalizedLabel == "big donation" ||
                normalizedLabel == "spread" ||
                normalizedLabel == "싱글샷" ||
                normalizedLabel == "샷건";

            config.scale = affectsProjectileScale
                ? projectileScaleBaselines[i] * projectileScaleMultiplier
                : projectileScaleBaselines[i];
            launcher.SetLaunchConfig(i, config);
        }

        if (!Mathf.Approximately(lastSavedProjectileScaleMultiplier, projectileScaleMultiplier))
        {
            lastSavedProjectileScaleMultiplier = projectileScaleMultiplier;
            launcher.SaveConfig();
            SaveUiState();
        }
    }

    void LoadUiState()
    {
        currentMode = (StreamerUiMode)Mathf.Clamp(PlayerPrefs.GetInt("ui.currentMode", (int)currentMode), 0, 2);
        currentPreset = (BroadcastPreset)Mathf.Clamp(PlayerPrefs.GetInt("ui.currentPreset", (int)currentPreset), 0, 1);
        transparentBackground = PlayerPrefs.GetInt("ui.transparentBackground", transparentBackground ? 1 : 0) == 1;
        controlPanelVisible = PlayerPrefs.GetInt("ui.controlPanelVisible", controlPanelVisible ? 1 : 0) == 1;
        profileSpriteVisible = PlayerPrefs.GetInt("ui.profileSpriteVisible", profileSpriteVisible ? 1 : 0) == 1;
        guideVisible = PlayerPrefs.GetInt("ui.guideVisible", guideVisible ? 1 : 0) == 1;
        settingsWindowVisible = PlayerPrefs.GetInt("ui.settingsWindowVisible", settingsWindowVisible ? 1 : 0) == 1;
        currentSettingsTab = (SettingsTab)Mathf.Clamp(PlayerPrefs.GetInt("ui.currentSettingsTab", (int)currentSettingsTab), 0, 5);
        previewStrengthValue = PlayerPrefs.GetFloat("ui.previewStrength", previewStrengthValue);
        cameraDepthOffset = PlayerPrefs.GetFloat("ui.cameraDepthOffset", cameraDepthOffset);
        projectileScaleMultiplier = PlayerPrefs.GetFloat("ui.projectileScaleMultiplier", projectileScaleMultiplier);

        cameraDepthOffset = Mathf.Clamp(cameraDepthOffset, cameraDepthOffsetMin, cameraDepthOffsetMax);

        lastSavedPreviewStrength = previewStrengthValue;
        lastSavedCameraDepthOffset = cameraDepthOffset;
        lastSavedProjectileScaleMultiplier = projectileScaleMultiplier;
    }

    void LoadSettingsData()
    {
        chzzkChannelText = PlayerPrefs.GetString("ui.chzzk.channel", chzzkChannelText);
        chzzkTokenText = PlayerPrefs.GetString("ui.chzzk.token", chzzkTokenText);
        if (loginManager != null && loginManager.CanOverrideBackendUrl)
            chzzkBackendUrlText = AuthStorage.GetBackendBaseUrl(chzzkBackendUrlText);
        else if (loginManager != null)
            chzzkBackendUrlText = loginManager.BackendBaseUrl;
        masterVolume = PlayerPrefs.GetFloat("ui.sound.master", masterVolume);
        effectVolume = PlayerPrefs.GetFloat("ui.sound.effect", effectVolume);
        stickerScale = PlayerPrefs.GetFloat("ui.sticker.scale", stickerScale);
        stickerOffsetX = PlayerPrefs.GetFloat("ui.sticker.offsetX", stickerOffsetX);
        stickerOffsetY = PlayerPrefs.GetFloat("ui.sticker.offsetY", stickerOffsetY);
        if (PlayerPrefs.GetInt("ui.guide.offsetVersion", 0) < GuideOffsetStorageVersion)
        {
            avatarOffsetX = 0f;
            avatarOffsetY = 0f;
            PlayerPrefs.DeleteKey("ui.avatar.offsetX");
            PlayerPrefs.DeleteKey("ui.avatar.offsetY");
            PlayerPrefs.SetFloat("ui.guide.offsetX", avatarOffsetX);
            PlayerPrefs.SetFloat("ui.guide.offsetY", avatarOffsetY);
            PlayerPrefs.SetInt("ui.guide.offsetVersion", GuideOffsetStorageVersion);
            PlayerPrefs.Save();
        }
        else
        {
            avatarOffsetX = PlayerPrefs.GetFloat("ui.guide.offsetX", avatarOffsetX);
            avatarOffsetY = PlayerPrefs.GetFloat("ui.guide.offsetY", avatarOffsetY);
        }
        stickerScale = Mathf.Clamp(stickerScale, 0.10f, 2.50f);
        stickerOffsetX = Mathf.Clamp(stickerOffsetX, -0.60f, 0.60f);
        stickerOffsetY = Mathf.Clamp(stickerOffsetY, -0.60f, 0.60f);
        avatarOffsetX = Mathf.Clamp(avatarOffsetX, -GuideOffsetLimit, GuideOffsetLimit);
        avatarOffsetY = Mathf.Clamp(avatarOffsetY, -GuideOffsetLimit, GuideOffsetLimit);

        commandRuleRows.Clear();
        bool hasSavedCommandRules = PlayerPrefs.HasKey("ui.command.rules.json");
        string json = PlayerPrefs.GetString("ui.command.rules.json", string.Empty);
        if (!string.IsNullOrWhiteSpace(json))
        {
            CommandRuleCollection collection = JsonUtility.FromJson<CommandRuleCollection>(json);
            if (collection != null && collection.rows != null)
            {
                for (int i = 0; i < collection.rows.Length; i++)
                {
                    if (collection.rows[i] != null)
                    {
                        commandRuleRows.Add(collection.rows[i]);
                    }
                }
            }
        }

        if (!hasSavedCommandRules)
            AddDefaultCommandRows();
        commandRowsInitialized = true;
    }

    void SaveUiState()
    {
        bool dirty = false;

        PlayerPrefs.SetInt("ui.currentMode", (int)currentMode);
        PlayerPrefs.SetInt("ui.currentPreset", (int)currentPreset);
        PlayerPrefs.SetInt("ui.transparentBackground", transparentBackground ? 1 : 0);
        PlayerPrefs.SetInt("ui.controlPanelVisible", controlPanelVisible ? 1 : 0);
        PlayerPrefs.SetInt("ui.profileSpriteVisible", profileSpriteVisible ? 1 : 0);
        PlayerPrefs.SetInt("ui.guideVisible", guideVisible ? 1 : 0);
        PlayerPrefs.SetInt("ui.settingsWindowVisible", settingsWindowVisible ? 1 : 0);
        PlayerPrefs.SetInt("ui.currentSettingsTab", (int)currentSettingsTab);
        PlayerPrefs.SetInt("ui.guide.offsetVersion", GuideOffsetStorageVersion);
        PlayerPrefs.SetFloat("ui.guide.offsetX", avatarOffsetX);
        PlayerPrefs.SetFloat("ui.guide.offsetY", avatarOffsetY);
        dirty = true;

        if (!Mathf.Approximately(lastSavedPreviewStrength, previewStrengthValue))
        {
            PlayerPrefs.SetFloat("ui.previewStrength", previewStrengthValue);
            lastSavedPreviewStrength = previewStrengthValue;
            dirty = true;
        }

        if (!Mathf.Approximately(lastSavedCameraDepthOffset, cameraDepthOffset))
        {
            PlayerPrefs.SetFloat("ui.cameraDepthOffset", cameraDepthOffset);
            lastSavedCameraDepthOffset = cameraDepthOffset;
            dirty = true;
        }

        if (!Mathf.Approximately(lastSavedProjectileScaleMultiplier, projectileScaleMultiplier))
        {
            PlayerPrefs.SetFloat("ui.projectileScaleMultiplier", projectileScaleMultiplier);
            lastSavedProjectileScaleMultiplier = projectileScaleMultiplier;
            dirty = true;
        }

        if (dirty)
            PlayerPrefs.Save();
    }

    void SaveSettingsData()
    {
        PlayerPrefs.SetString("ui.chzzk.channel", chzzkChannelText);
        PlayerPrefs.SetString("ui.chzzk.token", chzzkTokenText);
        if (loginManager != null && loginManager.CanOverrideBackendUrl)
            AuthStorage.SetBackendBaseUrl(chzzkBackendUrlText);
        PlayerPrefs.SetFloat("ui.sound.master", masterVolume);
        PlayerPrefs.SetFloat("ui.sound.effect", effectVolume);
        PlayerPrefs.SetFloat("ui.sticker.scale", stickerScale);
        PlayerPrefs.SetFloat("ui.sticker.offsetX", stickerOffsetX);
        PlayerPrefs.SetFloat("ui.sticker.offsetY", stickerOffsetY);
        PlayerPrefs.SetInt("ui.guide.offsetVersion", GuideOffsetStorageVersion);
        PlayerPrefs.SetFloat("ui.guide.offsetX", avatarOffsetX);
        PlayerPrefs.SetFloat("ui.guide.offsetY", avatarOffsetY);
        CommandRuleCollection collection = new CommandRuleCollection { rows = commandRuleRows.ToArray() };
        PlayerPrefs.SetString("ui.command.rules.json", JsonUtility.ToJson(collection));
        PlayerPrefs.Save();
    }

    void OpenPresetSaveDialog(int slot)
    {
        pendingPresetSlot = Mathf.Clamp(slot, 1, 3);
        pendingPresetOverwrite = HasQuickPreset(pendingPresetSlot);
        presetDialogVisible = true;
    }

    void ConfirmPresetSave()
    {
        SaveQuickPreset(pendingPresetSlot);
        presetDialogVisible = false;
    }

    bool HasQuickPreset(int slot)
    {
        return PlayerPrefs.GetInt("ui.quickPreset." + slot + ".saved", 0) == 1;
    }

    void SaveQuickPreset(int slot)
    {
        slot = Mathf.Clamp(slot, 1, 3);
        PlayerPrefs.SetInt("ui.quickPreset." + slot + ".saved", 1);
        PlayerPrefs.SetFloat("ui.quickPreset." + slot + ".previewStrength", previewStrengthValue);
        PlayerPrefs.SetFloat("ui.quickPreset." + slot + ".cameraDepthOffset", cameraDepthOffset);
        PlayerPrefs.SetFloat("ui.quickPreset." + slot + ".projectileScaleMultiplier", projectileScaleMultiplier);
        PlayerPrefs.Save();
    }

    void LoadQuickPreset(int slot)
    {
        slot = Mathf.Clamp(slot, 1, 3);
        if (!HasQuickPreset(slot))
            return;

        previewStrengthValue = PlayerPrefs.GetFloat("ui.quickPreset." + slot + ".previewStrength", previewStrengthValue);
        cameraDepthOffset = PlayerPrefs.GetFloat("ui.quickPreset." + slot + ".cameraDepthOffset", cameraDepthOffset);
        projectileScaleMultiplier = PlayerPrefs.GetFloat("ui.quickPreset." + slot + ".projectileScaleMultiplier", projectileScaleMultiplier);

        ApplyPreviewStrength();
        ApplyCameraDepthOffset();
        ApplyProjectileScaleMultiplier();
        SaveUiState();
    }

    void SyncPortTexts()
    {
        if (launcher != null)
            vmcReceivePortText = launcher.VSeeFaceReceivePort.ToString();

        if (launcher != null && launcher.vmcOscSender != null)
            vmcSendPortText = launcher.vmcOscSender.CurrentTargetPort.ToString();

        if (udpReceiver != null)
            eventReceivePortText = udpReceiver.CurrentPort.ToString();
    }

    void ApplyCommandSettings()
    {
        EnsureChatRouter();
        if (chatRouter == null)
            return;

        EnsureDefaultCommandRows();
        var rules = new System.Collections.Generic.List<ChatOrDonationRouter.CustomTriggerRule>();
        for (int i = 0; i < commandRuleRows.Count; i++)
        {
            CommandRuleRow row = commandRuleRows[i];
            if (row == null)
                continue;

            string chatText = string.IsNullOrWhiteSpace(row.chatText) ? string.Empty : row.chatText.Trim();
            int donationAmount = ParseDonationAmount(row.donationText);
            string launchLabel = GetEventLabelForIndex(row.eventIndex);

            if (string.IsNullOrWhiteSpace(chatText) && donationAmount <= 0)
                continue;

            rules.Add(new ChatOrDonationRouter.CustomTriggerRule
            {
                chatText = chatText,
                donationAmount = donationAmount,
                launchLabel = launchLabel,
                hitSoundIndex = Mathf.Clamp(row.soundIndex, 0, 1)
            });
        }

        chatRouter.ReplaceCustomRules(rules);
    }

    void ApplySoundSettings()
    {
        AudioListener.volume = Mathf.Clamp01(masterVolume);
    }

    void ApplyStickerSettings()
    {
        HeadStickerPlayer player = EnsureHeadStickerPlayer();
        if (player == null)
            return;

        stickerScale = Mathf.Clamp(stickerScale, 0.10f, 2.50f);
        stickerOffsetX = Mathf.Clamp(stickerOffsetX, -0.60f, 0.60f);
        stickerOffsetY = Mathf.Clamp(stickerOffsetY, -0.60f, 0.60f);
        player.SetStickerOffsetAndScale(stickerOffsetX, stickerOffsetY, stickerScale);
    }

    void ApplyAvatarPositionOffset()
    {
        Transform avatarRoot = EnsureAvatarRootTransform();
        if (avatarRoot == null || !avatarBasePositionInitialized)
            return;

        avatarOffsetX = Mathf.Clamp(avatarOffsetX, -GuideOffsetLimit, GuideOffsetLimit);
        avatarOffsetY = Mathf.Clamp(avatarOffsetY, -GuideOffsetLimit, GuideOffsetLimit);
        avatarRoot.position = avatarBasePosition + new Vector3(avatarOffsetX, avatarOffsetY, 0f) * GuideOffsetWorldScale;
    }

    void PreviewStickerEvent()
    {
        if (launcher == null)
            launcher = Object.FindAnyObjectByType<SimpleLauncher>();

        if (launcher != null)
        {
            launcher.PlayHeadSticker();
            return;
        }

        HeadStickerPlayer player = EnsureHeadStickerPlayer();
        Transform head = FindHeadTarget();
        if (player != null && head != null)
            player.Play(head);
    }

    void EnsureDefaultCommandRows()
    {
        if (commandRowsInitialized)
            return;

        AddDefaultCommandRows();
        commandRowsInitialized = true;
    }

    void AddDefaultCommandRows()
    {
        while (commandRuleRows.Count < 3)
            commandRuleRows.Add(new CommandRuleRow());
    }

    int ParseDonationAmount(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        string digits = string.Empty;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsDigit(c))
                digits += c;
        }

        if (string.IsNullOrWhiteSpace(digits))
            return 0;

        return int.TryParse(digits, out int amount) ? Mathf.Max(0, amount) : 0;
    }

    string GetEventLabelForIndex(int index)
    {
        switch (Mathf.Clamp(index, 0, 3))
        {
            case 1: return "shotgun";
            case 2: return "machinegun";
            case 3: return "sticker";
            default: return "single";
        }
    }

    void ApplyPortSettings()
    {
        if (launcher != null && int.TryParse(vmcReceivePortText, out int receivePort))
        {
            launcher.SetVSeeFaceReceivePort(receivePort);
            vmcReceivePortText = launcher.VSeeFaceReceivePort.ToString();
        }

        if (launcher != null && launcher.vmcOscSender != null && int.TryParse(vmcSendPortText, out int sendPort))
        {
            launcher.vmcOscSender.SetTargetPort(sendPort);
            vmcSendPortText = launcher.vmcOscSender.CurrentTargetPort.ToString();
        }

        if (udpReceiver != null && int.TryParse(eventReceivePortText, out int eventPort))
        {
            udpReceiver.RestartReceiver(eventPort);
            eventReceivePortText = udpReceiver.CurrentPort.ToString();
        }
    }

    void DrawPortStatusIcon(Rect rect, bool ready)
    {
        if (portStatusCheckTexture == null)
            portStatusCheckTexture = Resources.Load<Texture2D>("UI/PortStatusCheck");
        if (portStatusXTexture == null)
            portStatusXTexture = Resources.Load<Texture2D>("UI/PortStatusX");

        Texture2D texture = ready ? portStatusCheckTexture : portStatusXTexture;
        if (texture != null)
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
    }

    bool IsReceivePortReady()
    {
        return int.TryParse(vmcReceivePortText, out int port) &&
            launcher != null &&
            port == launcher.VSeeFaceReceivePort &&
            launcher.IsVSeeFaceConnected;
    }

    bool IsSendPortReady()
    {
        return int.TryParse(vmcSendPortText, out int port) &&
            launcher != null &&
            launcher.vmcOscSender != null &&
            port == launcher.vmcOscSender.CurrentTargetPort;
    }

    bool IsEventPortReady()
    {
        if (!int.TryParse(eventReceivePortText, out int port))
            return false;

        string status = udpReceiver != null ? (udpReceiver.LastStatus ?? string.Empty) : string.Empty;
        bool listening = status.StartsWith("Listening") || status.StartsWith("Received");
        return udpReceiver != null && port == udpReceiver.CurrentPort && listening;
    }

    string GetReceivePortStatusEmoji()
    {
        if (!int.TryParse(vmcReceivePortText, out int port))
            return "\u274C";

        return launcher != null && port == launcher.VSeeFaceReceivePort && launcher.IsVSeeFaceConnected ? "\u2705" : "\u274C";
    }

    string GetSendPortStatusEmoji()
    {
        if (!int.TryParse(vmcSendPortText, out int port))
            return "\u274C";

        return launcher != null && launcher.vmcOscSender != null && port == launcher.vmcOscSender.CurrentTargetPort ? "\u2705" : "\u274C";
    }

    string GetEventPortStatusEmoji()
    {
        if (!int.TryParse(eventReceivePortText, out int port))
            return "\u274C";

        string status = udpReceiver != null ? (udpReceiver.LastStatus ?? string.Empty) : string.Empty;
        bool listening = status.StartsWith("Listening") || status.StartsWith("Received");
        return udpReceiver != null && port == udpReceiver.CurrentPort && listening ? "\u2705" : "\u274C";
    }

    void ApplyPreset(BroadcastPreset preset)
    {
        currentPreset = preset;

        if (launcher == null)
            return;

        launcher.enableScreenShake = false;

        UpperBodyMotionSettings settings = launcher.GetUpperBodyMotionSettings();
        if (settings == null)
            settings = new UpperBodyMotionSettings();

        if (preset == BroadcastPreset.Weak)
        {
            settings.strength = 0.62f;
            settings.spineWeight = 0.22f;
            settings.chestWeight = 0.46f;
            settings.upperChestWeight = 0.68f;
            settings.impactDuration = 0.16f;
            previewStrengthValue = settings.strength;
        }
        else
        {
            settings.strength = 1.08f;
            settings.spineWeight = 0.34f;
            settings.chestWeight = 0.70f;
            settings.upperChestWeight = 0.96f;
            settings.impactDuration = 0.22f;
            previewStrengthValue = settings.strength;
        }

        launcher.SetUpperBodyMotionSettings(settings);
        ApplyVSeeFacePreviewStrength();
    }

    void ApplyVSeeFacePreviewStrength()
    {
        if (launcher == null || launcher.vmcOscSender == null)
            return;

        float baseStrength = currentPreset == BroadcastPreset.Weak ? 0.62f : 1.08f;
        float scale = Mathf.Clamp(previewStrengthValue / Mathf.Max(0.01f, baseStrength), 0.35f, 2.8f);

        if (currentPreset == BroadcastPreset.Weak)
        {
            launcher.vmcOscSender.upperChestPositionWeight = 0.22f * scale;
            launcher.vmcOscSender.upperChestRotationWeight = 1.05f * scale;
            launcher.vmcOscSender.chestPositionWeight = 0.14f * scale;
            launcher.vmcOscSender.chestRotationWeight = 0.78f * scale;
            launcher.vmcOscSender.spinePositionWeight = 0.08f * scale;
            launcher.vmcOscSender.spineRotationWeight = 0.34f * scale;
            return;
        }

        launcher.vmcOscSender.upperChestPositionWeight = 0.34f * scale;
        launcher.vmcOscSender.upperChestRotationWeight = 1.58f * scale;
        launcher.vmcOscSender.chestPositionWeight = 0.26f * scale;
        launcher.vmcOscSender.chestRotationWeight = 1.18f * scale;
        launcher.vmcOscSender.spinePositionWeight = 0.16f * scale;
        launcher.vmcOscSender.spineRotationWeight = 0.62f * scale;
    }
}
