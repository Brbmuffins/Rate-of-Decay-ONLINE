using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Mirror;

/// <summary>
/// EnemyController — Server-authoritative enemy AI.
/// State machine: Idle → Chase → Attack → Dead
/// Supports melee and ranged variants via isRanged toggle.
///
/// Required components: Health, NavMeshAgent, Collider
/// Optional:           EnemyProjectile prefab (ranged only)
///
/// Copy to: Assets/Game/Combat/EnemyController.cs
///
/// Setup via: BCE/Setup/4a (grunt) or BCE/Setup/4b (ranged)
/// Or wire manually in inspector.
/// </summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : NetworkBehaviour
{
    // ─── State ────────────────────────────────────────────────────────────────
    public enum EnemyState { Idle, Chase, Attack, Dead }

    [SyncVar(hook = nameof(OnStateChanged))]
    public EnemyState state = EnemyState.Idle;

    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Detection")]
    public float aggroRadius = 8f;
    public float leashRadius = 20f;   // lose aggro beyond this distance from spawn

    [Header("Combat")]
    public float attackRange = 1.5f;
    public float attackInterval = 1.5f;
    public float damage = 12f;

    [Header("Ranged")]
    public bool isRanged = false;
    public GameObject projectilePrefab;
    [Tooltip("Ranged enemy tries to maintain this distance from target")]
    public float preferredRange = 5f;
    [Tooltip("Ranged enemies back up if target is closer than this")]
    public float tooCloseDistance = 3f;

    [Header("Drops")]
    public DropTable dropTable;
    public GameObject worldItemPrefab;   // WorldItem prefab — assign in inspector or via EnemyBuilder

    // ─── Internal ─────────────────────────────────────────────────────────────
    private Health _health;
    private NavMeshAgent _agent;
    private Transform _target;
    private Vector3 _spawnPos;
    private float _attackTimer;

    // ─── Init ─────────────────────────────────────────────────────────────────
    void Awake()
    {
        _health = GetComponent<Health>();
        _agent  = GetComponent<NavMeshAgent>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _spawnPos = transform.position;
        _health.onDeath.AddListener(OnDeath);
        StartCoroutine(BehaviorLoop());
    }

    // ─── Main Loop (server only, 5 ticks/sec) ────────────────────────────────
    [Server]
    IEnumerator BehaviorLoop()
    {
        var tick = new WaitForSeconds(0.2f);
        while (!_health.isDead)
        {
            yield return tick;
            switch (state)
            {
                case EnemyState.Idle:   Idle();   break;
                case EnemyState.Chase:  Chase();  break;
                case EnemyState.Attack: Attack(); break;
            }
        }
    }

    // ─── Idle — scan for players ──────────────────────────────────────────────
    [Server]
    void Idle()
    {
        // OverlapSphere is server-only here — no client physics needed
        var hits = Physics.OverlapSphere(transform.position, aggroRadius);
        float nearest = float.MaxValue;
        Transform found = null;

        foreach (var col in hits)
        {
            if (!col.CompareTag("Player")) continue;
            // Skip dead players
            var h = col.GetComponent<Health>();
            if (h != null && h.isDead) continue;

            float d = Vector3.Distance(transform.position, col.transform.position);
            if (d < nearest) { nearest = d; found = col.transform; }
        }

        if (found != null)
        {
            _target = found;
            state   = EnemyState.Chase;
        }
    }

    // ─── Chase — pathfind toward target ───────────────────────────────────────
    [Server]
    void Chase()
    {
        if (_target == null || (_target.GetComponent<Health>()?.isDead ?? true))
        {
            ResetToIdle();
            return;
        }

        float dist = Vector3.Distance(transform.position, _target.position);

        // Leash check — if target is too far from spawn, give up
        if (dist > leashRadius)
        {
            ResetToIdle();
            _agent.SetDestination(_spawnPos);
            return;
        }

        if (isRanged)
        {
            if (dist < tooCloseDistance)
            {
                // Back away
                Vector3 away = (transform.position - _target.position).normalized;
                _agent.SetDestination(transform.position + away * 3f);
            }
            else
            {
                _agent.SetDestination(_target.position);
            }

            if (dist <= attackRange)
                state = EnemyState.Attack;
        }
        else
        {
            _agent.SetDestination(_target.position);
            if (dist <= attackRange)
                state = EnemyState.Attack;
        }
    }

    // ─── Attack ───────────────────────────────────────────────────────────────
    [Server]
    void Attack()
    {
        if (_target == null || (_target.GetComponent<Health>()?.isDead ?? true))
        {
            ResetToIdle();
            return;
        }

        float dist = Vector3.Distance(transform.position, _target.position);

        // Broke out of attack range — go back to chasing
        if (dist > attackRange * 1.3f)
        {
            state = EnemyState.Chase;
            return;
        }

        // Stop moving while attacking (melee); ranged keeps distance-adjusting
        if (!isRanged)
            _agent.SetDestination(transform.position);

        // Face target
        Vector3 dir = (_target.position - transform.position);
        dir.y = 0f;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir);

        // Cooldown tick (loop runs at 0.2s intervals)
        _attackTimer -= 0.2f;
        if (_attackTimer > 0f) return;
        _attackTimer = attackInterval;

        PerformAttack();
    }

    [Server]
    void PerformAttack()
    {
        if (_target == null) return;
        var targetHealth = _target.GetComponent<Health>();
        if (targetHealth == null || targetHealth.isDead) return;

        if (!isRanged)
        {
            // Melee — direct hit
            targetHealth.TakeDamage(damage, gameObject);
            RpcMeleeSwing();
        }
        else
        {
            // Ranged — spawn projectile or hitscan fallback
            if (projectilePrefab != null)
            {
                Vector3 spawnPos = transform.position + Vector3.up * 1.2f;
                Quaternion spawnRot = Quaternion.LookRotation(_target.position + Vector3.up - spawnPos);
                var proj = Instantiate(projectilePrefab, spawnPos, spawnRot);
                var ep   = proj.GetComponent<EnemyProjectile>();
                if (ep != null) ep.Init(damage);
                NetworkServer.Spawn(proj);
            }
            else
            {
                // Hitscan if no projectile prefab assigned
                targetHealth.TakeDamage(damage, gameObject);
            }
            RpcRangedShot();
        }
    }

    // ─── Death ────────────────────────────────────────────────────────────────
    [Server]
    void OnDeath()
    {
        state = EnemyState.Dead;
        StopAllCoroutines();

        if (_agent != null && _agent.isActiveAndEnabled)
            _agent.enabled = false;

        // Disable colliders so dead enemy isn't hittable
        foreach (var col in GetComponents<Collider>())
            col.enabled = false;

        StartCoroutine(DeathSequence());
    }

    [Server]
    IEnumerator DeathSequence()
    {
        RpcPlayDeathEffect();
        yield return new WaitForSeconds(0.4f);   // short pause before loot

        if (dropTable != null)
        {
            var (items, gold) = dropTable.RollDrops();
            foreach (var (itemId, qty) in items)
                SpawnWorldItem(itemId, qty);
            if (gold > 0)
                SpawnWorldItem($"gold:{gold}", 1);
        }

        yield return new WaitForSeconds(2.6f);   // loot stays visible for 3s total
        NetworkServer.Destroy(gameObject);
    }

    [Server]
    void SpawnWorldItem(string itemId, int qty)
    {
        if (worldItemPrefab == null)
        {
            Debug.LogWarning($"[COMBAT] EnemyController on {name}: worldItemPrefab not assigned — no loot spawned");
            return;
        }
        // Scatter drops around death position
        Vector3 offset = Random.insideUnitSphere * 1.2f;
        offset.y = 0.5f;
        Vector3 pos = transform.position + offset;

        var wi     = Instantiate(worldItemPrefab, pos, Quaternion.identity);
        var wiComp = wi.GetComponent<WorldItem>();
        if (wiComp != null)
        {
            wiComp.itemId   = itemId;
            wiComp.quantity = qty;
        }
        NetworkServer.Spawn(wi);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    [Server]
    void ResetToIdle()
    {
        _target = null;
        state   = EnemyState.Idle;
        if (_agent != null && _agent.isActiveAndEnabled)
            _agent.ResetPath();
    }

    // ─── State Hook (clients) ─────────────────────────────────────────────────
    void OnStateChanged(EnemyState oldState, EnemyState newState)
    {
        // Extend here for client-side animation state machine:
        // GetComponent<Animator>()?.SetInteger("state", (int)newState);
    }

    // ─── ClientRPCs (animation / VFX hooks) ──────────────────────────────────
    [ClientRpc]
    void RpcMeleeSwing()
    {
        // Trigger swing animation + SFX in Week 7 polish
    }

    [ClientRpc]
    void RpcRangedShot()
    {
        // Trigger ranged attack animation + SFX
    }

    [ClientRpc]
    void RpcPlayDeathEffect()
    {
        // Death VFX + SFX in Week 7 polish
    }

    // ─── Scene Gizmos ─────────────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, aggroRadius);

        Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = new Color(0f, 0.5f, 1f, 0.2f);
        Gizmos.DrawWireSphere(Application.isPlaying ? _spawnPos : transform.position, leashRadius);
    }
}
