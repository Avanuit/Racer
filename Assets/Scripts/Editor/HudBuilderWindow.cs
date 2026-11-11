#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// Genera todo el HUD con un clic:  Tools > HUD Builder
///  - Canvas 1920x1080 con velocímetro (agujas, marcas, arco de RPM, marcha), panel de vueltas, marco,
///    líneas de velocidad, flash de impacto y cuenta regresiva
///  - Todos los elementos ya conectados a RacingHUD (capas con parallax, resorte y reacción a choques)
///  - Sprites procedurales en Assets/HUD/Generated (si los reemplazas, se conservan al regenerar)
///  - Opcional: añade Gearbox/CollisionFeedback al carro y CarCamera a la cámara principal
public class HudBuilderWindow : EditorWindow
{
    Color accent = new Color(0.10f, 1.00f, 0.85f);      // cian
    Color accent2 = new Color(1.00f, 0.20f, 0.55f);     // magenta (aguja)
    Color panelColor = new Color(0.03f, 0.04f, 0.07f, 0.55f);
    int maxDisplaySpeed = 260;
    bool addCarScripts = true, setupCamera = true;

    const string Folder = "Assets/HUD/Generated";
    const string CanvasName = "HUD_Canvas";

    [MenuItem("Tools/HUD Builder")]
    static void Open() => GetWindow<HudBuilderWindow>("HUD Builder");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Identidad visual", EditorStyles.boldLabel);
        accent = EditorGUILayout.ColorField("Color principal", accent);
        accent2 = EditorGUILayout.ColorField("Color de la aguja", accent2);
        panelColor = EditorGUILayout.ColorField("Fondo de paneles", panelColor);
        maxDisplaySpeed = EditorGUILayout.IntPopup("Velocidad máxima del dial (km/h)", maxDisplaySpeed,
            new[] { "180", "220", "260", "300" }, new[] { 180, 220, 260, 300 });
        EditorGUILayout.Space();
        addCarScripts = EditorGUILayout.Toggle("Añadir Gearbox y CollisionFeedback al carro", addCarScripts);
        setupCamera = EditorGUILayout.Toggle("Configurar CarCamera en la cámara principal", setupCamera);
        EditorGUILayout.Space();
        GUI.backgroundColor = new Color(0.45f, 1f, 0.55f);
        if (GUILayout.Button("GENERAR HUD", GUILayout.Height(36))) Generate();
        GUI.backgroundColor = new Color(1f, 0.5f, 0.45f);
        if (GUILayout.Button("Borrar HUD generado")) DeleteExisting();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.HelpBox(
            "Necesita un PrometeoCarController en la escena (y el Track Builder si quieres vueltas).\n" +
            "Si los textos no se ven: Window > TextMeshPro > Import TMP Essential Resources.\n" +
            "Sprites en Assets/HUD/Generated: reemplázalos por los tuyos y se mantienen al regenerar.", MessageType.Info);
    }

    // ------------------------------------------------------------------ generar
    void Generate()
    {
        if (TMP_Settings.instance == null)
        {
            EditorUtility.DisplayDialog("HUD Builder",
                "Faltan los recursos de TextMeshPro.\nVe a Window > TextMeshPro > Import TMP Essential Resources y vuelve a generar.", "OK");
            return;
        }

        var pcc = FindFirst<PrometeoCarController>();
        if (!pcc) { EditorUtility.DisplayDialog("HUD Builder", "No encontré un PrometeoCarController en la escena.", "OK"); return; }
        var rb = pcc.GetComponent<Rigidbody>();

        DeleteExisting();
        EnsureFolder();

        // ---- Sprites ----
        int ticks = maxDisplaySpeed / 20;
        Sprite dial = Sprite_(Folder + $"/Dial_{maxDisplaySpeed}.png", 512, (x, y) => DialAlpha(x, y, ticks));
        Sprite arc = Sprite_(Folder + "/RpmRing.png", 512, (x, y) => Band(Mathf.Sqrt(x * x + y * y), 0.74f, 0.80f, 512));
        Sprite disc = Sprite_(Folder + "/Disc.png", 128, (x, y) => Mathf.Clamp01((1f - Mathf.Sqrt(x * x + y * y)) * 64f));
        Sprite lines = Sprite_(Folder + "/SpeedLines.png", 512, SpeedLinesAlpha);
        Sprite vignette = Sprite_(Folder + "/Vignette.png", 256, (x, y) =>
            Mathf.SmoothStep(0f, 1f, (Mathf.Sqrt(x * x + y * y) - 0.55f) / 0.8f));

        // ---- Canvas ----
        var canvasGO = new GameObject(CanvasName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(canvasGO, "Generar HUD");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        Transform root = canvasGO.transform;

        // Fondo: líneas de velocidad e impacto
        var speedLines = Img("SpeedLines", root, Stretch(), lines, Color.white, Image.Type.Simple);
        speedLines.color = new Color(1f, 1f, 1f, 0f);
        var flash = Img("ImpactFlash", root, Stretch(), vignette, new Color(1f, 0.1f, 0.1f, 0f), Image.Type.Simple);

        // ---- Velocímetro (abajo a la derecha) ----
        const float S = 480f, R = S * 0.5f;
        var speedo = NewRect("Layer_Velocimetro", root, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-50, 50), new Vector2(S, S));
        Img("DialBackground", speedo, Stretch(), disc, new Color(0.03f, 0.04f, 0.07f, 0.45f), Image.Type.Simple);
        Img("Dial", speedo, Stretch(), dial, new Color(1f, 1f, 1f, 0.85f), Image.Type.Simple);

        // Números del dial
        for (int v = 0; v <= maxDisplaySpeed; v += 40)
        {
            float ang = Mathf.Lerp(135f, -135f, v / (float)maxDisplaySpeed) * Mathf.Deg2Rad;
            Vector2 p = new Vector2(-Mathf.Sin(ang), Mathf.Cos(ang)) * (R * 0.64f);
            var t = Txt("Num_" + v, speedo, v.ToString(), 26f, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Center, FontStyles.Normal);
            SetRect(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), p, new Vector2(70, 40));
        }

        // Arco de RPM (fondo + relleno)
        var arcBg = Img("RpmTrack", speedo, Stretch(), arc, new Color(1f, 1f, 1f, 0.12f), Image.Type.Filled);
        ConfigureArc(arcBg, 0.75f);
        var rpmFill = Img("RpmFill", speedo, Stretch(), arc, accent, Image.Type.Filled);
        ConfigureArc(rpmFill, 0.0f);

        // Aguja
        var needle = Img("SpeedNeedle", speedo, new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.05f), Vector2.zero, new Vector2(8f, R * 0.72f)), null, accent2, Image.Type.Simple);
        Img("NeedleCap", speedo, new RectSpec(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f)), disc, accent2, Image.Type.Simple);

        // Textos
        var gearTxt = Txt("Gear", speedo, "N", 70f, accent, TextAlignmentOptions.Center, FontStyles.Bold);
        SetRect(gearTxt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -62), new Vector2(140, 90));
        var speedTxt = Txt("SpeedText", speedo, "0", 92f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        SetRect(speedTxt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -132), new Vector2(300, 110));
        var unitTxt = Txt("Unit", speedo, "KM/H", 24f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Center, FontStyles.Normal);
        SetRect(unitTxt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -190), new Vector2(160, 34));

        // ---- Vueltas (arriba a la izquierda) ----
        var laps = NewRect("Layer_Vueltas", root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, -50), new Vector2(430, 190));
        Img("Fondo", laps, Stretch(), null, panelColor, Image.Type.Simple);
        Img("BarraAcento", laps, new RectSpec(new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), Vector2.zero, new Vector2(6, 0)), null, accent, Image.Type.Simple);
        var lapTxt = Txt("LapText", laps, "VUELTA 1/3", 40f, accent, TextAlignmentOptions.Left, FontStyles.Bold);
        SetRect(lapTxt.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -12), new Vector2(380, 50));
        var timeTxt = Txt("TimeText", laps, "0:00.000", 64f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
        SetRect(timeTxt.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -62), new Vector2(380, 76));
        var bestTxt = Txt("BestText", laps, "MEJOR --:--.---", 24f, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Left, FontStyles.Normal);
        SetRect(bestTxt.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(26, 38), new Vector2(380, 30));
        var lastTxt = Txt("LastText", laps, "ÚLTIMA --:--.---", 24f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.Left, FontStyles.Normal);
        SetRect(lastTxt.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(26, 8), new Vector2(380, 30));

        // ---- Marco (esquinas) ----
        var frame = NewRect("Layer_Marco", root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var fc = new Color(accent.r, accent.g, accent.b, 0.55f);
        Corner(frame, new Vector2(0, 1), 1, -1, fc);
        Corner(frame, new Vector2(1, 1), -1, -1, fc);
        Corner(frame, new Vector2(0, 0), 1, 1, fc);
        Corner(frame, new Vector2(1, 0), -1, 1, fc);

        // ---- Cuenta regresiva ----
        var countdown = Txt("Countdown", root, "", 220f, accent, TextAlignmentOptions.Center, FontStyles.Bold);
        SetRect(countdown.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(900, 300));

        // ---- Scripts del carro ----
        Gearbox gearbox = pcc.GetComponent<Gearbox>();
        if (addCarScripts)
        {
            if (!gearbox) { gearbox = Undo.AddComponent<Gearbox>(pcc.gameObject); }
            gearbox.pcc = pcc;
            if (!pcc.GetComponent<CollisionFeedback>()) Undo.AddComponent<CollisionFeedback>(pcc.gameObject);
        }

        // ---- Componente RacingHUD ----
        var hud = canvasGO.AddComponent<RacingHUD>();
        hud.car = rb;
        hud.gearbox = gearbox;
        hud.lapManager = FindFirst<LapManager>();
        hud.speedText = speedTxt; hud.gearText = gearTxt;
        hud.speedNeedle = needle.rectTransform;
        hud.rpmBar = rpmFill; hud.rpmBarMaxFill = 0.75f;
        hud.maxDisplaySpeed = maxDisplaySpeed;
        hud.speedAngleMin = 135f; hud.speedAngleMax = -135f;
        hud.lapText = lapTxt; hud.timeText = timeTxt; hud.bestText = bestTxt; hud.lastText = lastTxt;
        hud.countdownText = countdown;
        hud.speedLines = speedLines; hud.impactFlash = flash;
        hud.layers = new[]
        {
            new RacingHUD.Layer { rect = speedo, depth = 1.0f, rollAmount = 1.0f },
            new RacingHUD.Layer { rect = laps,   depth = 0.6f, rollAmount = 0.5f },
            new RacingHUD.Layer { rect = frame,  depth = 0.25f, rollAmount = 0.15f },
        };

        // ---- Cámara ----
        if (setupCamera && Camera.main)
        {
            var cam = Camera.main;
            if (cam.transform.IsChildOf(pcc.transform)) Undo.SetTransformParent(cam.transform, null, "Separar cámara");
            var cc = cam.GetComponent<CarCamera>();
            if (!cc) cc = Undo.AddComponent<CarCamera>(cam.gameObject);
            cc.target = pcc.transform;
            cc.targetRb = rb;
        }

        if (!hud.lapManager) Debug.LogWarning("HUD Builder: no hay LapManager en la escena (genera el circuito con Track Builder); el panel de vueltas quedará vacío.");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = canvasGO;
        Debug.Log("HUD generado y conectado.");
    }

    // ------------------------------------------------------------------ construcción de UI
    struct RectSpec
    {
        public Vector2 aMin, aMax, pivot, pos, size;
        public RectSpec(Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        { this.aMin = aMin; this.aMax = aMax; this.pivot = pivot; this.pos = pos; this.size = size; }
    }

    static RectSpec Stretch() => new RectSpec(Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

    static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static RectTransform NewRect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        SetRect(rt, aMin, aMax, pivot, pos, size);
        return rt;
    }

    static Image Img(string name, Transform parent, RectSpec r, Sprite sprite, Color color, Image.Type type)
    {
        var rt = NewRect(name, parent, r.aMin, r.aMax, r.pivot, r.pos, r.size);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.type = type;
        img.raycastTarget = false;
        return img;
    }

    static TextMeshProUGUI Txt(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.fontStyle = style;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static void ConfigureArc(Image img, float fill)
    {
        img.fillMethod = Image.FillMethod.Radial360;
        img.fillOrigin = (int)Image.Origin360.Bottom;
        img.fillClockwise = true;
        img.fillAmount = fill;
        img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);   // el arco de 270° empieza abajo-izquierda
    }

    static void Corner(Transform parent, Vector2 anchor, int sx, int sy, Color c)
    {
        Vector2 pos = new Vector2(30f * sx, 30f * sy);
        Vector2 pivot = anchor;
        Img("CornerH", parent, new RectSpec(anchor, anchor, pivot, pos, new Vector2(120f, 4f)), null, c, Image.Type.Simple);
        Img("CornerV", parent, new RectSpec(anchor, anchor, pivot, pos, new Vector2(4f, 120f)), null, c, Image.Type.Simple);
    }

    // ------------------------------------------------------------------ sprites procedurales
    static float Band(float r, float inner, float outer, int size)
    {
        float aa = 2f / size;
        return Mathf.Clamp01((r - inner) / aa) * Mathf.Clamp01((outer - r) / aa);
    }

    static float DialAlpha(float x, float y, int ticks)
    {
        float r = Mathf.Sqrt(x * x + y * y);
        float a = Band(r, 0.985f, 1f, 512);                                     // contorno exterior
        float ang = Mathf.Atan2(-x, y) * Mathf.Rad2Deg;                         // grados CCW desde arriba
        if (ang <= 135f + 3f && ang >= -135f - 3f)
        {
            float k = Mathf.Round((135f - ang) / 270f * ticks);
            if (k >= 0 && k <= ticks)
            {
                float tickAng = 135f - 270f * k / ticks;
                float arcPx = Mathf.Abs(tickAng - ang) * Mathf.Deg2Rad * r * 256f;   // distancia lateral en píxeles
                float inner = ((int)k % 2 == 0) ? 0.85f : 0.92f;
                float lat = Mathf.Clamp01((1.6f - arcPx) / 1.0f);
                a = Mathf.Max(a, lat * Mathf.Clamp01((r - inner) * 256f) * Mathf.Clamp01((0.99f - r) * 256f));
            }
        }
        return a;
    }

    static float Hash(int n)
    {
        float s = Mathf.Sin(n * 127.1f) * 43758.5453f;
        return s - Mathf.Floor(s);
    }

    static float SpeedLinesAlpha(float x, float y)
    {
        float r = Mathf.Sqrt(x * x + y * y);
        float angN = (Mathf.Atan2(y, x) + Mathf.PI) / (2f * Mathf.PI) * 140f;
        int bin = Mathf.FloorToInt(angN);
        float h = Hash(bin);
        if (h < 0.45f) return 0f;
        float start = 0.45f + h * 0.3f;
        float along = Mathf.SmoothStep(0f, 1f, (r - start) / 0.5f);
        float center = 1f - Mathf.Abs((angN - bin) - 0.5f) * 2f;
        return along * Mathf.SmoothStep(0.55f, 0.95f, center) * 0.9f;
    }

    static Sprite Sprite_(string path, int size, System.Func<float, float, float> alphaAt)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing) return existing;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f, ny = (y + 0.5f) / size * 2f - 1f;
                byte a = (byte)(Mathf.Clamp01(alphaAt(nx, ny)) * 255f);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/HUD")) AssetDatabase.CreateFolder("Assets", "HUD");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/HUD", "Generated");
    }

    static T FindFirst<T>() where T : Object
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<T>();
#else
        return Object.FindObjectOfType<T>();
#endif
    }

    static void DeleteExisting()
    {
        var old = GameObject.Find(CanvasName);
        if (old) Undo.DestroyObjectImmediate(old);
    }
}
#endif
