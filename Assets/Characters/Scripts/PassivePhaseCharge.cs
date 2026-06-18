using UnityEngine;

// PHASER passive — Phase Charge
// Every ability cast (except Phase Shift) charges a meter.
// At 6 charges, the next OFFENSIVE ability deals +40% damage and resets the meter.
public class PassivePhaseCharge : ClassPassive
{
    public int   chargesNeeded   = 6;
    public float damageBonus     = 0.40f;

    private int _charges = 0;

    // Read by AbilityCaster before applying damage on offensive abilities.
    public bool IsCharged => _charges >= chargesNeeded;

    public int  CurrentCharges => _charges;

    // Called by AbilityCaster BEFORE applying damage so it can scale the hit.
    // Returns the multiplier to apply (1.0 normally, 1.4 when charged).
    public float ConsumeBonusIfCharged(AbilityDef ability)
    {
        if (!IsCharged) return 1f;
        if (ability.category != AbilityCategory.Damage) return 1f;

        _charges = 0;
        return 1f + damageBonus;
    }

    public override void OnAbilityCast(AbilityDef ability)
    {
        // Phase Shift is the only ability that doesn't charge the meter.
        if (ability.abilityName == "Phase Shift") return;

        if (!IsCharged)
            _charges = Mathf.Min(_charges + 1, chargesNeeded);
    }
}
