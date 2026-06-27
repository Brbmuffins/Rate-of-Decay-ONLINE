using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// ═══════════════════════════════════════════════════════════════════════════
//  RodCombatWorldBuilder
//  RoD ▶ World ▶ Build Combat Base
//
//  Populates GameWorld with:
//    • Improved arena floor — main plane + raised obstacle blocks
//    • Pre-deployed Sentinel Turret with brbmuffins ElectricalSparks VFX
//    • Three ability zone indicators (Circle, Cone, Rectangle) using
//      Dark Arts magic circle VFX as world hazard dressing
//    • Enemy cluster spawners (tagged "Enemy") with Health + EnemyAI,
//      upgraded with SmallExplosion death VFX
//    • Wave chest (WaveChest component)
//    • Ambient environmental VFX (EnergyExplosion idle loops)
//
//  Safe to re-run — removes previous build before placing new one.
//  Reopens LoginScene after finishing.
// ═══════════════════════════════════════════════════════════════════════════

public static class RodCombatWorldBuilder
{
    const string GAME_WORLD_PATH = "Assets/brbmuffins Skybox/Scenes/GameWorld.unity";
    const string LOGIN_SCENE     = "Assets/Game/Scenes/LoginScene.unity";
    const string ROOT_NAME       = "CombatBase";

    // ── VFX paths ─────────────────────────────────────────────────────────────

    // Turret
    const string VFX_SPARKS       = "Assets/brbmuffins Technologies/brbmuffins Particle Pack/EffectExamples/Misc Effects/Prefabs/ElectricalSparks.prefab";
    const string VFX_ENERGY_EXP   = "Assets/brbmuffins Technologies/brbmuffins Particle Pack/EffectExamples/Fire & Explosion Effects/Prefabs/EnergyExplosion.prefab";
    const string VFX_SMALL_EXP    = "Assets/brbmuffins Technologies/brbmuffins Particle Pack/EffectExamples/Fire & Explosion Effects/Prefabs/SmallExplosion.prefab";
    const string VFX_PLASMA_EXP   = "Assets/brbmuffins Technologies/brbmuffins Particle Pack/EffectExamples/Legacy Particles/Prefabs/PlasmaExplosionEffect.prefab";

    // Zone indicators (Dark Arts)
    const string VFX_MAGIC_CIRCLE = "Assets/brbmuffins Dark Arts/brbmuffins Fantasy Pack/Prefabs/Effects normal/Magic circle.prefab";
    const string VFX_DEATH_CIRCLE = "Assets/brbmuffins Dark Arts/brbmuffins Fantasy Pack/Prefabs/Effects normal/Death magic circle.prefab";
    const string VFX_LIGHTNING    = "Assets/brbmuffins Dark Arts/brbmuffins Fantasy Pack/Prefabs/Effects normal/Lightning strike skill.prefab";
    const string VFX_MANA_WALL    = "Assets/brbmuffins Dark Arts/brbmuffins Fantasy Pack/Prefabs/Effects normal/Mana wall.prefab";
    const string VFX_GROUND_SPIKE = "Assets/brbmuffins Dark Arts/brbmuffins Fantasy Pack/Prefabs/Ground spikes.prefab";

    // Ambient
    const string VFX_FIREFLIES    = "Assets/brbmuffins Technologies/brbmuffins Particle Pack/EffectExamples/Misc Effects/Prefabs/FireFlies.prefab";
    const string VFX_HEAT         = "Assets/brbmuffins Technologies/brbmuffins Particle Pack/EffectExamples/Misc Effects/Prefabs/HeatDistortion.prefab";

    // ── Menu entry ────────────────────────────────────────────────────────────

    [MenuItem("RoD/World/Build Combat Base", priority = 20)]
    static void Build()
    {
        if (!File.Exists(GAME_WORLD_PATH))
        {
            EditorUtility.DisplayDialog("Not Found",
                $"GameWorld not found at:\n{GAME_WORLD_PATH}", "OK");
            return;
        }

        var scene = EditorSceneManager.OpenScene(GAME_WORLD_PATH, OpenSceneMode.Single);

        // Remove previous build
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == ROOT_NAME) { Object.DestroyImmediate(root); break; }

        // Container
        var container = new GameObject(ROOT_NAME);
        SceneManager.MoveGameObjectToScene(container, scene);

        BuildArena(container, scene);
        BuildTurret(container);
        BuildZoneIndicators(container);
        BuildEnemyClusters(container);
        BuildWaveChest(container);
        BuildAmbientVFX(container);

