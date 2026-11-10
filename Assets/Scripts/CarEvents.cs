using System;
using UnityEngine;

/// Bus de eventos + utilidades compatibles con Unity 2022 y Unity 6.
public static class CarEvents
{
    public static event Action<float, Vector3> Impact;      // intensidad 0-1, dirección local del empujón
    public static event Action<int, int> GearShift;         // marcha anterior, marcha nueva
    public static void RaiseImpact(float k, Vector3 localDir) => Impact?.Invoke(k, localDir);
    public static void RaiseShift(int from, int to) => GearShift?.Invoke(from, to);
}

public static class CarUtil
{
    public static Vector3 Vel(Rigidbody rb)
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    public static void SetVel(Rigidbody rb, Vector3 v)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
    }

    public static void SetAngularDrag(Rigidbody rb, float d)
    {
#if UNITY_6000_0_OR_NEWER
        rb.angularDamping = d;
#else
        rb.angularDrag = d;
#endif
    }
}
