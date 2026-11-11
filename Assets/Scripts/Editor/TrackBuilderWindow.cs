#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Genera un circuito completo con un clic:  Tools > Track Builder
///  - Terrain con colinas, montañas en los bordes y la pista ya "tallada" con subidas y bajadas
///  - Road cerrada (waypoints + TerrainRoadPainter listo para volver a pintar)
///  - 3 Terrain Layers placeholder (Pasto, Roca, Asfalto) ya pintadas: solo cambias las texturas
///  - Checkpoints + LapManager configurados, línea de meta y punto de salida; coloca el carro
/// Al volver a generar se conservan las texturas que le hayas puesto a los Terrain Layers.
public class TrackBuilderWindow : EditorWindow
{
    // Semilla
    int seed = 1234;

    // Terreno
    float terrainSize = 1000f, terrainHeight = 150f;
    int heightmapRes = 513, alphamapRes = 512;
    float baseHeight = 50f, hillAmplitude = 35f, hillScale = 3f, edgeRidge = 45f;

    // Pista
    float trackRadius = 220f, trackWobble = 0.28f, stretchX = 1.2f, stretchZ = 0.85f;
    float roadWidth = 12f, flatVerge = 3f, blendDistance = 45f, elevationRange = 12f;
    int checkpointCount = 8, waypointCount = 16, totalLaps = 3;

    // Opciones
    bool placeCar = true, disablePiso = true;

    const string Folder = "Assets/Track/Generated";
    const int N = 480;                         // muestras del trazado

    [MenuItem("Tools/Track Builder")]
    static void Open() => GetWindow<TrackBuilderWindow>("Track Builder");

    Vector2 scroll;

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("Semilla", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        seed = EditorGUILayout.IntField("Semilla (misma = mismo circuito)", seed);
        if (GUILayout.Button("Aleatoria", GUILayout.Width(80))) seed = Random.Range(1, 99999);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Terreno", EditorStyles.boldLabel);
        terrainSize = EditorGUILayout.Slider("Tamaño (m)", terrainSize, 500f, 2000f);
        terrainHeight = EditorGUILayout.Slider("Altura máxima (m)", terrainHeight, 80f, 400f);
        baseHeight = EditorGUILayout.Slider("Altura base (m)", baseHeight, 20f, terrainHeight * 0.6f);
        hillAmplitude = EditorGUILayout.Slider("Amplitud de colinas (m)", hillAmplitude, 0f, 100f);
        hillScale = EditorGUILayout.Slider("Frecuencia de colinas", hillScale, 1f, 8f);
        edgeRidge = EditorGUILayout.Slider("Montañas en los bordes (m)", edgeRidge, 0f, 100f);
        heightmapRes = EditorGUILayout.IntPopup("Resolución heightmap", heightmapRes,
            new[] { "257", "513", "1025" }, new[] { 257, 513, 1025 });

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Pista", EditorStyles.boldLabel);
        trackRadius = EditorGUILayout.Slider("Radio base (m)", trackRadius, 100f, 400f);
        trackWobble = EditorGUILayout.Slider("Curvas (irregularidad)", trackWobble, 0f, 0.4f);
        stretchX = EditorGUILayout.Slider("Estirar en X", stretchX, 0.7f, 1.6f);
        stretchZ = EditorGUILayout.Slider("Estirar en Z", stretchZ, 0.7f, 1.6f);
        roadWidth = EditorGUILayout.Slider("Ancho de la carretera (m)", roadWidth, 8f, 24f);
        elevationRange = EditorGUILayout.Slider("Subidas y bajadas (± m)", elevationRange, 0f, 30f);
        blendDistance = EditorGUILayout.Slider("Transición al terreno (m)", blendDistance, 15f, 100f);
        flatVerge = EditorGUILayout.Slider("Banqueta plana a los lados (m)", flatVerge, 0f, 10f);
        checkpointCount = EditorGUILayout.IntSlider("Checkpoints", checkpointCount, 4, 24);
        totalLaps = EditorGUILayout.IntSlider("Vueltas", totalLaps, 1, 10);

        EditorGUILayout.Space();
        placeCar = EditorGUILayout.Toggle("Colocar el carro en la salida", placeCar);
        disablePiso = EditorGUILayout.Toggle("Desactivar objeto 'piso'", disablePiso);

        EditorGUILayout.Space();
        GUI.backgroundColor = new Color(0.45f, 1f, 0.55f);
        if (GUILayout.Button("GENERAR CIRCUITO", GUILayout.Height(36))) Generate();
        GUI.backgroundColor = new Color(1f, 0.5f, 0.45f);
        if (GUILayout.Button("Borrar circuito generado")) DeleteExisting();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.HelpBox(
            "Las texturas placeholder quedan en Assets/Track/Generated (TerrainLayer_Pasto/Roca/Asfalto). " +
            "Cámbiales el Diffuse/Normal y se actualiza todo el terreno.\n" +
            "En Tools > Brush de Edificios usa el índice de capa 2 (asfalto).", MessageType.Info);
        EditorGUILayout.EndScrollView();
    }

