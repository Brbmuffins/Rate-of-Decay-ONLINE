using UnityEngine;
using Mirror;

/// <summary>
/// EnemyProjectile — Linear projectile fired by ranged enemies.
/// Travels forward at a fixed speed, damages the first Player it hits,
/// then self-destructs. Also destroys after a timeout so it never orphans.
///
/// Copy to: Assets/Game/Combat/EnemyProjectile.cs
///
/// Setup: assign to EnemyController.projectilePrefab on ranged enemy prefabs.
/// The EnemyController spawns the projectile already aimed at the target.
/// This script just moves it forward and handles the hit.
///
/// Note: Spawned and destroyed server-side via NetworkServer.Spawn/Destroy.
/// </summary>
public class EnemyProjectile : NetworkBehaviour
{
    [Header("Movement")]
    public float speed = 12f;
    public float lifetime = 4f;

    [Header("VFX")]
    public TrailRenderer trail;  // assign in prefab inspector (optional)

    // Set by EnemyController.PerformAttack before NetworkServer.Spawn
    private float _damage;
    private bool _hit = false;

    public void Init(float damage)
    {
        _damage = damage;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        Invoke(nameof(SelfDestruct), lifetime);
    }

    void Update()
    {
        // Move forward every frame (runs on server and clients for visual smoothness)
        transform.Translate(Vector3.forward * speed * Time.deltaTime, Space.Self);
    }

    // ─── Collision (server resolves damage) ───────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        if (!isServer || _hit) return;
        if (!other.CompareTag("Player")) return;

        _hit = true;
        CancelInvoke(nameof(SelfDestruct));

        var health = other.GetComponent<Health>();
        if (health != null && !health.isDead)
            health.TakeDamage(_damage, gameObject);

        RpcHitEffect(transform.position);
        NetworkServer.Destroy(gameObject);
    }

    [Server]
    void SelfDestruct()
    {
        if (!_hit)
            NetworkServer.Destroy(gameObject);
    }

    [ClientRpc]
    void RpcHitEffect(Vector3 pos)
    {
        // Hook hit VFX / SFX here in Week 7 polish
        // e.g. Instantiate(hitParticles, pos, Quaternion.identity);
    }
}
