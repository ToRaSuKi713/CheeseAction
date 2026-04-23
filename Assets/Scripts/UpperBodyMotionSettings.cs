using System;
using UnityEngine;

[Serializable]
public class UpperBodyMotionSettings
{
    [Range(0f, 2f)] public float strength = 0.55f;
    [Range(0f, 1f)] public float spineWeight = 0.18f;
    [Range(0f, 1f)] public float chestWeight = 0.34f;
    [Range(0f, 1f)] public float upperChestWeight = 0.45f;
    [Range(0.05f, 0.6f)] public float impactDuration = 0.18f;

    public UpperBodyMotionSettings Clone()
    {
        return new UpperBodyMotionSettings
        {
            strength = strength,
            spineWeight = spineWeight,
            chestWeight = chestWeight,
            upperChestWeight = upperChestWeight,
            impactDuration = impactDuration
        };
    }

    public void Clamp()
    {
        strength = Mathf.Clamp(strength, 0f, 2f);
        spineWeight = Mathf.Clamp01(spineWeight);
        chestWeight = Mathf.Clamp01(chestWeight);
        upperChestWeight = Mathf.Clamp01(upperChestWeight);
        impactDuration = Mathf.Clamp(impactDuration, 0.05f, 0.6f);
    }
}
