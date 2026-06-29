#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.AI;

/// <summary>
/// WorldBossBuilder — Editor tool for BCE menu.
/// BCE → Setup/6 ▶ Create World Boss (Null Architect)
///
/// Drops a configured Null Architect boss into the active arena scene.
/// Wires phase thresholds, drop table, and health bar.
/// Requires an active scene with a NavMesh baked.
///
/// Copy to: Assets/Game/Editor/WorldBossBuilder.cs
/// </summary>
public static class WorldBossBuilder
{
    [MenuItem("BCE/Setup/6 ▶ Create World Boss (Null Architect)")]
    public static void CreateWorldBoss()
    {
        // ── Build the boss GameObject ─────────────────────────────────────────
        var boss = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        boss.name = "NullArchitect_Boss";
        boss.tag = "Enemy";
        boss.transform.position = FindBossSpawnPoint();
        boss.transform.localScale = new Vector3(2.5f, 3f, 2.5f);

        // Dark material (placeholder until real model is assigned)
        var renderer = boss.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.05f, 0.0f, 0.15f); // deep void purple
        renderer.material = mat;

        // ── NetworkIdentity (required for Mirror) ─────────────────────────────
        boss.AddComponent<Mirror.NetworkIdentity>();

        // ── NavMeshAgent ──────────────────────────────────────────────────────
        var agent = boss.AddComponent<NavMeshAgent>();
        agent.speed = 4f;
        agent.angularSpeed = 180f;
        agent.stoppingDistance = 2f;
        agent.radius = 1.2f;
        agent.height = 3f;

        // ── Health ────────────────────────────────────────────────────────────
        var health = boss.AddComponent<Health>();
        health.maxHp = 2000f;    // shared HP pool — scales with player count via WaveManager
        health.currentHp = 2000f;
        health.isInvulnerable = false;

        // ── WorldBossController ───────────────────────────────────────────────
        var boss_ctrl = boss.AddComponent<WorldBossController>();

        // Phase thresholds
        boss_ctrl.phase2Threshold = 0.60f;
        boss_ctrl.phase3Threshold = 0.30f;
        boss_ctrl.finalSurgeThreshold = 0.10f;

        // Phase 1 — Reflect Pulse
        boss_ctrl.reflectPulseInterval = 18f;
        boss_ctrl.reflectTelegraphDuration = 3f;
        boss_ctrl.reflectWindowDuration = 4f;

        // Phase 2 — Shards
        boss_ctrl.shardSpreadRadius = 6f;
        boss_ctrl.tethreWebInterval = 25f;
        boss_ctrl.tethreWebDuration = 6f;
        boss_ctrl.tethreWebLeashDistance = 6f;
        boss_ctrl.tethreWebSnapDamage = 40f;

        // Phase 3
        boss_ctrl.voidDrainInterval = 12f;
        boss_ctrl.voidDrainRadius = 5f;
        boss_ctrl.voidDrainTickDamage = 8f;
        boss_ctrl.voidDrainDuration = 4f;
        boss_ctrl.finalSurgeSpeedMultiplier = 3f;
        boss_ctrl.finalSurgeAttackMultiplier = 3f;
        boss_ctrl.finalSurgeDuration = 15f;

        // Transition
        boss_ctrl.immunityWindowDuration = 4f;

        // Drop table — IDs match seeded items in rod_online
        boss_ctrl.guaranteedDropItemIds = new System.Collections.Generic.List<string>
            { "sword_iron", "plate_iron" };
        boss_ctrl.rareDropItemIds = new System.Collections.Generic.List<string>
            { "ring_copper", "material_copper_bar" };
        boss_ctrl.rareDropChance = 0.35f;

        // ── Null Shard prefab (placeholder capsule) ───────────────────────────
        var shardPrefab = BuildShardPrefab();
        boss_ctrl.nullShardPrefab = shardPrefab;

        // ── Void Drain VFX placeholder ────────────────────────────────────────
        var drainVFX = new GameObject("VoidDrainVFX");
        drainVFX.transform.SetParent(boss.transform, false);
        // Wire a particle system or brbmuffins prefab here when available
        drainVFX.SetActive(false);
        boss_ctrl.voidDrainVFX = drainVFX;

