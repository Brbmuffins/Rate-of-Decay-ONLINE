using System.Collections.Generic;
using UnityEngine;

// ENGINEER passive — Overengineered
// Every 4 seconds, for each active deployable within 12 units:
// add 1 output stack (max 5). Each stack = +8% damage/healing from that deployable.
// Stacks decay back to 0 if the Engineer moves out of range.
public class PassiveOverengineered : ClassPassive
{
    public float stackInterval  = 4f;
    public float proximityRange = 12f;

    private float _timer = 0f;

    void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < stackInterval) return;
        _timer = 0f;

        if (DeployableManager.Instance == null) return;

        List<GameObject> deployables = DeployableManager.Instance.GetAll(gameObject.GetInstanceID());

        foreach (var dep in deployables)
        {
            if (dep == null) continue;
            float dist = Vector3.Distance(transform.position, dep.transform.position);
            if (dist <= proximityRange)
                DeployableManager.Instance.AddStack(dep);
        }
    }

    public override void OnAbilityCast(AbilityDef ability) { }
}
