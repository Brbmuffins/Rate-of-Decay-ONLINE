using System.Collections.Generic;
using UnityEngine;

// Singleton. Tracks all active deployables per player.
// Enforces per-class limits (Engineer: 3, all others: 1).
// Used by Overengineered passive and System Overload.
public class DeployableManager : MonoBehaviour
{
    public static DeployableManager Instance { get; private set; }

    // ownerInstanceID → ordered list of active deployables (oldest first)
    private readonly Dictionary<int, List<GameObject>> _byOwner =
        new Dictionary<int, List<GameObject>>();

    // deployable → output stack count (0–5), driven by Overengineered
    private readonly Dictionary<GameObject, int>   _stacks     = new Dictionary<GameObject, int>();
    private readonly Dictionary<GameObject, float> _multiplier = new Dictionary<GameObject, float>();

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // Call this right after spawning a deployable.
    // ownerID    = owner.GetInstanceID()
    // classLimit = max simultaneous deployables for this class (Engineer=3, others=1)
    public void Register(GameObject deployable, int ownerID, int classLimit = 1)
    {
        if (!_byOwner.ContainsKey(ownerID))
            _byOwner[ownerID] = new List<GameObject>();

        List<GameObject> list = _byOwner[ownerID];

        // Destroy oldest if we're at the limit
        while (list.Count >= classLimit)
        {
            GameObject oldest = list[0];
            list.RemoveAt(0);
            _stacks.Remove(oldest);
            _multiplier.Remove(oldest);
            if (oldest != null) Destroy(oldest);
        }

        list.Add(deployable);
        _stacks[deployable]     = 0;
        _multiplier[deployable] = 1f;
    }

    // Call from OnDestroy of the deployable object.
    public void Unregister(GameObject deployable)
    {
        _stacks.Remove(deployable);
        _multiplier.Remove(deployable);
        foreach (var list in _byOwner.Values)
            list.Remove(deployable);
    }

    public List<GameObject> GetAll(int ownerID)
    {
        return _byOwner.TryGetValue(ownerID, out var list)
            ? new List<GameObject>(list)
            : new List<GameObject>();
    }

    // ── Output stacks (Overengineered) ───────────────────────────

    public int GetStacks(GameObject dep) =>
        _stacks.TryGetValue(dep, out int s) ? s : 0;

    public void AddStack(GameObject dep)
    {
        if (!_stacks.ContainsKey(dep)) return;
        _stacks[dep] = Mathf.Min(_stacks[dep] + 1, 5);
        RecalcMultiplier(dep);
    }

    public void MaxStacks(GameObject dep)
    {
        if (!_stacks.ContainsKey(dep)) return;
        _stacks[dep] = 5;
        RecalcMultiplier(dep);
    }

    public float GetMultiplier(GameObject dep) =>
        _multiplier.TryGetValue(dep, out float m) ? m : 1f;

    // System Overload: force all deployables to max stacks + temp multiplier
    public void SystemOverload(int ownerID, float duration)
    {
        foreach (var dep in GetAll(ownerID))
        {
            MaxStacks(dep);
            // TurretController picks up GetMultiplier each frame
        }
    }

    private void RecalcMultiplier(GameObject dep)
    {
        int stacks = _stacks.TryGetValue(dep, out int s) ? s : 0;
        _multiplier[dep] = 1f + stacks * 0.08f; // +8% per stack, max +40% at 5
    }
}
