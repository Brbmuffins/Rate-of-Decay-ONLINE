#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.AI;

/// <summary>
/// EnemyBuilder — BCE editor menu for automating combat setup.
///
/// BCE toolbar → Setup:
///   4a ▶ Create Grunt Enemy Prefab     — melee, brown, DropTable auto-seeded
///   4b ▶ Create Ranged Enemy Prefab    — ranged, blue, DropTable auto-seeded
///   4c ▶ Create Elite Enemy Prefab     — large, dark red, richer drops
///   4d ▶ Create WorldItem Prefab       — floating pickup with glow light
///   4e ▶ Create Wave Spawner (Arena)   — spawner + 4 cardinal spawn points in scene
///
/// Output:
///   Assets/Game/Prefabs/Enemy_Grunt.prefab
///   Assets/Game/Prefabs/Enemy_Ranged.prefab
///   Assets/Game/Prefabs/Enemy_Elite.prefab
///   Assets/Game/Prefabs/WorldItem.prefab
///   Assets/Game/Data/DropTables/*.asset
///
/// After all five: assign prefabs to WaveSpawner, add to NetworkManager.spawnPrefabs, bake NavMesh.
/// </summary>
public static class EnemyBuilder
{
    const string PrefabDir = "Assets/Game/Prefabs";
    const string DropDir   = "Assets/Game/Data/DropTables";

    // ── 4a Grunt ─────────────────────────────────────────────────────────────
    [MenuItem("BCE/Setup/4a ▶ Create Grunt Enemy Prefab")]
    public static void CreateGrunt()
    {
        EnsureDirs();
        var dt = MakeDropTable("Grunt_DropTable", 1, 5, 6f, new (string, float, int, int)[]
        {
            ("material_copper_shard", 3f, 1, 2),
            ("material_copper_bar",   1f, 1, 1),
        });
        var go = MakeEnemyBase("Enemy_Grunt", 60f, false, 4.5f, 1.5f, 1.5f, 12f, 8f, 1.2f);
        go.GetComponent<Renderer>().sharedMaterial.color = new Color(0.55f, 0.25f, 0.1f);
        go.GetComponent<EnemyController>().dropTable = dt;
        SavePrefab(go, $"{PrefabDir}/Enemy_Grunt.prefab");
        Object.DestroyImmediate(go);
        Debug.Log("[BCE] Enemy_Grunt.prefab → Assets/Game/Prefabs/\nNEXT: WaveSpawner.enemyPrefabs[0], assign WorldItem prefab");
    }

    // ── 4b Ranged ─────────────────────────────────────────────────────────────
    [MenuItem("BCE/Setup/4b ▶ Create Ranged Enemy Prefab")]
    public static void CreateRanged()
    {
        EnsureDirs();
        var dt = MakeDropTable("Ranged_DropTable", 1, 3, 6.5f, new (string, float, int, int)[]
        {
            ("material_copper_shard", 3.5f, 1, 2),
        });
        var go = MakeEnemyBase("Enemy_Ranged", 40f, true, 3.5f, 5f, 2f, 8f, 10f, 4f);
        go.GetComponent<Renderer>().sharedMaterial.color = new Color(0.2f, 0.4f, 0.6f);
        var ctrl = go.GetComponent<EnemyController>();
        ctrl.dropTable        = dt;
        ctrl.preferredRange   = 5f;
        ctrl.tooCloseDistance = 3f;
        SavePrefab(go, $"{PrefabDir}/Enemy_Ranged.prefab");
        Object.DestroyImmediate(go);
        Debug.Log("[BCE] Enemy_Ranged.prefab → Assets/Game/Prefabs/\nNEXT: WaveSpawner.enemyPrefabs[1], assign EnemyProjectile prefab");
    }

    // ── 4c Elite ──────────────────────────────────────────────────────────────
    [MenuItem("BCE/Setup/4c ▶ Create Elite Enemy Prefab")]
    public static void CreateElite()
    {
        EnsureDirs();
        var dt = MakeDropTable("Elite_DropTable", 10, 25, 2f, new (string, float, int, int)[]
        {
            ("material_copper_bar",   4f, 1, 2),
            ("material_copper_shard", 3f, 2, 4),
            ("sword_copper",          1f, 1, 1),
            ("plate_copper",          1f, 1, 1),
        });
        var go = MakeEnemyBase("Enemy_Elite", 300f, false, 3.8f, 2f, 2f, 28f, 12f, 1.8f);
        go.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
        go.GetComponent<Renderer>().sharedMaterial.color = new Color(0.55f, 0.05f, 0.1f);

        var lightObj = new GameObject("EliteGlow");
        lightObj.transform.SetParent(go.transform, false);
        lightObj.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        var l = lightObj.AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(1f, 0.2f, 0.1f); l.intensity = 2.5f; l.range = 10f;

        go.GetComponent<EnemyController>().dropTable = dt;
        SavePrefab(go, $"{PrefabDir}/Enemy_Elite.prefab");
        Object.DestroyImmediate(go);
        Debug.Log("[BCE] Enemy_Elite.prefab → Assets/Game/Prefabs/\nNEXT: WaveSpawner.elitePrefab, add to NetworkManager.spawnPrefabs");
    }

