using UnityEngine;

/// Faros, luces de freno y neón inferior. Los faros se encienden solos de noche (AtmosphereController).
public class CarLights : MonoBehaviour
{
    public Rigidbody car;
    public Light[] headlights;
    public Renderer[] tailRenderers;                 // meshes de las calaveras (el material debe tener Emission)
    public Light[] tailLights;
    public Light[] underglow;
    public Color tailColor = new Color(1f, 0.05f, 0.05f);
    public float tailIdle = 0.8f, tailBrake = 4f;
    public Color underglowColor = new Color(0.1f, 1f, 0.9f);
    public KeyCode headlightKey = KeyCode.L;

    bool manualOverride, headlightsOn;

    void Start()
    {
        foreach (var l in underglow) if (l) { l.color = underglowColor; l.type = LightType.Point; }
        foreach (var r in tailRenderers) if (r) r.material.EnableKeyword("_EMISSION");
    }

    void Update()
    {
        if (Input.GetKeyDown(headlightKey)) { manualOverride = true; headlightsOn = !headlightsOn; }
        if (!manualOverride) headlightsOn = AtmosphereController.Night > 0.35f;
        foreach (var l in headlights) if (l) l.enabled = headlightsOn;

        float fwd = Vector3.Dot(CarUtil.Vel(car), car.transform.forward);
        bool braking = Input.GetKey(KeyCode.Space) || (Input.GetAxis("Vertical") < -0.1f && fwd > 1f);
        float k = braking ? tailBrake : tailIdle;
        foreach (var r in tailRenderers) if (r) r.material.SetColor("_EmissionColor", tailColor * k);
        foreach (var l in tailLights) if (l) { l.color = tailColor; l.intensity = braking ? 2.5f : 0.6f; }
    }
}
