using System;

[Serializable]
public class LaunchConfigFile
{
    public LaunchEventConfig[] launchConfigs;
    public UpperBodyMotionSettings upperBodyMotionSettings = new UpperBodyMotionSettings();
    public HeadBoxAnchorSettings headBoxAnchorSettings = new HeadBoxAnchorSettings();
}
