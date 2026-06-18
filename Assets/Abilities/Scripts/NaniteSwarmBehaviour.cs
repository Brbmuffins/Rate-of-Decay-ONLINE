using UnityEngine;

// Spawned by Medic. Drifts toward target ally, healing on arrival.
// Chips any enemy it passes through along the way.
// VFX: brbmuffins Dark Arts/Fantasy Pack/Prefabs/Healing buff.prefab  (attach as child)
//      brbmuffins Dark Arts/Fantasy Pack/Prefabs/Glowing orbs.prefab  (cast VFX override)
public class NaniteSwarmBehaviour : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed   = 4f;
    public float arrivalDist = 0.8f;

    [Header("Healing")]
    public float healAmount  = 30f;

    [Header("Chip Damage")]
    public float chipDamage    = 5f;
    public float chipRadius    = 1.2f;
    public float chipInterval  = 0.3f;
    public string enemyTag     = "Enemy";

    [Header("VFX")]
    // Assign: brbmuffins Dark Arts/.../Healing buff.prefab
    public GameObject trailVFX;
    // Assign: brbmuffins Technologies/.../SparksEffect.prefab (tinted green in material)
    public GameObject hitVFX;

    // Set by AbilityCaster
    [HideInInspector] public Health targetHealth;
    [HideInInspector] public Transform target;

    private float _chipTimer;
    private GameObject _trail;

    void Start()
    {
        if (trailVFX != null)
            _trail = Instantiate(trailVFX, transform.position, Quaternion.identity, transform);
    }

    void Update()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        // Move toward ally
        Vector3 dir  = (target.position - transform.position);
        float   dist = dir.magnitude;

        transform.position += dir.normalized * moveSpeed * Time.deltaTime;

        // Chip damage to nearby enemies every interval
        _chipTimer += Time.deltaTime;
        if (_chipTimer >= chipInterval)
        {
            _chipTimer = 0f;
            Collider[] hits = Physics.OverlapSphere(transform.position, chipRadius);
            foreach (var col in hits)
            {
                if (!col.CompareTag(enemyTag)) continue;
                col.GetComponent<Health>()?.TakeDamage(chipDamage);
            }
        }

        // Arrived
        if (dist <= arrivalDist)
        {
            targetHealth?.Heal(healAmount);

            if (hitVFX != null)
            {
                GameObject fx = Instantiate(hitVFX, target.position + Vector3.up, Quaternion.identity);
                Destroy(fx, 2f);
            }

            Destroy(gameObject);
        }
    }
}
