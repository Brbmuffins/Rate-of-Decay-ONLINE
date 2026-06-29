using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// ═══════════════════════════════════════════════════════════════════════════
//  WaveManager
//  Scene-level arena orchestrator. Place one in GameWorld.
//
//  DESIGN PILLARS
//  ──────────────
//  • Harder = more reward: loot multiplier scales with wave number + difficulty.
//  • Shared goal: one enemy pool all players damage together. Boss HP is
//    a single pool; any player can deal the killing blow.
//  • Multiple paths: wave mix includes mobs, elites, and world bosses.
//    Players can skip killing mobs and focus the boss — it still works.
//  • Player-count scaling: enemy count and HP scale with how many are online.
//
//  WAVE STRUCTURE
//  ──────────────
//  Waves 1–3   → Mob waves (EnemyAI swarms)
//  Wave  4–6   → Elite waves (AI with class-like abilities, higher HP)
//  Wave  7     → Mini-boss (BossController, phase 1 only)
//  Waves 8–9   → Mixed (mobs + elites)
//  Wave  10    → World Boss (BossController, full mechanics, shared HP pool)
//  Pattern repeats with +20% difficulty per full cycle.
//
//  LOOT
//  ────
//  Base loot score = wave number × difficultyMultiplier × playerCount.
//  Passed to WaveChest (if present) via onWaveCycleComplete event.
//  The event carries the score so the chest or future loot system can
//  determine drop rarity/quantity without coupling to this class.
// ═══════════════════════════════════════════════════════════════════════════

public class WaveManager : MonoBehaviour
{
    // ── Wave Definition ───────────────────────────────────────────────────

    [System.Serializable]
    public class WaveDefinition
    {
        [Tooltip("Label shown in UI (e.g. 'Wave 3 — Elites')")]
        public string label = "Wave";

        [Header("Mob Spawn")]
        public GameObject mobPrefab;
        public int        mobCount       = 5;
        public float      mobHpMultiplier = 1f;

        [Header("Elite Spawn")]
        public GameObject elitePrefab;
        public int        eliteCount      = 0;
        public float      eliteHpMultiplier = 2f;

        [Header("Boss")]
        public GameObject bossPrefab;            // null = no boss this wave
        [Tooltip("If true, boss death ends the wave immediately (mobs despawn).")]
        public bool       bossKillEndsWave = false;

        [Header("Timing")]
        public float prepTime          = 5f;     // countdown before spawns
        public float timeBetweenSpawns = 0.4f;   // stagger delay
    }

    // ── Inspector ─────────────────────────────────────────────────────────

    [Header("Wave Roster")]
    [Tooltip("Define each wave in order. Loops after the last wave with +difficulty.")]
    public WaveDefinition[] waves;

    [Header("Spawn Points")]
    [Tooltip("Enemies spawn from these transforms. Randomly selected per spawn.")]
    public Transform[] spawnPoints;

    [Header("Difficulty Scaling")]
    [Tooltip("Each full cycle multiplies enemy HP and count by this.")]
    public float cycleDifficultyMultiplier = 1.2f;
    [Tooltip("Maximum cycles before difficulty caps.")]
    public int maxCycles = 5;

    [Header("Arena")]
    [Tooltip("Enemies that wander beyond this distance from the arena center are destroyed.")]
    public float arenaRadius = 30f;

    [Header("Loot")]
    [Tooltip("Base loot score per wave. Multiplied by wave# × difficulty × playerCount.")]
    public float baseLootScore = 10f;

    [Header("Events — wire to UI / VFX / WaveChest")]
    public UnityEvent                    onArenaStarted;
    public UnityEvent<string, int, int>  onWaveStart;        // (label, waveNum, totalWaves)
    public UnityEvent<float>             onPrepTick;         // countdown seconds remaining
    public UnityEvent<int>               onWaveCleared;      // wave index
    public UnityEvent<float>             onWaveCycleComplete;// loot score for this cycle
    public UnityEvent                    onArenaFailed;       // all players dead

    // ── Runtime state ─────────────────────────────────────────────────────

    int   _currentWave  = -1;
    int   _cycle        = 0;
    bool  _running      = false;
    float _difficulty   = 1f;

