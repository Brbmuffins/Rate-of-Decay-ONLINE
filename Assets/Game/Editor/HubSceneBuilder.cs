// ═══════════════════════════════════════════════════════════════════════════
//  HubSceneBuilder — RoD/Build Hub Scene  (Editor-only, never ships in build)
//
//  Open Game/Scenes/Hub.unity in the editor, then run:
//    Menu → RoD → Build Hub Scene
//
//  The script is fully re-runnable: it destroys the "HubEnvironment" root
//  first so you can tweak and rebuild without duplicates.
//  Remember to save the scene (Ctrl+S) after running.
// ═══════════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

public static class HubSceneBuilder
{
    [MenuItem("RoD/Build Hub Scene")]
    static void Build()
    {
        Random.InitState(42); // deterministic — same layout every rebuild

        // ── Clear previous environment ────────────────────────────────────
        var old = GameObject.Find("HubEnvironment");
        if (old != null) Object.DestroyImmediate(old);

        var root = new GameObject("HubEnvironment");

        // ── Skybox ────────────────────────────────────────────────────────
        // Sunset gives a warm fantasy glow
        ApplySkybox("Assets/brbmuffins Skybox/Panoramics/FS017/FS017_Sunset.mat");

        // ── Sun (only add if no directional light already in scene) ───────
        bool hasSun = false;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { hasSun = true; break; }

        if (!hasSun)
        {
            var sunGO = new GameObject("Sun");
            sunGO.transform.SetParent(root.transform);
            sunGO.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            var sun = sunGO.AddComponent<Light>();
            sun.type      = LightType.Directional;
            sun.intensity = 1.15f;
            sun.color     = new Color(1f, 0.92f, 0.78f); // warm afternoon
            sun.shadows   = LightShadows.Soft;
        }

        // ── Ground — flat cube with BoxCollider (more reliable than Plane MeshCollider) ──
        // Surface sits exactly at Y=0; box extends downward so nothing falls through.
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.tag  = "Ground";
        ground.transform.SetParent(root.transform);
        ground.transform.position   = new Vector3(0f, -0.5f, 0f);  // top face at Y=0
        ground.transform.localScale = new Vector3(160f, 1f, 160f);

        var groundMat = Asset<Material>(
            "Assets/Vegetation_Stylized_Pack_ByLuxArtStudios/Materials/MI_Grass 1.mat");
        if (groundMat != null)
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;

        // ── Inner tree ring (radius 26) ────────────────────────────────────
        string[] treesInner =
        {
            "Assets/Vegetation_Stylized_Pack_ByLuxArtStudios/Prefabs/Trees/S_Tree_A.prefab",
            "Assets/Vegetation_Stylized_Pack_ByLuxArtStudios/Prefabs/Trees/S_Tree_B.prefab",
            "Assets/Vegetation_Stylized_Pack_ByLuxArtStudios/Prefabs/Trees/S_Tree_C.prefab",
            "Assets/Vegetation_Stylized_Pack_ByLuxArtStudios/Prefabs/Trees/S_Tree_D.prefab",
            "Assets/Vegetation_Stylized_Pack_ByLuxArtStudios/Prefabs/Trees/S_Tree_E.prefab",
        };
        PlaceInRing(root, treesInner, count: 10, radius: 26f, scaleMin: 0.9f, scaleMax: 1.4f);

        // ── Outer dense backdrop ring (radius 40) ─────────────────────────
        string[] treesOuter =
        {
            "Assets/Vegetation_Stylized_Pack_ByLuxArtStudios/Prefabs/Trees/S_Tree_F.prefab",
            "Assets/Vegetation_Stylized_Pack_ByLuxArtStudios/Prefabs/Trees/S_Tree_G.prefab",
            "Assets/Vegetation_Stylized_Pack_ByLuxArtStudios/Prefabs/Trees/S_Tree_H.prefab",
            "Assets/Vegetation_Stylized_Pack_ByLuxArtStudios/Prefabs/Trees/S_Tree_I.prefab",
            "Assets/Vegetation_Stylized_Pack_ByLuxArtStudios/Prefabs/Trees/S_Tree_J.prefab",
        };
        PlaceInRing(root, treesOuter, count: 22, radius: 40f, scaleMin: 1.3f, scaleMax: 2.2f);

        // ── Bushes & ferns — sparse mid-ring scatter ──────────────────────
        string[] bushes =
        {
            "Assets/Vegetation_Stylized_Pack_ByLuxArtStudios/Prefabs/Bushes/S_Bush_A.prefab",
            "Assets/Vegetation_Stylized_Pack_ByLuxArtStudios/Prefabs/Bushes/S_Bush_B.prefab",
            "Assets/Vegetation_Stylized_Pack_ByLuxArtStudios/Prefabs/Bushes/S_Fern_A.prefab",
            "Assets/Vegetation_Stylized_Pack_ByLuxArtStudios/Prefabs/Bushes/S_Fern_C.prefab",
        };
        PlaceScattered(root, bushes, count: 12, innerR: 14f, outerR: 27f);

        // ── Flowers — light accent near treeline only ─────────────────────
        string[] flowers =
        {
            "Assets/Vegetation_Stylized_Pack_ByLuxArtStudios/Prefabs/Bushes/S_Flowers_A.prefab",
            "Assets/Vegetation_Stylized_Pack_ByLuxArtStudios/Prefabs/Bushes/S_Flowers_C.prefab",
            "Assets/Vegetation_Stylized_Pack_ByLuxArtStudios/Prefabs/Bushes/S_Flowers_E.prefab",
        };
        PlaceScattered(root, flowers, count: 10, innerR: 18f, outerR: 26f, scaleMin: 0.6f, scaleMax: 1.0f);

        // (Ore nodes removed — felt like clutter in a social hub)

        // ── Crystal clusters — 4 cardinal accent points ───────────────────
        string[] crystals =
        {
            "Assets/brbmuffins Studio/brbmuffins Magic Pack/Prefabs/Environment/Crystal effect blue.prefab",
            "Assets/brbmuffins Studio/brbmuffins Magic Pack/Prefabs/Environment/Crystal effect green.prefab",
            "Assets/brbmuffins Studio/brbmuffins Magic Pack/Prefabs/Environment/Crystal effect blue.prefab",
            "Assets/brbmuffins Studio/brbmuffins Magic Pack/Prefabs/Environment/Crystal effect red.prefab",
        };
        float[] crystalAngles = { 45f, 135f, 225f, 315f };
        for (int i = 0; i < crystalAngles.Length; i++)
        {
            float rad = crystalAngles[i] * Mathf.Deg2Rad;
            Vector3 cp = new Vector3(Mathf.Sin(rad) * 17f, 0f, Mathf.Cos(rad) * 17f);
            Place(root, crystals[i], cp, Quaternion.Euler(0f, crystalAngles[i], 0f),
                  Vector3.one * Random.Range(0.9f, 1.3f));
        }

        // ── Three portals — evenly spaced at radius 18 ────────────────────
        //    Blue   = Combat Zone (GameWorld)
        //    Green  = Starting Zone (placeholder, coming soon)
        //    Yellow = future zone
        (string path, float angle, string label, string scene)[] portals =
        {
            ("Assets/brbmuffins Studio/brbmuffins Magic Pack/Prefabs/Portals/Portal blue.prefab",
             0f,   "Combat Zone",    "GameWorld"),
            ("Assets/brbmuffins Studio/brbmuffins Magic Pack/Prefabs/Portals/Portal green.prefab",
             120f, "Starting Zone",  ""),          // empty = coming soon
            ("Assets/brbmuffins Studio/brbmuffins Magic Pack/Prefabs/Portals/Portal yellow.prefab",
             240f, "Dark Frontier",  ""),          // empty = coming soon
        };

        foreach (var (pPath, pAngle, pLabel, pScene) in portals)
        {
            float rad      = pAngle * Mathf.Deg2Rad;
            Vector3 pos    = new Vector3(Mathf.Sin(rad) * 18f, 0f, Mathf.Cos(rad) * 18f);
            Quaternion rot = Quaternion.Euler(0f, pAngle + 180f, 0f); // face inward

            var portalGO = Place(root, pPath, pos, rot, Vector3.one * 1.6f);
            if (portalGO != null)
            {
                portalGO.name = pLabel;

                // Wire HubPortal component
                var hp          = portalGO.AddComponent<HubPortal>();
                hp.portalLabel  = pLabel;
                hp.targetScene  = pScene;
                hp.activateRadius = 5f;

                // Trigger collider for proximity (sphere, no physics response)
                var sc = portalGO.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.radius    = 3f;
            }

            // Light pillar rising from each portal
            Place(root,
                "Assets/brbmuffins VFX/brbmuffins Free VFX/Prefab/FX_LightPillar.prefab",
                pos + Vector3.up * 0.1f, Quaternion.identity, Vector3.one * 1.0f);
        }

        // ── Central shrine — magic circle on the ground ───────────────────
        Place(root,
            "Assets/brbmuffins Studio/brbmuffins Magic Pack/Prefabs/Magic circles/Magic circle.prefab",
            new Vector3(0f, 0.05f, 0f), Quaternion.identity, Vector3.one * 4f);

        // ── Ambient particle effects ──────────────────────────────────────
        // Fireflies drifting through the clearing
        Place(root,
            "Assets/brbmuffins Technologies/brbmuffins Particle Pack/EffectExamples/Misc Effects/Prefabs/FireFlies.prefab",
            new Vector3(0f, 0.5f, 0f), Quaternion.identity, Vector3.one * 4f);

        // Gentle dust motes in the air
        Place(root,
            "Assets/brbmuffins Technologies/brbmuffins Particle Pack/EffectExamples/Misc Effects/Prefabs/DustMotesEffect.prefab",
            new Vector3(0f, 1.5f, 0f), Quaternion.identity, Vector3.one * 3f);

        // Single light ground-fog at centre — subtle, not spammy
        Place(root,
            "Assets/brbmuffins Technologies/brbmuffins Particle Pack/EffectExamples/Smoke & Steam Effects/Prefabs/GroundFog.prefab",
            new Vector3(0f, 0f, 0f), Quaternion.identity, Vector3.one * 3f);

        // ── Candles near the central shrine (4 cardinal) ─────────────────
        float[] candleAngles = { 0f, 90f, 180f, 270f };
        foreach (float ca in candleAngles)
        {
            float rad   = ca * Mathf.Deg2Rad;
            Vector3 cp2 = new Vector3(Mathf.Sin(rad) * 4.5f, 0f, Mathf.Cos(rad) * 4.5f);
            Place(root,
                "Assets/brbmuffins Technologies/brbmuffins Particle Pack/EffectExamples/Misc Effects/Prefabs/Candles.prefab",
                cp2, Quaternion.Euler(0f, ca, 0f), Vector3.one * 0.6f);
        }

        // ── NetworkStartPosition — spawn points spread around the clearing ──
        // Mirror picks these via GetStartPosition() when spawning players.
        // 8 points evenly spaced at radius 5, at Y=0.5 to stay clear of ground surface.
        for (int i = 0; i < 8; i++)
        {
            float angle = (360f / 8f * i) * Mathf.Deg2Rad;
            Vector3 sp  = new Vector3(Mathf.Sin(angle) * 5f, 0.5f, Mathf.Cos(angle) * 5f);
            var spGO    = new GameObject($"SpawnPoint_{i}");
            spGO.transform.SetParent(root.transform);
            spGO.transform.position = sp;
            spGO.AddComponent<Mirror.NetworkStartPosition>();
        }

        // ── Mark scene dirty — remind user to save ────────────────────────
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("[HubSceneBuilder] ✓ Hub environment built. Press Ctrl+S to save the scene.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    static void ApplySkybox(string matPath)
    {
        var sky = Asset<Material>(matPath);
        if (sky != null)
        {
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            DynamicGI.UpdateEnvironment();
        }
        else Debug.LogWarning($"[HubSceneBuilder] Skybox not found at: {matPath}");
    }

    /// <summary>Evenly-spaced ring with random jitter.</summary>
    static void PlaceInRing(GameObject root, string[] prefabs, int count, float radius,
        float scaleMin = 1f, float scaleMax = 1f)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count * i + Random.Range(-8f, 8f)) * Mathf.Deg2Rad;
            float r     = radius + Random.Range(-2f, 2f);
            Vector3 pos = new Vector3(Mathf.Sin(angle) * r, 0f, Mathf.Cos(angle) * r);
            Place(root, prefabs[i % prefabs.Length], pos,
                  Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                  Vector3.one * Random.Range(scaleMin, scaleMax));
        }
    }

    /// <summary>Random scatter in an annular region.</summary>
    static void PlaceScattered(GameObject root, string[] prefabs, int count,
        float innerR, float outerR, float scaleMin = 0.8f, float scaleMax = 1.2f)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float r     = Random.Range(innerR, outerR);
            Vector3 pos = new Vector3(Mathf.Sin(angle) * r, 0f, Mathf.Cos(angle) * r);
            Place(root, prefabs[i % prefabs.Length], pos,
                  Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                  Vector3.one * Random.Range(scaleMin, scaleMax));
        }
    }

    /// <summary>Load, instantiate, and parent a prefab. Returns the instance (or null on missing).</summary>
    static GameObject Place(GameObject root, string prefabPath, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        var prefab = Asset<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[HubSceneBuilder] Prefab not found — skipping: {prefabPath}");
            return null;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        go.transform.SetParent(root.transform);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = scale;
        return go;
    }

    static T Asset<T>(string path) where T : Object =>
        AssetDatabase.LoadAssetAtPath<T>(path);
}
#endif
