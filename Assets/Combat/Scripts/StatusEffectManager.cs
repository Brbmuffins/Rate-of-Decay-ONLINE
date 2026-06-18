using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Attach to every character (player and enemy) that can receive status effects.
public class StatusEffectManager : MonoBehaviour
{
    private readonly List<StatusEffect> _effects = new List<StatusEffect>();

    // Quick state queries used by other systems
    public bool IsSuppressed => HasEffect(StatusEffectType.Suppress);
    public bool IsExposed    => HasEffect(StatusEffectType.Exposed);
    public bool IsTethered   => HasEffect(StatusEffectType.Tethered);
    public bool IsSlowed     => HasEffect(StatusEffectType.Slow);

    public UnityEvent<StatusEffectType> onEffectAdded;
    public UnityEvent<StatusEffectType> onEffectRemoved;
    public UnityEvent                   onAllEffectsCleared;

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
    }

    // Adds effect; refreshes duration if same type already present.
    public void AddEffect(StatusEffect effect)
    {
        for (int i = 0; i < _effects.Count; i++)
        {
            if (_effects[i].type == effect.type)
            {
                _effects[i].remainingTime = Mathf.Max(_effects[i].remainingTime, effect.duration);
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

    // Returns how many Debuffed stacks exist (consumed by Collapse).
    public int CountDebuffStacks()
    {
        int n = 0;
        foreach (var e in _effects)
            if (e.type == StatusEffectType.Debuffed) n++;
        return n;
    }

    // Collapse consumes all Debuffed stacks for damage — removes them and returns count.
    public int ConsumeDebuffStacks()
    {
        int count = 0;
        for (int i = _effects.Count - 1; i >= 0; i--)
        {
            if (_effects[i].type == StatusEffectType.Debuffed)
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
