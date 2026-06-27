using UnityEngine;

// ScriptableObject that defines which spellbook indices a class can equip.
// Create one asset per class via: Create > BCE > Class Ability Pool
// Assign to the player's AbilityCaster before the session starts.
[CreateAssetMenu(menuName = "BCE/Class Ability Pool", fileName = "ClassAbilityPool")]
public class ClassAbilityPool : ScriptableObject
{
    public string className;

    [Tooltip("Indices into AbilityCaster.spellbook that this class can use.")]
    public int[] availableIndices;

    [Tooltip("Default equipped loadout (4 indices from availableIndices) for this class.")]
    public int[] defaultEquipped = new int[4];
}
