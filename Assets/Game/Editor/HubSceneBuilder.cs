// ═══════════════════════════════════════════════════════════════════════════
//  HubSceneBuilder — BCE/Build Hub Scene  (Editor-only, never ships in build)
//
//  Wipes decoration/environment from the scene, then rebuilds with only:
//    • Directional light
//    • Gray ground plane (100×100, tagged "Ground")
//    • 8 NetworkStartPosition spawn points
//    • RodChatManager scene object (NetworkIdentity — required for chat)
//
//  Objects with NetworkIdentity or NetworkManager are always preserved.
//  Run: Menu → BCE → Build Hub Scene (Hub.unity must be open), then Ctrl+S.
// ═══════════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HubSceneBuilder
{
    [MenuItem("BCE/Build Hub Scene")]
    static void Build()
    {
        // ── Destroy decoration/environment — preserve networking objects ──
        // Keep anything with a NetworkIdentity (RodChatManager, etc.),
        // NetworkManager, or NetworkAuthenticator — deleting those breaks Mirror.
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go.transform.parent != null) continue; // root objects only
            if (go.GetComponent<Mirror.NetworkIdentity>()      != null) continue;
            if (go.GetComponent<Mirror.NetworkManager>()       != null) continue;
            if (go.GetComponent<Mirror.NetworkAuthenticator>() != null) continue;
            Object.DestroyImmediate(go);
        }

        // ── Ensure RodChatManager scene object exists ─────────────────────
        // Chat requires a scene NetworkBehaviour with a NetworkIdentity.
        // If the previous step preserved it, this is a no-op.
        if (Object.FindFirstObjectByType<RodChatManager>() == null)
        {
            var chatGO = new GameObject("RodChatManager");
            chatGO.AddComponent<Mirror.NetworkIdentity>();
            chatGO.AddComponent<RodChatManager>();
            Debug.Log("[HubSceneBuilder] Added RodChatManager to scene.");
        }

        // ── Main Camera ───────────────────────────────────────────────────
        // Must be tagged MainCamera so Camera.main resolves.
        // PlayerMovement.Start() finds it and adds CameraFollow automatically.
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.SetPositionAndRotation(
            new Vector3(0f, 5f, -8f),
            Quaternion.Euler(20f, 0f, 0f));
        camGO.AddComponent<Camera>();
        camGO.AddComponent<AudioListener>();

        // ── Directional light ─────────────────────────────────────────────
        var sunGO = new GameObject("Sun");
        sunGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        var sun = sunGO.AddComponent<Light>();
        sun.type      = LightType.Directional;
        sun.intensity = 1f;
        sun.shadows   = LightShadows.None;

        // ── Gray ground plane ─────────────────────────────────────────────
        // Plane primitive is 10×10 units natively; scale ×10 = 100×100.
        // Replace auto-MeshCollider with BoxCollider — more reliable edge behaviour.
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.tag  = "Ground";
        ground.transform.localScale = new Vector3(10f, 1f, 10f);
        Object.DestroyImmediate(ground.GetComponent<MeshCollider>());
        var bc    = ground.AddComponent<BoxCollider>();
        bc.center = Vector3.zero;
        bc.size   = new Vector3(1f, 0.02f, 1f);

        // ── 8 NetworkStartPosition spawn points ───────────────────────────
        for (int i = 0; i < 8; i++)
        {
            float a  = (360f / 8f * i) * Mathf.Deg2Rad;
            var   sp = new GameObject($"SpawnPoint_{i}");
            sp.transform.position = new Vector3(Mathf.Sin(a) * 4f, 0.1f, Mathf.Cos(a) * 4f);
            sp.AddComponent<Mirror.NetworkStartPosition>();
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[HubSceneBuilder] ✓ Gray plane hub ready. Ctrl+S to save.");
    }
}
#endif
