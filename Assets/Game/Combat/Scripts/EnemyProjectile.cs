using UnityEngine;
using Mirror;

/// <summary>
/// EnemyProjectile — Linear projectile fired by ranged enemies.
/// Travels forward at speed, damages first Player hit, self-destructs on timeout.
/// Assign to EnemyController.projectilePrefab on ranged enemy prefabs.
/// Prefab built by BCE/Setup/4d (via EnemyBuilder).
/// </summary>
public class EnemyProjectile : NetworkBehaviour
{
    [Header("Movement")]
    public float speed   = 12f;
    public float lifetime = 4f;

    private float _damage;
    private bool  _hit = false;

    /// Called server-side immediately after Instantiate to set damage value.
    public void Init(float damage) => _damage = damage;

    public override void OnStartServer()
    {
        base.OnStartServer();
        Invoke(nameof(SelfDestruct), lifetime);
    }

    void Update()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime, Space.Self);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!isServer || _hit) return;
        if (!other.CompareTag("Player")) return;

        _hit = true;
        CancelInvoke(nameof(SelfDestruct));

        var health = other.GetComponent<Health>();
        if (health != null && health.IsAlive)
            health.TakeDamage(_damage, gameObject);

        RpcHitEffect(transform.position);
        NetworkServer.Destroy(gameObject);
    }

    [Server]
    void SelfDestruct()
    {
        if (!_hit) NetworkServer.Destroy(gameObject);
    }

    [ClientRpc]
    void RpcHitEffect(Vector3 pos)
    {
        // Week 7: Instantiate impact VFX at pos
    }
}
