using UnityEngine;

/// Trigger de checkpoint. El forward del objeto = sentido de la carrera (se usa para respawn).
[RequireComponent(typeof(BoxCollider))]
public class Checkpoint : MonoBehaviour
{
    [HideInInspector] public LapManager manager;
    [HideInInspector] public int index;

    void Reset()
    {
        var b = GetComponent<BoxCollider>();
        b.isTrigger = true;
        b.size = new Vector3(16f, 6f, 1.5f);
        b.center = new Vector3(0f, 3f, 0f);
    }

    void OnTriggerEnter(Collider other)
    {
        if (manager && other.attachedRigidbody && other.attachedRigidbody.gameObject == manager.car.gameObject)
            manager.Pass(index);
    }

    void OnDrawGizmos()
    {
        var b = GetComponent<BoxCollider>();
        if (!b) return;
        Gizmos.color = index == 0 ? new Color(1f, 0.9f, 0.1f, 0.5f) : new Color(0.2f, 0.9f, 1f, 0.35f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(b.center, b.size);
        Gizmos.color = Color.white;
        Gizmos.DrawRay(b.center, Vector3.forward * 4f);
    }
}
