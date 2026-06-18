using UnityEngine;

// Abstract base for all class passives.
// Add the concrete subclass alongside AbilityCaster on the player prefab.
public abstract class ClassPassive : MonoBehaviour
{
    protected AbilityCaster caster;
    protected Health        health;

    protected virtual void Awake()
    {
        caster = GetComponent<AbilityCaster>();
        health = GetComponent<Health>();
    }

    // Called by AbilityCaster.FinalizeCast for every ability used.
    public abstract void OnAbilityCast(AbilityDef ability);

    // Called when this player kills an enemy (source = the killed enemy).
    public virtual void OnEnemyKilled(GameObject enemy, bool isElite) { }
}
