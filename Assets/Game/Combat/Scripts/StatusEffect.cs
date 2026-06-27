using System;
using UnityEngine;

public enum StatusEffectType
{
    Slow,           // reduces move speed
    Stagger,        // brief interrupt (Shieldwall Charge)
    Silenced,       // cannot use abilities (Silence Ward)
    Cursed,         // damage over time per second (Silence Ward) — Dark Harvest fuel
    Weakened,       // incoming damage +25% (Collapsing Void)
    Bound           // cannot move beyond max range (Rune Chain)
}

[Serializable]
public class StatusEffect
{
    public StatusEffectType type;
    public float duration;
    public float remainingTime;
    public float value;         // slow fraction, DOT dmg/sec, etc.
    public GameObject source;

    public bool IsExpired => remainingTime <= 0f;

    public StatusEffect(StatusEffectType t, float dur, float val = 0f, GameObject src = null)
    {
        type          = t;
        duration      = dur;
        remainingTime = dur;
        value         = val;
        source        = src;
    }

    public StatusEffect Clone()
    {
        return new StatusEffect(type, duration, value, source) { remainingTime = remainingTime };
    }
}
