using UnityEngine;

public static class ProjectileHitSoundPlayer
{
    private static readonly string[] ClipPaths =
    {
        "HitSounds/RubberDuck",
        "HitSounds/HitSoundEffect"
    };

    private static readonly float[] StartOffsets =
    {
        0.13f,
        0f
    };

    private static readonly AudioClip[] CachedClips = new AudioClip[ClipPaths.Length];
    private static AudioSource[] sourcePool;
    private static int nextSourceIndex;

    public static void Play(int soundIndex, Vector3 position)
    {
        AudioClip clip = GetClip(soundIndex);
        if (clip == null)
            return;

        int index = Mathf.Clamp(soundIndex, 0, ClipPaths.Length - 1);
        AudioSource source = GetSource();
        if (source == null)
            return;

        source.transform.position = position;
        source.clip = clip;
        source.time = Mathf.Clamp(StartOffsets[index], 0f, Mathf.Max(0f, clip.length - 0.01f));
        source.Play();
    }

    private static AudioClip GetClip(int soundIndex)
    {
        int index = Mathf.Clamp(soundIndex, 0, ClipPaths.Length - 1);
        if (CachedClips[index] == null)
            CachedClips[index] = Resources.Load<AudioClip>(ClipPaths[index]);

        return CachedClips[index];
    }

    private static AudioSource GetSource()
    {
        EnsureSourcePool();
        if (sourcePool == null || sourcePool.Length == 0)
            return null;

        AudioSource source = sourcePool[nextSourceIndex];
        nextSourceIndex = (nextSourceIndex + 1) % sourcePool.Length;
        return source;
    }

    private static void EnsureSourcePool()
    {
        if (sourcePool != null)
            return;

        GameObject audioObject = new GameObject("ProjectileHitSoundPlayer");
        UnityEngine.Object.DontDestroyOnLoad(audioObject);
        sourcePool = new AudioSource[8];

        for (int i = 0; i < sourcePool.Length; i++)
        {
            AudioSource source = audioObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 1f;
            sourcePool[i] = source;
        }
    }
}
