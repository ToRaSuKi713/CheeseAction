using System;

[Serializable]
public class TestEventData
{
    public string eventType;
    public string nickname;
    public string message;
    public int amount;
    public string spawnPattern;
    public float power;
    public string direction;
    public int count;
    public float scale;
    public string color;
    public int hitSoundIndex;

    public bool projectileEnabled;
    public bool vmcEnabled;
    public bool cameraRecoilEnabled;
    public float globalProjectilePowerMultiplier;
    public float globalVmcMultiplier;

    public float vmcImpulseX;
    public float vmcImpulseY;
    public float vmcImpulseZ;
    public float vmcYaw;
    public float vmcPitch;
    public float vmcRoll;
    public int vmcDurationMs;
}
