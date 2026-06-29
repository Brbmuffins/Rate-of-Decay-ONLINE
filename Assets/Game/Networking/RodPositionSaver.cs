using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

// ═══════════════════════════════════════════════════════════════════════════
//  RodPositionSaver
//  Added at runtime by RodNetworkManager to the server-side player object.
//  Saves the player's last position to the auth server DB when they disconnect.
//
//  Fires PATCH /character/position on:
//    - OnDestroy (player object cleaned up on server after disconnect)
//    - OnApplicationQuit (server shutdown)
// ═══════════════════════════════════════════════════════════════════════════

public class RodPositionSaver : MonoBehaviour
{
    [HideInInspector] public int    characterId;
    [HideInInspector] public string authServerURL;
    [HideInInspector] public string jwt;

    bool _saved;

    void OnDestroy()    => TrySave();
    void OnApplicationQuit() { TrySave(); }

    void TrySave()
    {
        if (_saved) return;
        if (characterId <= 0 || string.IsNullOrEmpty(jwt) || jwt == "dev") return;
        _saved = true;

        // Can't use coroutines after OnDestroy in some Unity versions — use static helper
        SavePosition(authServerURL, jwt, characterId, transform.position, transform.eulerAngles.y);
    }

    // Static so it can survive the MonoBehaviour being destroyed
    public static void SavePosition(string url, string jwt, int charId,
        Vector3 pos, float orientation)
    {
        // Fire-and-forget via a temporary GameObject coroutine host
        var host = new GameObject("_PositionSaveRequest");
        DontDestroyOnLoad(host);
        host.AddComponent<PositionSaveRoutine>().Run(url, jwt, charId, pos, orientation);
    }
}

// ── Temporary coroutine host ──────────────────────────────────────────────

class PositionSaveRoutine : MonoBehaviour
{
    public void Run(string url, string jwt, int charId, Vector3 pos, float orientation)
    {
        StartCoroutine(DoSave(url, jwt, charId, pos, orientation));
    }

    IEnumerator DoSave(string url, string jwt, int charId, Vector3 pos, float orientation)
    {
        // Force invariant culture so floats always serialize with a '.' decimal
        // separator. On a build whose OS locale uses ',' (much of Europe) the old
        // interpolation produced "x":1,234 — invalid JSON — and the save silently failed.
        var ic = System.Globalization.CultureInfo.InvariantCulture;
        string json = "{" +
                      $"\"x\":{pos.x.ToString("F3", ic)}," +
                      $"\"y\":{pos.y.ToString("F3", ic)}," +
                      $"\"z\":{pos.z.ToString("F3", ic)}," +
                      $"\"map\":\"GameWorld\"," +
                      $"\"orientation\":{orientation.ToString("F3", ic)}}}";

        using var req = new UnityWebRequest($"{url}/character/position", "PATCH");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", "Bearer " + jwt);
        req.timeout = 5;

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log($"[RodPositionSaver] Saved char {charId} at {pos}");
        else
            Debug.LogWarning($"[RodPositionSaver] Failed to save position: {req.error}");

        Destroy(gameObject);
    }
}
