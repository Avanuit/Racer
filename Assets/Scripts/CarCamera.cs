using UnityEngine;

/// Cámara de persecución: FOV que crece con la velocidad, se aleja, se inclina en curvas y tiembla en choques.
[RequireComponent(typeof(Camera))]
public class CarCamera : MonoBehaviour
{
    public Transform target;
    public Rigidbody targetRb;
    public float maxSpeedRef = 220f;                 // km/h

    [Header("Posición")]
    public float distance = 6.2f, height = 2.0f, lookHeight = 1.0f, lookAhead = 3f;
    public float speedPullBack = 1.8f;
    public float followSmooth = 10f, headingSmooth = 4f;

    [Header("FOV")]
    public float fovMin = 68f, fovMax = 98f, fovSmooth = 3f;
    public AnimationCurve fovCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Inclinación y choques")]
    public float rollFromTurn = 1.1f, maxRoll = 5f;      // si la inclinación se siente al revés, usa rollFromTurn negativo
    public float shakeMax = 0.3f, shakeDecay = 1.6f;

    Camera cam;
    Vector3 heading, smoothPos;
    float trauma, fovKick, roll;

    void Awake()
    {
        cam = GetComponent<Camera>();
        heading = target.forward;
        smoothPos = transform.position;
    }

    void OnEnable() { CarEvents.Impact += OnImpact; }
    void OnDisable() { CarEvents.Impact -= OnImpact; }

    void OnImpact(float k, Vector3 dir)
    {
        trauma = Mathf.Clamp01(trauma + k);
        fovKick += k * 7f;
    }

    void LateUpdate()
    {
        if (!target) return;
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        Vector3 vel = CarUtil.Vel(targetRb);
        float kmh = vel.magnitude * 3.6f;
        float n = Mathf.Clamp01(kmh / maxSpeedRef);

        // Dirección de la cámara: mezcla entre hacia dónde apunta el carro y hacia dónde se mueve
        Vector3 flatFwd = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
        Vector3 desired = flatFwd;
        Vector3 flatVel = Vector3.ProjectOnPlane(vel, Vector3.up);
        if (flatVel.magnitude > 6f && Vector3.Dot(flatVel, flatFwd) > 0f)
            desired = Vector3.Slerp(flatFwd, flatVel.normalized, 0.5f);
        heading = Vector3.Slerp(heading, desired, 1f - Mathf.Exp(-headingSmooth * dt)).normalized;

        Vector3 wantPos = target.position - heading * (distance + speedPullBack * n) + Vector3.up * height;
        smoothPos = Vector3.Lerp(smoothPos, wantPos, 1f - Mathf.Exp(-followSmooth * dt));

        Vector3 look = target.position + Vector3.up * lookHeight + heading * lookAhead;
        Quaternion rot = Quaternion.LookRotation(look - smoothPos, Vector3.up);

        // Inclinación en curva
        float rollTarget = Mathf.Clamp(-targetRb.angularVelocity.y * rollFromTurn * Mathf.Lerp(0.3f, 1f, n) * 3f, -maxRoll, maxRoll);
        roll = Mathf.Lerp(roll, rollTarget, 1f - Mathf.Exp(-5f * dt));

        // Shake (trauma²)
        trauma = Mathf.MoveTowards(trauma, 0f, shakeDecay * dt);
        float s = trauma * trauma;
        float t = Time.time * 28f;
        Vector3 off = new Vector3(Mathf.PerlinNoise(t, 0f) - 0.5f, Mathf.PerlinNoise(0f, t) - 0.5f, 0f) * 2f * shakeMax * s;
        float rz = (Mathf.PerlinNoise(t, t) - 0.5f) * 2f * 4f * s;

        transform.rotation = rot * Quaternion.Euler(0f, 0f, roll + rz);
        transform.position = smoothPos + transform.TransformDirection(off);

        // FOV
        fovKick = Mathf.Lerp(fovKick, 0f, dt * 6f);
        float targetFov = Mathf.Lerp(fovMin, fovMax, fovCurve.Evaluate(n)) + fovKick;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 1f - Mathf.Exp(-fovSmooth * dt));
    }
}
