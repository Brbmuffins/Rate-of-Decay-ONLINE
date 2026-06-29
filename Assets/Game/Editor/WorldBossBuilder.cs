#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.AI;

/// <summary>
/// WorldBossBuilder — BCE menu to drop the Null Architect boss into the active arena scene.
/// BCE/Setup/6 ▶ Create World Boss (Null Architect)
///
/// Requires: active scene with a NavMesh baked.
/// After running: assign VFX prefabs in inspector, save scene, add to NetworkManager.spawnPrefabs.
/// </summary>
public static class WorldBossBuilder
{
    [MenuItem("BCE/Setup/6 ▶ Create World Boss (Null Architect)")]
    public static void CreateWorldBoss()
    {
        var boss = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        boss.name = "NullArchitect_Boss";
        boss.tag  = "Enemy";
        boss.transform.position   = FindBossSpawnPoint();
        boss.transform.localScale = new Vector3(2.5f, 3f, 2.5f);

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.05f, 0.0f, 0.15f);
        boss.GetComponent<Renderer>().sharedMaterial = mat;

        boss.AddComponent<Mirror.NetworkIdentity>();

        var agent              = boss.AddComponent<NavMeshAgent>();
        agent.speed            = 4f;
        agent.angularSpeed     = 180f;
        agent.stoppingDistance = 2f;
        agent.radius           = 1.2f;
        agent.height           = 3f;

        var health       = boss.AddComponent<Health>();
        health.maxHealth = 2000f;
        // currentHealth initialized in Health.Awake

        var ctrl = boss.AddComponent<WorldBossController>();
        ctrl.phase2Threshold         = 0.60f;
        ctrl.phase3Threshold         = 0.30f;
        ctrl.finalSurgeThreshold     = 0.10f;
        ctrl.reflectPulseInterval    = 18f;
        ctrl.reflectTelegraphDuration = 3f;
        ctrl.reflectWindowDuration   = 4f;
        ctrl.shardSpreadRadius       = 6f;
        ctrl.tetherWebInterval       = 25f;
        ctrl.tetherWebDuration       = 6f;
        ctrl.tetherWebLeashDistance  = 6f;
        ctrl.tetherWebSnapDamage     = 40f;
        ctrl.voidDrainInterval       = 12f;
        ctrl.voidDrainRadius         = 5f;
        ctrl.voidDrainTickDamage     = 8f;
        ctrl.voidDrainDuration       = 4f;
        ctrl.finalSurgeSpeedMultiplier  = 3f;
        ctrl.finalSurgeAttackMultiplier = 3f;
        ctrl.finalSurgeDuration      = 15f;
        ctrl.immunityWindowDuration  = 4f;
        ctrl.guaranteedDropItemIds   = new System.Collections.Generic.List<string> { "sword_iron", "plate_iron" };
        ctrl.rareDropItemIds         = new System.Collections.Generic.List<string> { "ring_copper", "material_copper_bar" };
        ctrl.rareDropChance          = 0.35f;

        // Null Shard placeholder
        ctrl.nullShardPrefab = BuildShardPrefab();

        // Void Drain VFX placeholder
        var drainVFX = new GameObject("VoidDrainVFX");
        drainVFX.transform.SetParent(boss.transform, false);
        drainVFX.SetActive(false);
        ctrl.voidDrainVFX = drainVFX;

        // Proximity trigger
        var triggerObj = new GameObject("BossTrigger");
        triggerObj.transform.SetParent(boss.transform, false);
        var triggerCol     = triggerObj.AddComponent<SphereCollider>();
        triggerCol.isTrigger = true;
        triggerCol.radius  = 15f;
        triggerObj.AddComponent<BossTrigger>();

        // Atmospheric light
        var lightObj = new GameObject("BossLight");
        lightObj.transform.SetParent(boss.transform, false);
        lightObj.transform.localPosition = new Vector3(0f, 4f, 0f);
        var l = lightObj.AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(0.4f, 0.1f, 1f); l.intensity = 3f; l.range = 20f;

        Selection.activeGameObject = boss;
        EditorUtility.SetDirty(boss);

        Debug.Log("[BCE] Null Architect boss created.\n" +
                  "NEXT:\n" +
                  "1. Bake NavMesh (Window → AI → Navigation → Bake)\n" +
                  "2. Assign reflectTelegraphVFX in inspector\n" +
                  "3. Replace capsule with real model\n" +
                  "4. Add NullArchitect_Boss to NetworkManager.spawnPrefabs\n" +
                  "5. Ctrl+S");
    }

    static GameObject BuildShardPrefab()
    {
        var shard = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shard.name = "NullShard";
        shard.tag  = "Enemy";
        shard.transform.localScale = Vector3.one * 1.2f;

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.3f, 0f, 0.8f);
        shard.GetComponent<Renderer>().sharedMaterial = mat;

        shard.AddComponent<Mirror.NetworkIdentity>();

        var shardHealth      = shard.AddComponent<Health>();
        shardHealth.maxHealth = 400f;
        // currentHealth initialized in Health.Awake

        var shardLight = new GameObject("ShardLight");
        shardLight.transform.SetParent(shard.transform, false);
        var l = shardLight.AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(0.5f, 0.2f, 1f); l.intensity = 2f; l.range = 8f;

        // Ensure dir exists
        if (!AssetDatabase.IsValidFolder("Assets/Game/Prefabs"))
            AssetDatabase.CreateFolder("Assets/Game", "Prefabs");

        string path = "Assets/Game/Prefabs/NullShard.prefab";
        bool ok;
        var prefab = PrefabUtility.SaveAsPrefabAsset(shard, path, out ok);
        Object.DestroyImmediate(shard);

        if (ok) { Debug.Log($"[BCE] NullShard prefab saved to {path}"); return prefab; }
        Debug.LogWarning($"[BCE] Could not save NullShard to {path}");
        return null;
    }

    static Vector3 FindBossSpawnPoint()
    {
        // "BossSpawnPoint" tag is not registered — return default arena centre.
        // Place the boss manually after creation if a different spawn is needed.
        return new Vector3(0f, 1.5f, 0f);
    }
}

/// <summary>
/// BossTrigger — child of the boss. First player to enter starts the fight.
/// </summary>
public class BossTrigger : MonoBehaviour
{
    private bool _triggered = false;

    void OnTriggerEnter(Collider other)
    {
        if (_triggered || !other.CompareTag("Player")) return;
        _triggered = true;
        var boss = GetComponentInParent<WorldBossController>();
        if (boss != null && Mirror.NetworkServer.active) boss.StartFight();
        Destroy(gameObject);
    }
}
#endif
