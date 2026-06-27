using UnityEditor;

// ─────────────────────────────────────────────────────────────────────────────
//  RodProjectSettings
//  Runs automatically on every Unity compile via [InitializeOnLoad].
//  Sets project-level options that are annoying to configure manually.
// ─────────────────────────────────────────────────────────────────────────────

[InitializeOnLoad]
public static class RodProjectSettings
{
    static RodProjectSettings()
    {
        // Allow HTTP requests to the auth server (http://15.204.243.36:3000).
        // Without this Unity 6 throws "Insecure connection not allowed" and
        // blocks all UnityWebRequest calls to non-HTTPS endpoints.
        if (PlayerSettings.insecureHttpOption != InsecureHttpOption.AlwaysAllowed)
        {
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            UnityEngine.Debug.Log("[RoD] HTTP allowed in Project Settings (insecureHttpOption = AlwaysAllowed).");
        }
    }
}
