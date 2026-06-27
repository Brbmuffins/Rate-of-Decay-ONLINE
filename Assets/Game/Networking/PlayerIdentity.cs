using Mirror;
using UnityEngine;

// ═══════════════════════════════════════════════════════════════════════════
//  PlayerIdentity
//  Add to every class prefab (Engineer, Guardian, Wraith, Medic).
//  Syncs the player's name and class to all clients.
//
//  Usage: read playerName / classIndex anywhere on the player object.
// ═══════════════════════════════════════════════════════════════════════════

public class PlayerIdentity : NetworkBehaviour
{
    [SyncVar] public string playerName  = "Player";
    [SyncVar] public int    classIndex  = 0;

    static readonly string[] ClassNames = { "Engineer", "Guardian", "Wraith", "Medic" };

    public string ClassName => classIndex >= 0 && classIndex < ClassNames.Length
        ? ClassNames[classIndex]
        : "Unknown";

    public override void OnStartLocalPlayer()
    {
        // Tag this as the local player object so other scripts can find it
        gameObject.name = playerName + " (Local)";

        // Refresh nameplate (it will hide itself for local player)
        GetComponent<PlayerNameplate>()?.Refresh();
    }

    public override void OnStartClient()
    {
        // Update display name for remote players
        if (!isLocalPlayer)
            gameObject.name = playerName;

        // Attach nameplate if not already present, then populate it.
        // SyncVars are populated before OnStartClient fires on the client,
        // so playerName and classIndex are already correct here.
        var plate = GetComponent<PlayerNameplate>();
        if (plate == null) plate = gameObject.AddComponent<PlayerNameplate>();
        plate.Refresh();

        // Notify player list so it updates immediately on join
        PlayerListUI.RequestRefresh();
    }

    public override void OnStopClient()
    {
        // Notify player list immediately on leave
        PlayerListUI.RequestRefresh();
    }
}
