using System;
using System.Collections.Generic;
using UnityEngine;

public class EventLogPanel : MonoBehaviour
{
    public int maxEntries = 10;
    public bool showPanel = true;

    private static EventLogPanel instance;

    private readonly List<string> entries = new List<string>();

    private GUIStyle titleStyle;
    private GUIStyle textStyle;
    private GUIStyle boxStyle;

    public static void AddLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (instance == null)
        {
            if (RuntimeLogSettings.VerboseRealtimeLogs)
                Debug.Log("[EventLogPanel] " + RuntimeLogSettings.MaskAndCompact(message));
            return;
        }

        instance.InternalAddLog(RuntimeLogSettings.MaskAndCompact(message));
    }

    void Awake()
    {
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    void Update()
    {
        if (InputKeyHelper.GetKeyDown(KeyCode.L))
        {
            showPanel = !showPanel;
        }
    }

    void InternalAddLog(string message)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string line = $"[{timestamp}] {message}";

        entries.Insert(0, line);

        if (entries.Count > maxEntries)
        {
            entries.RemoveRange(maxEntries, entries.Count - maxEntries);
        }
    }

    void EnsureStyles()
    {
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 20;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = Color.white;
        }

        if (textStyle == null)
        {
            textStyle = new GUIStyle(GUI.skin.label);
            textStyle.fontSize = 14;
            textStyle.normal.textColor = Color.white;
        }

        if (boxStyle == null)
        {
            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.alignment = TextAnchor.UpperLeft;
            boxStyle.fontSize = 14;
        }
    }

    void OnGUI()
    {
        if (!showPanel)
            return;

        EnsureStyles();

        float boxHeight = 80f + (maxEntries * 24f);

        GUI.Box(new Rect(600, 20, 650, boxHeight), "", boxStyle);
        GUI.Label(new Rect(615, 35, 300, 30), "Event Log", titleStyle);
        GUI.Label(new Rect(615, 60, 300, 22), "L = Toggle Log Panel", textStyle);

        if (entries.Count == 0)
        {
            GUI.Label(new Rect(615, 90, 500, 22), "No events yet.", textStyle);
            return;
        }

        float y = 90f;

        for (int i = 0; i < entries.Count; i++)
        {
            GUI.Label(new Rect(615, y, 620, 22), entries[i], textStyle);
            y += 24f;
        }
    }
}
