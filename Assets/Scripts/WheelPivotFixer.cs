using System.Text.RegularExpressions;
using UnityEngine;

/// Arregla modelos con llanta, rin, etc. como objetos separados.
/// 1) "Crear pivotes de ruedas": un pivote por rueda con el grupo visual dentro, asignado a PCC.
/// 2) "Recoger rines sueltos": busca objetos llamados "rim..." que quedaron fuera de un pivote y los mete en el más cercano.
/// 3) "Diagnóstico": lista qué hay dentro de cada pivote y qué piezas de rueda quedaron sueltas.
public class WheelPivotFixer : MonoBehaviour
{
    public PrometeoCarController pcc;

    [Header("Grupos visuales (llanta + rin juntos, SIN el caliper)")]
    public Transform frontLeftVisual;
    public Transform frontRightVisual;
    public Transform rearLeftVisual;
    public Transform rearRightVisual;

    [Header("Opciones")]
    public bool snapColliderToWheel = false;   // ahora desactivado: puede romper la física si el modelo tiene escalas raras
    public float adoptMaxDistance = 0.7f;     // un rin suelto solo se adopta si está a menos de esto (m) de un pivote

    static readonly Regex RimName = new Regex(@"(^|[^a-z])(rim|rin|wheel|tire|tyre)", RegexOptions.IgnoreCase);

    [ContextMenu("Crear pivotes de ruedas")]
    public void Fix()
    {
        if (!pcc) pcc = GetComponent<PrometeoCarController>();
        if (!pcc) { Debug.LogError("WheelPivotFixer: asigna el PrometeoCarController."); return; }

        GameObject m;
        m = Build("WheelPivot_FL", frontLeftVisual,  pcc.frontLeftCollider);  if (m) pcc.frontLeftMesh = m;
        m = Build("WheelPivot_FR", frontRightVisual, pcc.frontRightCollider); if (m) pcc.frontRightMesh = m;
        m = Build("WheelPivot_RL", rearLeftVisual,   pcc.rearLeftCollider);   if (m) pcc.rearLeftMesh = m;
        m = Build("WheelPivot_RR", rearRightVisual,  pcc.rearRightCollider);  if (m) pcc.rearRightMesh = m;
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(pcc);
#endif
        AdoptLooseRims();
        Diagnose();
    }

    GameObject Build(string name, Transform visual, WheelCollider col)
    {
        if (!visual) { Debug.LogWarning(name + ": falta el grupo visual."); return null; }

        // Si ya existe el pivote (se ejecutó antes), reutilízalo en vez de anidar otro
        if (visual.parent && visual.parent.name == name)
        {
            if (snapColliderToWheel && col) SnapCollider(col, VisualBounds(visual.parent));
            return visual.parent.gameObject;
        }

        var rends = visual.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) { Debug.LogWarning(name + ": no hay renderers."); return null; }
        Bounds b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);

        Transform pivot = visual.parent ? visual.parent.Find(name) : null;   // ¿ya existe uno (vacío)?
        if (!pivot)
        {
            pivot = new GameObject(name).transform;
            pivot.SetParent(visual.parent, true);
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(pivot.gameObject, "Crear pivote");
#endif
        }
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(pivot, "Colocar pivote");
#endif
        pivot.position = b.center;
        pivot.rotation = pcc.transform.rotation;      // orientación neutra = la del carro
        pivot.localScale = Vector3.one;
#if UNITY_EDITOR
        UnityEditor.Undo.SetTransformParent(visual, pivot, "Reparentar rueda");
#else
        visual.SetParent(pivot, true);
