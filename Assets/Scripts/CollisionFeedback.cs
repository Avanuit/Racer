using UnityEngine;

/// Detecta choques y avisa a cámara + HUD.
[RequireComponent(typeof(Rigidbody))]
public class CollisionFeedback : MonoBehaviour
{
    public float minImpactSpeed = 3f;
    public float maxImpactSpeed = 25f;
    public float cooldown = 0.15f;
    public AudioSource sfx;
    public AudioClip[] clips;

    float last;

    void OnCollisionEnter(Collision c)
    {
        if (Time.time - last < cooldown) return;
        float s = c.relativeVelocity.magnitude;
        if (s < minImpactSpeed) return;
        last = Time.time;

        float k = Mathf.InverseLerp(minImpactSpeed, maxImpactSpeed, s);
        Vector3 localPush = transform.InverseTransformDirection(c.GetContact(0).normal);
        CarEvents.RaiseImpact(k, localPush);

        if (sfx && clips != null && clips.Length > 0)
            sfx.PlayOneShot(clips[Random.Range(0, clips.Length)], Mathf.Lerp(0.3f, 1f, k));
    }
}
