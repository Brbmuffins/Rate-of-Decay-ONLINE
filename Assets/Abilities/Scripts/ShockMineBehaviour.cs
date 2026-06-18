using UnityEngine;

// Spawned at target location by AbilityCaster when Shock Mine is cast.
// Becomes a proximity trigger: first enemy inside detonates it for burst damage.
// VFX: drag in brbmuffins Technologies/Particle Pack/Fire & Explosion Effects/Prefabs/SmallExplosion.prefab
//      and brbmuffins Technologies/Particle Pack/Misc Effects/Prefabs/ElectricalSparks.prefab
[RequireComponent(typeof(SphereCollider))]
public class ShockMineBehaviour : MonoBehaviour
{
    [Header("Damage")]
    public float damage          = 40f;
    public float blastRadius     = 2.5f;
    public string targetTag      = "Enemy";

    [Header("Timing")]
    public float armDelay        = 0.5f;   // brief delay so caster can walk away

    [Header("VFX")]
    // Assign: brbmuffins Technologies/.../SmallExplosion.prefab
    public GameObject explosionVFX;
    // Assign: brbmuffins Technologies/.../ElectricalSparks.prefab
    public GameObject idleVFX;

    // Who planted this mine (for BountySystem credit)
    [HideInInspector] public GameObject owner;

    private bool  _armed  = false;
    private float _armTimer;
    private GameObject _idleInstance;

    void Start()
    {
        SphereCollider col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius    = blastRadius / 2f;

        if (idleVFX != null)
            _idleInstance = Instantiate(idleVFX, transform.position, Quaternion.identity, transform);
    }

    void Update()
    {
        if (_armed) return;
        _armTimer += Time.deltaTime;
        if (_armTimer >= armDelay) _armed = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!_armed) return;
        if (!other.CompareTag(targetTag)) return;

        Detonate();
    }

    void Detonate()
    {
        // AoE damage to all enemies in blast radius
        Collider[] hits = Physics.OverlapSphere(transform.position, blastRadius);
        foreach (var col in hits)
        {
            if (!col.CompareTag(targetTag)) continue;
            Health h = col.GetComponent<Health>();
            h?.TakeDamage(damage, owner);
        }

        if (explosionVFX != null)
        {
            GameObject fx = Instantiate(explosionVFX, transform.position, Quaternion.identity);
            Destroy(fx, 3f);
        }

        if (DeployableManager.Instance != null)
            DeployableManager.Instance.Unregister(gameObject);

        Destroy(gameObject);
    }
}
