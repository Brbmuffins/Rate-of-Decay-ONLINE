using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.Networking;

// ═══════════════════════════════════════════════════════════════════════════
//  RodNetworkAuthenticator
//  Attach to the NetworkManager GameObject alongside RodNetworkManager.
//
//  Flow (production):
//    1. Client connects → sends AuthRequestMessage with JWT + username.
//    2. Server receives JWT → calls GET /character on the auth server.
//    3. Auth server returns the character record (class, saved position).
//    4. Server stores class + position on conn.authenticationData → ServerAccept.
//    5. RodNetworkManager.OnCreatePlayer spawns the right prefab at saved position.
//
//  Flow (dev mode):
//    Steps 2-3 are skipped. Class comes from PlayerPrefs, position is default.
// ═══════════════════════════════════════════════════════════════════════════

[AddComponentMenu("BCE/Network/Rod Network Authenticator")]
public class RodNetworkAuthenticator : NetworkAuthenticator
{
    [Header("Auth Server")]
    public string authServerURL = "http://15.204.243.36:3000";

    [Header("Dev Mode")]
    [Tooltip("Bypasses JWT + DB lookup. Editor-only local testing.")]
    public bool devMode = false;

    // ── Network messages ─────────────────────────────────────────────────────

    public struct AuthRequestMessage : NetworkMessage
    {
        public string jwt;
        public string username;
    }

    public struct AuthResponseMessage : NetworkMessage
    {
        public bool   success;
        public string message;
    }

    // ── Server ───────────────────────────────────────────────────────────────

    public override void OnStartServer()
    {
        NetworkServer.RegisterHandler<AuthRequestMessage>(OnAuthRequest, false);
    }

    public override void OnStopServer()
    {
        NetworkServer.UnregisterHandler<AuthRequestMessage>();
    }

    public override void OnServerAuthenticate(NetworkConnectionToClient conn)
    {
        // Wait for AuthRequestMessage — nothing to do here.
    }

    void OnAuthRequest(NetworkConnectionToClient conn, AuthRequestMessage msg)
    {
        // Auto-detect dev mode in the editor even when the Inspector checkbox is off:
        // the LoginManager's HOST (DEV) button sets jwt_token = "dev".
        // On a real Linux server build, Application.isEditor is always false so this
        // never fires in production — keeping the server auth-protected.
#if UNITY_EDITOR
        bool isDev = devMode || msg.jwt == "dev";
#else
        bool isDev = devMode;
#endif
        if (isDev)
        {
            // Dev: skip auth server, trust PlayerPrefs class selection
            conn.authenticationData = new RodPlayerAuth
            {
                username    = string.IsNullOrEmpty(msg.username) ? "DevPlayer" : msg.username,
                jwt         = "dev",
                classIndex  = 0,       // will be overridden by CreatePlayerMessage in dev mode
                characterId = -1,
                spawnX      = 0f,
                spawnY      = 2f,
                spawnZ      = 0f,
                fromDB      = false
            };
            conn.Send(new AuthResponseMessage { success = true, message = "Dev mode — accepted." });
            ServerAccept(conn);
            return;
        }

        if (string.IsNullOrEmpty(msg.jwt) || string.IsNullOrEmpty(msg.username))
        {
            Reject(conn, "Missing credentials.");
            return;
        }

        StartCoroutine(FetchCharacterAndAccept(conn, msg));
    }

    IEnumerator FetchCharacterAndAccept(NetworkConnectionToClient conn, AuthRequestMessage msg)
    {
        string url = $"{authServerURL}/character";

        using var req = UnityWebRequest.Get(url);
        req.SetRequestHeader("Authorization", "Bearer " + msg.jwt);
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = 8;

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[RodAuth] Character fetch failed for {msg.username}: {req.error}");
            Reject(conn, "Could not verify character. Please try again.");
            yield break;
        }

        CharacterResponse character = null;
        try { character = JsonUtility.FromJson<CharacterResponse>(req.downloadHandler.text); }
        catch { }

        if (character == null || character.id == 0)
        {
            // No character yet — client should have POSTed /character before connecting.
            // Reject so they go back through CharacterSelect.
            Reject(conn, "No character found. Please complete character selection.");
            yield break;
        }

        conn.authenticationData = new RodPlayerAuth
        {
            username    = msg.username,
            jwt         = msg.jwt,
            classIndex  = character.class_index,
            characterId = character.id,
            spawnX      = character.pos_x,
            spawnY      = character.pos_y,
            spawnZ      = character.pos_z,
            fromDB      = true
        };

        conn.Send(new AuthResponseMessage { success = true, message = "Authenticated." });
        ServerAccept(conn);

        Debug.Log($"[RodAuth] Accepted {msg.username} — class {character.class_index}, " +
                  $"spawn ({character.pos_x:F1}, {character.pos_y:F1}, {character.pos_z:F1})");
    }

    void Reject(NetworkConnectionToClient conn, string reason)
    {
        conn.Send(new AuthResponseMessage { success = false, message = reason });
        StartCoroutine(DelayedReject(conn, 1f));
    }

    IEnumerator DelayedReject(NetworkConnectionToClient conn, float delay)
    {
        yield return new WaitForSeconds(delay);
        ServerReject(conn);
    }

    // ── Client ───────────────────────────────────────────────────────────────

    public override void OnStartClient()
    {
        NetworkClient.RegisterHandler<AuthResponseMessage>(OnAuthResponse, false);
    }

    public override void OnStopClient()
    {
        NetworkClient.UnregisterHandler<AuthResponseMessage>();
    }

    public override void OnClientAuthenticate()
    {
        // Class is no longer sent here — server fetches it from DB.
        // Only JWT and username are needed for identity.
        NetworkClient.Send(new AuthRequestMessage
        {
            jwt      = devMode ? "dev" : PlayerPrefs.GetString("jwt_token", ""),
            username = PlayerPrefs.GetString("username", "DevPlayer"),
        });
    }

    void OnAuthResponse(AuthResponseMessage msg)
    {
        if (msg.success) ClientAccept();
        else
        {
            Debug.LogError("[RodAuth] Rejected: " + msg.message);
            ClientReject();
        }
    }

    // ── JSON response shape ───────────────────────────────────────────────────

    [System.Serializable]
    class CharacterResponse
    {
        public int    id;
        public int    class_index;
        public string class_name;
        public float  pos_x;
        public float  pos_y;
        public float  pos_z;
    }
}

// ── Auth data stored on each server connection ────────────────────────────
// RodNetworkManager reads this to spawn the right prefab at the right position.

public class RodPlayerAuth
{
    public string username;
    public string jwt;
    public int    classIndex;
    public int    characterId;  // DB row id — used when saving position on disconnect
    public float  spawnX, spawnY, spawnZ;
    public bool   fromDB;       // false in dev mode — falls back to CreatePlayerMessage class
}
