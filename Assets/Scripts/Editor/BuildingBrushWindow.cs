#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Brush de edificios para el Terrain.  Menú: Tools > Brush de Edificios
/// Click izq. (arrastrar) = pintar   |   Ctrl + click = borrar   |   Alt = mover la vista normalmente
public class BuildingBrushWindow : EditorWindow
{
    [SerializeField] List<GameObject> prefabs = new List<GameObject>();
    Terrain terrain;
    Transform parent;
    bool paintMode;
    float radius = 25f, minSpacing = 16f, maxSlope = 18f, sink = 0.2f, roadClearance = 6f;
    int perStroke = 3, roadLayerIndex = -1;
    Vector2 scaleRange = new Vector2(0.9f, 1.15f);
    bool snapYaw90 = true;

    [MenuItem("Tools/Brush de Edificios")]
    static void Open() => GetWindow<BuildingBrushWindow>("Brush de Edificios");

    void OnEnable() { SceneView.duringSceneGui += OnScene; }
    void OnDisable() { SceneView.duringSceneGui -= OnScene; }

    void OnGUI()
    {
        var so = new SerializedObject(this);
        so.Update();
        EditorGUILayout.PropertyField(so.FindProperty("prefabs"), new GUIContent("Prefabs de edificios"), true);
        so.ApplyModifiedProperties();

        terrain = (Terrain)EditorGUILayout.ObjectField("Terrain", terrain, typeof(Terrain), true);
        parent = (Transform)EditorGUILayout.ObjectField("Padre (opcional)", parent, typeof(Transform), true);
        radius = EditorGUILayout.Slider("Radio del brush", radius, 2f, 120f);
        perStroke = EditorGUILayout.IntSlider("Intentos por trazo", perStroke, 1, 20);
        minSpacing = EditorGUILayout.Slider("Separación mínima", minSpacing, 2f, 60f);
        maxSlope = EditorGUILayout.Slider("Pendiente máxima (°)", maxSlope, 0f, 60f);
        sink = EditorGUILayout.Slider("Hundir en el suelo", sink, 0f, 2f);
        scaleRange = EditorGUILayout.Vector2Field("Escala (mín, máx)", scaleRange);
        snapYaw90 = EditorGUILayout.Toggle("Rotar en pasos de 90°", snapYaw90);
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Evitar la carretera", EditorStyles.boldLabel);
        roadLayerIndex = EditorGUILayout.IntField("Índice de Terrain Layer de asfalto (-1 = off)", roadLayerIndex);
        roadClearance = EditorGUILayout.Slider("Margen a la carretera (m)", roadClearance, 0f, 40f);
        EditorGUILayout.Space();

        GUI.backgroundColor = paintMode ? new Color(0.4f, 1f, 0.5f) : Color.white;
        if (GUILayout.Button(paintMode ? "PINTANDO (clic para desactivar)" : "Activar pincel", GUILayout.Height(32)))
        {
            paintMode = !paintMode;
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.HelpBox("Clic izq.: pintar · Ctrl+clic: borrar · Alt: orbitar cámara.\nUsa prefabs con collider y escala 1 u = 1 m.", MessageType.Info);
    }

    void OnScene(SceneView sv)
    {
        if (!paintMode) return;
        if (!terrain) terrain = Terrain.activeTerrain;
        if (!terrain) return;

        Event e = Event.current;
        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        if (e.alt) return;

        var col = terrain.GetComponent<TerrainCollider>();
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (!col || !col.Raycast(ray, out RaycastHit hit, 8000f)) return;

        bool erase = e.control || e.command;
        Handles.color = erase ? new Color(1f, 0.25f, 0.2f) : new Color(0.25f, 1f, 0.5f);
        Handles.DrawWireDisc(hit.point, hit.normal, radius);
        if (e.type == EventType.MouseMove) sv.Repaint();

        if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
        {
            if (erase) Erase(hit.point); else Paint(hit.point);
            e.Use();
        }
    }

    Transform Parent()
    {
        if (parent) return parent;
        var go = GameObject.Find("Edificios");
        if (!go) { go = new GameObject("Edificios"); Undo.RegisterCreatedObjectUndo(go, "Crear padre"); }
        parent = go.transform;
        return parent;
    }

    void Paint(Vector3 center)
    {
        var valid = prefabs.FindAll(p => p != null);
        if (valid.Count == 0) return;
        TerrainData td = terrain.terrainData;
        Vector3 tp = terrain.transform.position;
        Transform par = Parent();

        for (int i = 0; i < perStroke; i++)
        {
            Vector2 r = Random.insideUnitCircle * radius;
            Vector3 p = center + new Vector3(r.x, 0f, r.y);
            float u = (p.x - tp.x) / td.size.x, v = (p.z - tp.z) / td.size.z;
            if (u < 0f || u > 1f || v < 0f || v > 1f) continue;

            p.y = terrain.SampleHeight(p) + tp.y;
            if (Vector3.Angle(td.GetInterpolatedNormal(u, v), Vector3.up) > maxSlope) continue;
            if (NearRoad(td, u, v)) continue;
            if (TooClose(par, p)) continue;

            var prefab = valid[Random.Range(0, valid.Count)];
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, par);
            go.transform.position = p + Vector3.down * sink;
            go.transform.rotation = Quaternion.Euler(0f, snapYaw90 ? 90f * Random.Range(0, 4) : Random.Range(0f, 360f), 0f);
            go.transform.localScale = Vector3.one * Random.Range(scaleRange.x, scaleRange.y);
            go.isStatic = true;
            Undo.RegisterCreatedObjectUndo(go, "Pintar edificio");
        }
    }

    void Erase(Vector3 center)
    {
        var par = Parent();
        for (int i = par.childCount - 1; i >= 0; i--)
        {
            Vector3 d = par.GetChild(i).position - center; d.y = 0f;
            if (d.magnitude < radius) Undo.DestroyObjectImmediate(par.GetChild(i).gameObject);
        }
    }

    bool TooClose(Transform par, Vector3 p)
    {
        float sq = minSpacing * minSpacing;
        for (int i = 0; i < par.childCount; i++)
        {
            Vector3 d = par.GetChild(i).position - p; d.y = 0f;
            if (d.sqrMagnitude < sq) return true;
        }
        return false;
    }

    bool NearRoad(TerrainData td, float u, float v)
    {
        if (roadLayerIndex < 0 || roadLayerIndex >= td.alphamapLayers) return false;
        int ar = td.alphamapResolution;
        int steps = roadClearance > 0.1f ? 8 : 1;
        for (int k = -1; k < steps; k++)
        {
            float ang = k < 0 ? 0f : k * Mathf.PI * 2f / steps;
            float off = k < 0 ? 0f : roadClearance;
            int x = Mathf.Clamp(Mathf.RoundToInt((u + Mathf.Cos(ang) * off / td.size.x) * ar), 0, ar - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt((v + Mathf.Sin(ang) * off / td.size.z) * ar), 0, ar - 1);
            if (td.GetAlphamaps(x, z, 1, 1)[0, 0, roadLayerIndex] > 0.3f) return true;
        }
        return false;
    }
}
#endif