        EditorSceneManager.SaveScene(scene);
        AssetDatabase.Refresh();

        Debug.Log("[RoD] ✅ Combat base built in GameWorld.");
        EditorUtility.DisplayDialog("✅ Combat Base Ready",
            "GameWorld populated with:\n" +
            "  • Arena floor + cover blocks\n" +
            "  • Sentinel Turret (ElectricalSparks VFX)\n" +
            "  • 3 ability zone indicators (Circle / Cone / Rect)\n" +
            "  • 3 enemy clusters (Health + EnemyAI)\n" +
            "  • Wave chest\n" +
            "  • Ambient VFX\n\n" +
            "Tag your enemy GameObjects with 'Enemy' if not already set.\n" +
            "Assign AbilityCaster on player prefabs to activate cone indicators.",
            "Let's go!");

        if (File.Exists(LOGIN_SCENE))
            EditorSceneManager.OpenScene(LOGIN_SCENE, OpenSceneMode.Single);
    }

    // ── Arena ─────────────────────────────────────────────────────────────────

    static void BuildArena(GameObject container, Scene scene)
    {
        // Main ground plane
        EnsureGround(scene);

        // Cover blocks — low walls / pillars for tactical movement
        var coverData = new (Vector3 pos, Vector3 scale, string name)[]
        {
            (new Vector3( 6f, 0.5f,  0f), new Vector3(0.5f, 1f, 3f), "Cover_E"),
            (new Vector3(-6f, 0.5f,  0f), new Vector3(0.5f, 1f, 3f), "Cover_W"),
            (new Vector3( 0f, 0.5f,  6f), new Vector3(3f, 1f, 0.5f), "Cover_N"),
            (new Vector3( 0f, 0.5f, -6f), new Vector3(3f, 1f, 0.5f), "Cover_S"),
            (new Vector3( 4f, 0.5f,  4f), new Vector3(1f, 1f, 1f),   "Pillar_NE"),
            (new Vector3(-4f, 0.5f,  4f), new Vector3(1f, 1f, 1f),   "Pillar_NW"),
            (new Vector3( 4f, 0.5f, -4f), new Vector3(1f, 1f, 1f),   "Pillar_SE"),
            (new Vector3(-4f, 0.5f, -4f), new Vector3(1f, 1f, 1f),   "Pillar_SW"),
        };

        foreach (var (pos, scale, name) in coverData)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(container.transform);
            block.transform.position   = pos;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().material.color = new Color(0.12f, 0.12f, 0.16f);
            block.isStatic = true;
        }
    }

    static void EnsureGround(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == "RodGroundPlane") return;

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "RodGroundPlane";
        ground.transform.position   = Vector3.zero;
        ground.transform.localScale = new Vector3(20f, 1f, 20f);
        ground.GetComponent<Renderer>().material.color = new Color(0.08f, 0.08f, 0.1f);
        ground.isStatic = true;
        SceneManager.MoveGameObjectToScene(ground, scene);
    }

    // ── Sentinel Turret ───────────────────────────────────────────────────────

    static void BuildTurret(GameObject container)
    {
        // Turret base (cylinder)
        var turretGO = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        turretGO.name = "SentinelTurret";
        turretGO.transform.SetParent(container.transform);
        turretGO.transform.position   = new Vector3(0f, 0.5f, 3f);
        turretGO.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
        turretGO.GetComponent<Renderer>().material.color = new Color(0.18f, 0.55f, 1f);
        turretGO.tag = "Untagged";

        // Barrel (capsule child)
        var barrel = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        barrel.name = "Barrel";
        barrel.transform.SetParent(turretGO.transform);
        barrel.transform.localPosition = new Vector3(0f, 0.5f, 1f);
        barrel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        barrel.transform.localScale    = new Vector3(0.3f, 0.8f, 0.3f);
        barrel.GetComponent<Renderer>().material.color = new Color(0.12f, 0.12f, 0.18f);
        Object.DestroyImmediate(barrel.GetComponent<CapsuleCollider>());

        // Muzzle point
        var muzzle = new GameObject("MuzzlePoint");
        muzzle.transform.SetParent(barrel.transform);
        muzzle.transform.localPosition = new Vector3(0f, 1f, 0f);

        // TurretController
        var tc = turretGO.AddComponent<TurretController>();
        tc.barrel      = barrel.transform;
        tc.muzzlePoint = muzzle.transform;
        tc.range       = 10f;
        tc.fireRate    = 1.5f;
        tc.damage      = 12f;

        // VFX: ElectricalSparks idle aura around the base
        SpawnVFX(VFX_SPARKS, turretGO.transform, Vector3.zero, 0.6f, "TurretAura");

        // VFX: small energy pulse at muzzle (muzzle flash upgrade)
        var muzzleVFX = SpawnVFX(VFX_SPARKS, muzzle.transform, Vector3.zero, 0.3f, "MuzzleVFX");
        if (muzzleVFX != null)
        {
            var ps = muzzleVFX.GetComponentInChildren<ParticleSystem>();
            if (ps != null)
            {
                var main = ps.main;
                main.loop     = false;
                main.playOnAwake = false;
            }
            // Wire this as the muzzle flash
            if (ps != null) tc.muzzleFlash = ps;
        }

        Debug.Log("[RoD] Turret placed at (0, 0.5, 3)");
    }

    // ── Zone Indicators ───────────────────────────────────────────────────────
    //  These show the three ability shapes as standing world hazards.
    //  Circle → Magic circle VFX
    //  Cone   → Lightning strike + ground spike pointing forward
    //  Rect   → Mana wall (line barrier)

    static void BuildZoneIndicators(GameObject container)
    {
        var zonesRoot = new GameObject("ZoneIndicators");
        zonesRoot.transform.SetParent(container.transform);

        // ── Circle zone (heal / AoE marker) ──
        var circleZone = new GameObject("Zone_Circle");
        circleZone.transform.SetParent(zonesRoot.transform);
        circleZone.transform.position = new Vector3(-8f, 0f, 0f);
        SpawnVFX(VFX_MAGIC_CIRCLE, circleZone.transform, Vector3.zero, 1.2f, "CircleVFX");
        AddZoneLabel(circleZone, "CIRCLE ZONE\nAoE / Heal", new Color(0.1f, 1f, 0.45f));

        // ── Cone zone (dark blast / damage cone) ──
        var coneZone = new GameObject("Zone_Cone");
        coneZone.transform.SetParent(zonesRoot.transform);
        coneZone.transform.position = new Vector3(0f, 0f, -8f);
        SpawnVFX(VFX_LIGHTNING, coneZone.transform, Vector3.zero, 0.8f, "ConeVFX");
        SpawnVFX(VFX_GROUND_SPIKE, coneZone.transform, new Vector3(0f, 0f, 2f), 0.5f, "SpikeFwd");
        SpawnVFX(VFX_GROUND_SPIKE, coneZone.transform, new Vector3(1.5f, 0f, 1.5f), 0.4f, "SpikeR");
        SpawnVFX(VFX_GROUND_SPIKE, coneZone.transform, new Vector3(-1.5f, 0f, 1.5f), 0.4f, "SpikeL");
        AddZoneLabel(coneZone, "CONE ZONE\nDark Blast / Arc Cannon", new Color(0.6f, 0.1f, 1f));

        // ── Rectangle zone (line / beam) ──
        var rectZone = new GameObject("Zone_Rect");
        rectZone.transform.SetParent(zonesRoot.transform);
        rectZone.transform.position = new Vector3(8f, 0f, 0f);
        SpawnVFX(VFX_MANA_WALL, rectZone.transform, Vector3.zero, 1f, "RectVFX");
        SpawnVFX(VFX_DEATH_CIRCLE, rectZone.transform, new Vector3(0f, 0f, -1.5f), 0.5f, "RectEndA");
        SpawnVFX(VFX_DEATH_CIRCLE, rectZone.transform, new Vector3(0f, 0f,  1.5f), 0.5f, "RectEndB");
        AddZoneLabel(rectZone, "RECT ZONE\nBeam / Last Bastion", new Color(0.15f, 0.65f, 1f));

        Debug.Log("[RoD] Zone indicators placed.");
    }

    static void AddZoneLabel(GameObject parent, string text, Color color)
    {
        // Float a text marker above the zone using a default TextMesh
        var labelGO = new GameObject("ZoneLabel");
        labelGO.transform.SetParent(parent.transform);
        labelGO.transform.localPosition = new Vector3(0f, 2.5f, 0f);
        var tm = labelGO.AddComponent<TextMesh>();
        tm.text          = text;
        tm.fontSize      = 18;
        tm.characterSize = 0.1f;
        tm.anchor        = TextAnchor.MiddleCenter;
        tm.alignment     = TextAlignment.Center;
        tm.color         = color;
        tm.fontStyle     = FontStyle.Bold;
        // Billboard toward camera at runtime
        labelGO.AddComponent<RodBillboard>();
    }

    // ── Enemy Clusters ────────────────────────────────────────────────────────

    static void BuildEnemyClusters(GameObject container)
    {
        var enemyRoot = new GameObject("EnemyClusters");
        enemyRoot.transform.SetParent(container.transform);

        // Cluster layout: three groups at different distances
        var clusters = new (Vector3 center, int count, float radius, float hp, string label)[]
        {
            (new Vector3( 0f, 0f, -12f), 3, 1.5f, 60f,  "ClusterFront"),
            (new Vector3(-10f, 0f, 5f),  2, 1.2f, 100f, "ClusterLeft"),
            (new Vector3( 10f, 0f, 5f),  2, 1.2f, 100f, "ClusterRight"),
        };

        foreach (var (center, count, radius, hp, label) in clusters)
        {
            var clusterGO = new GameObject(label);
            clusterGO.transform.SetParent(enemyRoot.transform);
            clusterGO.transform.position = center;

            for (int i = 0; i < count; i++)
            {
                float angle = i * (360f / count) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                var enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                enemy.name = $"Zompy_{label}_{i}";
                enemy.tag  = "Enemy";
                enemy.transform.SetParent(clusterGO.transform);
                enemy.transform.position = center + offset + Vector3.up;
                enemy.GetComponent<Renderer>().material.color = new Color(0.6f, 0.15f, 0.1f);

                // Health
                var health = enemy.AddComponent<Health>();
                health.maxHealth = hp;

                // StatusEffectManager (required by EnemyAI)
                enemy.AddComponent<StatusEffectManager>();

                // EnemyAI
                var ai = enemy.AddComponent<EnemyAI>();
                ai.moveSpeed    = 2.5f;
                ai.attackDamage = 8f;
                ai.attackRange  = 1.8f;

                // Death VFX — SmallExplosion spawned via health.onDeath
                // We wire this at runtime rather than here since onDeath is a UnityEvent
                // and VFX prefab ref needs to survive to play mode.
                // The EnemyDeathVFX helper below handles it.
                var deathVFX = enemy.AddComponent<EnemyDeathVFX>();
                deathVFX.vfxPath = VFX_SMALL_EXP;
            }
        }

        Debug.Log("[RoD] Enemy clusters placed.");
    }

    // ── Wave Chest ────────────────────────────────────────────────────────────

    static void BuildWaveChest(GameObject container)
    {
        var chestGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
        chestGO.name = "WaveChest";
        chestGO.transform.SetParent(container.transform);
        chestGO.transform.position   = new Vector3(0f, 0.5f, -3f);
        chestGO.transform.localScale = new Vector3(1f, 1f, 0.6f);
        chestGO.GetComponent<Renderer>().material.color = new Color(0.9f, 0.7f, 0.1f);

        chestGO.AddComponent<WaveChest>();

        // Ambient glow
        SpawnVFX(VFX_MAGIC_CIRCLE, chestGO.transform, new Vector3(0f, -0.5f, 0f), 0.4f, "ChestGlow");

        Debug.Log("[RoD] Wave chest placed.");
    }

    // ── Ambient VFX ───────────────────────────────────────────────────────────

    static void BuildAmbientVFX(GameObject container)
    {
        var ambientRoot = new GameObject("AmbientVFX");
        ambientRoot.transform.SetParent(container.transform);

        // Fireflies scattered around the arena edges
        var fireflyPositions = new Vector3[]
        {
            new Vector3(-9f, 0f, -9f),
            new Vector3( 9f, 0f, -9f),
            new Vector3(-9f, 0f,  9f),
            new Vector3( 9f, 0f,  9f),
        };
        foreach (var pos in fireflyPositions)
            SpawnVFX(VFX_FIREFLIES, ambientRoot.transform, pos, 1f, "Fireflies");

        // Heat distortion near the turret (implying a hot barrel)
        SpawnVFX(VFX_HEAT, ambientRoot.transform, new Vector3(0f, 1.5f, 3f), 0.4f, "TurretHeat");

        Debug.Log("[RoD] Ambient VFX placed.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static GameObject SpawnVFX(string path, Transform parent, Vector3 localPos, float scale, string label)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"[RoD] VFX not found: {path}");
            return null;
        }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = label;
        go.transform.localPosition = localPos;
        go.transform.localScale    = Vector3.one * scale;
        return go;
    }
}

// EnemyDeathVFX and RodBillboard live in Assets/Game/Combat/Scripts/
// (runtime scripts — cannot be defined in Editor/ folder)
