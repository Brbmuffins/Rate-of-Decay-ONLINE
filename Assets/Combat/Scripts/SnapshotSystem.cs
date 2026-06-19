using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Attach to a persistent manager object in the scene.
// Records position/HP snapshots for all registered characters every 0.5s.
// System Rollback replays the team 5 seconds backward.
public class SnapshotSystem : MonoBehaviour
{
    public static SnapshotSystem Instance { get; private set; }

    [Header("Settings")]
    public float snapshotInterval = 0.5f;
    public int   maxSnapshots     = 10;   // 10 × 0.5s = 5 seconds

    // ── Snapshot data ─────────────────────────────────────────────
    [System.Serializable]
    private struct CharSnap
    {
        public Vector3      position;
        public Quaternion   rotation;
        public float        hp;
        // NOTE: status effects are intentionally NOT snapshotted — Rollback
        // clears all debuffs (see ApplyRollback), so there's nothing to restore.
    }

    // Each entry in the ring buffer is one point-in-time for ALL tracked characters.
    private struct FrameSnap
    {
        public Dictionary<int, CharSnap> chars; // instanceID → snap
    }

    private readonly List<FrameSnap>   _buffer    = new List<FrameSnap>();
    private readonly List<GameObject>  _tracked   = new List<GameObject>();
    private float _timer;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= snapshotInterval)
        {
            _timer = 0f;
            TakeSnapshot();
        }
    }

    // Register a character (player) to be tracked.
    public void Track(GameObject go)
    {
        if (!_tracked.Contains(go))
            _tracked.Add(go);
    }

    public void Untrack(GameObject go) => _tracked.Remove(go);

    // System Rollback: rewind all tracked characters by `seconds` (max 5).
    public void Rollback(float seconds)
    {
        int stepsBack = Mathf.Clamp(Mathf.RoundToInt(seconds / snapshotInterval), 1, _buffer.Count);
        if (_buffer.Count == 0) return;

        FrameSnap target = _buffer[Mathf.Max(0, _buffer.Count - stepsBack)];
        StartCoroutine(ApplyRollback(target));
    }

    private IEnumerator ApplyRollback(FrameSnap frame)
    {
        // Brief flash pause — give player feedback
        yield return new WaitForSeconds(0.05f);

        foreach (GameObject go in _tracked)
        {
            if (go == null) continue;
            int id = go.GetInstanceID();
            if (!frame.chars.TryGetValue(id, out CharSnap snap)) continue;

            // Restore position
            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.MovePosition(snap.position);
                rb.MoveRotation(snap.rotation);
                rb.linearVelocity = Vector3.zero;
            }
            else
            {
                go.transform.SetPositionAndRotation(snap.position, snap.rotation);
            }

            // Restore HP
            Health health = go.GetComponent<Health>();
            if (health != null)
            {
                health.currentHealth = Mathf.Max(snap.hp, 1f); // never restore to 0
                health.onHealthChanged?.Invoke(health.currentHealth, health.maxHealth);

                // Revive downed players if they had HP in the snapshot
                if (health.IsDowned && snap.hp > 0f)
                    health.Revive(snap.hp / health.maxHealth);
            }

            // Clear all debuffs
            StatusEffectManager sem = go.GetComponent<StatusEffectManager>();
            sem?.RemoveAll();
        }
    }

    private void TakeSnapshot()
    {
        var frame = new FrameSnap { chars = new Dictionary<int, CharSnap>() };

        foreach (GameObject go in _tracked)
        {
            if (go == null) continue;
            Health h = go.GetComponent<Health>();

            frame.chars[go.GetInstanceID()] = new CharSnap
            {
                position = go.transform.position,
                rotation = go.transform.rotation,
                hp       = h != null ? h.currentHealth : 0f
            };
        }

        _buffer.Add(frame);
        if (_buffer.Count > maxSnapshots)
            _buffer.RemoveAt(0);
    }
}
