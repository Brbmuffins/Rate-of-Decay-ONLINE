using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════
//  RodNpcBuilder
//  Menu: RoD/World/Populate GameWorld with NPCs
//
//  Creates 4 NPC prefabs and places them in GameWorld:
//    • Zompy   — zombie enemy,   Death magic circle + Lightning aura
//    • Bob     — enemy,          Glowing orbs + Electrical sparks
//    • Kodiac  — friendly NPC,   Healing aura + Healing circle
//    • Turret  — environmental,  Plexus aura + Dust motes
//
//  NPCs are scattered across the terrain at Y=0.
//  Adjust positions in the Hierarchy after running.
// ═══════════════════════════════════════════════════════════════════════════

public static class RodNpcBuilder
{
    // ── Asset paths ───────────────────────────────────────────────────────────
    const string GAME_WORLD    = "Assets/brbmuffins Skybox/Scenes/GameWorld.unity";
    const string NPC_PREFAB_DIR = "Assets/Game/Prefabs/NPCs";

    // Models
    const string ZOMPY_MODEL   = "Assets/Game/Characters/Zompy/Zombie Scratch Idle.fbx";
    const string BOB_MODEL     = "Assets/Game/Characters/Bob/Models/Bob.obj";
    const string KODIAC_MODEL  = "Assets/Game/Characters/Kodiac/tripo_convert_65e3ca4d-9272-40c7-bdfd-c88b62e90c00.fbm/Dwarf Idle.fbx";
    const string TURRET_MODEL  = "Assets/Game/Characters/Engineer/Turret/Walking Turret.fbx";

    // VFX — Dark Arts
    const string VFX_DEATH_CIRCLE  = "Assets/brbmuffins Dark Arts/brbmuffins Fantasy Pack/Prefabs/Effects normal/Death magic circle.prefab";
    const string VFX_LIGHTNING     = "Assets/brbmuffins Dark Arts/brbmuffins Fantasy Pack/Prefabs/Effects normal/Lightning strike skill.prefab";
    const string VFX_GLOWING_ORBS  = "Assets/brbmuffins Dark Arts/brbmuffins Fantasy Pack/Prefabs/Glowing orbs.prefab";
    const string VFX_MAGIC_CIRCLE  = "Assets/brbmuffins Dark Arts/brbmuffins Fantasy Pack/Prefabs/Effects normal/Magic circle.prefab";

    // VFX — Studio
    const string VFX_HEALING_AURA  = "Assets/brbmuffins Studio/brbmuffins Magic Pack/Prefabs/Character auras/Healing.prefab";
    const string VFX_HEALING_CIRCLE = "Assets/brbmuffins Studio/brbmuffins Magic Pack/Prefabs/Magic circles/Healing circle.prefab";
    const string VFX_LIGHTNING_AURA = "Assets/brbmuffins Studio/brbmuffins Magic Pack/Prefabs/Character auras/Lightning aura.prefab";
    const string VFX_PLEXUS_AURA   = "Assets/brbmuffins Studio/brbmuffins Magic Pack/Prefabs/Character auras/Plexus.prefab";

    // VFX — Technologies
    const string VFX_DUST_MOTES    = "Assets/brbmuffins Technologies/brbmuffins Particle Pack/EffectExamples/Misc Effects/Prefabs/DustMotesEffect.prefab";
    const string VFX_SPARKS        = "Assets/brbmuffins Technologies/brbmuffins Particle Pack/EffectExamples/Misc Effects/Prefabs/ElectricalSparks.prefab";

    // ── World spawn positions (scattered around GameWorld origin) ─────────────
    static readonly Vector3[] SpawnPositions =
    {
        new Vector3( 12f, 0f,  10f),   // Zompy
        new Vector3(-15f, 0f,   8f),   // Bob
        new Vector3(  4f, 0f, -12f),   // Kodiac
        new Vector3(-8f,  0f, -18f),   // Turret
    };

    // ── Menu item ─────────────────────────────────────────────────────────────

