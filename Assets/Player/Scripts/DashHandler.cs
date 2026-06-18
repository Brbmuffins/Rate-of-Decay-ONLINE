using System.Collections;
using UnityEngine;

// Handles Breach Slam (Guardian) and Phase Shift (Phaser).
// Breach Slam: dash forward 6u, damage + stagger enemies along path.
// Phase Shift: teleport to aimed point up to 10u away.
// VFX Breach Slam: brbmuffins Technologies/Particle Pack/Magic Effects/Prefabs/EarthShatter.prefab
// VFX Phase Shift: brbmuffins Dark Arts/Fantasy Pack/Prefabs/Plazma sphere.prefab (on arrive)
//                  brbmuffins Technologies/Particle Pack/Misc Effects/Prefabs/Dissolve.prefab (depart)
public class DashHandler : MonoBehaviour
{
    [Header("Breach Slam")]
    public float slamDistance    = 6f;
    public float slamSpeed       = 24f;
    public float slamDamage      = 25f;
    public float slamWidth       = 1.5f;   // box half-width for hit detection
    public float staggerDuration = 0.8f;
    public string enemyTag       = "Enemy";

    [Header("Phase Shift")]
    public float teleportMaxDist = 10f;

    [Header("VFX — Breach Slam")]
    // Assign: brbmuffins Technologies/.../EarthShatter.prefab
    public GameObject slamImpactVFX;
    // Assign: brbmuffins Technologies/.../SparksEffect.prefab
    public GameObject slamTrailVFX;

    [Header("VFX — Phase Shift")]
    // Assign: brbmuffins Technologies/.../Dissolve.prefab
    public GameObject shiftDepartVFX;
    // Assign: brbmuffins Dark Arts/.../Plazma sphere.prefab
    public GameObject shiftArriveVFX;

    private Rigidbody _rb;
    private bool      _dashing = false;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    // ── Breach Slam ───────────────────────────────────────────────
    public void BreachSlam(PassiveThreatProtocol threat = null)
    {
        if (_dashing) return;
        StartCoroutine(SlamRoutine(threat));
    }

    private IEnumerator SlamRoutine(PassiveThreatProtocol threat)
    {
        _dashing = true;
        Vector3 dir   = transform.forward;
        Vector3 start = transform.position;
        Vector3 end   = start + dir * slamDistance;
        float   time  = slamDistance / slamSpeed;
        float   elapsed = 0f;

        if (slamTrailVFX != null)
        {
            GameObject trail = Instantiate(slamTrailVFX, transform.position, transform.rotation, transform);
            Destroy(trail, time + 0.5f);
        }

        while (elapsed < time)
        {
            elapsed += Time.fixedDeltaTime;
            if (_rb != null)
                _rb.MovePosition(Vector3.MoveTowards(transform.position, end, slamSpeed * Time.fixedDeltaTime));
            else
                transform.position = Vector3.MoveTowards(transform.position, end, slamSpeed * Time.fixedDeltaTime);

            yield return new WaitForFixedUpdate();
        }

        // Impact: box check along path
        Vector3 boxCenter = start + dir * (slamDistance / 2f) + Vector3.up * 0.5f;
        Vector3 halfExtents = new Vector3(slamWidth, 1f, slamDistance / 2f);
        Collider[] hits = Physics.OverlapBox(boxCenter, halfExtents, Quaternion.LookRotation(dir));
        foreach (var col in hits)
        {
            if (!col.CompareTag(enemyTag)) continue;
            col.GetComponent<Health>()?.TakeDamage(slamDamage, gameObject);
            col.GetComponent<StatusEffectManager>()?.AddEffect(
                new StatusEffect(StatusEffectType.Stagger, staggerDuration));
        }

        // Threat stacks
        threat?.AddStacks(3);

        if (slamImpactVFX != null)
        {
            GameObject fx = Instantiate(slamImpactVFX, transform.position, Quaternion.identity);
            Destroy(fx, 3f);
        }

        _dashing = false;
    }

    // ── Phase Shift ───────────────────────────────────────────────
    // targetPoint: the world position the Phaser aimed at.
    public void PhaseShift(Vector3 targetPoint)
    {
        Vector3 dir  = (targetPoint - transform.position);
        dir.y = 0;
        float dist   = Mathf.Min(dir.magnitude, teleportMaxDist);
        Vector3 dest = transform.position + dir.normalized * dist;

        // Depart VFX
        if (shiftDepartVFX != null)
        {
            GameObject dep = Instantiate(shiftDepartVFX, transform.position + Vector3.up, Quaternion.identity);
            Destroy(dep, 2f);
        }

        // Teleport
        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.MovePosition(dest);
        }
        else
        {
            transform.position = dest;
        }

        // Arrive VFX
        if (shiftArriveVFX != null)
        {
            GameObject arr = Instantiate(shiftArriveVFX, dest + Vector3.up, Quaternion.identity);
            Destroy(arr, 2f);
        }
    }
}
