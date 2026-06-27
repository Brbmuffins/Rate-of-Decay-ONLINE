using System.Collections.Generic;
using System.IO;
using Mirror;
using kcp2k;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

// ═══════════════════════════════════════════════════════════════════════════════
//  RodEditorSetup
//  Unity menu: RoD/Setup/...
//
//  Run in order:
//    1 ▶ Create Login Scene      — builds LoginScene.unity from scratch,
//                                  wires NetworkManager + Auth + Transport,
//                                  adds both scenes to Build Settings.
//    2 ▶ Clean GameWorld         — removes stale NetworkManager from GameWorld.
//    3 ▶ Fix Build Settings      — re-applies correct scene order if ever lost.
//    4 ▶ Create Class Prefabs    — creates Engineer/Guardian/Wraith/Medic prefabs
//                                  from the Engineer FBX + all required components,
//                                  then auto-assigns them to RodNetworkManager.
//
//  After step 1+4 the only drag left is:
//    • LoginScreenVFX → brbmuffins prefab slots
// ═══════════════════════════════════════════════════════════════════════════════

public static class RodEditorSetup
{
    const string LOGIN_SCENE_PATH       = "Assets/Game/Scenes/LoginScene.unity";
    const string CHAR_SELECT_SCENE_PATH = "Assets/Game/Scenes/CharacterSelect.unity";
    const string GAME_WORLD_PATH        = "Assets/brbmuffins Skybox/Scenes/GameWorld.unity";
    const string SERVER_ADDRESS         = "15.204.243.36";
    const string ENGINEER_FBX_PATH      = "Assets/Game/Characters/Engineer/Model/Idle.fbx";
    const string PREFABS_DIR            = "Assets/Game/Prefabs";

