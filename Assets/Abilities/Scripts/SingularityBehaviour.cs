using System.Collections;
using UnityEngine;

// Phaser — Singularity / Event Horizon
// Phase 1: pull all enemies in radius toward center for pullDuration.
// Phase 2: burst AoE damage, then destroy.
// VFX: brbmuffins Dark Arts/Fantasy Pack/Prefabs/Effects normal/Death magic circle.prefab (cast)
//      brbmuffins Technologies/Particle Pack/Legacy Particles/PlasmaExplosionEffect.prefab (burst)
//      brbmuffins Technologies/Particle Pack/Misc Effects/HeatDistortion.prefab (ambient)
public class SingularityBehaviour : MonoBehaviour
{
    [Header("Pull Phase")]
    public float pullRadius   = 8f;
    public float pullDuration = 3f;
    public float pullForce    = 12f;
    public string enemyTag    = "Enemy";

    [Header("Burst Phase")]
    public float burstDamage  = 20f;
    public float burstRadius  = 8f;

    [Header("Exposed Debuff (Event Horizon only)")]
    public bool  applyExposed        = false;
    public float exposedDuration     = 8f;

    [Header("Phase Relay bonus")]
    // Increased by PhaseRelayDeployable if one is nearby
    public float pullDurationBonus   = 0f;

    [Header("VFX")]
    // Assign: brbmuffins Dark Arts/.../Death magic circle.prefab
    public GameObject ambientVFX;
    // Assign: brbmuffins Technologies/.../PlasmaExplosionEffect.prefab
    public GameObject burstVFX;

    // Set by AbilityCaster
    [HideInInspector] public GameObject owner;

    private GameObject _ambientInstance;

    void Start()
    {
        if (ambientVFX != null)
            _ambientInstance = Instantiate(ambientVFX, transform.position, Quaternion.identity, transform);

        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        float total = pullDuration + pullDurationBonus;
        float elapsed = 0f;

        while (elapsed < total)
        {
            elapsed += Time.fixedDeltaTime;

            Collider[] hits = Physics.OverlapSphere(transform.position, pullRadius);
            foreach (var col in hits)
            {
                if (!col.CompareTag(enemyTag)) continue;
                Rigidbody rb = col.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 toward = (transform.position - col.transform.position).normalized;
                    rb.AddForce(toward * pullForce, ForceMode.Acceleration);
                }
                else
                {
                    // Fallback: move via transform
                    Vector3 dir = (transform.position - col.transform.position).normalized;
                    col.transform.position += dir * 3f * Time.fixedDeltaTime;
                }
            }

            yield return new WaitForFixedUpdate();
        }

        // Burst
        Collider[] finalHits = Physics.OverlapSphere(transform.position, burstRadius);
        foreach (var col in finalHits)
        {
            if (!col.CompareTag(enemyTag)) continue;
            Health h = col.GetComponent<Health>();
            h?.TakeDamage(burstDamage, owner);

            // Event Horizon: apply Exposed debuff
            if (applyExposed)
            {
                var sem = col.GetComponent<StatusEffectManager>();
                sem?.AddEffect(new StatusEffect(StatusEffectType.Exposed, exposedDuration));
            }
        }

        if (burstVFX != null)
        {
            GameObject fx = Instantiate(burstVFX, transform.position, Quaternion.identity);
            Destroy(fx, 4f);
        }

        Destroy(gameObject);
    }
}
