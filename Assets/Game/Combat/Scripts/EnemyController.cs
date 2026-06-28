using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Mirror;

/// <summary>
/// EnemyController — Server-authoritative enemy AI.
/// State machine: Idle → Chase → Attack → Dead
/// Supports melee and ranged variants via isRanged toggle.
/// Respects StatusEffectManager (Stagger, Bound, Slow) — same as EnemyAI.
///
/// Required: Health, NavMeshAgent, NetworkIdentity
/// Optional: StatusEffectManager (auto-detected), Collider
/// Setup via BCE/Setup/4a (grunt), 4b (ranged), 4c (elite)
/// </summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : NetworkBehaviour
{
    public enum EnemyState { Idle, Chase, Attack, Dead }

    [SyncVar(hook = nameof(OnStateChanged))]
    public EnemyState state = EnemyState.Idle;

    // ── Detection ────────────────────────────────────────────────────────────────
    [Header("Detection")]
    public float aggroRadius = 8f;
    public float leashRadius = 20f;

    // ── Combat ───────────────────────────────────────────────────────────────────
    [Header("Combat")]
    public float attackRange    = 1.5f;
    public float attackInterval = 1.5f;
    public float damage         = 12f;

    // ── Ranged ───────────────────────────────────────────────────────────────────
    [Header("Ranged")]
    public bool      isRanged         = false;
    public GameObject projectilePrefab;
    public float     preferredRange   = 5f;
    public float     tooCloseDistance = 3f;

    // ── Drops ────────────────────────────────────────────────────────────────────
    [Header("Drops")]
    public DropTable  dropTable;
    public GameObject worldItemPrefab;

    // ── Private ──────────────────────────────────────────────────────────────────
    private Health               _health;
    private NavMeshAgent         _agent;
    private StatusEffectManager  _status;   // may be null on basic enemies
    private float                _baseSpeed;
    private Transform            _target;
    private Vector3              _spawnPos;
    private float                _attackTimer;

    // ─────────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        _health    = GetComponent<Health>();
        _agent     = GetComponent<NavMeshAgent>();
        _status    = GetComponent<StatusEffectManager>();
        _baseSpeed = _agent != null ? _agent.speed : 0f;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _spawnPos = transform.position;
        _health.onDeath.AddListener(OnDeath);
        StartCoroutine(BehaviorLoop());
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Behavior loop
    // ─────────────────────────────────────────────────────────────────────────────

    [Server]
    IEnumerator BehaviorLoop()
    {
        var tick = new WaitForSeconds(0.2f);

        while (_health.IsAlive)
        {
            yield return tick;

            // Stagger: cannot act this tick
            if (_status != null && _status.IsStaggered) continue;

            switch (state)
            {
                case EnemyState.Idle:   TickIdle();   break;
                case EnemyState.Chase:  TickChase();  break;
                case EnemyState.Attack: TickAttack(); break;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // State logic
    // ─────────────────────────────────────────────────────────────────────────────

    [Server]
    void TickIdle()
    {
        var hits = Physics.OverlapSphere(transform.position, aggroRadius);
        float     nearest = float.MaxValue;
        Transform found   = null;

        foreach (var col in hits)
        {
            if (!col.CompareTag("Player")) continue;
            var h = col.GetComponent<Health>();
            if (h == null || !h.IsAlive) continue;
            float d = Vector3.Distance(transform.position, col.transform.position);
            if (d < nearest) { nearest = d; found = col.transform; }
        }

        if (found != null) { _target = found; state = EnemyState.Chase; }
    }

    [Server]
    void TickChase()
    {
        // Target gone or dead → return home
        if (_target == null || !(_target.GetComponent<Health>()?.IsAlive ?? false))
        {
            ResetToIdle();
            return;
        }

        // Leash check
        if (Vector3.Distance(transform.position, _spawnPos) > leashRadius)
        {
            ResetToIdle();
            _agent?.SetDestination(_spawnPos);
            return;
        }

        // Bound — cannot move
        if (_status != null && _status.IsBound)
        {
            _agent?.ResetPath();
            return;
        }

        // Apply slow
        float slow = _status != null ? _status.GetSlowFraction() : 0f;
        if (_agent != null) _agent.speed = _baseSpeed * (1f - slow);

        float dist = Vector3.Distance(transform.position, _target.position);

        if (isRanged)
        {
            // Back off if too close; otherwise close to preferred range
            if (dist < tooCloseDistance)
            {
                Vector3 away = (transform.position - _target.position).normalized;
                _agent?.SetDestination(transform.position + away * 3f);
            }
            else
                _agent?.SetDestination(_target.position);

            if (dist <= attackRange) state = EnemyState.Attack;
        }
        else
        {
            _agent?.SetDestination(_target.position);
            if (dist <= attackRange) state = EnemyState.Attack;
        }
    }

    [Server]
    void TickAttack()
    {
        if (_target == null || !(_target.GetComponent<Health>()?.IsAlive ?? false))
        {
            ResetToIdle();
            return;
        }

        float dist = Vector3.Distance(transform.position, _target.position);

        // Target stepped out of range — re-chase
        if (dist > attackRange * 1.3f) { state = EnemyState.Chase; return; }

        // Stand still for melee; keep pathing for ranged backpedal
        if (!isRanged) _agent?.SetDestination(transform.position);

        // Face target
        Vector3 dir = (_target.position - transform.position); dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(dir);

        // Tick attack cooldown (0.2s = one BehaviorLoop tick)
        _attackTimer -= 0.2f;
        if (_attackTimer > 0f) return;
        _attackTimer = attackInterval;

        PerformAttack();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Attack
    // ─────────────────────────────────────────────────────────────────────────────

    [Server]
    void PerformAttack()
    {
        if (_target == null) return;
        var targetHealth = _target.GetComponent<Health>();
        if (targetHealth == null || !targetHealth.IsAlive) return;

        // Silenced: cannot attack (mirrors EnemyAI behaviour)
        if (_status != null && _status.IsSilenced) return;

        if (!isRanged)
        {
            targetHealth.TakeDamage(damage, gameObject);
            RpcMeleeSwing();
        }
        else
        {
            if (projectilePrefab != null)
            {
                Vector3    spawnPos = transform.position + Vector3.up * 1.2f;
                Quaternion spawnRot = Quaternion.LookRotation(
                    (_target.position + Vector3.up * 0.5f) - spawnPos);
                var proj = Instantiate(projectilePrefab, spawnPos, spawnRot);
                var ep   = proj.GetComponent<EnemyProjectile>();
                if (ep != null) ep.Init(damage);
                NetworkServer.Spawn(proj);
            }
            else
            {
                // Fallback instant damage if no projectile prefab set
                targetHealth.TakeDamage(damage, gameObject);
            }
            RpcRangedShot();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Death
    // ─────────────────────────────────────────────────────────────────────────────

    [Server]
    void OnDeath()
    {
        state = EnemyState.Dead;
        StopAllCoroutines();

        if (_agent != null && _agent.isActiveAndEnabled) _agent.enabled = false;
        foreach (var col in GetComponents<Collider>()) col.enabled = false;

        StartCoroutine(DeathSequence());
    }

    [Server]
    IEnumerator DeathSequence()
    {
        RpcPlayDeathEffect();
        yield return new WaitForSeconds(0.4f);   // brief VFX moment

        if (dropTable != null)
        {
            var (items, gold) = dropTable.RollDrops();

            foreach (var (itemId, qty) in items)
            {
                SpawnWorldItem(itemId, qty);
                Debug.Log($"[LOOT] {name} dropped {qty}x {itemId}");
            }

            if (gold > 0)
            {
                SpawnWorldItem($"gold:{gold}", 1);
                Debug.Log($"[LOOT] {name} dropped {gold} gold");
            }
        }

        yield return new WaitForSeconds(2.6f);
        NetworkServer.Destroy(gameObject);
    }

    [Server]
    void SpawnWorldItem(string itemId, int qty)
    {
        if (worldItemPrefab == null)
        {
            Debug.LogWarning($"[COMBAT] {name}: worldItemPrefab not assigned — loot lost");
            return;
        }

        Vector3 offset = Random.insideUnitSphere * 1.2f;
        offset.y = 0.5f;

        var wi   = Instantiate(worldItemPrefab, transform.position + offset, Quaternion.identity);
        var comp = wi.GetComponent<WorldItem>();
        if (comp != null) { comp.itemId = itemId; comp.quantity = qty; }
        NetworkServer.Spawn(wi);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────────

    [Server]
    void ResetToIdle()
    {
        _target      = null;
        state        = EnemyState.Idle;
        _attackTimer = 0f;
        if (_agent != null && _agent.isActiveAndEnabled) _agent.ResetPath();
        // Restore full speed (slow may have been applied)
        if (_agent != null) _agent.speed = _baseSpeed;
    }

    void OnStateChanged(EnemyState _, EnemyState newState)
    {
        // Hook animator here when animation rig is ready (Week 7)
        // GetComponent<Animator>()?.SetInteger("state", (int)newState);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // RPCs (Week 7: wire anim + SFX)
    // ─────────────────────────────────────────────────────────────────────────────

    [ClientRpc] void RpcMeleeSwing()    { /* swing anim + impact SFX */ }
    [ClientRpc] void RpcRangedShot()    { /* ranged anim + projectile SFX */ }
    [ClientRpc] void RpcPlayDeathEffect() { /* death VFX + SFX */ }

    // ─────────────────────────────────────────────────────────────────────────────
    // Gizmos
    // ─────────────────────────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        Vector3 origin = Application.isPlaying ? _spawnPos : transform.position;

        Gizmos.color = new Color(1f, 1f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, aggroRadius);

        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.15f);
        Gizmos.DrawWireSphere(origin, leashRadius);
    }
}
