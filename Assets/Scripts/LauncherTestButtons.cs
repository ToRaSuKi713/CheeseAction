using UnityEngine;

public class LauncherTestButtons : MonoBehaviour
{
    public SimpleLauncher launcher;

    private GUIStyle buttonStyle;
    private GUIStyle titleStyle;
    private GUIStyle boxStyle;

    void EnsureStyles()
    {
        if (buttonStyle == null)
        {
            buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fontSize = 14;
        }

        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 18;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = Color.white;
        }

        if (boxStyle == null)
        {
            boxStyle = new GUIStyle(GUI.skin.box);
        }
    }

    void OnGUI()
    {
        EnsureStyles();

        GUI.Box(new Rect(520, 20, 220, 220), "", boxStyle);
        GUI.Label(new Rect(540, 35, 180, 30), "Test Events", titleStyle);

        if (launcher == null)
        {
            GUI.Label(new Rect(540, 70, 180, 25), "Launcher not assigned");
            return;
        }

        if (GUI.Button(new Rect(540, 75, 180, 40), "Launch Chat", buttonStyle))
        {
            launcher.LaunchByLabel("Chat");
        }

        if (GUI.Button(new Rect(540, 125, 180, 40), "Launch 싱글샷", buttonStyle))
        {
            launcher.LaunchByLabel("싱글샷");
        }

        if (GUI.Button(new Rect(540, 175, 180, 40), "Launch 샷건", buttonStyle))
        {
            launcher.LaunchByLabel("샷건");
        }
    }
}
