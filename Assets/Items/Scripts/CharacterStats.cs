using UnityEngine;

// Central hub for gear-driven character power.
// DESIGN PILLAR: no leveling — every bonus here comes from equipped gear and
// the attunements socketed into it.
//
// Reads Equipment, sums every StatModifier, and:
//   • pushes Max Health + Damage Reduction into Health (which owns those)
//   • exposes DamageMultiplier / CooldownReduction / MoveSpeedMultiplier /
//     HealMultiplier as read-only properties for AbilityCaster, PlayerMovement,
//     and healing code to consume.
//
// Call Recalculate() whenever equipped gear or its attunements change
// (Equipment does this automatically on equip/unequip).
[RequireComponent(typeof(Health))]
public class CharacterStats : MonoBehaviour
{
    private Health    _health;
    private Equipment _equipment;

    // ── Aggregated results (recomputed on every gear change) ──────────
    public float MaxHealthBonus      { get; private set; }        // flat HP added
    public float DamageMultiplier    { get; private set; } = 1f;  // ×outgoing damage
    public float DamageReduction     { get; private set; }        // 0..0.8 fraction
    public float MoveSpeedMultiplier { get; private set; } = 1f;  // ×movement speed
    public float CooldownReduction   { get; private set; }        // 0..0.6 fraction
    public float HealMultiplier      { get; private set; } = 1f;  // ×healing dealt

    void Awake()
    {
        _health    = GetComponent<Health>();
        _equipment = GetComponent<Equipment>();
    }

    void Start()
    {
        Recalculate();
    }

    // Re-reads all equipped gear + attunements and re-applies the totals.
    public void Recalculate()
    {
        float flatHealth = 0f;
        float pctDamage = 0f, pctDR = 0f, pctSpeed = 0f, pctCdr = 0f, pctHeal = 0f;

        if (_equipment != null)
        {
            foreach (var kvp in _equipment.equippedItems)
            {
                ItemData item = kvp.Value;
                if (item == null) continue;

                foreach (var m in item.AllModifiers())
                {
                    switch (m.stat)
                    {
                        case StatType.MaxHealth:
                            flatHealth += m.kind == ModifierKind.Percent
                                ? _health.BaseMaxHealth * m.value
                                : m.value;
                            break;
                        case StatType.Damage:            pctDamage += m.value; break;
                        case StatType.DamageReduction:   pctDR     += m.value; break;
                        case StatType.MoveSpeed:         pctSpeed  += m.value; break;
                        case StatType.CooldownReduction: pctCdr    += m.value; break;
                        case StatType.HealPower:         pctHeal   += m.value; break;
                    }
                }
            }
        }

        MaxHealthBonus      = flatHealth;
        DamageMultiplier    = Mathf.Max(0f,   1f + pctDamage);
        DamageReduction     = Mathf.Clamp(pctDR,  0f, 0.8f);
        MoveSpeedMultiplier = Mathf.Max(0.1f, 1f + pctSpeed);
        CooldownReduction   = Mathf.Clamp(pctCdr, 0f, 0.6f);
        HealMultiplier      = Mathf.Max(0f,   1f + pctHeal);

        // Hand off the channels Health owns.
        if (_health != null)
        {
            _health.SetGearMaxHealthBonus(MaxHealthBonus);
            _health.SetGearDamageReduction(DamageReduction);
        }
    }
}
