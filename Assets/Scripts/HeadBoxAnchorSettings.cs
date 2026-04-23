using System;
using UnityEngine;

[Serializable]
public class HeadBoxAnchorSettings
{
    public Vector3 localPosition = new Vector3(0.004f, 0.12f, 0.03f);
    public Vector3 localEuler = Vector3.zero;

    public HeadBoxAnchorSettings Clone()
    {
        return new HeadBoxAnchorSettings
        {
            localPosition = localPosition,
            localEuler = localEuler
        };
    }

    public void Clamp()
    {
        localPosition.x = Mathf.Clamp(localPosition.x, -0.3f, 0.3f);
        localPosition.y = Mathf.Clamp(localPosition.y, -0.1f, 0.35f);
        localPosition.z = Mathf.Clamp(localPosition.z, -0.3f, 0.3f);
        localEuler.x = Mathf.Clamp(localEuler.x, -90f, 90f);
        localEuler.y = Mathf.Clamp(localEuler.y, -90f, 90f);
        localEuler.z = Mathf.Clamp(localEuler.z, -90f, 90f);
    }

    public static HeadBoxAnchorSettings FromTransform(Transform target)
    {
        if (target == null)
            return new HeadBoxAnchorSettings();

        return new HeadBoxAnchorSettings
        {
            localPosition = target.localPosition,
            localEuler = target.localEulerAngles
        };
    }

    public void ApplyTo(Transform target)
    {
        if (target == null)
            return;

        Clamp();
        target.localPosition = localPosition;
        target.localRotation = Quaternion.Euler(localEuler);
    }
}
