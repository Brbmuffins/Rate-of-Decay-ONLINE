using UnityEngine;

// Basic enemy AI. Attach to Zompy and other enemies.
// Checks StatusEffectManager for suppression/tether before acting.
// Aggro target can be overridden by ThreatProtocol passive.
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(StatusEffectManager))]
public class EnemyAI : MonoBehaviour
{
    [Header("Combat")]
    public float moveSpeed   = 2.5f;
    public float attackRange = 1.8f;
    public float attackDamage = 8f;
    public float attackRate  = 1f;
    public bool  isElite     = false;

    [Header("Aggro")]
    public Transform aggroTarget;      // set by scene or overridden by ThreatProtocol
    public string    playerTag = "Player";

    private Health               _health;
    private StatusEffectManager  _status;
    private float                _attackTimer;

    // Stealth suppression — when StealthHandler clears aggroTarget,
    // we block FindNearestPlayer() for this window so the cloaked player
    // isn't immediately re-acquired on the next frame.
    private float _searchSuppressedUntil = 0f;
    [Tooltip("After losing a target to stealth, how long before the AI searches again.")]
    public float stealthConfusionDuration = 6f;

    void Awake()
    {
        _health = GetComponent<Health>();
        _status = GetComponent<StatusEffectManager>();
    }

    void Start()
    {
        // Auto-find nearest player if no target assigned
        if (aggroTarget == null)
            FindNearestPlayer();
    }

    void Update()
    {
        if (_health == null || !_health.IsAlive) return;

        // Staggered: briefly interrupted (Breach Slam) — no move, no attack
        if (_status.IsStaggered) return;

        // Bound: movement handled externally by IronTetherHandler
        if (_status.IsBound) return;

        if (aggroTarget == null)
        {
            // Only search if the confusion window has passed (prevents re-acquiring
            // a cloaked Wraith the frame after StealthHandler cleared our target).
            if (Time.time >= _searchSuppressedUntil)
                FindNearestPlayer();
            return;
        }

        float dist = Vector3.Distance(transform.position, aggroTarget.position);

        // Move toward target
        if (dist > attackRange)
        {
            Vector3 dir = (aggroTarget.position - transform.position).normalized;
            dir.y = 0f;

            // Apply slow from StatusEffects
            float slowFrac = _status.GetSlowFraction();
            transform.position += dir * moveSpeed * (1f - slowFrac) * Time.deltaTime;

            if (dir != Vector3.zero)
                transform.forward = Vector3.Slerp(transform.forward, dir, Time.deltaTime * 6f);
        }
        else
        {
            // Silenced (Silence Ward): cannot attack — the zone is real CC now
            if (_status.IsSilenced) return;

            // Attack
            _attackTimer += Time.deltaTime;
            if (_attackTimer >= 1f / attackRate)
            {
                _attackTimer = 0f;
                Attack();
            }
        }
    }

    public void SetAggroTarget(Transform t)
    {
        if (t == null && aggroTarget != null)
        {
            // Target was cleared externally (stealth) — suppress search window
            _searchSuppressedUntil = Time.time + stealthConfusionDuration;
        }
        aggroTarget = t;
    }

    void Attack()
    {
        // (Suppress / Stagger are checked in Update before this runs.)
        if (aggroTarget == null) return;
        Health targetHealth = aggroTarget.GetComponent<Health>();
        targetHealth?.TakeDamage(attackDamage, gameObject);
    }

    void FindNearestPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag(playerTag);
        float best = Mathf.Infinity;
        foreach (var p in players)
        {
            float d = Vector3.Distance(transform.position, p.transform.position);
            if (d < best) { best = d; aggroTarget = p.transform; }
        }
    }
}