    readonly List<GameObject> _aliveEnemies = new List<GameObject>();
    int _aliveCount => _aliveEnemies.Count;

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Call this to start the arena (from a button, trigger, or WaveChest).</summary>
    public void StartArena()
    {
        if (_running) return;
        _running = true;
        _cycle   = 0;
        _difficulty = 1f;
        StartCoroutine(RunArena());
        onArenaStarted?.Invoke();
        Debug.Log("[WaveManager] Arena started.");
    }

    public int  CurrentWave   => _currentWave + 1;
    public int  TotalWaves    => waves.Length;
    public int  CurrentCycle  => _cycle + 1;
    public bool IsRunning     => _running;

    /// <summary>GM command: jump directly to a wave index, restarting the arena if needed.</summary>
    public void JumpToWave(int waveIndex)
    {
        StopAllCoroutines();
        DespawnRemaining(); // destroy current enemies instead of orphaning them (also clears the list)
        waveIndex = Mathf.Clamp(waveIndex - 1, 0, waves.Length - 1); // convert 1-based to 0-based
        _currentWave = waveIndex;
        _running     = true;
        StartCoroutine(RunFromWave(waveIndex));
        Debug.Log($"[WaveManager] GM jumped to wave {waveIndex + 1}.");
    }

    IEnumerator RunFromWave(int startWave)
    {
        for (int w = startWave; w < waves.Length; w++)
        {
            _currentWave = w;
            var def = waves[w];
            yield return StartCoroutine(PrepCountdown(def.prepTime));
            int displayNum = _cycle * waves.Length + w + 1;
            onWaveStart?.Invoke(def.label, displayNum, -1);
            yield return StartCoroutine(SpawnWave(def));
            yield return StartCoroutine(WaitForClear(def));
            onWaveCleared?.Invoke(displayNum);
            if (w < waves.Length - 1) yield return new WaitForSeconds(3f);
        }
        float loot = CalculateLootScore();
        onWaveCycleComplete?.Invoke(loot);
    }

    // ── Arena loop ────────────────────────────────────────────────────────

    IEnumerator RunArena()
    {
        while (_running)
        {
            for (int w = 0; w < waves.Length; w++)
            {
                _currentWave = w;
                var def = waves[w];

                // ── Prep countdown ──
                yield return StartCoroutine(PrepCountdown(def.prepTime));

                // ── Announce wave ──
                int displayNum = _cycle * waves.Length + w + 1;
                onWaveStart?.Invoke(def.label, displayNum, -1);
                Debug.Log($"[WaveManager] Wave {displayNum} — {def.label} (cycle {_cycle + 1}, ×{_difficulty:F1})");

                // ── Spawn enemies ──
                yield return StartCoroutine(SpawnWave(def));

                // ── Wait for clear ──
                yield return StartCoroutine(WaitForClear(def));

                onWaveCleared?.Invoke(displayNum);
                Debug.Log($"[WaveManager] Wave {displayNum} cleared.");

                // Brief breather between waves (not after the last one)
                if (w < waves.Length - 1)
                    yield return new WaitForSeconds(3f);
            }

            // ── Cycle complete ──
            float loot = CalculateLootScore();
            onWaveCycleComplete?.Invoke(loot);
            Debug.Log($"[WaveManager] Cycle {_cycle + 1} complete. Loot score: {loot:F0}");

            _cycle++;
            _difficulty = Mathf.Min(
                Mathf.Pow(cycleDifficultyMultiplier, _cycle),
                Mathf.Pow(cycleDifficultyMultiplier, maxCycles));

            // Short rest before next cycle
            yield return new WaitForSeconds(8f);
        }
    }

    IEnumerator PrepCountdown(float seconds)
    {
        float remaining = seconds;
        while (remaining > 0f)
        {
            onPrepTick?.Invoke(remaining);
            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }
    }

