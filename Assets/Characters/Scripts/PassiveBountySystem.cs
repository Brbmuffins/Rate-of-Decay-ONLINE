using UnityEngine;

// WRAITH passive — Bounty System
// Normal kill: reduce all ability cooldowns by 2s.
// Elite kill:  reduce all ability cooldowns by 5s.
public class PassiveBountySystem : ClassPassive
{
    public float normalKillCDR = 2f;
    public float eliteKillCDR  = 5f;
    public string enemyTag     = "Enemy";

    protected override void Awake()
    {
        base.Awake();
    }

    void Start()
    {
        // Hook into every enemy's health death event at scene start.
        // For enemies spawned later, call HookEnemy() after spawning.
        HookAllEnemies();
    }

    public void HookAllEnemies()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        foreach (var e in enemies)
            HookEnemy(e);
    }

    public void HookEnemy(GameObject enemy)
    {
        Health h = enemy.GetComponent<Health>();
        if (h == null) return;
        h.onKilledBy.AddListener(src =>
        {
            // Only trigger if this player landed the killing blow
            if (src == gameObject)
            {
                EnemyAI ai    = enemy.GetComponent<EnemyAI>();
                bool isElite  = ai != null && ai.isElite;
                float cdr     = isElite ? eliteKillCDR : normalKillCDR;
                caster?.ReduceAllCooldowns(cdr);
                OnEnemyKilled(enemy, isElite);
            }
        });
    }

    public override void OnAbilityCast(AbilityDef ability) { }
}