#endif
        if (snapColliderToWheel && col) SnapCollider(col, b);
        return pivot.gameObject;
    }


    // ---------- WheelColliders ----------
    Bounds VisualBounds(Transform pivot)
    {
        var rends = pivot.GetComponentsInChildren<Renderer>();
        Bounds b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);
        return b;
    }

    void SnapCollider(WheelCollider col, Bounds b)
    {
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(col, "Ajustar WheelCollider");
        UnityEditor.Undo.RecordObject(col.transform, "Ajustar WheelCollider");
#endif
        // IMPORTANTE: el WheelCollider escala radio y suspensión con la escala del objeto.
        // Tu jerarquía tiene escala ~120, así que hay que pasar de metros reales a unidades locales.
        float scale = Mathf.Max(0.0001f, Mathf.Abs(col.transform.lossyScale.y));
        float worldRadius = Mathf.Clamp(b.extents.y, 0.25f, 0.45f);
        float worldSusp = 0.2f;
        col.radius = worldRadius / scale;
        col.center = Vector3.zero;
        col.suspensionDistance = worldSusp / scale;
        var sp = col.suspensionSpring;
        sp.spring = 35000f; sp.damper = 4500f; sp.targetPosition = 0.5f;
        col.suspensionSpring = sp;
        // La rueda en reposo queda a media carrera de la suspensión => el collider va un poco más arriba
        col.transform.position = b.center + pcc.transform.up * (worldSusp * 0.5f);
    }

    [ContextMenu("Diagnóstico de WheelColliders")]
    public void DiagnoseColliders()
    {
        if (!pcc) pcc = GetComponent<PrometeoCarController>();
        Check("FL", pcc.frontLeftCollider, pcc.frontLeftMesh);
        Check("FR", pcc.frontRightCollider, pcc.frontRightMesh);
        Check("RL", pcc.rearLeftCollider, pcc.rearLeftMesh);
        Check("RR", pcc.rearRightCollider, pcc.rearRightMesh);
    }

    void Check(string n, WheelCollider c, GameObject mesh)
    {
        if (!c) { Debug.LogWarning(n + ": sin WheelCollider"); return; }
        float scale = Mathf.Abs(c.transform.lossyScale.y);
        float rWorld = c.radius * scale, sWorld = c.suspensionDistance * scale;
        float dist = mesh ? Vector3.Distance(c.transform.position + c.center, mesh.transform.position) : -1f;
        bool bad = rWorld < 0.2f || rWorld > 0.6f || sWorld > 0.5f || dist > 0.6f;
        string msg = $"{n}: RADIO REAL={rWorld:F3} m | SUSPENSIÓN REAL={sWorld:F3} m | (valores locales: radio {c.radius:F5}, susp {c.suspensionDistance:F5}) | escala={scale:F2} | distancia al pivote={dist:F2} m";
        if (bad) Debug.LogWarning("⚠ " + msg, c); else Debug.Log(msg, c);
    }

    [ContextMenu("Reparar WheelColliders (valores seguros)")]
    public void RepairColliders()
    {
        if (!pcc) pcc = GetComponent<PrometeoCarController>();
        Repair(pcc.frontLeftCollider, pcc.frontLeftMesh);
        Repair(pcc.frontRightCollider, pcc.frontRightMesh);
        Repair(pcc.rearLeftCollider, pcc.rearLeftMesh);
        Repair(pcc.rearRightCollider, pcc.rearRightMesh);
        DiagnoseColliders();
    }

    void Repair(WheelCollider c, GameObject pivot)
    {
        if (!c || !pivot) return;
        SnapCollider(c, VisualBounds(pivot.transform));
    }


    // ---------- Simetría izquierda / derecha ----------
    [Header("Simetría")]
    public bool mirrorAroundBodyCenter = true;   // usa el centro de la carrocería (hijo "Body") como eje de simetría

    float SymmetryAxisX()
    {
        if (!mirrorAroundBodyCenter) return 0f;
        Transform body = pcc.transform.Find("Body");
        if (!body) return 0f;
        var rs = body.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return 0f;
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return pcc.transform.InverseTransformPoint(b.center).x;
    }

    [ContextMenu("Alinear ruedas izquierdas con las derechas")]
    public void MirrorLeftToRight()
    {
        if (!pcc) pcc = GetComponent<PrometeoCarController>();
        float cx = SymmetryAxisX();
        Debug.Log($"Eje de simetría (X local del carro) = {cx:F3}");
        MirrorPair("Delanteras", pcc.frontRightCollider, pcc.frontLeftCollider, cx);
        MirrorPair("Traseras", pcc.rearRightCollider, pcc.rearLeftCollider, cx);
        DiagnoseColliders();
    }

    void MirrorPair(string label, WheelCollider good, WheelCollider fix, float cx)
    {
        if (!good || !fix) return;
        Vector3 g = pcc.transform.InverseTransformPoint(good.transform.position);
        Vector3 f = pcc.transform.InverseTransformPoint(fix.transform.position);
        float newX = 2f * cx - g.x;
        Debug.Log($"{label}: derecha X={g.x:F3} | izquierda X={f.x:F3} → izquierda nueva X={newX:F3}");
        f.x = newX;
#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(fix.transform, "Alinear rueda izquierda");
#endif
        fix.transform.position = pcc.transform.TransformPoint(f);
    }


    // ---------- TODO EN UNO ----------
    Transform Vis(Transform current, GameObject meshField)
    {
        if (current) return current;
        if (!meshField) return null;
        Transform m = meshField.transform;
        if (m.name.StartsWith("WheelPivot_") && m.childCount > 0) return m.GetChild(0);   // ya hay pivote
        if (m.parent && m.parent.name.StartsWith("3DWheel")) m = m.parent;                // apuntaba a la llanta: sube al grupo
        return m;
    }

    [ContextMenu("ARREGLAR RUEDAS (todo en uno)")]
    public void FixAll()
    {
        if (!pcc) pcc = GetComponent<PrometeoCarController>();
        if (!pcc) { Debug.LogError("WheelPivotFixer: asigna el PrometeoCarController."); return; }

        // Toma los grupos visuales de lo que ya tengas asignado en PCC si dejaste los campos vacíos
        frontLeftVisual  = Vis(frontLeftVisual,  pcc.frontLeftMesh);
        frontRightVisual = Vis(frontRightVisual, pcc.frontRightMesh);
        rearLeftVisual   = Vis(rearLeftVisual,   pcc.rearLeftMesh);
        rearRightVisual  = Vis(rearRightVisual,  pcc.rearRightMesh);

        bool prev = snapColliderToWheel;
        snapColliderToWheel = true;       // centra cada WheelCollider en su rueda, con radio y suspensión correctos
        Fix();
        snapColliderToWheel = prev;
        DiagnoseColliders();
        Debug.Log("Ruedas arregladas: pivotes centrados + colliders en el centro real de cada rueda.");
    }

    Transform[] Pivots()
    {
        return new[] {
            pcc.frontLeftMesh ? pcc.frontLeftMesh.transform : null,
            pcc.frontRightMesh ? pcc.frontRightMesh.transform : null,
            pcc.rearLeftMesh ? pcc.rearLeftMesh.transform : null,
            pcc.rearRightMesh ? pcc.rearRightMesh.transform : null };
    }

    bool UnderAnyPivot(Transform t, Transform[] pivots)
    {
        foreach (var p in pivots) if (p && t.IsChildOf(p)) return true;
        return false;
    }

    [ContextMenu("Recoger rines sueltos")]
    public void AdoptLooseRims()
    {
        if (!pcc) pcc = GetComponent<PrometeoCarController>();
        var pivots = Pivots();
        int adopted = 0;

        foreach (var r in pcc.GetComponentsInChildren<Renderer>(true))
        {
            Transform t = r.transform;
            if (UnderAnyPivot(t, pivots) || !RimName.IsMatch(t.name)) continue;
            if (t.name.ToLower().Contains("caliper") || t.name.ToLower().Contains("calliper")) continue;

            // Sube hasta el objeto más alto cuyo nombre también parece de rin/llanta (sin subir a "Meshes"/"Wheels")
            while (t.parent && RimName.IsMatch(t.parent.name) && t.parent != pcc.transform
                   && !UnderAnyPivot(t.parent, pivots) && t.parent.GetComponentsInChildren<Renderer>().Length <= 8)
                t = t.parent;

            Transform best = null; float bestD = float.MaxValue;
            Vector3 c = r.bounds.center;
            foreach (var p in pivots)
            {
                if (!p) continue;
                float d = Vector3.Distance(p.position, c);
                if (d < bestD) { bestD = d; best = p; }
            }
            if (!best || bestD > adoptMaxDistance) continue;

#if UNITY_EDITOR
            UnityEditor.Undo.SetTransformParent(t, best, "Adoptar rin");
#else
            t.SetParent(best, true);
#endif
            adopted++;
            Debug.Log($"Rin '{t.name}' movido a {best.name} (distancia {bestD:F2} m)");
        }
        Debug.Log($"Rines recogidos: {adopted}");
    }

    [ContextMenu("Diagnóstico")]
    public void Diagnose()
    {
        if (!pcc) pcc = GetComponent<PrometeoCarController>();
        var pivots = Pivots();
        foreach (var p in pivots)
        {
            if (!p) { Debug.LogWarning("Falta un pivote."); continue; }
            var rs = p.GetComponentsInChildren<Renderer>();
            string list = "";
            foreach (var r in rs) list += r.name + ", ";
            Debug.Log($"{p.name}: {rs.Length} renderers → {list}");
        }
        foreach (var r in pcc.GetComponentsInChildren<Renderer>(true))
            if (!UnderAnyPivot(r.transform, pivots) && RimName.IsMatch(r.name)
                && !r.name.ToLower().Contains("calliper") && !r.name.ToLower().Contains("caliper"))
                Debug.LogWarning($"Pieza de rueda SUELTA (fuera de pivotes): {r.name}", r);
    }
}
