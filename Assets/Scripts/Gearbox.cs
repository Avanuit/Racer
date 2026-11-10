using UnityEngine;

/// Caja de cambios simulada (visual): marcha y RPM a partir de la velocidad real del Rigidbody.
[RequireComponent(typeof(Rigidbody))]
public class Gearbox : MonoBehaviour
{
    public PrometeoCarController pcc;
    public int gearCount = 6;
    public float idleRPM = 1000f, redlineRPM = 7600f, shiftDownRPM = 3800f;

    public int Gear { get; private set; } = 1;     // -1 = R, 0 = N
    public float RPM { get; private set; }
    public float SpeedKmh { get; private set; }
    public string GearLabel => Gear < 0 ? "R" : Gear == 0 ? "N" : Gear.ToString();

    Rigidbody rb;
    float[] gearTop;
    float rpmVel, shiftDip;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (!pcc) pcc = GetComponent<PrometeoCarController>();
        gearTop = new float[gearCount + 1];
        float top = pcc.maxSpeed * 1.05f;
        for (int i = 1; i <= gearCount; i++)
            gearTop[i] = top * Mathf.Pow(i / (float)gearCount, 0.8f);
        RPM = idleRPM;
    }

    void Update()
    {
        float fwd = Vector3.Dot(CarUtil.Vel(rb), transform.forward) * 3.6f;
        SpeedKmh = Mathf.Abs(fwd);
        float throttle = Input.GetAxis("Vertical");
        float target;

        if (fwd < -1f)
        {
            Gear = -1;
            target = Mathf.Lerp(idleRPM, 4500f, SpeedKmh / Mathf.Max(1, pcc.maxReverseSpeed));
        }
        else
        {
            if (Gear <= 0) Gear = 1;
            if (SpeedKmh < 0.8f && throttle <= 0.05f) Gear = 0;
            else
            {
                if (Gear == 0) Gear = 1;
                if (Gear < gearCount && SpeedKmh > gearTop[Gear] * 0.96f) Shift(Gear + 1);
                else if (Gear > 1 && SpeedKmh < gearTop[Gear - 1] * 0.88f) Shift(Gear - 1);
            }

            if (Gear == 0) target = idleRPM + Mathf.Max(0, throttle) * 2500f;
            else
            {
                float lo = Gear == 1 ? 0f : gearTop[Gear - 1] * 0.88f;
                float t = Mathf.InverseLerp(lo, gearTop[Gear], SpeedKmh);
                float low = Gear == 1 ? idleRPM + Mathf.Max(0, throttle) * 1500f : shiftDownRPM;
                target = Mathf.Lerp(low, redlineRPM, t);
            }
        }

        shiftDip = Mathf.MoveTowards(shiftDip, 0f, Time.deltaTime * 5f);
        target -= shiftDip * 1800f;                          // caída de RPM al cambiar
        RPM = Mathf.Clamp(Mathf.SmoothDamp(RPM, target, ref rpmVel, 0.06f), idleRPM * 0.8f, redlineRPM);
    }

    void Shift(int newGear)
    {
        int old = Gear;
        Gear = newGear;
        shiftDip = 1f;
        CarEvents.RaiseShift(old, newGear);
    }
}
