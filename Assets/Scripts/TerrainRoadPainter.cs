using System;
using System.Collections.Generic;
using UnityEngine;

/// Crea una carretera sobre el Terrain: aplana el relieve siguiendo una curva (Catmull-Rom)
/// y pinta una Terrain Layer de asfalto. Coloca waypoints (empties) en orden alrededor del circuito.
/// Click derecho en el componente -> "Pintar carretera en Terrain".
public class TerrainRoadPainter : MonoBehaviour
{
    public Terrain terrain;
    public Transform[] waypoints;
    public bool closedLoop = true;

    [Header("Forma")]
    public float roadWidth = 12f;
    public float shoulder = 8f;                  // distancia de transición hacia el terreno natural
    public float edgeBlend = 1.5f;               // difuminado del borde del asfalto
    public float sampleSpacing = 1f;

    [Header("Altura")]
    public bool flatten = true;
    [Range(0f, 1f)] public float flattenStrength = 1f;
    public bool snapToTerrain = true;            // ignora la Y de los waypoints y suaviza la del terreno
    public int smoothingWindow = 25;             // más alto = carretera más suave

    [Header("Textura")]
    public TerrainLayer roadLayer;               // tu material de asfalto como Terrain Layer

    public int RoadLayerIndex { get; private set; } = -1;

    [ContextMenu("Pintar carretera en Terrain")]
    public void Paint()
    {
        if (!terrain || waypoints == null || waypoints.Length < 3 || !roadLayer)
        {
            Debug.LogWarning("TerrainRoadPainter: faltan terrain, roadLayer o al menos 3 waypoints.");
            return;
        }

        TerrainData td = terrain.terrainData;
#if UNITY_EDITOR
        UnityEditor.Undo.RegisterCompleteObjectUndo(td, "Pintar carretera");
#endif
        Vector3 tp = terrain.transform.position;
        Vector3 size = td.size;
        List<Vector3> pts = Sample(tp);

        // ---- Altura ----
        if (flatten)
        {
            int hr = td.heightmapResolution;
            float[,] h = td.GetHeights(0, 0, hr, hr);
            float half = roadWidth * 0.5f;
            float radius = half + shoulder;
            float cell = size.x / (hr - 1);
            int span = Mathf.CeilToInt(radius / cell) + 1;

            foreach (Vector3 s in pts)
            {
                int cx = Mathf.RoundToInt((s.x - tp.x) / size.x * (hr - 1));
                int cz = Mathf.RoundToInt((s.z - tp.z) / size.z * (hr - 1));
                float targetH = (s.y - tp.y) / size.y;
                for (int z = Mathf.Max(0, cz - span); z <= Mathf.Min(hr - 1, cz + span); z++)
                    for (int x = Mathf.Max(0, cx - span); x <= Mathf.Min(hr - 1, cx + span); x++)
                    {
                        float wx = tp.x + (float)x / (hr - 1) * size.x;
                        float wz = tp.z + (float)z / (hr - 1) * size.z;
                        float d = Mathf.Sqrt((wx - s.x) * (wx - s.x) + (wz - s.z) * (wz - s.z));
                        if (d > radius) continue;
                        float w = d <= half ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (d - half) / shoulder);
                        h[z, x] = Mathf.Lerp(h[z, x], targetH, w * flattenStrength);
                    }
            }
            td.SetHeights(0, 0, h);
        }

        // ---- Textura ----
        var layers = new List<TerrainLayer>(td.terrainLayers);
        RoadLayerIndex = layers.IndexOf(roadLayer);
        if (RoadLayerIndex < 0)
        {
            layers.Add(roadLayer);
            td.terrainLayers = layers.ToArray();
            RoadLayerIndex = layers.Count - 1;
        }

        int ar = td.alphamapResolution;
        float[,,] a = td.GetAlphamaps(0, 0, ar, ar);
        int nl = a.GetLength(2);
        float halfW = roadWidth * 0.5f;
        float acell = size.x / ar;
        int aspan = Mathf.CeilToInt((halfW + edgeBlend) / acell) + 1;

        foreach (Vector3 s in pts)
        {
            int cx = Mathf.RoundToInt((s.x - tp.x) / size.x * ar);
            int cz = Mathf.RoundToInt((s.z - tp.z) / size.z * ar);
            for (int z = Mathf.Max(0, cz - aspan); z < Mathf.Min(ar, cz + aspan); z++)
                for (int x = Mathf.Max(0, cx - aspan); x < Mathf.Min(ar, cx + aspan); x++)
                {
                    float wx = tp.x + (x + 0.5f) / ar * size.x;
                    float wz = tp.z + (z + 0.5f) / ar * size.z;
                    float d = Mathf.Sqrt((wx - s.x) * (wx - s.x) + (wz - s.z) * (wz - s.z));
                    if (d > halfW + edgeBlend) continue;
                    float w = d <= halfW ? 1f : 1f - Mathf.InverseLerp(halfW, halfW + edgeBlend, d);
                    for (int l = 0; l < nl; l++)
                        a[z, x, l] = l == RoadLayerIndex ? a[z, x, l] * (1f - w) + w : a[z, x, l] * (1f - w);
                }
        }
        td.SetAlphamaps(0, 0, a);
        terrain.Flush();
        Debug.Log($"Carretera pintada: {pts.Count} muestras, capa #{RoadLayerIndex}.");
    }

    // ---------- Curva ----------
    List<Vector3> Sample(Vector3 tp)
    {
        var list = new List<Vector3>();
        int n = waypoints.Length;
        int segs = closedLoop ? n : n - 1;
        for (int i = 0; i < segs; i++)
        {
            Vector3 p0 = P(i - 1), p1 = P(i), p2 = P(i + 1), p3 = P(i + 2);
            int steps = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(p1, p2) / sampleSpacing));
            for (int s = 0; s < steps; s++) list.Add(CR(p0, p1, p2, p3, s / (float)steps));
        }
        if (!closedLoop) list.Add(P(n - 1));

        if (snapToTerrain)
            for (int i = 0; i < list.Count; i++)
            {
                Vector3 v = list[i];
                v.y = terrain.SampleHeight(v) + tp.y;
                list[i] = v;
            }

        // Media móvil de la altura => carretera suave, sin escalones
        int w = Mathf.Max(0, smoothingWindow);
        if (w > 0)
        {
            var ys = new float[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                float sum = 0; int c = 0;
                for (int k = -w; k <= w; k++)
                {
                    int j = i + k;
                    if (closedLoop) j = ((j % list.Count) + list.Count) % list.Count;
                    else if (j < 0 || j >= list.Count) continue;
                    sum += list[j].y; c++;
                }
                ys[i] = sum / c;
            }
            for (int i = 0; i < list.Count; i++) { var v = list[i]; v.y = ys[i]; list[i] = v; }
        }
        return list;
    }

    Vector3 P(int i)
    {
        int n = waypoints.Length;
        return closedLoop ? waypoints[((i % n) + n) % n].position : waypoints[Mathf.Clamp(i, 0, n - 1)].position;
    }

    static Vector3 CR(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Length < 3) return;
        for (int i = 0; i < waypoints.Length; i++) if (!waypoints[i]) return;
        Gizmos.color = new Color(1f, 0.8f, 0.1f);
        Vector3 prev = P(0);
        int segs = closedLoop ? waypoints.Length : waypoints.Length - 1;
        for (int i = 0; i < segs; i++)
            for (int s = 1; s <= 12; s++)
            {
                Vector3 p = CR(P(i - 1), P(i), P(i + 1), P(i + 2), s / 12f);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
    }
}
