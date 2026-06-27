using System.Collections.Generic;
using System.IO;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// RoD/Setup/4 — Creates Engineer/Guardian/Wraith/Medic prefabs from the
/// Engineer FBX model and auto-assigns them to RodNetworkManager.
/// </summary>
public static class RodPrefabBuilder
{
    const string FBX_PATH      = "Assets/Game/Characters/Engineer/Model/Idle.fbx";
    const string PREFABS_DIR   = "Assets/Game/Prefabs";
    const string LOGIN_SCENE   = "Assets/Game/Scenes/LoginScene.unity";

    static readonly string[] ClassNames = { "Engineer", "Guardian", "Wraith", "Medic" };

    [MenuItem("BCE/Setup/4 ▶ Create Class Prefabs (Engineer x4)", priority = 4)]
    static void Build()
    {
        // ── Load source FBX ───────────────────────────────────────────────────
        var sourceFbx = AssetDatabase.LoadAssetAtPath<GameObject>(FBX_PATH);
        if (sourceFbx == null)
        {
            EditorUtility.DisplayDialog("FBX Not Found",
                "Could not load:\n" + FBX_PATH + "\n\nCheck the path.", "OK");
            return;
        }

        Directory.CreateDirectory(PREFABS_DIR);

        var builtPaths = new List<string>();

        for (int i = 0; i < ClassNames.Length; i++)
        {
            string prefabPath = PREFABS_DIR + "/" + ClassNames[i] + ".prefab";

            // Root GameObject
            GameObject go = new GameObject(ClassNames[i]);

            // Mirror networking (NetworkTransform added at runtime — see RodNetworkManager)
            go.AddComponent<NetworkIdentity>();

            // Our identity SyncVar
            PlayerIdentity pid = go.AddComponent<PlayerIdentity>();
            pid.classIndex = i;

            // Physics
            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.freezeRotation = true;
            rb.mass = 80f;

            // Collision
            CapsuleCollider cap = go.AddComponent<CapsuleCollider>();
            cap.height = 1.8f;
            cap.radius = 0.35f;
            cap.center = new Vector3(0f, 0.9f, 0f);

            // Player movement (handles local-vs-remote check internally)
            go.AddComponent<PlayerMovement>();

            // Attach the FBX model as a child
            GameObject modelChild = (GameObject)PrefabUtility.InstantiatePrefab(sourceFbx, go.transform);
            modelChild.name = "Model";
            modelChild.transform.localPosition = Vector3.zero;
            modelChild.transform.localRotation = Quaternion.identity;
            modelChild.transform.localScale    = Vector3.one;

            // Save
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);

            builtPaths.Add(prefabPath);
            Debug.Log("[BCE] Created: " + prefabPath);
        }

        AssetDatabase.Refresh();

        // ── Wire to NetworkManager in LoginScene ──────────────────────────────
        if (!File.Exists(LOGIN_SCENE))
        {
            Debug.LogWarning("[BCE] LoginScene not found — run step 1 first.");
            ShowDone(builtPaths, wired: false);
            return;
        }

        var scene = EditorSceneManager.OpenScene(LOGIN_SCENE, OpenSceneMode.Single);

        RodNetworkManager nm = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            nm = root.GetComponent<RodNetworkManager>();
            if (nm != null) break;
        }

        bool wired = false;
        if (nm != null)
        {
            nm.classPrefabs = new GameObject[builtPaths.Count];
            for (int i = 0; i < builtPaths.Count; i++)
                nm.classPrefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(builtPaths[i]);

            EditorUtility.SetDirty(nm);
            EditorSceneManager.SaveScene(scene);
            wired = true;
            Debug.Log("[BCE] classPrefabs assigned to RodNetworkManager.");
        }

        ShowDone(builtPaths, wired);
    }

    // ── Step 5: Fix AnimatorController on existing prefabs ────────────────────

    const string ANIM_CTRL = "Assets/Game/Characters/Engineer/Animations/AnimationController.controller";

    [MenuItem("BCE/Setup/5 ▶ Fix Animator Controllers", priority = 5)]
    static void FixAnimators()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ANIM_CTRL);
        if (ctrl == null)
        {
            EditorUtility.DisplayDialog("Controller Not Found",
                "Could not find:\n" + ANIM_CTRL + "\n\nCheck the path.", "OK");
            return;
        }

        int fixed_ = 0;
        foreach (var className in ClassNames)
        {
            string path = PREFABS_DIR + "/" + className + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogWarning("[BCE] Prefab not found: " + path); continue; }

            // Edit the prefab asset directly
            using (var scope = new PrefabUtility.EditPrefabContentsScope(path))
            {
                var root  = scope.prefabContentsRoot;
                // Animator may be on the root or on the Model child
                var anim  = root.GetComponentInChildren<Animator>(true);
                if (anim != null)
                {
                    anim.runtimeAnimatorController = ctrl;
                    fixed_++;
                    Debug.Log("[BCE] Assigned AnimatorController to: " + path);
                }
                else
                {
                    Debug.LogWarning("[BCE] No Animator found in: " + path);
                }
            }
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("✅ Animators Fixed",
            $"AnimatorController assigned to {fixed_} prefab(s).\nWarnings gone next Play.", "Done!");
    }

    static void ShowDone(List<string> paths, bool wired)
    {
        string msg = "Prefabs saved to Assets/Game/Prefabs/:\n";
        for (int i = 0; i < paths.Count; i++)
            msg += "  [" + i + "] " + ClassNames[i] + "\n";

        msg += wired
            ? "\nAuto-assigned to RodNetworkManager ✅"
            : "\nRun step 1 first, then step 4 to auto-assign.";

        EditorUtility.DisplayDialog("✅ Class Prefabs Created", msg, "Done!");
    }
}
