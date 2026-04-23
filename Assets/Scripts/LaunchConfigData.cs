using System;
using UnityEngine;

[Serializable]
public class LaunchEventConfig
{
    public string label = "New Event";
    public KeyCode triggerKey = KeyCode.None;
    public float force = 10f;
    public float cooldown = 0.3f;
    public Color color = Color.white;
    public Vector3 scale = Vector3.one;

    public LaunchEventConfig Clone()
    {
        return new LaunchEventConfig
        {
            label = label,
            triggerKey = triggerKey,
            force = force,
            cooldown = cooldown,
            color = color,
            scale = scale
        };
    }

    public void Clamp()
    {
        force = Mathf.Clamp(force, 0f, 100f);
        cooldown = Mathf.Clamp(cooldown, 0f, 10f);
        scale.x = Mathf.Clamp(scale.x, 0.05f, 10f);
        scale.y = Mathf.Clamp(scale.y, 0.05f, 10f);
        scale.z = Mathf.Clamp(scale.z, 0.05f, 10f);
        color.r = Mathf.Clamp01(color.r);
        color.g = Mathf.Clamp01(color.g);
        color.b = Mathf.Clamp01(color.b);
        color.a = Mathf.Clamp01(color.a);
    }
}

[CreateAssetMenu(fileName = "LaunchConfigData", menuName = "RangE/Launch Config Data")]
public class LaunchConfigData : ScriptableObject
{
    public LaunchEventConfig[] launchConfigs;
    public UpperBodyMotionSettings upperBodyMotionSettings = new UpperBodyMotionSettings();
    public HeadBoxAnchorSettings headBoxAnchorSettings = new HeadBoxAnchorSettings();
}
