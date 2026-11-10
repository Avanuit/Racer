using UnityEngine;

/// Capa de "sensación de manejo" sobre PrometeoCarController.
/// Dirección que se endurece con la velocidad, downforce, asistencia al derrape y centro de masa bajo.
[RequireComponent(typeof(Rigidbody))]
public class CarFeel : MonoBehaviour
{
    public PrometeoCarController pcc;

    [Header("Dirección según velocidad")]
    public int steerLowSpeed = 32;
    public int steerHighSpeed = 8;
    public AnimationCurve steerCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.35f, 0.65f), new Keyframe(1f, 1f));

    [Header("Estabilidad")]
    public Vector3 centerOfMass = new Vector3(0f, -0.5f, 0.15f);
    public float downforceCoef = 1.2f;      // F = coef * v²
    public float angularDrag = 1.5f;

    [Header("Asistencia (recuperar el carro tras un derrape)")]
    public float driftAssist = 3f;
    public float assistMinSpeed = 12f;      // m/s

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (!pcc) pcc = GetComponent<PrometeoCarController>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;   // cámara sin jitter
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        CarUtil.SetAngularDrag(rb, angularDrag);
        if (pcc) pcc.bodyMassCenter = centerOfMass;               // PCC lo aplica en su Start()
    }

    void FixedUpdate()
    {
        if (!pcc) return;
        Vector3 v = CarUtil.Vel(rb);
        float speed = v.magnitude;
        float n = Mathf.Clamp01(speed * 3.6f / Mathf.Max(1, pcc.maxSpeed));

        // Dirección progresiva: giro amplio a baja velocidad, preciso a alta
        pcc.maxSteeringAngle = Mathf.RoundToInt(Mathf.Lerp(steerLowSpeed, steerHighSpeed, steerCurve.Evaluate(n)));

        // Downforce aerodinámico
        rb.AddForce(-transform.up * downforceCoef * speed * speed, ForceMode.Force);

        // Asistencia: alinea suavemente la nariz con la dirección de movimiento (no actúa con freno de mano)
        if (speed > assistMinSpeed && !Input.GetKey(KeyCode.Space))
        {
            Vector3 flatV = Vector3.ProjectOnPlane(v, transform.up);
            if (Vector3.Dot(flatV, transform.forward) > 0f)
            {
                float ang = Vector3.SignedAngle(transform.forward, flatV, transform.up) * Mathf.Deg2Rad;
                rb.AddTorque(transform.up * ang * driftAssist, ForceMode.Acceleration);
            }
        }
    }
}
