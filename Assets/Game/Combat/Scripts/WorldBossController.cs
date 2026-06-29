using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <summary>
/// WorldBossController — The Null Architect.
///
/// Phase 1 (100–60 %): Reflect Pulse — all nearby players take damage when
///   they strike the boss during a reflect window.
/// Phase 2  (60–30 %): Three Null Shards — must burst simultaneously or
///   the surviving shards cross-heal. Periodic Tether Web pairs players.
/// Phase 3  (30–0 %): Void Drain forces melee stacking. Final Surge at 10 %.
///
/// Phase transitions are event-driven via Health.onHealthChanged — no polling.
/// Reflect is server-authoritative via Health.onDamageTaken.
///
/// Requires: Health, NavMeshAgent, StatusEffectManager, NetworkIdentity
/// </summary>
[RequireComponent(typeof(Health))]
[RequireComponent(typeof(StatusEffectManager))]
public class WorldBossController : NetworkBehaviour
{
    // ── Phase thresholds ────────────────────────────────────────────────────────
    [Header("Phase HP Thresholds")]
    public float phase2Threshold    = 0.60f;
    public float phase3Threshold    = 0.30f;
    public float finalSurgeThreshold = 0.10f;

    // ── Phase 1 — Reflect Pulse ─────────────────────────────────────────────────
    [Header("Phase 1 — Reflect Pulse")]
    public float reflectPulseInterval      = 18f;
    public float reflectTelegraphDuration  = 3f;
    public float reflectWindowDuration     = 4f;
    [Tooltip("Fraction of incoming damage re-dealt to all players in range.")]
    [Range(0f, 2f)] public float reflectDamageFraction = 0.75f;
    public float reflectAoeRadius          = 10f;
    public GameObject reflectTelegraphVFX;

    // ── Phase 2 — Shards & Tether ────────────────────────────────────────────────
    [Header("Phase 2 — Null Shards")]
    public GameObject nullShardPrefab;
    public float shardSpreadRadius       = 6f;

    [Header("Phase 2 — Tether Web")]
    public float tetherWebInterval       = 25f;
    public float tetherWebDuration       = 6f;
    public float tetherWebLeashDistance  = 6f;
    public float tetherWebSnapDamage     = 40f;

    // ── Phase 3 — Void Drain ─────────────────────────────────────────────────────
    [Header("Phase 3 — Void Drain")]
    public float voidDrainInterval   = 12f;
    public float voidDrainRadius     = 5f;
    public float voidDrainTickDamage = 8f;
    public float voidDrainDuration   = 4f;
    public GameObject voidDrainVFX;

    // ── Phase 3 — Final Surge ────────────────────────────────────────────────────
    [Header("Phase 3 — Final Surge")]
    public float finalSurgeSpeedMultiplier  = 3f;
    public float finalSurgeAttackMultiplier = 3f;
    public float finalSurgeDuration         = 15f;

    // ── Transition ────────────────────────────────────────────────────────────────
    [Header("Transition")]
    public float immunityWindowDuration = 4f;

    // ── Loot ─────────────────────────────────────────────────────────────────────
    [Header("Drop Table")]
    public List<string> guaranteedDropItemIds = new List<string> { "sword_iron", "plate_iron" };
    public List<string> rareDropItemIds       = new List<string> { "ring_copper", "material_copper_bar" };
    [Range(0f, 1f)] public float rareDropChance = 0.35f;

    // ── Phase state ───────────────────────────────────────────────────────────────
    public enum BossPhase { Idle, Phase1, Transition, Phase2, Phase3, Dead }

    [SyncVar(hook = nameof(OnPhaseSync))]
    public BossPhase currentPhase = BossPhase.Idle;

    [SyncVar] public bool isImmune    = false;
    [SyncVar] public bool isReflecting = false;
    [SyncVar] public bool isDraining   = false;

    // ── Private ───────────────────────────────────────────────────────────────────
    private Health                        _health;
    private UnityEngine.AI.NavMeshAgent   _agent;
    private StatusEffectManager           _status;
    private float                         _baseSpeed;
    private bool                          _finalSurgeTriggered  = false;
    private bool                          _inTransition         = false;
    private readonly List<GameObject>     _activeShards         = new List<GameObject>();

    // Per-phase ability coroutines (stored so we can stop only the active one)
    private Coroutine _phase1Coroutine;
    private Coroutine _phase2Coroutine;
    private Coroutine _phase3Coroutine;

