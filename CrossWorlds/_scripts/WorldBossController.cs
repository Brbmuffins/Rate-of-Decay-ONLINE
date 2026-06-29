using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <summary>
/// WorldBossController — The Null Architect
/// Drop onto a boss GameObject that also has a Health component and NavMeshAgent.
/// Handles all three phases, immunity windows, shared HP pool, and telegraphed abilities.
///
/// Phase 1 (100–60%): Reflect Pulse — direct damage reflected back. DoT builds shine.
/// Phase 2 (60–30%): Three Null Shards — must burst simultaneously. Tether Web.
/// Phase 3 (30–0%): Exposed permanently. Void Drain forces melee stacking. Final Surge at 10%.
///
/// Copy to: Assets/Game/Combat/WorldBossController.cs
/// </summary>
[RequireComponent(typeof(Health))]
public class WorldBossController : NetworkBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Phase HP Thresholds")]
    public float phase2Threshold = 0.60f;   // 60% HP triggers Phase 2
    public float phase3Threshold = 0.30f;   // 30% HP triggers Phase 3
    public float finalSurgeThreshold = 0.10f; // 10% HP — Final Surge

    [Header("Phase 1 — Reflect Pulse")]
    public float reflectPulseInterval = 18f;
    public float reflectTelegraphDuration = 3f;  // glow warning before window opens
    public float reflectWindowDuration = 4f;     // damage reflected during this window
    public GameObject reflectTelegraphVFX;       // assign in inspector — warning glow prefab

    [Header("Phase 2 — Shards")]
    public GameObject nullShardPrefab;           // prefab for the 3 split shards
    public float shardSpreadRadius = 6f;
    public float tethreWebInterval = 25f;
    public float tethreWebDuration = 6f;
    public float tethreWebLeashDistance = 6f;
    public float tethreWebSnapDamage = 40f;

    [Header("Phase 3 — Void Drain")]
    public float voidDrainInterval = 12f;
    public float voidDrainRadius = 5f;
    public float voidDrainTickDamage = 8f;
    public float voidDrainDuration = 4f;
    public GameObject voidDrainVFX;

    [Header("Phase 3 — Final Surge")]
    public float finalSurgeSpeedMultiplier = 3f;
    public float finalSurgeAttackMultiplier = 3f;
    public float finalSurgeDuration = 15f;

    [Header("Transition")]
    public float immunityWindowDuration = 4f;

    [Header("Drop Table")]
    public List<string> guaranteedDropItemIds = new List<string> { "sword_iron", "plate_iron" };
    public List<string> rareDropItemIds = new List<string> { "ring_copper", "copper_bar" };
    [Range(0f, 1f)] public float rareDropChance = 0.35f;

    // ─── State ────────────────────────────────────────────────────────────────
    public enum BossPhase { Idle, Phase1, Transition, Phase2, Phase3, Dead }

    [SyncVar(hook = nameof(OnPhaseChanged))]
    public BossPhase currentPhase = BossPhase.Idle;

    [SyncVar]
    public bool isImmune = false;

    [SyncVar]
    public bool isReflecting = false;   // Phase 1 reflect window active

    [SyncVar]
    public bool isDraining = false;     // Phase 3 drain window active

    // ─── Internal ─────────────────────────────────────────────────────────────
    private Health _health;
    private UnityEngine.AI.NavMeshAgent _agent;
    private float _baseSpeed;
    private float _baseAttackRate;
    private bool _finalSurgeTriggered = false;
    private List<GameObject> _activeShards = new List<GameObject>();

    // Tether tracking (Phase 2)
    private List<(NetworkIdentity, NetworkIdentity)> _activeTethers = new List<(NetworkIdentity, NetworkIdentity)>();

    // ─── Init ─────────────────────────────────────────────────────────────────
    void Awake()
    {
        _health = GetComponent<Health>();
        _agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (_agent != null) _baseSpeed = _agent.speed;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _health.onDeath.AddListener(OnBossDeath);
        _health.onDamageReceived.AddListener(OnDamageReceived);
    }

    // ─── Entry Point ──────────────────────────────────────────────────────────
    [Server]
    public void StartFight()
    {
        currentPhase = BossPhase.Phase1;
        StartCoroutine(PhaseLoop());
    }

    // ─── Core Phase Loop ──────────────────────────────────────────────────────
    [Server]
    IEnumerator PhaseLoop()
    {
        // Phase 1
        yield return StartCoroutine(RunPhase1());

        // Transition 1→2
        yield return StartCoroutine(RunTransition("PHASE SHIFT — Null Architect fragments"));

        // Phase 2
        yield return StartCoroutine(RunPhase2());

        // Transition 2→3
        yield return StartCoroutine(RunTransition("CRITICAL — Null Architect destabilising"));

        // Phase 3
        yield return StartCoroutine(RunPhase3());
    }

    // ─── Phase 1: Reflect Pulse ───────────────────────────────────────────────
    [Server]
    IEnumerator RunPhase1()
    {
        currentPhase = BossPhase.Phase1;
        RpcAnnounce("Phase 1 — The Mirror begins");

        while (currentPhase == BossPhase.Phase1 && !_health.isDead)
        {
            yield return new WaitForSeconds(reflectPulseInterval);
            if (currentPhase != BossPhase.Phase1) yield break;

            // Telegraph
            RpcShowReflectTelegraph();
            yield return new WaitForSeconds(reflectTelegraphDuration);

            // Open reflect window
            isReflecting = true;
            RpcAnnounce("⚠ REFLECT ACTIVE — Hold direct damage!");
            yield return new WaitForSeconds(reflectWindowDuration);
            isReflecting = false;
            RpcAnnounce("Reflect window closed");
        }
    }

    // ─── Phase 2: Null Shards + Tether Web ────────────────────────────────────
    [Server]
    IEnumerator RunPhase2()
    {
        currentPhase = BossPhase.Phase2;
        RpcAnnounce("Phase 2 — Shard Fracture. Damage all shards simultaneously!");

        // Boss becomes invisible/inactive — shards take over
        GetComponent<Collider>().enabled = false;
        GetComponent<Renderer>().enabled = false;
        if (_agent != null) _agent.enabled = false;

        SpawnShards();

        // Run tether web on interval while shards are alive
        while (_activeShards.Count > 0 && !_health.isDead)
        {
            yield return new WaitForSeconds(tethreWebInterval);
            if (_activeShards.Count == 0) break;
            yield return StartCoroutine(RunTetherWeb());
        }

        // All shards dead — reassemble boss
        GetComponent<Collider>().enabled = true;
        GetComponent<Renderer>().enabled = true;
        if (_agent != null) _agent.enabled = true;
    }

    [Server]
    void SpawnShards()
    {
        if (nullShardPrefab == null) { Debug.LogError("[BOSS] nullShardPrefab not assigned"); return; }
        _activeShards.Clear();
        Vector3[] offsets = { Vector3.forward, Vector3.left, Vector3.right };
        foreach (var offset in offsets)
        {
            Vector3 pos = transform.position + offset * shardSpreadRadius;
            GameObject shard = Instantiate(nullShardPrefab, pos, Quaternion.identity);
            NetworkServer.Spawn(shard);
            _activeShards.Add(shard);

            // Wire shard death callback
            var shardHealth = shard.GetComponent<Health>();
            if (shardHealth != null)
            {
                var captured = shard;
                shardHealth.onDeath.AddListener(() => OnShardDeath(captured));
                // Shard cross-heal: when one takes damage, others heal
                shardHealth.onDamageReceived.AddListener((dmg, src) => OnShardDamaged(captured, dmg));
            }
        }
        RpcAnnounce($"3 Null Shards spawned — burst all at once or they cross-heal!");
    }

    [Server]
    void OnShardDamaged(GameObject damagedShard, float damage)
    {
        // Other shards heal for 50% of damage dealt to any one shard
        foreach (var shard in _activeShards)
        {
            if (shard == damagedShard || shard == null) continue;
            var h = shard.GetComponent<Health>();
            if (h != null) h.Heal(damage * 0.5f);
        }
    }

    [Server]
    void OnShardDeath(GameObject shard)
    {
        _activeShards.Remove(shard);
        RpcAnnounce($"Shard destroyed — {_activeShards.Count} remaining!");
        if (_activeShards.Count == 0)
            RpcAnnounce("All shards destroyed — Null Architect reassembles!");
    }

    [Server]
    IEnumerator RunTetherWeb()
    {
        RpcAnnounce("⚠ TETHER WEB — Stay within 6 units of your partner!");
        var players = FindObjectsOfType<NetworkIdentity>(); // filter to players only
        var playerList = new List<NetworkIdentity>();
        foreach (var p in players)
        {
            if (p.GetComponent<Health>() != null && p.gameObject.CompareTag("Player"))
                playerList.Add(p);
        }

        // Pair players and apply tethers
        for (int i = 0; i + 1 < playerList.Count; i += 2)
        {
            RpcCreateTether(playerList[i].netId, playerList[i + 1].netId, tethreWebDuration);
        }

        // Server-side leash enforcement
        float elapsed = 0f;
        while (elapsed < tethreWebDuration)
        {
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
            for (int i = 0; i + 1 < playerList.Count; i += 2)
            {
                if (playerList[i] == null || playerList[i + 1] == null) continue;
                float dist = Vector3.Distance(
                    playerList[i].transform.position,
                    playerList[i + 1].transform.position);
                if (dist > tethreWebLeashDistance)
                {
                    playerList[i].GetComponent<Health>()?.TakeDamage(tethreWebSnapDamage, null);
                    playerList[i + 1].GetComponent<Health>()?.TakeDamage(tethreWebSnapDamage, null);
                    RpcAnnounce("Tether snapped! Players took snap damage.");
                }
            }
        }
        RpcAnnounce("Tether Web dissipated");
    }

    // ─── Phase 3: Exposed + Void Drain + Final Surge ──────────────────────────
    [Server]
    IEnumerator RunPhase3()
    {
        currentPhase = BossPhase.Phase3;
        RpcAnnounce("Phase 3 — Null Architect EXPOSED. All damage amplified!");

        // Apply permanent Exposed to boss (StatusEffectManager if available)
        var status = GetComponent<StatusEffectManager>();
        if (status != null) status.ApplyExposed(9999f);  // effectively permanent

        while (!_health.isDead)
        {
            // Check Final Surge threshold
            if (!_finalSurgeTriggered && _health.currentHp / _health.maxHp <= finalSurgeThreshold)
            {
                _finalSurgeTriggered = true;
                StartCoroutine(RunFinalSurge());
            }

            yield return new WaitForSeconds(voidDrainInterval);
            if (_health.isDead) yield break;
            yield return StartCoroutine(RunVoidDrain());
        }
    }

    [Server]
    IEnumerator RunVoidDrain()
    {
        isDraining = true;
        RpcAnnounce("⚠ VOID DRAIN — Stack inside the boss radius or take 8/s!");
        RpcShowVoidDrainVFX(true);

        float elapsed = 0f;
        while (elapsed < voidDrainDuration)
        {
            yield return new WaitForSeconds(1f);
            elapsed += 1f;
            // Damage players OUTSIDE the radius
            var players = FindObjectsOfType<Health>();
            foreach (var p in players)
            {
                if (!p.gameObject.CompareTag("Player")) continue;
                float dist = Vector3.Distance(p.transform.position, transform.position);
                if (dist > voidDrainRadius)
                    p.TakeDamage(voidDrainTickDamage, null);
            }
        }

        isDraining = false;
        RpcShowVoidDrainVFX(false);
        RpcAnnounce("Void Drain ended — spread out!");
    }

    [Server]
    IEnumerator RunFinalSurge()
    {
        RpcAnnounce("⚠⚠ FINAL SURGE — Boss enraged! Burn it down!");
        if (_agent != null) _agent.speed = _baseSpeed * finalSurgeSpeedMultiplier;

        yield return new WaitForSeconds(finalSurgeDuration);

        if (!_health.isDead)
        {
            if (_agent != null) _agent.speed = _baseSpeed;
            RpcAnnounce("Final Surge ended");
        }
    }

    // ─── Transition (Immunity Window) ─────────────────────────────────────────
    [Server]
    IEnumerator RunTransition(string message)
    {
        currentPhase = BossPhase.Transition;
        isImmune = true;
        RpcAnnounce(message);
        RpcShowTransitionVFX();
        yield return new WaitForSeconds(immunityWindowDuration);
        isImmune = false;
    }

    // ─── Damage Hook — Reflect + Phase Transitions ───────────────────────────
    [Server]
    void OnDamageReceived(float damage, GameObject source)
    {
        // Reflect damage back to source during Phase 1 window
        if (isReflecting && source != null)
        {
            var srcHealth = source.GetComponent<Health>();
            if (srcHealth != null)
            {
                srcHealth.TakeDamage(damage * 1.5f, null);
                RpcAnnounce($"Damage reflected back at {source.name}!");
            }
            // Heal the boss — nullify the damage
            _health.Heal(damage);
        }

        // Check phase transitions
        float hpPct = _health.currentHp / _health.maxHp;
        if (currentPhase == BossPhase.Phase1 && hpPct <= phase2Threshold)
            StopAllCoroutines(); // PhaseLoop handles the rest via StartFight
        if (currentPhase == BossPhase.Phase2 && hpPct <= phase3Threshold)
            StopAllCoroutines();
    }

    // ─── Death ────────────────────────────────────────────────────────────────
    [Server]
    void OnBossDeath()
    {
        currentPhase = BossPhase.Dead;
        StopAllCoroutines();
        RpcAnnounce("BOSS DEFEATED — The Null Architect collapses!");
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
        // Guaranteed drops broadcast to all players
        foreach (var itemId in guaranteedDropItemIds)
            RpcSpawnLoot(itemId);

        // Rare drops
        foreach (var itemId in rareDropItemIds)
        {
            if (Random.value <= rareDropChance)
                RpcSpawnLoot(itemId);
        }
    }

    // ─── Phase Change Hook (runs on all clients) ──────────────────────────────
    void OnPhaseChanged(BossPhase oldPhase, BossPhase newPhase)
    {
        // Update boss health bar phase markers
        var hpBar = FindObjectOfType<WorldBossHealthBar>();
        if (hpBar != null) hpBar.OnPhaseChanged(newPhase);
    }

    // ─── ClientRPCs ───────────────────────────────────────────────────────────
    [ClientRpc]
    void RpcAnnounce(string message)
    {
        Debug.Log($"[BOSS] {message}");
        // Hook into your chat system to show boss announcements in-world
        var chat = FindObjectOfType<RodChatManager>();
        if (chat != null) chat.ReceiveBossAnnouncement(message);
    }

    [ClientRpc]
    void RpcShowReflectTelegraph()
    {
        if (reflectTelegraphVFX != null)
            Instantiate(reflectTelegraphVFX, transform.position, Quaternion.identity);
    }

    [ClientRpc]
    void RpcShowTransitionVFX()
    {
        // Add any between-phase VFX here
    }

    [ClientRpc]
    void RpcShowVoidDrainVFX(bool active)
    {
        if (voidDrainVFX != null) voidDrainVFX.SetActive(active);
    }

    [ClientRpc]
    void RpcPlayDeathVFX()
    {
        // Particle burst on death — wire up brbmuffins EnergyExplosion here
    }

    [ClientRpc]
    void RpcSpawnLoot(string itemId)
    {
        Debug.Log($"[BOSS] Loot dropped: {itemId}");
        // WorldItem prefab spawns here with the item ID
        // Wire to your WorldItemSpawner when built
    }

    [ClientRpc]
    void RpcCreateTether(uint playerNetIdA, uint playerNetIdB, float duration)
    {
        // Visual tether line between two players
        // Implement with LineRenderer between the two player positions
        StartCoroutine(ShowTetherVisual(playerNetIdA, playerNetIdB, duration));
    }

    IEnumerator ShowTetherVisual(uint netIdA, uint netIdB, float duration)
    {
        // Stub — wire up LineRenderer for tether visual
        yield return new WaitForSeconds(duration);
    }
}
