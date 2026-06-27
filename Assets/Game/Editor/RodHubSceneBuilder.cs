using System.Collections.Generic;
using System.IO;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

// ═══════════════════════════════════════════════════════════════════════════
//  RodHubSceneBuilder
//  Unity menu: RoD/Setup/5 ▶ Create Hub Scene
//
//  Creates Assets/Game/Scenes/Hub.unity and wires it as the NetworkManager
//  online scene. Run this AFTER step 1 (Create Login Scene).
//
//  What it builds:
//    • Camera + Directional Light
//    • Placeholder ground plane (200×200 units)
//    • 8 NetworkStartPosition spawn points in a circle
//    • GmConsole (` or F1)
//    • RodChatManager + NetworkIdentity (Enter to chat)
//    • EventSystem
//
//  Also:
//    • Sets RodNetworkManager.onlineScene → Hub.unity in LoginScene
//    • Updates Build Settings: Login(0), CharacterSelect(1), Hub(2)
// ═══════════════════════════════════════════════════════════════════════════

public static class RodHubSceneBuilder
{
    const string HUB_SCENE_PATH   = "Assets/Game/Scenes/Hub.unity";
    const string LOGIN_SCENE_PATH = "Assets/Game/Scenes/LoginScene.unity";
    const string CHAR_SELECT_PATH = "Assets/Game/Scenes/CharacterSelect.unity";

    [MenuItem("BCE/Setup/5 ▶ Create Hub Scene", priority = 5)]
    static void CreateHubScene()
    {
        if (File.Exists(HUB_SCENE_PATH))
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "Scene Exists",
                $"Hub.unity already exists at:\n{HUB_SCENE_PATH}\n\nRebuild it from scratch?",
                "Yes, rebuild", "Cancel");
            if (!overwrite) return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(HUB_SCENE_PATH)!);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Camera ───────────────────────────────────────────────────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags   = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.04f, 0.08f, 1f);
        cam.farClipPlane = 1000f;
        cam.transform.SetPositionAndRotation(
            new Vector3(0f, 6f, -12f), Quaternion.Euler(15f, 0f, 0f));
        camGO.AddComponent<AudioListener>();

        // URP post-processing (no-op on Built-In)
        var urpCam = camGO.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (urpCam != null) urpCam.renderPostProcessing = true;

        // ── Directional light ─────────────────────────────────────────────────
        var lightGO = new GameObject("Directional Light");
        var dirLight = lightGO.AddComponent<Light>();
        dirLight.type      = LightType.Directional;
        dirLight.intensity = 1.0f;
        dirLight.color     = new Color(1f, 0.92f, 0.80f);
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ── Ground plane placeholder ──────────────────────────────────────────
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(20f, 1f, 20f);  // 200×200 units
        ground.isStatic = true;

        // Try URP lit shader first, fall back to Standard
        var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (groundMat == null || groundMat.shader.name == "Hidden/InternalErrorShader")
            groundMat = new Material(Shader.Find("Standard"));
        groundMat.color = new Color(0.12f, 0.12f, 0.18f);
        ground.GetComponent<Renderer>().material = groundMat;

        // ── Spawn points — 8 positions in a circle, radius 6 ─────────────────
        var spawnRoot = new GameObject("SpawnPoints");
        const int   spawnCount = 8;
        const float spawnRadius = 6f;
        for (int i = 0; i < spawnCount; i++)
        {
            float angle = i * (360f / spawnCount) * Mathf.Deg2Rad;
            var sp = new GameObject($"SpawnPoint_{i}");
            sp.transform.SetParent(spawnRoot.transform);
            sp.transform.position = new Vector3(
                Mathf.Sin(angle) * spawnRadius,
                0.1f,
                Mathf.Cos(angle) * spawnRadius);
            sp.AddComponent<NetworkStartPosition>();
        }

        // ── GmConsole (` or F1) ───────────────────────────────────────────────
        var gmGO = new GameObject("GmConsole");
        gmGO.AddComponent<GmConsole>();

        // ── RodChatManager — needs NetworkIdentity for ClientRpc ──────────────
        var chatGO = new GameObject("RodChatManager");
        chatGO.AddComponent<NetworkIdentity>();
        chatGO.AddComponent<RodChatManager>();

        // ── EventSystem ───────────────────────────────────────────────────────
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        var inputModType = System.Type.GetType(
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputModType != null) esGO.AddComponent(inputModType);
        else                      esGO.AddComponent<StandaloneInputModule>();

        // ── Save Hub scene ────────────────────────────────────────────────────
        EditorSceneManager.SaveScene(scene, HUB_SCENE_PATH);
        AssetDatabase.Refresh();

        // ── Point NetworkManager → Hub ────────────────────────────────────────
        UpdateOnlineScene();

        // ── Build Settings ────────────────────────────────────────────────────
        ApplyBuildSettings();

        Debug.Log($"[BCE] ✅ Hub scene created → {HUB_SCENE_PATH}");
        EditorUtility.DisplayDialog(
            "✅ Hub Scene Ready",
            "Hub.unity created in Assets/Game/Scenes/\n\n" +
            "Contents:\n" +
            "  • Camera + Directional Light\n" +
            "  • 200×200 ground plane (placeholder)\n" +
            "  • 8 NetworkStartPosition spawn points\n" +
            "  • GmConsole  (` or F1)\n" +
            "  • RodChatManager  (Enter to type)\n\n" +
            "NetworkManager.onlineScene → Hub.unity ✓\n\n" +
            "Build Settings:\n" +
            "  0 → LoginScene\n" +
            "  1 → CharacterSelect\n" +
            "  2 → Hub\n\n" +
            "Start a Host from LoginScene to test!",
            "Let's go!");
    }

    // ── Wire NetworkManager ───────────────────────────────────────────────────

    static void UpdateOnlineScene()
    {
        if (!File.Exists(LOGIN_SCENE_PATH))
        {
            Debug.LogWarning("[BCE] LoginScene not found — run step 1 first, then step 5.");
            return;
        }

        var loginScene = EditorSceneManager.OpenScene(LOGIN_SCENE_PATH, OpenSceneMode.Single);
        RodNetworkManager nm = null;
        foreach (var root in loginScene.GetRootGameObjects())
        {
            nm = root.GetComponent<RodNetworkManager>();
            if (nm != null) break;
        }

        if (nm != null)
        {
            nm.onlineScene = HUB_SCENE_PATH;
            EditorUtility.SetDirty(nm);
            EditorSceneManager.SaveScene(loginScene);
            Debug.Log($"[BCE] RodNetworkManager.onlineScene → {HUB_SCENE_PATH}");
        }
        else
        {
            Debug.LogWarning("[BCE] No RodNetworkManager in LoginScene — run step 1 first.");
        }
    }

    // ── Build Settings ────────────────────────────────────────────────────────

    static void ApplyBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>();
        foreach (var path in new[] { LOGIN_SCENE_PATH, CHAR_SELECT_PATH, HUB_SCENE_PATH })
        {
            if (File.Exists(path))
                scenes.Add(new EditorBuildSettingsScene(path, true));
            else
                Debug.LogWarning($"[BCE] Scene not found, skipping build entry: {path}");
        }
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("[BCE] Build Settings: LoginScene(0), CharacterSelect(1), Hub(2)");
    }
}