    // ─────────────────────────────────────────────────────────────────────────
    //  0. Create Character Select Scene
    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("BCE/Setup/0 ▶ Create Character Select Scene", priority = 0)]
    static void CreateCharacterSelectScene()
    {
        if (File.Exists(CHAR_SELECT_SCENE_PATH))
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "Scene Exists",
                $"CharacterSelect already exists at:\n{CHAR_SELECT_SCENE_PATH}\n\nOverwrite it?",
                "Yes, rebuild it", "Cancel");
            if (!overwrite) return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(CHAR_SELECT_SCENE_PATH)!);

        // ── New empty scene ──────────────────────────────────────────────────
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Camera — dark background, URP post-processing ────────────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.025f, 0.018f, 0.06f, 1f);
        cam.farClipPlane    = 100f;
        // Exclude layer 31 — the 3D preview uses that layer with its own camera
        cam.cullingMask &= ~(1 << 31);
        cam.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
        camGO.AddComponent<AudioListener>();

        var urpData = camGO.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (urpData != null) urpData.renderPostProcessing = true;

        // ── CharacterSelectManager ────────────────────────────────────────────
        var mgrGO = new GameObject("CharacterSelectManager");
        mgrGO.AddComponent<CharacterSelectManager>();

        // ── EventSystem ───────────────────────────────────────────────────────
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        var inputModuleType = System.Type.GetType(
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputModuleType != null)
            esGO.AddComponent(inputModuleType);
        else
            esGO.AddComponent<StandaloneInputModule>();

        // ── Save ──────────────────────────────────────────────────────────────
        EditorSceneManager.SaveScene(scene, CHAR_SELECT_SCENE_PATH);
        AssetDatabase.Refresh();

        // ── Rebuild build settings with all 3 scenes ──────────────────────────
        ApplyBuildSettings();

        Debug.Log($"[BCE] ✅ CharacterSelect scene created → {CHAR_SELECT_SCENE_PATH}");
        EditorUtility.DisplayDialog(
            "✅ Character Select Scene Ready",
            "CharacterSelect.unity created!\n\n" +
            "Build settings updated:\n" +
            "  Index 0 → LoginScene\n" +
            "  Index 1 → CharacterSelect\n" +
            "  Index 2 → GameWorld\n\n" +
            "Flow: LOGIN → CHARACTER SELECT → GAME WORLD",
            "Got it!");

        // Return to LoginScene
        if (File.Exists(LOGIN_SCENE_PATH))
            EditorSceneManager.OpenScene(LOGIN_SCENE_PATH, OpenSceneMode.Single);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  1. Create Login Scene
    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("BCE/Setup/1 ▶ Create Login Scene", priority = 1)]
    static void CreateLoginScene()
    {
        if (File.Exists(LOGIN_SCENE_PATH))
        {
            bool overwrite = EditorUtility.DisplayDialog(
                "Scene Exists",
                $"LoginScene already exists at:\n{LOGIN_SCENE_PATH}\n\nOverwrite it?",
                "Yes, rebuild it", "Cancel");
            if (!overwrite) return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(LOGIN_SCENE_PATH)!);

        // ── New empty scene ──────────────────────────────────────────────────
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Camera ───────────────────────────────────────────────────────────
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags      = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.03f, 0.02f, 0.07f, 1f);
        cam.farClipPlane    = 100f;
        cam.transform.SetPositionAndRotation(new Vector3(0f, 1f, -10f), Quaternion.identity);
        camGO.AddComponent<AudioListener>();

        // URP camera data (safe no-op on Built-in)
        var urpData = camGO.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (urpData != null)
            urpData.renderPostProcessing = true;

        // ── NetworkManager ────────────────────────────────────────────────────
        var nmGO = new GameObject("NetworkManager");

        // Transport must be added before NetworkManager so it can be referenced
        var transport = nmGO.AddComponent<KcpTransport>();

        var nm = nmGO.AddComponent<RodNetworkManager>();
        nm.transport        = transport;
        Transport.active    = transport;

        var auth = nmGO.AddComponent<RodNetworkAuthenticator>();
        nm.authenticator    = auth;

        nm.networkAddress   = SERVER_ADDRESS;
        nm.onlineScene      = GAME_WORLD_PATH;
        nm.autoCreatePlayer = false;
        nm.playerPrefab     = null;

        // ── Login UI root ─────────────────────────────────────────────────────
        var loginGO  = new GameObject("LoginUI");
        var loginMgr = loginGO.AddComponent<LoginManager>();
        var loginVFX = loginGO.AddComponent<LoginScreenVFX>();
        loginMgr.sceneVFX = loginVFX;

        // ── EventSystem (required for UI button clicks) ───────────────────────
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        // Use new Input System module if available, otherwise Standalone
        var inputModuleType = System.Type.GetType(
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputModuleType != null)
            esGO.AddComponent(inputModuleType);
        else
            esGO.AddComponent<StandaloneInputModule>();

        // ── Save scene ────────────────────────────────────────────────────────
        EditorSceneManager.SaveScene(scene, LOGIN_SCENE_PATH);
        AssetDatabase.Refresh();

        // ── Build Settings ────────────────────────────────────────────────────
        ApplyBuildSettings();

        Debug.Log($"[BCE] ✅ LoginScene created → {LOGIN_SCENE_PATH}");
        EditorUtility.DisplayDialog(
            "✅ Login Scene Ready",
            "LoginScene.unity created and wired!\n\n" +
            "Just 2 quick drags left:\n" +
            "  1. NetworkManager → classPrefabs [0–3]\n" +
            "     (Engineer / Guardian / Wraith / Medic)\n\n" +
            "  2. LoginUI (LoginScreenVFX) → VFX prefab slots\n\n" +
            "Then run menu item 2 to clean GameWorld.",
            "Got it!");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  2. Clean GameWorld — remove old NetworkManager
    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("BCE/Setup/2 ▶ Clean GameWorld (remove old NetworkManager)", priority = 2)]
    static void CleanGameWorld()
    {
        if (!File.Exists(GAME_WORLD_PATH))
        {
            EditorUtility.DisplayDialog("Not Found",
                $"Scene not found:\n{GAME_WORLD_PATH}", "OK");
            return;
        }

        var scene   = EditorSceneManager.OpenScene(GAME_WORLD_PATH, OpenSceneMode.Single);
        int removed = 0;

        foreach (var root in scene.GetRootGameObjects())
        {
            // Remove NetworkManager (and any subclass)
            foreach (var nm in root.GetComponentsInChildren<NetworkManager>(true))
            {
                Object.DestroyImmediate(nm);
                removed++;
            }
            // Remove any Transport that was only there to serve the old NM
            foreach (var t in root.GetComponentsInChildren<Transport>(true))
                Object.DestroyImmediate(t);

            // Remove any Authenticator
            foreach (var a in root.GetComponentsInChildren<NetworkAuthenticator>(true))
                Object.DestroyImmediate(a);
        }

        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[BCE] ✅ Removed {removed} NetworkManager(s) from GameWorld.");

        EditorUtility.DisplayDialog("✅ GameWorld Cleaned",
            $"Removed {removed} NetworkManager component(s) from GameWorld.\n\n" +
            "NetworkManager now lives only in LoginScene — correct!",
            "OK");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  3. Fix Build Settings
    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("BCE/Setup/3 ▶ Fix Build Settings", priority = 3)]
    static void FixBuildSettings()
    {
        ApplyBuildSettings();
        EditorUtility.DisplayDialog("✅ Build Settings Updated",
            "Scene order:\n" +
            "  Index 0 → LoginScene\n" +
            "  Index 1 → CharacterSelect\n" +
            "  Index 2 → GameWorld\n\n" +
            "This is required for Mirror's online/offline scene system.", "OK");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  4. Moved to RodPrefabBuilder.cs
    // ─────────────────────────────────────────────────────────────────────────

    // (see Assets/Game/Editor/RodPrefabBuilder.cs)
    static void CreateClassPrefabs()
    {
        // Load the Engineer FBX model
        var engineerFbx = AssetDatabase.LoadAssetAtPath<GameObject>(ENGINEER_FBX_PATH);
        if (engineerFbx == null)
        {
            EditorUtility.DisplayDialog("Model Not Found",
                $"Could not find Engineer FBX at:\n{ENGINEER_FBX_PATH}\n\n" +
                "Check the path in RodEditorSetup.cs → ENGINEER_FBX_PATH.", "OK");
            return;
        }

        Directory.CreateDirectory(PREFABS_DIR);

        // Class names — index must match RodNetworkManager.classPrefabs order
        string[] classNames = { "Engineer", "Guardian", "Wraith", "Medic" };
        var createdPaths    = new string[classNames.Length];

        for (int i = 0; i < classNames.Length; i++)
        {
            string name     = classNames[i];
            string path     = $"{PREFABS_DIR}/{name}.prefab";

            // ── Build the root GameObject ─────────────────────────────────────
            var root = new GameObject(name);

            // Required by Mirror — every networked object needs this
            root.AddComponent<NetworkIdentity>();

            // Our custom SyncVar identity (name + class index)
            var identity    = root.AddComponent<PlayerIdentity>();
            identity.classIndex = i;

            // Physics
            var rb          = root.AddComponent<Rigidbody>();
            rb.freezeRotation = true;
            rb.mass         = 80f;
            rb.linearDamping = 1f;

            // Collision — capsule sized for a humanoid
            var col         = root.AddComponent<CapsuleCollider>();
            col.height      = 1.8f;
            col.radius      = 0.35f;
            col.center      = new Vector3(0f, 0.9f, 0f);

            // Player movement script (cam assigned at runtime via CameraFollow)
            root.AddComponent<PlayerMovement>();

            // ── Attach the Engineer model as a child ──────────────────────────
            var model = (GameObject)PrefabUtility.InstantiatePrefab(engineerFbx, root.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale    = Vector3.one;

            // ── Save as prefab ────────────────────────────────────────────────
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            createdPaths[i] = path;
            Debug.Log($"[BCE] Created prefab: {path}");

            // Mark for Mirror spawning (prefab must have NetworkIdentity GUID)
            _ = prefab; // suppress unused warning
        }

        AssetDatabase.Refresh();

        // ── Auto-assign to RodNetworkManager in LoginScene ────────────────────
        if (File.Exists(LOGIN_SCENE_PATH))
        {
            var loginScene = EditorSceneManager.OpenScene(LOGIN_SCENE_PATH, OpenSceneMode.Single);

            RodNetworkManager nm = null;
            foreach (var root in loginScene.GetRootGameObjects())
            {
                nm = root.GetComponent<RodNetworkManager>();
                if (nm != null) break;
            }

            if (nm != null)
            {
                nm.classPrefabs = new GameObject[createdPaths.Length];
                for (int i = 0; i < createdPaths.Length; i++)
                    nm.classPrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(createdPaths[i]);

                EditorUtility.SetDirty(nm);
                EditorSceneManager.SaveScene(loginScene);
                Debug.Log("[BCE] ✅ classPrefabs assigned to RodNetworkManager in LoginScene.");
            }
            else
            {
                Debug.LogWarning("[BCE] LoginScene found but no RodNetworkManager — run step 1 first.");
            }
        }
        else
        {
            Debug.LogWarning("[BCE] LoginScene not found — run step 1 first, then step 4.");
        }

        EditorUtility.DisplayDialog("✅ Class Prefabs Created",
            "4 prefabs created in Assets/Game/Prefabs/:\n" +
            "  [0] Engineer\n  [1] Guardian\n  [2] Wraith\n  [3] Medic\n\n" +
            "All using the Engineer model as a placeholder.\n" +
            "classPrefabs auto-assigned to RodNetworkManager.\n\n" +
            "Swap individual models later when you have the real assets.",
            "Done!");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Shared helpers
    // ─────────────────────────────────────────────────────────────────────────

    static void ApplyBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>();

        // Order matters: Login(0) → CharacterSelect(1) → GameWorld(2)
        foreach (var path in new[] { LOGIN_SCENE_PATH, CHAR_SELECT_SCENE_PATH, GAME_WORLD_PATH })
        {
            if (File.Exists(path))
                scenes.Add(new EditorBuildSettingsScene(path, true));
            else
                Debug.LogWarning($"[BCE] Scene not found, skipping: {path}");
        }

        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log("[BCE] Build Settings updated: LoginScene(0), CharacterSelect(1), GameWorld(2)");
    }
}
