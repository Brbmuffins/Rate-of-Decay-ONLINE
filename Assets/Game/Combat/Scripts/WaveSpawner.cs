using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <summary>
/// WaveSpawner — Server-authoritative arena wave manager.
/// Escalates enemy count each wave. 67% grunt / 33% ranged split.
/// Elites spawn every N waves. Waits for all enemies to die before advancing.
///
/// Setup: BCE/Setup/4e drops this into the active scene with 4 cardinal spawn points.
/// Call StartWaves() from your portal arrival trigger.
/// </summary>
public class WaveSpawner : NetworkBehaviour
{
    [Header("Enemy Prefabs")]
    [Tooltip("Index 0 = Grunt, Index 1 = Ranged")]
    public List<GameObject> enemyPrefabs = new List<GameObject>();
    public GameObject elitePrefab;

    [Header("Spawn Points")]
    public List<Transform> spawnPoints = new List<Transform>();

    [Header("Wave Config")]
    public int   baseEnemiesPerWave  = 4;
    public int   enemiesAddedPerWave = 2;
    public float timeBetweenWaves    = 8f;
    public float spawnStagger        = 0.5f;
    public int   eliteEveryNWaves    = 3;
    public float introDelay          = 4f;

    [SyncVar(hook = nameof(OnWaveChanged))]
    public int  currentWave  = 0;
    [SyncVar] public int  enemiesAlive = 0;
    [SyncVar] public bool waveActive   = false;

    private bool _running = false;

    // ─────────────────────────────────────────────────────────────────────────────

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
        _running   = false;
        waveActive = false;
        StopAllCoroutines();
        Debug.Log("[WAVE] WaveSpawner stopped.");
    }

    // ─────────────────────────────────────────────────────────────────────────────

    [Server]
    IEnumerator WaveLoop()
    {
        RpcAnnounce($"Arena active! First wave in {introDelay}s. Prepare!");
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
        int  count      = baseEnemiesPerWave + (wave - 1) * enemiesAddedPerWave;
        bool spawnElite = eliteEveryNWaves > 0
                          && wave % eliteEveryNWaves == 0
                          && elitePrefab != null;

        int total = count + (spawnElite ? 1 : 0);
        RpcAnnounce($"Wave {wave} — {total} enemies incoming!");
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

    [Server]
    void SpawnEnemy(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError("[WAVE] Prefab is null — check WaveSpawner inspector");
            return;
        }

        Transform sp  = GetSpawnPoint();
        var enemy     = Instantiate(prefab, sp.position, sp.rotation);
        var health    = enemy.GetComponent<Health>();
        if (health != null) health.onDeath.AddListener(OnEnemyDied);
        NetworkServer.Spawn(enemy);
        enemiesAlive++;
    }

    [Server]
    void OnEnemyDied() => enemiesAlive = Mathf.Max(0, enemiesAlive - 1);

    GameObject PickEnemyPrefab()
    {
        if (enemyPrefabs == null || enemyPrefabs.Count == 0)
        {
            Debug.LogError("[WAVE] No enemy prefabs assigned on WaveSpawner");
            return null;
        }
        return enemyPrefabs.Count > 1 && Random.value < 0.33f
            ? enemyPrefabs[1]
            : enemyPrefabs[0];
    }

    Transform GetSpawnPoint()
    {
        if (spawnPoints == null || spawnPoints.Count == 0) return transform;
        return spawnPoints[Random.Range(0, spawnPoints.Count)];
    }

    void OnWaveChanged(int _, int newVal)
    {
        // Hook into ArenaHUD.UpdateWave(newVal) when that UI exists (Week 7)
    }

    [ClientRpc]
    void RpcAnnounce(string message)
    {
        Debug.Log($"[WAVE] {message}");
        RodChatManager.Instance?.AddSystemMessage(message);
    }

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