    // ------------------------------------------------------------------ generar
    void Generate()
    {
        try
        {
            DeleteExisting();
            var rng = new System.Random(seed);
            float P() => (float)(rng.NextDouble() * Mathf.PI * 2.0);
            float p1 = P(), p2 = P(), p3 = P(), p4 = P(), p5 = P();
            float ox = (float)rng.NextDouble() * 1000f, oz = (float)rng.NextDouble() * 1000f;
            float half = terrainSize * 0.5f;

            // 1) Trazado cerrado (coordenadas relativas al centro del terreno) + elevación
            var xz = new Vector2[N];
            var elev = new float[N];
            float maxExtent = 0f;
            for (int i = 0; i < N; i++)
            {
                float a = i / (float)N * Mathf.PI * 2f;
                float r = 1f + trackWobble * (0.5f * Mathf.Sin(2f * a + p1) + 0.3f * Mathf.Sin(3f * a + p2) + 0.2f * Mathf.Sin(5f * a + p3));
                xz[i] = new Vector2(trackRadius * stretchX * r * Mathf.Cos(a), trackRadius * stretchZ * r * Mathf.Sin(a));
                float prof = 0.6f * Mathf.Sin(a + p4) + 0.4f * Mathf.Sin(2f * a + p5);
                elev[i] = baseHeight + elevationRange * prof;
                maxExtent = Mathf.Max(maxExtent, Mathf.Abs(xz[i].x), Mathf.Abs(xz[i].y));
            }
            if (maxExtent > half * 0.72f)
                Debug.LogWarning($"Track Builder: la pista llega a {maxExtent:F0} m del centro; reduce el radio/estiramiento o aumenta el tamaño del terreno.");

            float maxGrade = 0f;
            for (int i = 0; i < N; i++)
            {
                int j = (i + 1) % N;
                float g = Mathf.Abs(elev[j] - elev[i]) / Mathf.Max(0.01f, Vector2.Distance(xz[i], xz[j]));
                maxGrade = Mathf.Max(maxGrade, g);
            }
            if (maxGrade > 0.12f)
                Debug.LogWarning($"Track Builder: pendiente máxima de la pista {maxGrade * 100f:F0}% (muy inclinada). Reduce 'Subidas y bajadas'.");

            // 2) Campo de distancia a la pista + elevación de la pista más cercana
            int hr = heightmapRes;
            var distMap = new float[hr, hr];
            var elevMap = new float[hr, hr];
            for (int z = 0; z < hr; z++)
            {
                if (z % 16 == 0) EditorUtility.DisplayProgressBar("Track Builder", "Calculando distancia a la pista…", z / (float)hr * 0.5f);
                float wz = -half + z / (float)(hr - 1) * terrainSize;
                for (int x = 0; x < hr; x++)
                {
                    float wx = -half + x / (float)(hr - 1) * terrainSize;
                    float best = float.MaxValue, bestE = baseHeight;
                    for (int j = 0; j < N; j++)
                    {
                        Vector2 a = xz[j], b = xz[(j + 1) % N];
                        float abx = b.x - a.x, abz = b.y - a.y;
                        float t = ((wx - a.x) * abx + (wz - a.y) * abz) / (abx * abx + abz * abz);
                        t = t < 0f ? 0f : (t > 1f ? 1f : t);
                        float dx = wx - (a.x + abx * t), dz = wz - (a.y + abz * t);
                        float d2 = dx * dx + dz * dz;
                        if (d2 < best) { best = d2; bestE = Mathf.Lerp(elev[j], elev[(j + 1) % N], t); }
                    }
                    distMap[z, x] = Mathf.Sqrt(best);
                    elevMap[z, x] = bestE;
                }
            }

            // 3) Alturas
            EditorUtility.DisplayProgressBar("Track Builder", "Esculpiendo el terreno…", 0.55f);
            var heights = new float[hr, hr];
            float roadHalf = roadWidth * 0.5f;
            for (int z = 0; z < hr; z++)
            {
                float wz = -half + z / (float)(hr - 1) * terrainSize;
                for (int x = 0; x < hr; x++)
                {
                    float wx = -half + x / (float)(hr - 1) * terrainSize;
                    float d = distMap[z, x];
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(roadHalf + flatVerge, roadHalf + flatVerge + blendDistance, d));
                    float ampF = Mathf.Lerp(0.35f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(40f, 200f, d)));
                    float n = Fbm(wx, wz, ox, oz);
                    float edge = Mathf.InverseLerp(half * 0.78f, half, Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wz)));
                    float ridge = edgeRidge * Mathf.SmoothStep(0f, 1f, edge) * (0.6f + n);
                    float hills = baseHeight + Mathf.Clamp((n - 0.5f) * 2.4f, -1f, 1f) * hillAmplitude * ampF + ridge;
                    heights[z, x] = Mathf.Clamp01(Mathf.Lerp(elevMap[z, x], hills, t) / terrainHeight);
                }
            }

            // 4) TerrainData + capas
            EditorUtility.DisplayProgressBar("Track Builder", "Creando Terrain…", 0.7f);
            EnsureFolder();
            string tdPath = Folder + "/CircuitoTerrainData.asset";
            AssetDatabase.DeleteAsset(tdPath);
            var td = new TerrainData();
            td.heightmapResolution = hr;
            td.size = new Vector3(terrainSize, terrainHeight, terrainSize);
            td.alphamapResolution = alphamapRes;
            AssetDatabase.CreateAsset(td, tdPath);
            td.SetHeights(0, 0, heights);

            TerrainLayer grass = LoadOrCreateLayer("Pasto", new Color(0.30f, 0.45f, 0.22f), 10f);
            TerrainLayer rock = LoadOrCreateLayer("Roca", new Color(0.42f, 0.40f, 0.38f), 14f);
            TerrainLayer road = LoadOrCreateLayer("Asfalto", new Color(0.16f, 0.16f, 0.18f), 6f);
            td.terrainLayers = new[] { grass, rock, road };    // índice 0 pasto, 1 roca, 2 asfalto

            // 5) Splatmap: asfalto por distancia, roca por pendiente, pasto el resto
            EditorUtility.DisplayProgressBar("Track Builder", "Pintando texturas…", 0.8f);
            int ar = alphamapRes;
            var alpha = new float[ar, ar, 3];
            for (int z = 0; z < ar; z++)
                for (int x = 0; x < ar; x++)
                {
                    float u = (x + 0.5f) / ar, v = (z + 0.5f) / ar;
                    float d = distMap[Mathf.Clamp(Mathf.RoundToInt(v * (hr - 1)), 0, hr - 1), Mathf.Clamp(Mathf.RoundToInt(u * (hr - 1)), 0, hr - 1)];
                    float wRoad = d <= roadHalf ? 1f : 1f - Mathf.InverseLerp(roadHalf, roadHalf + 1.2f, d);
                    wRoad = Mathf.Clamp01(wRoad);
                    float wRock = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(22f, 38f, td.GetSteepness(u, v)));
                    float rest = 1f - wRoad;
                    alpha[z, x, 2] = wRoad;
                    alpha[z, x, 1] = rest * wRock;
                    alpha[z, x, 0] = rest * (1f - wRock);
                }
            td.SetAlphamaps(0, 0, alpha);

            // 6) Objetos de escena
            EditorUtility.DisplayProgressBar("Track Builder", "Creando objetos…", 0.92f);
            var root = new GameObject("Circuito");
            Undo.RegisterCreatedObjectUndo(root, "Generar circuito");

            GameObject terrainGO = Terrain.CreateTerrainGameObject(td);
            terrainGO.name = "Terrain";
            terrainGO.transform.SetParent(root.transform, false);
            terrainGO.transform.position = new Vector3(-half, 0f, -half);
            var terrain = terrainGO.GetComponent<Terrain>();

            // Road + waypoints
            var roadGO = new GameObject("Road");
            roadGO.transform.SetParent(root.transform, false);
            var wps = new Transform[waypointCount];
            for (int k = 0; k < waypointCount; k++)
            {
                int idx = k * N / waypointCount;
                var wp = new GameObject($"WP_{k:00}").transform;
                wp.SetParent(roadGO.transform, false);
                wp.position = new Vector3(xz[idx].x, elev[idx], xz[idx].y);
                wps[k] = wp;
            }
            var painter = roadGO.AddComponent<TerrainRoadPainter>();
            painter.terrain = terrain;
            painter.waypoints = wps;
            painter.closedLoop = true;
            painter.roadWidth = roadWidth;
            painter.shoulder = Mathf.Min(blendDistance * 0.25f, 15f);
            painter.roadLayer = road;
            painter.flatten = false;            // el terreno ya viene tallado; actívalo si mueves los waypoints
            painter.snapToTerrain = false;
            painter.smoothingWindow = 10;

            // Checkpoints
            var cpParent = new GameObject("Checkpoints").transform;
            cpParent.SetParent(root.transform, false);
            var cps = new Checkpoint[checkpointCount];
            for (int k = 0; k < checkpointCount; k++)
            {
                int idx = k * N / checkpointCount;
                Quaternion rot = Quaternion.LookRotation(FlatTangent(xz, idx), Vector3.up);
                var go = new GameObject(k == 0 ? "Meta_Checkpoint_00" : $"Checkpoint_{k:00}");
                go.transform.SetParent(cpParent, false);
                go.transform.SetPositionAndRotation(new Vector3(xz[idx].x, elev[idx], xz[idx].y), rot);
                var bc = go.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.size = new Vector3(roadWidth + 10f, 8f, 3f);
                bc.center = new Vector3(0f, 4f, 0f);
                cps[k] = go.AddComponent<Checkpoint>();
            }

            // Línea de meta (placeholder para que le pongas tu textura/prefab)
            var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = "LineaMeta (cambia el material)";
            Object.DestroyImmediate(line.GetComponent<Collider>());
            line.transform.SetParent(root.transform, false);
            line.transform.SetPositionAndRotation(new Vector3(xz[0].x, elev[0] + 0.03f, xz[0].y),
                Quaternion.LookRotation(FlatTangent(xz, 0), Vector3.up));
            line.transform.localScale = new Vector3(roadWidth, 0.05f, 1.5f);

            // Salida
            int spawnIdx = (N - 3) % N;
            Quaternion spawnRot = Quaternion.LookRotation(FlatTangent(xz, spawnIdx), Vector3.up);
            var spawn = new GameObject("SpawnPoint");
            spawn.transform.SetParent(root.transform, false);
            spawn.transform.SetPositionAndRotation(new Vector3(xz[spawnIdx].x, elev[spawnIdx] + 1f, xz[spawnIdx].y), spawnRot);

            // LapManager
            var race = new GameObject("Race");
            race.transform.SetParent(root.transform, false);
            var lm = race.AddComponent<LapManager>();
            lm.checkpoints = cps;
            lm.totalLaps = totalLaps;

            var carPcc = FindPcc();
            if (carPcc)
            {
                lm.pcc = carPcc;
                lm.car = carPcc.GetComponent<Rigidbody>();
                if (placeCar)
                {
                    Undo.RecordObject(carPcc.transform, "Colocar carro");
                    carPcc.transform.SetPositionAndRotation(spawn.transform.position, spawnRot);
                }
            }
            else Debug.LogWarning("Track Builder: no encontré un PrometeoCarController en la escena; asigna el carro en 'Race > LapManager' y colócalo en 'SpawnPoint'.");

            var buildings = new GameObject("Edificios");       // padre listo para el Brush de Edificios
            buildings.transform.SetParent(root.transform, false);

            if (disablePiso) { var piso = GameObject.Find("piso"); if (piso) piso.SetActive(false); }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            Debug.Log($"Circuito generado: pendiente máxima {maxGrade * 100f:F1}% | {checkpointCount} checkpoints | semilla {seed}.");
        }
        finally { EditorUtility.ClearProgressBar(); }
    }

    // ------------------------------------------------------------------ utilidades
    float Fbm(float x, float z, float ox, float oz)
    {
        float sum = 0f, amp = 1f, norm = 0f, freq = hillScale / terrainSize;
        for (int o = 0; o < 5; o++)
        {
            sum += amp * Mathf.PerlinNoise(x * freq + ox, z * freq + oz);
            norm += amp; amp *= 0.5f; freq *= 2.1f;
        }
        return sum / norm;
    }

    static Vector3 FlatTangent(Vector2[] p, int i)
    {
        Vector2 d = p[(i + 1) % N] - p[(i - 1 + N) % N];
        return new Vector3(d.x, 0f, d.y).normalized;
    }

    static PrometeoCarController FindPcc()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<PrometeoCarController>();
