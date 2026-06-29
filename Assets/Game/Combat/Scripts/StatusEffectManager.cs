using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Attach to every character (player and enemy) that can receive status effects.
public class StatusEffectManager : MonoBehaviour
{
    private readonly List<StatusEffect> _effects = new List<StatusEffect>();

    [Tooltip("How often (s) damage-over-time effects deal their damage.")]
    public float dotTickInterval = 0.5f;
    private float  _dotTimer = 0f;
    private Health _health;

    // Quick state queries used by other systems
    public bool IsSilenced  => HasEffect(StatusEffectType.Silenced);
    public bool IsWeakened  => HasEffect(StatusEffectType.Weakened);
    public bool IsBound     => HasEffect(StatusEffectType.Bound);
    public bool IsStaggered => HasEffect(StatusEffectType.Stagger);

    public UnityEvent<StatusEffectType> onEffectAdded;
    public UnityEvent<StatusEffectType> onEffectRemoved;
    public UnityEvent                   onAllEffectsCleared;

    void Awake()
    {
        _health = GetComponent<Health>();
    }

    void Update()
    {
        for (int i = _effects.Count - 1; i >= 0; i--)
        {
            _effects[i].remainingTime -= Time.deltaTime;
            if (_effects[i].IsExpired)
            {
                var t = _effects[i].type;
                _effects.RemoveAt(i);
                onEffectRemoved?.Invoke(t);
            }
        }

        TickDamageOverTime();
    }

    // Applies all active DamageOverTime effects on a fixed tick. Summing first
    // and dealing damage once per tick keeps onDamageTaken from firing every
    // frame (which would, e.g., spam Threat Protocol stacks).
    void TickDamageOverTime()
    {
        float dps = 0f;
        GameObject src = null;
        foreach (var e in _effects)
        {
            if (e.type != StatusEffectType.Cursed) continue;
            dps += e.value;
            if (src == null) src = e.source;
        }

        if (dps <= 0f) { _dotTimer = 0f; return; }

        _dotTimer += Time.deltaTime;
        if (_dotTimer < dotTickInterval) return;
        _dotTimer = 0f;

        _health?.TakeDamage(dps * dotTickInterval, src);
    }

    // Adds effect; refreshes duration if same type already present.
    public void AddEffect(StatusEffect effect)
    {
        for (int i = 0; i < _effects.Count; i++)
        {
            if (_effects[i].type == effect.type)
            {
                _effects[i].remainingTime = Mathf.Max(_effects[i].remainingTime, effect.duration);
                _effects[i].value         = Mathf.Max(_effects[i].value, effect.value); // keep the stronger magnitude on refresh
                _effects[i].source        = effect.source;
                return;
            }
        }
        _effects.Add(effect);
        onEffectAdded?.Invoke(effect.type);
    }

    // Purge Protocol: remove every effect instantly.
    public void RemoveAll()
    {
        _effects.Clear();
        onAllEffectsCleared?.Invoke();
    }

    public bool HasEffect(StatusEffectType type)
    {
        foreach (var e in _effects)
            if (e.type == type) return true;
        return false;
    }

    // Effect types Dark Harvest can detonate. Bound (the Ironclad's leash) is
    // positional control rather than detonatable decay, so it's excluded — a
    // Shadowblade's Dark Harvest shouldn't break the Ironclad's chain.
    public static bool IsDebuff(StatusEffectType t)
    {
        switch (t)
        {
            case StatusEffectType.Slow:
            case StatusEffectType.Stagger:
            case StatusEffectType.Silenced:
            case StatusEffectType.Cursed:
            case StatusEffectType.Weakened:
                return true;
            default:
                return false;
        }
    }

    // Returns how many detonatable debuffs are active (consumed by Collapse).
    public int CountDebuffStacks()
    {
        int n = 0;
        foreach (var e in _effects)
            if (IsDebuff(e.type)) n++;
        return n;
    }

    // Collapse consumes all detonatable debuffs for damage — removes them and returns count.
    public int ConsumeDebuffStacks()
    {
        int count = 0;
        for (int i = _effects.Count - 1; i >= 0; i--)
        {
            if (IsDebuff(_effects[i].type))
            {
                count++;
                _effects.RemoveAt(i);
            }
        }
        if (count > 0) onAllEffectsCleared?.Invoke();
        return count;
    }

    public List<StatusEffect> GetAll() => new List<StatusEffect>(_effects);

    // Applies a slow (0–1 fraction) to attached PlayerMovement or EnemyAI.
    public float GetSlowFraction()
    {
        float max = 0f;
        foreach (var e in _effects)
            if (e.type == StatusEffectType.Slow) max = Mathf.Max(max, e.value);
        return max;
    }
}
