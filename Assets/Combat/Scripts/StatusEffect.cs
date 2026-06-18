using System;
using UnityEngine;

public enum StatusEffectType
{
    Slow,           // reduces move speed
    Stagger,        // brief interrupt (Breach Slam)
    Suppress,       // cannot use abilities (Null Field)
    DamageOverTime,
    Exposed,        // incoming damage +25% (Event Horizon)
    Tethered,       // cannot move beyond max range (Iron Tether)
    Debuffed        // generic stack — consumed by Collapse for burst damage
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