#else
        return Object.FindObjectOfType<PrometeoCarController>();
#endif
    }

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Track")) AssetDatabase.CreateFolder("Assets", "Track");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Track", "Generated");
    }

    // Reutiliza la capa si ya existe (así no pierdes las texturas que le pusiste)
    static TerrainLayer LoadOrCreateLayer(string name, Color color, float tile)
    {
        string layerPath = $"{Folder}/TerrainLayer_{name}.terrainlayer";
        var existing = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
        if (existing) return existing;

        string texPath = $"{Folder}/Placeholder_{name}.png";
        var tex = new Texture2D(64, 64, TextureFormat.RGB24, false);
        var rnd = new System.Random(name.GetHashCode());
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float n = 0.9f + (float)rnd.NextDouble() * 0.2f;
                tex.SetPixel(x, y, new Color(color.r * n, color.g * n, color.b * n));
            }
        tex.Apply();
        File.WriteAllBytes(texPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(texPath);

        var layer = new TerrainLayer
        {
            diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath),
            tileSize = new Vector2(tile, tile)
        };
        AssetDatabase.CreateAsset(layer, layerPath);
        return layer;
    }

    static void DeleteExisting()
    {
        var old = GameObject.Find("Circuito");
        if (old) Undo.DestroyObjectImmediate(old);
    }
}
#endif
