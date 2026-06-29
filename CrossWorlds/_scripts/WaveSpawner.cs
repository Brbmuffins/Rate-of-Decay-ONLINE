using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <summary>
/// WaveSpawner — Server-authoritative arena wave manager.
///
/// Setup: BCE/Setup/4e ▶ Create Wave Spawner (drops this into the scene).
/// Assign enemy prefabs and spawn points in the inspector.
///
/// Wave escalation:
///   Wave 1: 4 enemies  (baseEnemiesPerWave)
///   Wave 2: 6 enemies  (+enemiesAddedPerWave each wave)
///   Wave 3: 8 enemies  + 1 Elite
///   Wave N: escalates indefinitely until players die or quit
///
/// Trigger:
///   Call StartWaves() from your portal arrival trigger.
///   Or call StopWaves() to halt between sessions.
///
/// Copy to: Assets/Game/Combat/WaveSpawner.cs
/// </summary>
public class WaveSpawner : NetworkBehaviour
{
    // ─── Inspector ────────────────────────────────────────────────────────────
    [Header("Enemy Prefabs")]
    [Tooltip("Index 0 = Grunt, Index 1 = Ranged")]
    public List<GameObject> enemyPrefabs = new List<GameObject>();
    public GameObject elitePrefab;

    [Header("Spawn Points")]
    public List<Transform> spawnPoints = new List<Transform>();

    [Header("Wave Config")]
    public int   baseEnemiesPerWave  = 4;
    public int   enemiesAddedPerWave = 2;
    [Tooltip("Seconds between waves after all enemies die")]
    public float timeBetweenWaves    = 8f;
    [Tooltip("Stagger between each individual enemy spawn")]
    public float spawnStagger        = 0.5f;
    [Tooltip("Spawn an elite every N waves (0 = never)")]
    public int   eliteEveryNWaves    = 3;

    [Header("Start Delay")]
    [Tooltip("Seconds after StartWaves() before Wave 1 begins")]
    public float introDelay = 4f;

    // ─── Synced State ─────────────────────────────────────────────────────────
    [SyncVar(hook = nameof(OnWaveChanged))]
    public int currentWave = 0;

    [SyncVar]
    public int enemiesAlive = 0;

    [SyncVar]
    public bool waveActive = false;

    // ─── Internal ─────────────────────────────────────────────────────────────
    private bool _running = false;

    // ─── Public API ───────────────────────────────────────────────────────────
    [Server]
    public void StartWaves()
    {
        if (_running) return;
        _running = true;
        StartCoroutine(WaveLoop());
    }

    [Server]
    public void StopWaves()
    {
        _running = false;
        StopAllCoroutines();
        waveActive = false;
    }

    // ─── Core Loop ────────────────────────────────────────────────────────────
    [Server]
    IEnumerator WaveLoop()
    {
        RpcAnnounce($"Arena active — first wave in {introDelay}s. Prepare!");
        yield return new WaitForSeconds(introDelay);

        while (_running)
        {
            currentWave++;
            yield return StartCoroutine(RunWave(currentWave));

            if (!_running) yield break;

            RpcAnnounce($"Wave {currentWave} cleared! Next wave in {timeBetweenWaves}s...");
            yield return new WaitForSeconds(timeBetweenWaves);
        }
    }

    [Server]
    IEnumerator RunWave(int wave)
    {
        int count        = baseEnemiesPerWave + (wave - 1) * enemiesAddedPerWave;
        bool spawnElite  = eliteEveryNWaves > 0 && wave % eliteEveryNWaves == 0 && elitePrefab != null;

        RpcAnnounce($"Wave {wave} — {count + (spawnElite ? 1 : 0)} enemies incoming!");
        yield return new WaitForSeconds(1.5f);

        waveActive = true;

        for (int i = 0; i < count; i++)
        {
            if (!_running) yield break;
            SpawnEnemy(PickEnemyPrefab());
            yield return new WaitForSeconds(spawnStagger);
        }

        if (spawnElite)
        {
            yield return new WaitForSeconds(1f);
            SpawnEnemy(elitePrefab);
            RpcAnnounce("⚠ ELITE has arrived!");
        }

        // Wait until all enemies are dead
        while (enemiesAlive > 0 && _running)
            yield return new WaitForSeconds(0.5f);

        waveActive = false;
    }

    // ─── Spawn ────────────────────────────────────────────────────────────────
    [Server]
    void SpawnEnemy(GameObject prefab)
    {
        if (prefab == null) { Debug.LogError("[WAVE] Enemy prefab is null — check inspector"); return; }

        Transform sp = GetSpawnPoint();
        var enemy = Instantiate(prefab, sp.position, sp.rotation);

        var health = enemy.GetComponent<Health>();
        if (health != null)
            health.onDeath.AddListener(OnEnemyDied);

        NetworkServer.Spawn(enemy);
        enemiesAlive++;
    }

    [Server]
    void OnEnemyDied()
    {
        enemiesAlive = Mathf.Max(0, enemiesAlive - 1);

        if (enemiesAlive == 0 && waveActive)
            Debug.Log($"[WAVE] Wave {currentWave}: all enemies dead");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    GameObject PickEnemyPrefab()
    {
        if (enemyPrefabs == null || enemyPrefabs.Count == 0)
        {
            Debug.LogError("[WAVE] No enemy prefabs assigned to WaveSpawner");
            return null;
        }
        // 67% grunt (index 0), 33% ranged (index 1) if available
        if (enemyPrefabs.Count > 1 && Random.value < 0.33f)
            return enemyPrefabs[1];
        return enemyPrefabs[0];
    }

    Transform GetSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Count == 0) return transform;
        return spawnPoints[Random.Range(0, spawnPoints.Count)];
    }

    // ─── SyncVar Hook ─────────────────────────────────────────────────────────
    void OnWaveChanged(int oldVal, int newVal)
    {
        // Update ArenaHUD wave counter if it exists
        // var hud = FindObjectOfType<ArenaHUD>();
        // if (hud != null) hud.UpdateWave(newVal);
    }

    // ─── Announcements ────────────────────────────────────────────────────────
    [ClientRpc]
    void RpcAnnounce(string message)
    {
        Debug.Log($"[WAVE] {message}");
        var chat = FindObjectOfType<RodChatManager>();
        if (chat != null) chat.ReceiveBossAnnouncement(message);
    }

    // ─── Gizmos ───────────────────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        if (spawnPoints == null) return;
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.8f);
        foreach (var sp in spawnPoints)
        {
            if (sp == null) continue;
            Gizmos.DrawWireSphere(sp.position, 1f);
            Gizmos.DrawLine(transform.position, sp.position);
        }
    }
}
