using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// Place on a chest GameObject. Player holds E to open it.
// A prep window gives the team time to position before waves begin.
// After all waves clear, the chest rewards loot.
public class WaveChest : MonoBehaviour
{
    [System.Serializable]
    public struct Wave
    {
        public GameObject enemyPrefab;
        public int        count;
    }

    [Header("Interaction")]
    public float holdTime      = 2f;   // seconds to hold E
    public float prepTime      = 8f;   // seconds before wave 1 (Engineer prep window)
    public float timeBetweenWaves = 5f;

    [Header("Waves")]
    public Wave[] waves;
    public float  spawnRadius  = 10f;  // enemies spawn around the chest

    [Header("Loot")]
    public GameObject lootPrefab;      // spawns on chest when done

    [Header("UI feedback (optional)")]
    public UnityEvent             onOpened;          // play open animation / sound
    public UnityEvent<int, int>   onWaveStart;       // (waveNumber, totalWaves)
    public UnityEvent<float>      onPrepTick;        // remaining prep seconds
    public UnityEvent             onAllWavesCleared;

    private bool   _opened     = false;
    private float  _holdProgress = 0f;
    private int    _aliveCount   = 0;

    private GameObject _cachedNearest;   // refreshed on a timer, not every frame
    private float      _scanTimer  = 0f;

    void Update()
    {
        if (_opened) return;

        // Simple proximity check — swap for InputSystem.InputAction if your players use it.
        // Nearest-player lookup is throttled (proximity barely changes frame-to-frame).
        _scanTimer -= Time.deltaTime;
        if (_scanTimer <= 0f)
        {
            _cachedNearest = FindNearestPlayer();
            _scanTimer = 0.25f;
        }
        if (_cachedNearest == null) return;

        float dist = Vector3.Distance(transform.position, _cachedNearest.transform.position);
        bool inRange = dist <= 2.5f;

        // NOTE: Uses old Input.GetKey — if your game uses InputSystem, wire an InputAction here instead
        if (inRange && Input.GetKey(KeyCode.E))
        {
            _holdProgress += Time.deltaTime;
            if (_holdProgress >= holdTime)
                StartCoroutine(ChestSequence());
        }
        else
        {
            _holdProgress = Mathf.Max(0f, _holdProgress - Time.deltaTime * 2f);
        }
    }

    IEnumerator ChestSequence()
    {
        _opened = true;
        onOpened.Invoke();

        // Prep window — count down so UI can show it
        float remaining = prepTime;
        while (remaining > 0f)
        {
            onPrepTick.Invoke(remaining);
            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }

        // Run each wave
        for (int i = 0; i < waves.Length; i++)
        {
            onWaveStart.Invoke(i + 1, waves.Length);
            yield return StartCoroutine(RunWave(waves[i]));

            if (i < waves.Length - 1)
                yield return new WaitForSeconds(timeBetweenWaves);
        }

        onAllWavesCleared.Invoke();

        if (lootPrefab != null)
            Instantiate(lootPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);

        // Open the chest lid visually — animate or just disable the lock mesh
        Animator anim = GetComponent<Animator>();
        if (anim != null) anim.SetTrigger("Open");
    }

    IEnumerator RunWave(Wave wave)
    {
        if (wave.enemyPrefab == null) yield break;

        int nearby = CountNearbyPlayers(20f);
        // Scale count with player count (min 1 player worth)
        int scaled = Mathf.Max(wave.count, wave.count * nearby / 2);
        _aliveCount = scaled;

        for (int j = 0; j < scaled; j++)
        {
            Vector3 spawnPos = RandomSpawnPoint();
            GameObject enemy = Instantiate(wave.enemyPrefab, spawnPos, Quaternion.identity);

            Health h = enemy.GetComponent<Health>();
            if (h != null)
            {
                h.onDeath.AddListener(() =>
                {
                    _aliveCount--;
                });
            }

            yield return new WaitForSeconds(0.3f); // stagger spawns slightly
        }

        // Wait for every enemy to die
        while (_aliveCount > 0)
            yield return new WaitForSeconds(0.5f);
    }

    Vector3 RandomSpawnPoint()
    {
        Vector2 rand = Random.insideUnitCircle.normalized * spawnRadius;
        Vector3 pos  = transform.position + new Vector3(rand.x, 0f, rand.y);

        // Try to land on NavMesh if available
        if (UnityEngine.AI.NavMesh.SamplePosition(pos, out var hit, 3f, UnityEngine.AI.NavMesh.AllAreas))
            return hit.position;

        return pos;
    }

    GameObject FindNearestPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        GameObject best = null;
        float bestDist = Mathf.Infinity;
        foreach (var p in players)
        {
            float d = Vector3.Distance(transform.position, p.transform.position);
            if (d < bestDist) { bestDist = d; best = p; }
        }
        return best;
    }

    int CountNearbyPlayers(float radius)
    {
        int count = 0;
        foreach (var col in Physics.OverlapSphere(transform.position, radius))
            if (col.CompareTag("Player")) count++;
        return Mathf.Max(1, count);
    }
}