    // ── 4d WorldItem ──────────────────────────────────────────────────────────
    [MenuItem("BCE/Setup/4d ▶ Create WorldItem Prefab")]
    public static void CreateWorldItem()
    {
        EnsureDirs();

        var root = new GameObject("WorldItem");
        // "Pickup" tag must be registered in Tags & Layers before use — leave Untagged,
        // WorldItem.OnTriggerEnter checks CompareTag("Player") on the entering collider, not itself.

        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Visual";
        sphere.transform.SetParent(root.transform, false);
        sphere.transform.localScale = Vector3.one * 0.3f;
        Object.DestroyImmediate(sphere.GetComponent<SphereCollider>());
        sphere.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        var lightObj = new GameObject("GlowLight");
        lightObj.transform.SetParent(root.transform, false);
        var glow = lightObj.AddComponent<Light>();
        glow.type = LightType.Point; glow.color = new Color(0.75f, 0.75f, 0.75f); glow.intensity = 1.5f; glow.range = 2.5f;

        var col = root.AddComponent<SphereCollider>();
        col.isTrigger = true; col.radius = 0.8f;

        root.AddComponent<Mirror.NetworkIdentity>();

        var wi = root.AddComponent<WorldItem>();
        wi.glowLight = glow;

        SavePrefab(root, $"{PrefabDir}/WorldItem.prefab");
        Object.DestroyImmediate(root);
        Debug.Log("[BCE] WorldItem.prefab → Assets/Game/Prefabs/\nNEXT: Assign to EnemyController.worldItemPrefab on all enemy prefabs\n      Add to NetworkManager.spawnPrefabs");
    }

    // ── 4e Wave Spawner ───────────────────────────────────────────────────────
    [MenuItem("BCE/Setup/4e ▶ Create Wave Spawner (Arena)")]
    public static void CreateWaveSpawner()
    {
        var ws = new GameObject("WaveSpawner");
        ws.AddComponent<Mirror.NetworkIdentity>();
        var spawner = ws.AddComponent<WaveSpawner>();

        var spRoot = new GameObject("SpawnPoints");
        spRoot.transform.SetParent(ws.transform, false);

        var pts = new (string n, Vector3 o)[]
        {
            ("SpawnPoint_N", new Vector3(  0f, 0f,  15f)),
            ("SpawnPoint_S", new Vector3(  0f, 0f, -15f)),
            ("SpawnPoint_E", new Vector3( 15f, 0f,   0f)),
            ("SpawnPoint_W", new Vector3(-15f, 0f,   0f)),
        };

        foreach (var (n, o) in pts)
        {
            var sp = new GameObject(n);
            sp.transform.SetParent(spRoot.transform, false);
            sp.transform.localPosition = o;
            spawner.spawnPoints.Add(sp.transform);
        }

        Selection.activeGameObject = ws;
        EditorUtility.SetDirty(ws);

        Debug.Log("[BCE] WaveSpawner created in scene.\n" +
                  "NEXT:\n" +
                  "1. enemyPrefabs[0] = Enemy_Grunt.prefab\n" +
                  "2. enemyPrefabs[1] = Enemy_Ranged.prefab\n" +
                  "3. elitePrefab     = Enemy_Elite.prefab\n" +
                  "4. Call WaveSpawner.StartWaves() from portal arrival trigger\n" +
                  "5. Ctrl+S");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static GameObject MakeEnemyBase(string name, float hp, bool isRanged,
        float speed, float attackRange, float attackInterval, float damage,
        float aggroRadius, float stoppingDist)
    {
        var go  = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name;
        go.tag  = "Enemy";

        Object.DestroyImmediate(go.GetComponent<CapsuleCollider>());
        var col = go.AddComponent<CapsuleCollider>();
        col.height = 2f; col.radius = 0.4f; col.center = new Vector3(0f, 0.1f, 0f);

        go.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        go.AddComponent<Mirror.NetworkIdentity>();

        var agent              = go.AddComponent<NavMeshAgent>();
        agent.speed            = speed;
        agent.angularSpeed     = 250f;
        agent.acceleration     = 8f;
        agent.stoppingDistance = stoppingDist;
        agent.radius           = 0.4f;
        agent.height           = 2f;

        var health       = go.AddComponent<Health>();
        health.maxHealth = hp;
        // currentHealth is initialized to maxHealth in Health.Awake — don't set it here

        var ctrl            = go.AddComponent<EnemyController>();
        ctrl.aggroRadius    = aggroRadius;
        ctrl.attackRange    = attackRange;
        ctrl.attackInterval = attackInterval;
        ctrl.damage         = damage;
        ctrl.isRanged       = isRanged;

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
        EnsureDir(DropDir);
        AssetDatabase.CreateAsset(dt, $"{DropDir}/{assetName}.asset");
        AssetDatabase.SaveAssets();
        return dt;
    }

    static void SavePrefab(GameObject go, string path)
    {
        EnsureDir(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
        bool ok;
        PrefabUtility.SaveAsPrefabAsset(go, path, out ok);
        if (!ok) Debug.LogError($"[BCE] Failed to save prefab: {path}");
        AssetDatabase.Refresh();
    }

    static void EnsureDirs() { EnsureDir(PrefabDir); EnsureDir(DropDir); }

    static void EnsureDir(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts  = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
#endif