    [MenuItem("BCE/World/Populate GameWorld with NPCs", priority = 10)]
    static void BuildNpcs()
    {
        if (!File.Exists(GAME_WORLD))
        {
            EditorUtility.DisplayDialog("Scene Not Found",
                "GameWorld scene not found at:\n" + GAME_WORLD, "OK");
            return;
        }

        Directory.CreateDirectory(NPC_PREFAB_DIR);

        var scene = EditorSceneManager.OpenScene(GAME_WORLD, OpenSceneMode.Single);

        // ── Ground plane (gives the player a collider to land on) ────────────
        bool groundExists = false;
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == "RodGroundPlane") { groundExists = true; break; }
        if (!groundExists)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "RodGroundPlane";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(20f, 1f, 20f);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ground, scene);
        }

        // ── Remove any duplicate NPCs from previous runs ──────────────────────
        string[] npcNames = { "Zompy", "Bob", "Kodiac", "Turret" };
        foreach (var root in scene.GetRootGameObjects())
            foreach (var n in npcNames)
                if (root.name == n) { Object.DestroyImmediate(root); break; }

        // Build each NPC and place in scene
        PlaceNpc(scene, "Zompy",  ZOMPY_MODEL,
            primary:   VFX_DEATH_CIRCLE,
            secondary: VFX_LIGHTNING_AURA,
            type: NpcController.NpcType.Enemy,
            pos: SpawnPositions[0], patrol: 8f, speed: 1.0f);

        PlaceNpc(scene, "Bob",    BOB_MODEL,
            primary:   VFX_GLOWING_ORBS,
            secondary: VFX_SPARKS,
            type: NpcController.NpcType.Enemy,
            pos: SpawnPositions[1], patrol: 6f, speed: 1.4f);

        PlaceNpc(scene, "Kodiac", KODIAC_MODEL,
            primary:   VFX_HEALING_CIRCLE,
            secondary: VFX_HEALING_AURA,
            type: NpcController.NpcType.Friendly,
            pos: SpawnPositions[2], patrol: 3f, speed: 0.8f);

        PlaceNpc(scene, "Turret", TURRET_MODEL,
            primary:   VFX_PLEXUS_AURA,
            secondary: VFX_DUST_MOTES,
            type: NpcController.NpcType.Environmental,
            pos: SpawnPositions[3], patrol: 0f, speed: 0f);

        EditorSceneManager.SaveScene(scene);
        AssetDatabase.Refresh();

        // ── Return to LoginScene so buttons stay wired ────────────────────────
        const string LOGIN_SCENE = "Assets/Game/Scenes/LoginScene.unity";
        if (File.Exists(LOGIN_SCENE))
            EditorSceneManager.OpenScene(LOGIN_SCENE, OpenSceneMode.Single);

        EditorUtility.DisplayDialog("✅ NPCs Placed",
            "4 NPCs added to GameWorld:\n\n" +
            "  Zompy   — zombie enemy (pos 12, 0, 10)\n" +
            "  Bob     — enemy (pos -15, 0, 8)\n" +
            "  Kodiac  — friendly NPC (pos 4, 0, -12)\n" +
            "  Turret  — environmental (pos -8, 0, -18)\n\n" +
            "Adjust positions in the Hierarchy to suit the terrain.\n" +
            "Each NPC has NpcController — tweak patrol/speed there.",
            "Done!");
    }

    // ── Builder ───────────────────────────────────────────────────────────────

    static void PlaceNpc(
        UnityEngine.SceneManagement.Scene scene,
        string npcName,
        string modelPath,
        string primary,
        string secondary,
        NpcController.NpcType type,
        Vector3 pos,
        float patrol,
        float speed)
    {
        // ── Root ──────────────────────────────────────────────────────────────
        var root = new GameObject(npcName);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.position = pos;

        // ── NpcController ─────────────────────────────────────────────────────
        var ctrl          = root.AddComponent<NpcController>();
        ctrl.npcName      = npcName;
        ctrl.npcType      = type;
        ctrl.patrolRadius = patrol;
        ctrl.moveSpeed    = speed;

        // Assign VFX prefabs
        if (!string.IsNullOrEmpty(primary))
            ctrl.primaryVFX   = AssetDatabase.LoadAssetAtPath<GameObject>(primary);
        if (!string.IsNullOrEmpty(secondary))
            ctrl.secondaryVFX = AssetDatabase.LoadAssetAtPath<GameObject>(secondary);

        // ── Collider ──────────────────────────────────────────────────────────
        var col    = root.AddComponent<CapsuleCollider>();
        col.height = 1.8f;
        col.radius = 0.4f;
        col.center = new Vector3(0f, 0.9f, 0f);

        // ── Model ─────────────────────────────────────────────────────────────
        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (modelAsset != null)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, root.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale    = Vector3.one;
        }
        else
        {
            // Model not found — placeholder capsule so the NPC still shows
            Debug.LogWarning($"[BCE] Model not found for {npcName}: {modelPath} — using placeholder.");
            var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            cap.transform.SetParent(root.transform);
            cap.transform.localPosition = new Vector3(0f, 1f, 0f);
            cap.name = "Model_Placeholder";
            Object.DestroyImmediate(cap.GetComponent<CapsuleCollider>());
        }

        EditorUtility.SetDirty(root);
        Debug.Log($"[BCE] Placed NPC: {npcName} at {pos}");
    }
}
