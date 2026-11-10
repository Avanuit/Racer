using UnityEngine;

/// Hora del día: mueve el sol, cambia colores de luz/niebla/ambiente. Atardecer neón por defecto.
[ExecuteAlways]
public class AtmosphereController : MonoBehaviour
{
    public static float Night { get; private set; }   // 0 = día, 1 = noche

    public Light sun;
    [Range(0f, 24f)] public float timeOfDay = 18.2f;
    public float dayLengthMinutes = 0f;                // 0 = hora fija
    public Gradient sunColor, ambientColor, fogColor;
    public AnimationCurve sunIntensity = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.22f, 0.15f), new Keyframe(0.3f, 1f), new Keyframe(0.7f, 1f),
        new Keyframe(0.8f, 0.2f), new Keyframe(1f, 0f));
    public float fogDensity = 0.0025f;

    void Reset()
    {
        sunColor = Make(new Color(0.05f, 0.07f, 0.2f), new Color(1f, 0.45f, 0.2f), new Color(1f, 0.96f, 0.88f));
        ambientColor = Make(new Color(0.04f, 0.05f, 0.14f), new Color(0.45f, 0.32f, 0.45f), new Color(0.5f, 0.6f, 0.75f));
        fogColor = Make(new Color(0.03f, 0.04f, 0.1f), new Color(0.8f, 0.42f, 0.38f), new Color(0.65f, 0.75f, 0.9f));
    }

    static Gradient Make(Color night, Color dusk, Color day)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(night, 0f), new GradientColorKey(dusk, 0.27f), new GradientColorKey(day, 0.5f),
                    new GradientColorKey(dusk, 0.77f), new GradientColorKey(night, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    void Update()
    {
        if (Application.isPlaying && dayLengthMinutes > 0f)
            timeOfDay = (timeOfDay + Time.deltaTime * 24f / (dayLengthMinutes * 60f)) % 24f;
        Apply();
    }

    void Apply()
    {
        float t = timeOfDay / 24f;
        float h = Mathf.Sin((t - 0.25f) * Mathf.PI * 2f);          // >0 de día
        Night = Mathf.Clamp01(Mathf.InverseLerp(0.15f, -0.2f, h));

        if (sun)
        {
            sun.transform.rotation = Quaternion.Euler(t * 360f - 90f, 160f, 0f);
            sun.color = sunColor.Evaluate(t);
            sun.intensity = sunIntensity.Evaluate(t) * 1.3f;
        }
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor.Evaluate(t);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor.Evaluate(t);
        RenderSettings.fogDensity = fogDensity;
    }
}
