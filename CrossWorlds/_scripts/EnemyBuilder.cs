#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.AI;

/// <summary>
/// EnemyBuilder — BCE editor menu for automating combat setup.
///
/// Copy to: Assets/Game/Editor/EnemyBuilder.cs
///
/// Menu items (BCE toolbar → Setup):
///   4a ▶ Create Grunt Enemy Prefab     — melee, brown, DropTable seeded
///   4b ▶ Create Ranged Enemy Prefab    — ranged, blue, DropTable seeded
///   4c ▶ Create Elite Enemy Prefab     — large, dark red, richer drops
///   4d ▶ Create WorldItem Prefab       — floating pickup with glow light
///   4e ▶ Create Wave Spawner (Arena)   — spawner + 4 cardinal spawn points
///
/// Produces:
///   Assets/Game/Prefabs/Enemy_Grunt.prefab
///   Assets/Game/Prefabs/Enemy_Ranged.prefab
///   Assets/Game/Prefabs/Enemy_Elite.prefab
///   Assets/Game/Prefabs/WorldItem.prefab
///   Assets/Game/Data/DropTables/  (ScriptableObjects)
///
/// After running all four prefab builders, run 4e in your arena scene, then:
///   1. Drag prefabs into WaveSpawner inspector slots
///   2. Drag WorldItem.prefab into each EnemyController.worldItemPrefab slot
///   3. Add Enemy_Grunt, Enemy_Ranged, WorldItem to NetworkManager spawnPrefabs
///   4. Bake NavMesh (Window → AI → Navigation → Bake)
///   5. Call WaveSpawner.StartWaves() from your portal arrival trigger
/// </summary>
public static class EnemyBuilder
{
    // ── Directory constants ───────────────────────────────────────────────────
    const string PrefabDir = "Assets/Game/Prefabs";
    const string DropDir   = "Assets/Game/Data/DropTables";

