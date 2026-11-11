using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// HUD estilo Apex/Titanfall: capas con parallax que se mueven con resorte
/// según fuerza G lateral/longitudinal, choques y cambios de marcha.
public class RacingHUD : MonoBehaviour
{
    [System.Serializable]
    public class Layer
    {
        public RectTransform rect;
        public float depth = 1f;          // cuánto se mueve (capas "cercanas" más, lejanas menos)
        public float rollAmount = 0f;     // cuánto se inclina
        [HideInInspector] public Vector2 basePos;
    }

    [Header("Fuentes")]
    public Rigidbody car;
    public Gearbox gearbox;
    public LapManager lapManager;

    [Header("Velocímetro")]
    public TMP_Text speedText, gearText, rpmText;
    public RectTransform speedNeedle, rpmNeedle;
    public Image rpmBar;                              // Image tipo Filled (radial)
    public float rpmBarMaxFill = 0.75f;               // 0.75 = arco de 270°
    public float maxDisplaySpeed = 260f, maxRpm = 8000f;
    public float speedAngleMin = 130f, speedAngleMax = -130f;
    public float rpmAngleMin = 130f, rpmAngleMax = -130f;

    [Header("Vueltas")]
    public TMP_Text lapText, timeText, bestText, lastText, countdownText;

    [Header("Reacción del HUD")]
    public Layer[] layers;
    public float stiffness = 70f, damping = 9f, maxOffset = 45f;
    public float swayLateral = 18f, swayLongitudinal = 14f;
    public float impactForce = 900f, rollMax = 3.5f;

    [Header("Efectos")]
    public Image speedLines;                          // overlay con líneas radiales
    public float speedLinesStart = 140f, speedLinesMax = 240f;
    public Image impactFlash;                         // overlay rojo/blanco a pantalla completa
    public float flashMax = 0.4f;

    Vector2 pos, vel;
    float roll, rollVel, prevFwd, longSmooth, shiftPunch, flash;

    void Awake()
    {
        foreach (var l in layers) if (l.rect) l.basePos = l.rect.anchoredPosition;
    }

    void OnEnable()
    {
        CarEvents.Impact += OnImpact;
        CarEvents.GearShift += OnShift;
    }
    void OnDisable()
    {
        CarEvents.Impact -= OnImpact;
        CarEvents.GearShift -= OnShift;
    }

    void OnImpact(float k, Vector3 dir)
    {
        vel += new Vector2(dir.x, dir.z - 0.4f) * k * impactForce;
        rollVel += (Random.value > 0.5f ? 1f : -1f) * k * 60f;
        flash = Mathf.Max(flash, k * flashMax);
    }

    void OnShift(int from, int to)
    {
        shiftPunch = 1f;
        vel += new Vector2(0f, -140f);               // pequeño "tirón" al cambiar
    }

    void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        Vector3 v = CarUtil.Vel(car);
        float fwd = Vector3.Dot(v, car.transform.forward);
        float kmh = Mathf.Abs(fwd) * 3.6f;

        // G lateral (centrípeta) y longitudinal
        float latG = car.angularVelocity.y * fwd / 9.81f;
        float longG = (fwd - prevFwd) / Mathf.Max(dt, 0.0001f) / 9.81f;
        prevFwd = fwd;
        longSmooth = Mathf.Lerp(longSmooth, longG, 10f * dt);

        Vector2 target = new Vector2(-latG * swayLateral, -longSmooth * swayLongitudinal);
        float rollTarget = -latG * rollMax;

        // Resorte-amortiguador
        Vector2 acc = (target - pos) * stiffness - vel * damping;
        vel += acc * dt;
        pos += vel * dt;
        pos = Vector2.ClampMagnitude(pos, maxOffset);

        float rAcc = (rollTarget - roll) * stiffness - rollVel * damping;
        rollVel += rAcc * dt;
        roll = Mathf.Clamp(roll + rollVel * dt, -rollMax * 2f, rollMax * 2f);

        foreach (var l in layers)
        {
            if (!l.rect) continue;
            l.rect.anchoredPosition = l.basePos + pos * l.depth;
            l.rect.localRotation = Quaternion.Euler(0f, 0f, roll * l.rollAmount);
        }

        // Velocímetro
        if (speedText) speedText.text = Mathf.RoundToInt(kmh).ToString();
        if (gearbox)
        {
            if (gearText)
            {
                gearText.text = gearbox.GearLabel;
                shiftPunch = Mathf.MoveTowards(shiftPunch, 0f, dt * 5f);
                gearText.transform.localScale = Vector3.one * (1f + shiftPunch * 0.45f);
            }
            if (rpmText) rpmText.text = (Mathf.RoundToInt(gearbox.RPM / 10f) * 10).ToString();
            float rn = Mathf.Clamp01(gearbox.RPM / maxRpm);
            if (rpmNeedle) rpmNeedle.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(rpmAngleMin, rpmAngleMax, rn));
            if (rpmBar) rpmBar.fillAmount = rn * rpmBarMaxFill;
        }
        if (speedNeedle)
            speedNeedle.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(speedAngleMin, speedAngleMax, Mathf.Clamp01(kmh / maxDisplaySpeed)));

        // Líneas de velocidad e impacto
        if (speedLines)
        {
            var c = speedLines.color;
            c.a = Mathf.Lerp(c.a, Mathf.InverseLerp(speedLinesStart, speedLinesMax, kmh) * 0.55f, 6f * dt);
            speedLines.color = c;
        }
        if (impactFlash)
        {
            flash = Mathf.MoveTowards(flash, 0f, dt * 1.8f);
            var c = impactFlash.color; c.a = flash; impactFlash.color = c;
        }

        // Vueltas
        if (lapManager)
        {
            if (lapText) lapText.text = $"VUELTA {Mathf.Min(lapManager.CurrentLap, lapManager.totalLaps)}/{lapManager.totalLaps}";
            if (timeText) timeText.text = Fmt(lapManager.LapTime);
            if (bestText) bestText.text = "MEJOR " + (lapManager.BestLap < 9e8f ? Fmt(lapManager.BestLap) : "--:--.---");
            if (lastText) lastText.text = "ÚLTIMA " + (lapManager.LastLap > 0 ? Fmt(lapManager.LastLap) : "--:--.---");
            if (countdownText) countdownText.text = lapManager.CountdownLabel;
        }
    }

    static string Fmt(float t)
    {
        int m = (int)(t / 60f);
        return $"{m}:{(t % 60f):00.000}";
    }
}
