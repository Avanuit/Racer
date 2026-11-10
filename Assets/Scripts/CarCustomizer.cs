using System.Collections.Generic;
using UnityEngine;

/// Identidad visual propia: repinta los materiales de carrocería y agrega un kit de carrocería
/// (alerón, splitter, faldones, franjas) para que el silueta no sea la del modelo original.
/// Click derecho en el componente -> "Aplicar" para ver el resultado en el editor.
public class CarCustomizer : MonoBehaviour
{
    [Header("Pintura")]
    public Renderer[] bodyRenderers;
    public string[] paintMaterialKeywords = { "body", "paint", "carpaint", "car_paint" };
    public Color paintColor = new Color(0.04f, 0.05f, 0.09f);
    public Color accentColor = new Color(0.1f, 1f, 0.85f);
    [Range(0, 1)] public float metallic = 0.85f;
    [Range(0, 1)] public float smoothness = 0.92f;

    [Header("Kit de carrocería")]
    public bool addBodyKit = true;
    public Vector3 spoilerPos = new Vector3(0f, 1.3f, -2.25f);
    public Vector3 splitterPos = new Vector3(0f, 0.18f, 2.45f);
    public Vector3 skirtOffset = new Vector3(0.93f, 0.2f, 0f);
    public float carWidth = 1.95f;

    const string KitName = "BodyKit";

    void Start() { Apply(); }

    [ContextMenu("Aplicar")]
    public void Apply()
    {
        // 1) Pintura
        foreach (var r in bodyRenderers)
        {
            if (!r) continue;
            var mats = Application.isPlaying ? r.materials : r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                string n = mats[i].name.ToLowerInvariant();
                bool match = false;
                foreach (var k in paintMaterialKeywords) if (n.Contains(k)) match = true;
                if (!match) continue;
                SetColor(mats[i], paintColor);
                if (mats[i].HasProperty("_Metallic")) mats[i].SetFloat("_Metallic", metallic);
                if (mats[i].HasProperty("_Smoothness")) mats[i].SetFloat("_Smoothness", smoothness);
                if (mats[i].HasProperty("_Glossiness")) mats[i].SetFloat("_Glossiness", smoothness);
            }
        }

        // 2) Kit
        var old = transform.Find(KitName);
        if (old) { if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject); }
        if (!addBodyKit) return;

        var root = new GameObject(KitName).transform;
        root.SetParent(transform, false);
        Color dark = new Color(0.02f, 0.02f, 0.03f);

        // Alerón (ala + soportes)
        Part(root, "Ala", spoilerPos + Vector3.up * 0.22f, new Vector3(carWidth * 0.95f, 0.05f, 0.45f), dark, 0f, new Vector3(-6f, 0, 0));
        Part(root, "AlaBordeL", spoilerPos + new Vector3(carWidth * 0.48f, 0.22f, 0f), new Vector3(0.04f, 0.28f, 0.5f), accentColor, 3f);
        Part(root, "AlaBordeR", spoilerPos + new Vector3(-carWidth * 0.48f, 0.22f, 0f), new Vector3(0.04f, 0.28f, 0.5f), accentColor, 3f);
        Part(root, "SoporteL", spoilerPos + new Vector3(0.45f, 0.08f, 0f), new Vector3(0.05f, 0.3f, 0.12f), dark);
        Part(root, "SoporteR", spoilerPos + new Vector3(-0.45f, 0.08f, 0f), new Vector3(0.05f, 0.3f, 0.12f), dark);

        // Splitter frontal + faldones
        Part(root, "Splitter", splitterPos, new Vector3(carWidth * 0.98f, 0.04f, 0.45f), dark);
        Part(root, "SplitterNeon", splitterPos + new Vector3(0, 0.025f, 0.2f), new Vector3(carWidth * 0.9f, 0.015f, 0.03f), accentColor, 4f);
        Part(root, "FaldonL", skirtOffset, new Vector3(0.06f, 0.1f, 2.6f), dark);
        Part(root, "FaldonR", new Vector3(-skirtOffset.x, skirtOffset.y, skirtOffset.z), new Vector3(0.06f, 0.1f, 2.6f), dark);
        Part(root, "FaldonNeonL", skirtOffset + new Vector3(0.035f, -0.03f, 0), new Vector3(0.01f, 0.02f, 2.5f), accentColor, 4f);
        Part(root, "FaldonNeonR", new Vector3(-skirtOffset.x - 0.035f, skirtOffset.y - 0.03f, skirtOffset.z), new Vector3(0.01f, 0.02f, 2.5f), accentColor, 4f);

        // Franjas de capó y techo (ajusta Y/Z al modelo)
        Part(root, "FranjaCapoL", new Vector3(0.14f, 1.04f, 1.35f), new Vector3(0.16f, 0.012f, 1.3f), accentColor, 1.5f);
        Part(root, "FranjaCapoR", new Vector3(-0.14f, 1.04f, 1.35f), new Vector3(0.16f, 0.012f, 1.3f), accentColor, 1.5f);
    }

    void Part(Transform root, string name, Vector3 pos, Vector3 scale, Color c, float emission = 0f, Vector3? euler = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        if (Application.isPlaying) Destroy(go.GetComponent<Collider>()); else DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(root, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        if (euler.HasValue) go.transform.localEulerAngles = euler.Value;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) shader = Shader.Find("Standard");
        var m = new Material(shader);
        SetColor(m, c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.8f);
        if (emission > 0f) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * emission); }
        go.GetComponent<Renderer>().sharedMaterial = m;
    }

    static void SetColor(Material m, Color c)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }
}