    // ══════════════════════════════════════════════════════════════════════════
    // 4a — Grunt
    // ══════════════════════════════════════════════════════════════════════════
    [MenuItem("BCE/Setup/4a ▶ Create Grunt Enemy Prefab")]
    public static void CreateGrunt()
    {
        EnsureDirs();

        var dt = MakeDropTable("Grunt_DropTable", minGold: 1, maxGold: 5,
            nothingWeight: 6f,
            entries: new (string id, float w, int min, int max)[]
            {
                ("material_copper_shard", 3f, 1, 2),
                ("material_copper_bar",   1f, 1, 1),
            });

        var go = MakeEnemyBase("Enemy_Grunt", hp: 60f, isRanged: false,
            speed: 4.5f, attackRange: 1.5f, attackInterval: 1.5f, damage: 12f,
            aggroRadius: 8f, stoppingDist: 1.2f);

        go.GetComponent<Renderer>().sharedMaterial.color = new Color(0.55f, 0.25f, 0.1f);
        go.GetComponent<EnemyController>().dropTable = dt;

        SavePrefab(go, $"{PrefabDir}/Enemy_Grunt.prefab");
        Object.DestroyImmediate(go);

        Debug.Log("[BCE] Enemy_Grunt.prefab saved to Assets/Game/Prefabs/\n" +
                  "NEXT: Assign to WaveSpawner.enemyPrefabs[0]\n" +
                  "      Assign WorldItem.prefab to EnemyController.worldItemPrefab");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 4b — Ranged
    // ══════════════════════════════════════════════════════════════════════════
    [MenuItem("BCE/Setup/4b ▶ Create Ranged Enemy Prefab")]
    public static void CreateRanged()
    {
        EnsureDirs();

        var dt = MakeDropTable("Ranged_DropTable", minGold: 1, maxGold: 3,
            nothingWeight: 6.5f,
            entries: new (string, float, int, int)[]
            {
                ("material_copper_shard", 3.5f, 1, 2),
            });

        var go = MakeEnemyBase("Enemy_Ranged", hp: 40f, isRanged: true,
            speed: 3.5f, attackRange: 5f, attackInterval: 2f, damage: 8f,
            aggroRadius: 10f, stoppingDist: 4f);

        go.GetComponent<Renderer>().sharedMaterial.color = new Color(0.2f, 0.4f, 0.6f);
        var ctrl = go.GetComponent<EnemyController>();
        ctrl.dropTable       = dt;
        ctrl.preferredRange  = 5f;
        ctrl.tooCloseDistance = 3f;

        SavePrefab(go, $"{PrefabDir}/Enemy_Ranged.prefab");
        Object.DestroyImmediate(go);

        Debug.Log("[BCE] Enemy_Ranged.prefab saved to Assets/Game/Prefabs/\n" +
                  "NEXT: Assign to WaveSpawner.enemyPrefabs[1]\n" +
                  "      Assign an EnemyProjectile prefab to EnemyController.projectilePrefab (optional)");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 4c — Elite
    // ══════════════════════════════════════════════════════════════════════════
    [MenuItem("BCE/Setup/4c ▶ Create Elite Enemy Prefab")]
    public static void CreateElite()
    {
        EnsureDirs();

        var dt = MakeDropTable("Elite_DropTable", minGold: 10, maxGold: 25,
            nothingWeight: 2f,
            entries: new (string, float, int, int)[]
            {
                ("material_copper_bar",   4f, 1, 2),
                ("material_copper_shard", 3f, 2, 4),
                ("sword_copper",          1f, 1, 1),
                ("plate_copper",          1f, 1, 1),
            });

        var go = MakeEnemyBase("Enemy_Elite", hp: 300f, isRanged: false,
            speed: 3.8f, attackRange: 2f, attackInterval: 2f, damage: 28f,
            aggroRadius: 12f, stoppingDist: 1.8f);

        go.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
        go.GetComponent<Renderer>().sharedMaterial.color = new Color(0.55f, 0.05f, 0.1f);

        // Atmospheric point light
        var lightObj = new GameObject("EliteGlow");
        lightObj.transform.SetParent(go.transform, false);
        lightObj.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        var l = lightObj.AddComponent<Light>();
        l.type      = LightType.Point;
        l.color     = new Color(1f, 0.2f, 0.1f);
        l.intensity = 2.5f;
        l.range     = 10f;

        go.GetComponent<EnemyController>().dropTable = dt;

        SavePrefab(go, $"{PrefabDir}/Enemy_Elite.prefab");
        Object.DestroyImmediate(go);

        Debug.Log("[BCE] Enemy_Elite.prefab saved to Assets/Game/Prefabs/\n" +
                  "NEXT: Assign to WaveSpawner.elitePrefab\n" +
                  "      Add to NetworkManager.spawnPrefabs");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 4d — WorldItem prefab
    // ══════════════════════════════════════════════════════════════════════════
    [MenuItem("BCE/Setup/4d ▶ Create WorldItem Prefab")]
    public static void CreateWorldItem()
    {
        EnsureDirs();

        var root = new GameObject("WorldItem");
        root.tag = "Pickup";

        // Visual — small sphere (swap with real model later)
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Visual";
        sphere.transform.SetParent(root.transform, false);
        sphere.transform.localScale = Vector3.one * 0.3f;
        Object.DestroyImmediate(sphere.GetComponent<SphereCollider>());
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = Color.white;
        sphere.GetComponent<Renderer>().sharedMaterial = mat;

        // Glow light (color set at runtime by WorldItem.ApplyRarityGlow)
        var lightObj = new GameObject("GlowLight");
        lightObj.transform.SetParent(root.transform, false);
        lightObj.transform.localPosition = Vector3.zero;
        var glow = lightObj.AddComponent<Light>();
        glow.type      = LightType.Point;
        glow.color     = new Color(0.75f, 0.75f, 0.75f);
        glow.intensity = 1.5f;
        glow.range     = 2.5f;

        // Pickup collider
        var col       = root.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius    = 0.8f;

        // Mirror
        root.AddComponent<Mirror.NetworkIdentity>();

        // WorldItem component — wire glow light
        var wi       = root.AddComponent<WorldItem>();
        wi.glowLight = glow;

        SavePrefab(root, $"{PrefabDir}/WorldItem.prefab");
        Object.DestroyImmediate(root);

        Debug.Log("[BCE] WorldItem.prefab saved to Assets/Game/Prefabs/\n" +
                  "NEXT: Assign to EnemyController.worldItemPrefab on all enemy prefabs\n" +
                  "      Add WorldItem.prefab to NetworkManager.spawnPrefabs");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 4e — Wave Spawner (drops into active scene)
    // ══════════════════════════════════════════════════════════════════════════
    [MenuItem("BCE/Setup/4e ▶ Create Wave Spawner (Arena)")]
    public static void CreateWaveSpawner()
    {
        var ws = new GameObject("WaveSpawner");
        ws.AddComponent<Mirror.NetworkIdentity>();
        var spawner = ws.AddComponent<WaveSpawner>();

        // Four cardinal spawn points 15 units out
        var spRoot = new GameObject("SpawnPoints");
        spRoot.transform.SetParent(ws.transform, false);

        var pts = new (string name, Vector3 offset)[]
        {
            ("SpawnPoint_N", new Vector3(  0f, 0f,  15f)),
            ("SpawnPoint_S", new Vector3(  0f, 0f, -15f)),
            ("SpawnPoint_E", new Vector3( 15f, 0f,   0f)),
            ("SpawnPoint_W", new Vector3(-15f, 0f,   0f)),
        };

        foreach (var (n, offset) in pts)
        {
            var sp = new GameObject(n);
            sp.transform.SetParent(spRoot.transform, false);
            sp.transform.localPosition = offset;
            spawner.spawnPoints.Add(sp.transform);
        }

        Selection.activeGameObject = ws;
        EditorUtility.SetDirty(ws);

        Debug.Log("[BCE] WaveSpawner created in scene.\n" +
                  "NEXT STEPS:\n" +
                  "1. Assign Enemy_Grunt.prefab  → WaveSpawner.enemyPrefabs[0]\n" +
                  "2. Assign Enemy_Ranged.prefab → WaveSpawner.enemyPrefabs[1]\n" +
                  "3. Assign Enemy_Elite.prefab  → WaveSpawner.elitePrefab\n" +
                  "4. Call WaveSpawner.StartWaves() from your portal arrival trigger\n" +
                  "5. Ctrl+S to save scene");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════

    static GameObject MakeEnemyBase(string name, float hp, bool isRanged,
        float speed, float attackRange, float attackInterval, float damage,
        float aggroRadius, float stoppingDist)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name;
        go.tag  = "Enemy";

        // Replace auto-generated collider with a tuned one
        Object.DestroyImmediate(go.GetComponent<CapsuleCollider>());
        var col       = go.AddComponent<CapsuleCollider>();
        col.height    = 2f;
        col.radius    = 0.4f;
        col.center    = new Vector3(0f, 0.1f, 0f);

        // Unique material so colour changes don't bleed across prefabs
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        go.GetComponent<Renderer>().sharedMaterial = mat;

        // Mirror
        go.AddComponent<Mirror.NetworkIdentity>();

        // NavMeshAgent
        var agent              = go.AddComponent<NavMeshAgent>();
        agent.speed            = speed;
        agent.angularSpeed     = 250f;
        agent.acceleration     = 8f;
        agent.stoppingDistance = stoppingDist;
        agent.radius           = 0.4f;
        agent.height           = 2f;

        // Health
        var health       = go.AddComponent<Health>();
        health.maxHp     = hp;
        health.currentHp = hp;

        // EnemyController
        var ctrl              = go.AddComponent<EnemyController>();
        ctrl.aggroRadius      = aggroRadius;
        ctrl.attackRange      = attackRange;
        ctrl.attackInterval   = attackInterval;
        ctrl.damage           = damage;
        ctrl.isRanged         = isRanged;

        return go;
    }

    static DropTable MakeDropTable(string assetName, int minGold, int maxGold,
        float nothingWeight, (string id, float w, int min, int max)[] entries)
    {
        var dt           = ScriptableObject.CreateInstance<DropTable>();
        dt.minGold       = minGold;
        dt.maxGold       = maxGold;
        dt.nothingWeight = nothingWeight;
        dt.drops         = new List<DropEntry>();
        foreach (var e in entries)
            dt.drops.Add(new DropEntry { itemId = e.id, weight = e.w, minQty = e.min, maxQty = e.max });

        string path = $"{DropDir}/{assetName}.asset";
        AssetDatabase.CreateAsset(dt, path);
        AssetDatabase.SaveAssets();
        return dt;
    }

    static void SavePrefab(GameObject go, string path)
    {
        bool ok;
        PrefabUtility.SaveAsPrefabAsset(go, path, out ok);
        if (!ok) Debug.LogError($"[BCE] Failed to save prefab: {path} — does the directory exist?");
        AssetDatabase.Refresh();
    }

    static void EnsureDirs()
    {
        EnsureDir(PrefabDir);
        EnsureDir(DropDir);
    }

    static void EnsureDir(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts   = path.Split('/');
        string cur  = parts[0];
        for (int i  = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
#endif