    // ─────────────────────────────────────────────────────────────────────────────
    // Bootstrap
    // ─────────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        _health = GetComponent<Health>();
        _agent  = GetComponent<UnityEngine.AI.NavMeshAgent>();
        _status = GetComponent<StatusEffectManager>();
        if (_agent != null) _baseSpeed = _agent.speed;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _health.onDeath.AddListener(OnBossDeath);
        _health.onDamageTaken.AddListener(OnDamageTakenServer);
        _health.onHealthChanged.AddListener(OnHealthChangedServer);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Public — called by BossTrigger when first player enters
    // ─────────────────────────────────────────────────────────────────────────────

    [Server]
    public void StartFight()
    {
        if (currentPhase != BossPhase.Idle) return;
        currentPhase = BossPhase.Phase1;
        _phase1Coroutine = StartCoroutine(RunPhase1Abilities());
        RpcAnnounce("Phase 1 — The Mirror begins. Cease fire during REFLECT windows!");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Health event listeners (server-side)
    // ─────────────────────────────────────────────────────────────────────────────

    /// Called every time the boss takes damage.
    /// During reflect window: AoE pulse hits all nearby players.
    [Server]
    void OnDamageTakenServer(float damage)
    {
        if (!isReflecting || damage <= 0f) return;

        float reflected = damage * reflectDamageFraction;
        int   hitCount  = 0;

        foreach (var col in Physics.OverlapSphere(transform.position, reflectAoeRadius))
        {
            if (!col.CompareTag("Player")) continue;
            col.GetComponent<Health>()?.TakeDamage(reflected, gameObject);
            hitCount++;
        }

        if (hitCount > 0)
            RpcAnnounce($"⚡ REFLECT — {reflected:F0} damage pulsed to {hitCount} player(s)!");
    }

    /// Drives phase transitions without polling.
    [Server]
    void OnHealthChangedServer(float currentHp, float maxHp)
    {
        if (_inTransition || !_health.IsAlive) return;

        float fraction = maxHp > 0f ? currentHp / maxHp : 0f;

        if (currentPhase == BossPhase.Phase1 && fraction <= phase2Threshold)
        {
            StartCoroutine(BeginTransition(BossPhase.Phase2));
        }
        else if (currentPhase == BossPhase.Phase2 && fraction <= phase3Threshold)
        {
            StartCoroutine(BeginTransition(BossPhase.Phase3));
        }
        else if (currentPhase == BossPhase.Phase3
                 && !_finalSurgeTriggered
                 && fraction <= finalSurgeThreshold)
        {
            _finalSurgeTriggered = true;
            StartCoroutine(RunFinalSurge());
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Phase transitions
    // ─────────────────────────────────────────────────────────────────────────────

    [Server]
    IEnumerator BeginTransition(BossPhase nextPhase)
    {
        _inTransition = true;

        // Stop current phase ability loop
        if (_phase1Coroutine != null) { StopCoroutine(_phase1Coroutine); _phase1Coroutine = null; }
        if (_phase2Coroutine != null) { StopCoroutine(_phase2Coroutine); _phase2Coroutine = null; }
        if (_phase3Coroutine != null) { StopCoroutine(_phase3Coroutine); _phase3Coroutine = null; }

        isReflecting = false;

        string flavour = nextPhase == BossPhase.Phase2
            ? "PHASE SHIFT — Null Architect fragments into shards!"
            : "CRITICAL — Null Architect destabilises! All damage amplified!";

        currentPhase = BossPhase.Transition;
        isImmune     = true;
        RpcAnnounce(flavour);
        RpcShowTransitionVFX();

        yield return new WaitForSeconds(immunityWindowDuration);

        isImmune     = false;
        currentPhase = nextPhase;
        _inTransition = false;

        if (nextPhase == BossPhase.Phase2)
        {
            _phase2Coroutine = StartCoroutine(RunPhase2Abilities());
            RpcAnnounce("Phase 2 — Shard Fracture. Destroy all shards simultaneously or they cross-heal!");
            SpawnShards();
        }
        else if (nextPhase == BossPhase.Phase3)
        {
            // Apply Weakened debuff to boss — incoming damage +25%
            _status?.AddEffect(new StatusEffect(StatusEffectType.Weakened, 99999f, 0.25f));
            _phase3Coroutine = StartCoroutine(RunPhase3Abilities());
            RpcAnnounce("Phase 3 — Null Architect EXPOSED. Stay inside Void Drain range or suffer!");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Phase 1 — Periodic Reflect Pulse
    // ─────────────────────────────────────────────────────────────────────────────

    [Server]
    IEnumerator RunPhase1Abilities()
    {
        while (currentPhase == BossPhase.Phase1 && _health.IsAlive)
        {
            yield return new WaitForSeconds(reflectPulseInterval);
            if (currentPhase != BossPhase.Phase1) yield break;

            // Telegraph
            RpcAnnounce($"⚠ REFLECT in {reflectTelegraphDuration}s — stop attacking!");
            RpcShowReflectTelegraph();
            yield return new WaitForSeconds(reflectTelegraphDuration);

            if (currentPhase != BossPhase.Phase1) yield break;

            // Active reflect window
            isReflecting = true;
            RpcAnnounce("⚡ REFLECT ACTIVE — do NOT attack the boss!");
            yield return new WaitForSeconds(reflectWindowDuration);
            isReflecting = false;
            RpcAnnounce("Reflect window closed — resume attack!");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Phase 2 — Null Shards + Tether Web
    // ─────────────────────────────────────────────────────────────────────────────

    [Server]
    void SpawnShards()
    {
        if (nullShardPrefab == null)
        {
            Debug.LogError("[BOSS] nullShardPrefab not assigned — skipping shards");
            return;
        }

        _activeShards.Clear();
        Vector3[] offsets = { Vector3.forward, Vector3.left, Vector3.right };

        foreach (var offset in offsets)
        {
            Vector3 pos   = transform.position + offset * shardSpreadRadius;
            var shard     = Instantiate(nullShardPrefab, pos, Quaternion.identity);
            NetworkServer.Spawn(shard);
            _activeShards.Add(shard);

            var shardHealth = shard.GetComponent<Health>();
            if (shardHealth == null) continue;

            var captured = shard;
            shardHealth.onDamageTaken.AddListener(dmg => OnShardDamaged(captured, dmg));
            shardHealth.onDeath.AddListener(() => OnShardDeath(captured));
        }

        RpcAnnounce($"3 Null Shards spawned! Kill them simultaneously — " +
                    $"survivors cross-heal for 50% of damage dealt to the others.");
    }

    [Server]
    void OnShardDamaged(GameObject damagedShard, float damage)
    {
        // Surviving shards cross-heal for 50% of damage dealt to their sibling
        foreach (var shard in _activeShards)
        {
            if (shard == null || shard == damagedShard) continue;
            shard.GetComponent<Health>()?.Heal(damage * 0.5f);
        }
    }

    [Server]
    void OnShardDeath(GameObject shard)
    {
        _activeShards.Remove(shard);
        int remaining = _activeShards.Count;
        RpcAnnounce(remaining > 0
            ? $"Shard destroyed — {remaining} remaining! Kill them together!"
            : "All shards destroyed — Null Architect reassembles!");
    }

    [Server]
    IEnumerator RunPhase2Abilities()
    {
        while (currentPhase == BossPhase.Phase2 && _health.IsAlive)
        {
            yield return new WaitForSeconds(tetherWebInterval);
            if (currentPhase != BossPhase.Phase2) yield break;
            yield return StartCoroutine(RunTetherWeb());
        }
    }

    [Server]
    IEnumerator RunTetherWeb()
    {
        RpcAnnounce($"⚠ TETHER WEB — stay within {tetherWebLeashDistance}u of your partner or take {tetherWebSnapDamage} damage!");

        var players = new List<Health>();
        foreach (var h in FindObjectsOfType<Health>())
            if (h.IsAlive && h.gameObject.CompareTag("Player")) players.Add(h);

        // Pair players; odd one out is safe
        for (int i = 0; i + 1 < players.Count; i += 2)
        {
            var a = players[i];
            var b = players[i + 1];
            RpcCreateTether(a.GetComponent<NetworkIdentity>().netId,
                            b.GetComponent<NetworkIdentity>().netId,
                            tetherWebDuration);

            StartCoroutine(EnforceTether(a, b, tetherWebDuration));
        }

        yield return new WaitForSeconds(tetherWebDuration + 0.5f);
        RpcAnnounce("Tether Web dissipated — spread out!");
    }

    [Server]
    IEnumerator EnforceTether(Health a, Health b, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
            if (a == null || b == null || !a.IsAlive || !b.IsAlive) yield break;
            if (Vector3.Distance(a.transform.position, b.transform.position) > tetherWebLeashDistance)
            {
                a.TakeDamage(tetherWebSnapDamage, gameObject);
                b.TakeDamage(tetherWebSnapDamage, gameObject);
                RpcAnnounce($"Tether snapped! {a.name} and {b.name} took {tetherWebSnapDamage} damage.");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Phase 3 — Void Drain + Final Surge
    // ─────────────────────────────────────────────────────────────────────────────

    [Server]
    IEnumerator RunPhase3Abilities()
    {
        while (currentPhase == BossPhase.Phase3 && _health.IsAlive)
        {
            yield return new WaitForSeconds(voidDrainInterval);
            if (currentPhase != BossPhase.Phase3) yield break;
            yield return StartCoroutine(RunVoidDrain());
        }
    }

    [Server]
    IEnumerator RunVoidDrain()
    {
        isDraining = true;
        RpcAnnounce($"⚠ VOID DRAIN — stack within {voidDrainRadius}u of the boss or take {voidDrainTickDamage}/s!");
        RpcShowVoidDrainVFX(true);

        float elapsed = 0f;
        while (elapsed < voidDrainDuration)
        {
            yield return new WaitForSeconds(1f);
            elapsed += 1f;
            foreach (var h in FindObjectsOfType<Health>())
            {
                if (!h.IsAlive || !h.gameObject.CompareTag("Player")) continue;
                if (Vector3.Distance(h.transform.position, transform.position) > voidDrainRadius)
                    h.TakeDamage(voidDrainTickDamage, gameObject);
            }
        }

        isDraining = false;
        RpcShowVoidDrainVFX(false);
        RpcAnnounce("Void Drain ended — spread out!");
    }

    [Server]
    IEnumerator RunFinalSurge()
    {
        RpcAnnounce("⚠⚠ FINAL SURGE — Null Architect ENRAGED! Burn it down NOW!");
        if (_agent != null) _agent.speed = _baseSpeed * finalSurgeSpeedMultiplier;

        yield return new WaitForSeconds(finalSurgeDuration);

        if (_health.IsAlive)
        {
            if (_agent != null) _agent.speed = _baseSpeed;
            RpcAnnounce("Final Surge ended.");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Death
    // ─────────────────────────────────────────────────────────────────────────────

    [Server]
    void OnBossDeath()
    {
        currentPhase = BossPhase.Dead;

        // Stop all ability coroutines cleanly
        if (_phase1Coroutine != null) StopCoroutine(_phase1Coroutine);
        if (_phase2Coroutine != null) StopCoroutine(_phase2Coroutine);
        if (_phase3Coroutine != null) StopCoroutine(_phase3Coroutine);
        isReflecting = false;
        isDraining   = false;

        RpcAnnounce("💀 BOSS DEFEATED — The Null Architect collapses!");
        StartCoroutine(BossDeathSequence());
    }

    [Server]
    IEnumerator BossDeathSequence()
    {
        RpcPlayDeathVFX();
        yield return new WaitForSeconds(3f);
        RollDrops();
        yield return new WaitForSeconds(2f);
        NetworkServer.Destroy(gameObject);
    }

    [Server]
    void RollDrops()
    {
        foreach (var id in guaranteedDropItemIds)
        {
            RpcSpawnLoot(id);
            Debug.Log($"[LOOT] Boss dropped (guaranteed): {id}");
        }
        foreach (var id in rareDropItemIds)
        {
            if (Random.value <= rareDropChance)
            {
                RpcSpawnLoot(id);
                Debug.Log($"[LOOT] Boss dropped (rare): {id}");
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Phase change hook (clients)
    // ─────────────────────────────────────────────────────────────────────────────

    void OnPhaseSync(BossPhase _, BossPhase newPhase)
    {
        FindObjectOfType<WorldBossHealthBar>()?.OnPhaseChanged(newPhase);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // RPCs
    // ─────────────────────────────────────────────────────────────────────────────

    [ClientRpc]
    void RpcAnnounce(string message)
    {
        Debug.Log($"[BOSS] {message}");
        RodChatManager.Instance?.AddSystemMessage(message);
    }

    [ClientRpc]
    void RpcShowReflectTelegraph()
    {
        if (reflectTelegraphVFX != null)
            Instantiate(reflectTelegraphVFX, transform.position, Quaternion.identity);
    }

    [ClientRpc]
    void RpcShowTransitionVFX() { /* Wire phase-transition VFX here (Week 7) */ }

    [ClientRpc]
    void RpcShowVoidDrainVFX(bool active)
    {
        if (voidDrainVFX != null) voidDrainVFX.SetActive(active);
    }

    [ClientRpc]
    void RpcPlayDeathVFX() { /* Wire death VFX here (Week 7) */ }

    [ClientRpc]
    void RpcSpawnLoot(string itemId)
    {
        Debug.Log($"[BOSS] Loot dropped: {itemId}");
    }

    [ClientRpc]
    void RpcCreateTether(uint netIdA, uint netIdB, float duration)
    {
        StartCoroutine(ShowTetherVisual(netIdA, netIdB, duration));
    }

    IEnumerator ShowTetherVisual(uint netIdA, uint netIdB, float duration)
    {
        // Wire LineRenderer tether visual here (Week 7)
        yield return new WaitForSeconds(duration);
    }
}
