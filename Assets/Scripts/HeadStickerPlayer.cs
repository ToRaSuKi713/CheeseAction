using System.Collections;
using UnityEngine;

public class HeadStickerPlayer : MonoBehaviour
{
    [SerializeField] private string stickerResourcePath = "Stickers/Sticker200";
    [SerializeField] private float defaultDurationSeconds = 3f;
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.02f, 0.09f);
    [SerializeField] private Vector3 localEuler = Vector3.zero;
    [SerializeField] private float localScale = 0.55f;
    [SerializeField] private int sortingOrder = 50;

    private Sprite[] sprites;
    private float[] frameDurations;
    private GameObject stickerObject;
    private SpriteRenderer spriteRenderer;
    private Coroutine playRoutine;
    private Transform followTarget;

    public Vector3 LocalOffset => localOffset;
    public float LocalScale => localScale;

    public void SetStickerOffsetAndScale(float offsetX, float offsetY, float scale)
    {
        localOffset = new Vector3(offsetX, offsetY, localOffset.z);
        localScale = Mathf.Clamp(scale, 0.01f, 5f);
        ApplyStickerPose();
    }

    public void Play(Transform headTransform)
    {
        if (headTransform == null)
            return;

        EnsureAssetsLoaded();
        if (sprites == null || sprites.Length == 0)
            return;

        EnsureStickerObject(headTransform);
        followTarget = headTransform;
        ApplyStickerPose();

        if (playRoutine != null)
            StopCoroutine(playRoutine);

        playRoutine = StartCoroutine(PlayRoutine());
    }

    private void LateUpdate()
    {
        if (stickerObject == null || !stickerObject.activeSelf || followTarget == null)
            return;

        if (stickerObject.transform.parent != followTarget)
            stickerObject.transform.SetParent(followTarget, false);

        ApplyStickerPose();
    }

    private IEnumerator PlayRoutine()
    {
        stickerObject.SetActive(true);

        float elapsed = 0f;
        int frame = 0;
        while (elapsed < defaultDurationSeconds)
        {
            spriteRenderer.sprite = sprites[frame];
            float wait = ResolveFrameDuration(frame);
            elapsed += wait;
            frame = (frame + 1) % sprites.Length;
            yield return new WaitForSeconds(wait);
        }

        stickerObject.SetActive(false);
        followTarget = null;
        playRoutine = null;
    }

    private void EnsureStickerObject(Transform headTransform)
    {
        if (stickerObject == null)
        {
            stickerObject = new GameObject("HeadGifSticker");
            spriteRenderer = stickerObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = sortingOrder;
        }

        stickerObject.transform.SetParent(headTransform, false);
    }

    private void ApplyStickerPose()
    {
        if (stickerObject == null)
            return;

        stickerObject.transform.localPosition = localOffset;
        stickerObject.transform.localEulerAngles = localEuler;
        stickerObject.transform.localScale = Vector3.one * Mathf.Max(0.01f, localScale);
    }

    private void EnsureAssetsLoaded()
    {
        if (sprites != null && sprites.Length > 0)
            return;

        Texture2D[] textures = Resources.LoadAll<Texture2D>(stickerResourcePath);
        if (textures == null || textures.Length == 0)
            return;

        System.Array.Sort(textures, (a, b) => string.CompareOrdinal(a.name, b.name));
        sprites = new Sprite[textures.Length];
        for (int i = 0; i < textures.Length; i++)
        {
            Texture2D texture = textures[i];
            sprites[i] = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                200f);
        }

        frameDurations = LoadFrameDurations(textures.Length);
    }

    private float[] LoadFrameDurations(int frameCount)
    {
        TextAsset text = Resources.Load<TextAsset>(stickerResourcePath + "/durations");
        float[] result = new float[frameCount];
        for (int i = 0; i < result.Length; i++)
            result[i] = 0.07f;

        if (text == null || string.IsNullOrWhiteSpace(text.text))
            return result;

        string[] lines = text.text.Split('\n');
        for (int i = 0; i < frameCount && i < lines.Length; i++)
        {
            if (float.TryParse(lines[i].Trim(), out float ms))
                result[i] = Mathf.Max(0.02f, ms / 1000f);
        }

        return result;
    }

    private float ResolveFrameDuration(int frame)
    {
        if (frameDurations == null || frameDurations.Length == 0)
            return 0.07f;

        return frameDurations[Mathf.Clamp(frame, 0, frameDurations.Length - 1)];
    }
}
