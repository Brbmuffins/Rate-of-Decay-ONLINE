using System;
using UnityEngine;

public enum StatusEffectType
{
    Slow,           // reduces move speed
    Stagger,        // brief interrupt (Breach Slam)
    Suppress,       // cannot attack (Null Field)
    DamageOverTime, // decay damage per second (Null Field) — Collapse fuel
    Exposed,        // incoming damage +25% (Event Horizon)
    Tethered        // cannot move beyond max range (Iron Tether)
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