        // ── Boss trigger collider (proximity — starts the fight) ──────────────
        var triggerObj = new GameObject("BossTrigger");
        triggerObj.transform.SetParent(boss.transform, false);
        var triggerCol = triggerObj.AddComponent<SphereCollider>();
        triggerCol.isTrigger = true;
        triggerCol.radius = 15f;
        triggerObj.AddComponent<BossTrigger>();

        // ── Light (atmospheric) ───────────────────────────────────────────────
        var lightObj = new GameObject("BossLight");
        lightObj.transform.SetParent(boss.transform, false);
        lightObj.transform.localPosition = new Vector3(0f, 4f, 0f);
        var light = lightObj.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.4f, 0.1f, 1f); // purple
        light.intensity = 3f;
        light.range = 20f;

        // ── Register with NetworkManager spawnPrefabs ─────────────────────────
        // Note: must also be added to NetworkManager.spawnPrefabs in inspector
        // or registered via NetworkClient.RegisterPrefab() in RodNetworkManager

        // ── Select in hierarchy ───────────────────────────────────────────────
        Selection.activeGameObject = boss;
        EditorUtility.SetDirty(boss);

        Debug.Log("[BCE] World Boss (Null Architect) created.\n" +
                  "NEXT STEPS:\n" +
                  "1. Bake NavMesh if not already done (Window → AI → Navigation → Bake)\n" +
                  "2. Assign reflectTelegraphVFX in the inspector (brbmuffins Magic Circle works)\n" +
                  "3. Replace placeholder capsule with actual boss model\n" +
                  "4. Add NullArchitect_Boss prefab to NetworkManager.spawnPrefabs\n" +
                  "5. Wire BossTrigger to WorldBossController.StartFight()\n" +
                  "6. Press Ctrl+S to save the scene");
    }

    // ── Build a placeholder Null Shard prefab ─────────────────────────────────
    static GameObject BuildShardPrefab()
    {
        var shard = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shard.name = "NullShard";
        shard.tag = "Enemy";
        shard.transform.localScale = Vector3.one * 1.2f;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.3f, 0f, 0.8f);
        shard.GetComponent<Renderer>().material = mat;

        shard.AddComponent<Mirror.NetworkIdentity>();

        var shardHealth = shard.AddComponent<Health>();
        shardHealth.maxHp = 400f;
        shardHealth.currentHp = 400f;

        var shardLight = new GameObject("ShardLight");
        shardLight.transform.SetParent(shard.transform, false);
        var l = shardLight.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(0.5f, 0.2f, 1f);
        l.intensity = 2f;
        l.range = 8f;

        // Save as prefab in Assets/Game/Prefabs/
        string path = "Assets/Game/Prefabs/NullShard.prefab";
        bool success;
        var prefab = PrefabUtility.SaveAsPrefabAsset(shard, path, out success);
        Object.DestroyImmediate(shard);

        if (success)
        {
            Debug.Log($"[BCE] NullShard prefab saved to {path}");
            return prefab;
        }
        else
        {
            Debug.LogWarning($"[BCE] Could not save NullShard prefab to {path} — directory may not exist. Create Assets/Game/Prefabs/ first.");
            return null;
        }
    }

    // ── Find a good spawn point (center of scene or NavMesh center) ───────────
    static Vector3 FindBossSpawnPoint()
    {
        // Try to find an existing BossSpawnPoint tag
        var tagged = GameObject.FindGameObjectWithTag("BossSpawnPoint");
        if (tagged != null) return tagged.transform.position;

        // Fall back to scene center elevated
        return new Vector3(0f, 1.5f, 0f);
    }
}

/// <summary>
/// BossTrigger — attach to the trigger collider child of the boss.
/// When a player enters the radius, starts the fight.
/// </summary>
public class BossTrigger : MonoBehaviour
{
    private bool _triggered = false;

    void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag("Player")) return;

        _triggered = true;
        var boss = GetComponentInParent<WorldBossController>();
        if (boss != null && Mirror.NetworkServer.active)
            boss.StartFight();

        // Destroy trigger after activation
        Destroy(gameObject);
    }
}
#endif