    IEnumerator SpawnWave(WaveDefinition def)
    {
        int playerCount = CountActivePlayers();

        // ── Mobs ──
        int mobCount = Mathf.RoundToInt(def.mobCount * _difficulty * Mathf.Sqrt(playerCount));
        for (int i = 0; i < mobCount; i++)
        {
            if (def.mobPrefab == null) break;
            SpawnEnemy(def.mobPrefab, def.mobHpMultiplier * _difficulty);
            yield return new WaitForSeconds(def.timeBetweenSpawns);
        }

        // ── Elites ──
        int eliteCount = Mathf.RoundToInt(def.eliteCount * _difficulty);
        for (int i = 0; i < eliteCount; i++)
        {
            if (def.elitePrefab == null) break;
            SpawnEnemy(def.elitePrefab, def.eliteHpMultiplier * _difficulty);
            yield return new WaitForSeconds(def.timeBetweenSpawns * 2f);
        }

        // ── Boss ──
        if (def.bossPrefab != null)
            SpawnEnemy(def.bossPrefab, _difficulty, isBoss: true);
    }

    IEnumerator WaitForClear(WaveDefinition def)
    {
        // Purge nulls (enemies destroyed outside OnDeath — e.g. despawned)
        while (true)
        {
            _aliveEnemies.RemoveAll(e => e == null);

            if (_aliveEnemies.Count == 0) yield break;

            // Boss-kill-ends-wave: if a boss was spawned and is dead, end early
            if (def.bossKillEndsWave && !BossAlive())
            {
                DespawnRemaining();
                yield break;
            }

            // Fail check — all players dead
            if (AllPlayersDead())
            {
                _running = false;
                onArenaFailed?.Invoke();
                Debug.Log("[WaveManager] All players dead — arena failed.");
                yield break;
            }

            yield return new WaitForSeconds(0.5f);
        }
    }

    // ── Spawn helper ──────────────────────────────────────────────────────

    void SpawnEnemy(GameObject prefab, float hpMult, bool isBoss = false)
    {
        Vector3 pos = RandomSpawnPoint();
        var go = Instantiate(prefab, pos, Quaternion.identity);
        go.tag = "Enemy";

        // Scale HP
        var health = go.GetComponent<Health>();
        if (health != null)
        {
            health.maxHealth *= hpMult;
            // Reset current HP to match scaled max
            health.currentHealth = health.maxHealth;
        }

        // Boss label
        if (isBoss) go.name = $"[BOSS] {prefab.name}";

        // Track for wave clear
        _aliveEnemies.Add(go);

        // Remove from tracking on death
        if (health != null)
            health.onDeath.AddListener(() =>
            {
                _aliveEnemies.Remove(go);
            });
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    Vector3 RandomSpawnPoint()
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
            return spawnPoints[Random.Range(0, spawnPoints.Length)].position;

        // Fallback: random point on arena edge
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float r     = arenaRadius * 0.85f;
        return transform.position + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
    }

    float CalculateLootScore()
    {
        int players = CountActivePlayers();
        int wave    = (_cycle * waves.Length) + waves.Length;
        return baseLootScore * wave * _difficulty * Mathf.Sqrt(players);
    }

    bool BossAlive()
    {
        foreach (var e in _aliveEnemies)
            if (e != null && e.name.StartsWith("[BOSS]")) return true;
        return false;
    }

    void DespawnRemaining()
    {
        foreach (var e in _aliveEnemies)
            if (e != null) Destroy(e);
        _aliveEnemies.Clear();
    }

    bool AllPlayersDead()
    {
        var players = GameObject.FindGameObjectsWithTag("Player");
        foreach (var p in players)
        {
            var h = p.GetComponent<Health>();
            if (h != null && h.IsAlive) return false;
        }
        return true;
    }

    int CountActivePlayers()
    {
        return Mathf.Max(1, GameObject.FindGameObjectsWithTag("Player").Length);
    }

    // ── Arena boundary — leash strayed enemies back ───────────────────────

    void Update()
    {
        if (!_running) return;
        for (int i = _aliveEnemies.Count - 1; i >= 0; i--)
        {
            var e = _aliveEnemies[i];
            if (e == null) { _aliveEnemies.RemoveAt(i); continue; }
            if (Vector3.Distance(e.transform.position, transform.position) > arenaRadius)
            {
                // Snap back to a spawn point rather than destroy — keeps wave counts stable
                e.transform.position = RandomSpawnPoint() + Vector3.up;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.25f);
        Gizmos.DrawSphere(transform.position, arenaRadius);

        if (spawnPoints == null) return;
        Gizmos.color = Color.yellow;
        foreach (var sp in spawnPoints)
            if (sp != null) Gizmos.DrawSphere(sp.position, 0.5f);
    }
}
