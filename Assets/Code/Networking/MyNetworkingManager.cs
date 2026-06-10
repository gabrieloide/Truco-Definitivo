using System;
using System.Collections.Generic;
using System.Linq;
using Code.GameLogic;
using Code.Networking;
using Code.Player;
using Mirror;
using UnityEngine;

public class MyNetworkingManager : NetworkManager
{
    [Header("Network Settings")]
    [SerializeField] private int targetPlayerCount = 2;
    [SerializeField] private float clientsReadyTimeout = 30f;

    public override void OnStartHost()
    {
        base.OnStartHost();
        Debug.Log("[MyNetworkingManager] Host started.");
    }

    // ─────────────────────── Connection diagnostics ───────────────────────
    // Mirror disconnects a connection when a handler throws (exceptionsDisconnect),
    // when the transport reports an error, or when the link itself drops. These logs
    // plus the [NetDiag] transport logs tell which one actually happened.

    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);
        Debug.Log($"[NetDiag] t={Time.realtimeSinceStartup:F1}s — Mirror server: client connected (conn={conn.connectionId}, address={conn.address}).");
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        Debug.LogWarning($"[NetDiag] t={Time.realtimeSinceStartup:F1}s — Mirror server: client DISCONNECTED (conn={conn.connectionId}).");
        base.OnServerDisconnect(conn);
    }

    public override void OnServerError(NetworkConnectionToClient conn, TransportError error, string reason)
    {
        base.OnServerError(conn, error, reason);
        Debug.LogError($"[NetDiag] t={Time.realtimeSinceStartup:F1}s — Mirror server: transport ERROR on conn={conn.connectionId}: {error} — {reason}");
    }

    public override void OnServerTransportException(NetworkConnectionToClient conn, Exception exception)
    {
        base.OnServerTransportException(conn, exception);
        Debug.LogError($"[NetDiag] t={Time.realtimeSinceStartup:F1}s — Mirror server: EXCEPTION on conn={conn.connectionId} (this kicks the client!): {exception}");
    }

    public override void OnClientConnect()
    {
        base.OnClientConnect();
        Debug.Log($"[NetDiag] t={Time.realtimeSinceStartup:F1}s — Mirror client: connected to server.");
    }

    public override void OnClientError(TransportError error, string reason)
    {
        base.OnClientError(error, reason);
        Debug.LogError($"[NetDiag] t={Time.realtimeSinceStartup:F1}s — Mirror client: transport ERROR: {error} — {reason}");
    }

    public override void OnClientTransportException(Exception exception)
    {
        base.OnClientTransportException(exception);
        Debug.LogError($"[NetDiag] t={Time.realtimeSinceStartup:F1}s — Mirror client: EXCEPTION (this drops the connection!): {exception}");
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();

        Debug.LogWarning($"[NetDiag] t={Time.realtimeSinceStartup:F1}s — Mirror client: DISCONNECTED (check the [NetDiag] lines right above for the reason).");

        // The gameplay HUD is DontDestroyOnLoad: without this it survives the scene
        // change and keeps drawing over the main menu after an unexpected disconnect.
        var hud = PlayerHUD.Instance;
        if (hud != null) Destroy(hud.gameObject);

        // Leave the lobby too, so the player can host/join a fresh room right away.
        UnityServicesManager.Instance?.LeaveLobby();

        // Also fires when the host stops while already in the menu — only reload
        // when actually coming back from the game scene.
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MainMenu")
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        base.OnServerAddPlayer(conn);

        var identity = conn.identity;
        if (identity == null) return;

        // Add Player component if missing
        var playerComp = identity.gameObject.GetComponent<Code.Player.Player>()
                      ?? identity.gameObject.AddComponent<Code.Player.Player>();

        var playerLocal = identity.gameObject.GetComponent<PlayerLocal>();

        var netSync = identity.GetComponent<PlayerNetworkSync>();

        if (playerLocal != null)
        {
            // Fallback name; the client overwrites it via CmdSetPlayerName with its nickname
            int idx = numPlayers - 1;
            playerLocal.player.playerName = $"Jugador {idx + 1}";

            // Register to GameManager if game scene is already loaded
            if (GameManager.Instance != null)
                GameManager.Instance.AddPlayerToServer(playerLocal);

            Debug.Log($"[MyNetworkingManager] Player {idx + 1} added. conn={conn.connectionId}");
        }

        if (netSync != null)
        {
            if (string.IsNullOrEmpty(netSync.playerName))
                netSync.playerName = $"Jugador {numPlayers}";
            if (netSync.teamIndex < 0)
                netSync.teamIndex = GetBalancedTeamIndex(netSync);
        }
    }

    /// <summary>Team with fewer lobby players; ties go to team 0.</summary>
    private static int GetBalancedTeamIndex(PlayerNetworkSync newcomer)
    {
        int t0 = 0, t1 = 0;
        foreach (var sync in FindObjectsByType<PlayerNetworkSync>(FindObjectsSortMode.None))
        {
            if (sync == newcomer) continue;
            if (sync.teamIndex == 0) t0++;
            else if (sync.teamIndex == 1) t1++;
        }
        return t0 <= t1 ? 0 : 1;
    }

    public override void OnServerSceneChanged(string newSceneName)
    {
        base.OnServerSceneChanged(newSceneName);

        if (newSceneName != "GameScene") return;

        StartCoroutine(SetupMultiplayerMatch());
    }

    /// <summary>
    /// Waits for every connected client to finish loading GameScene (isReady) so that
    /// seat SyncVars and the dealing RPCs are not sent into the void, then seats all
    /// players and starts the match on the server.
    /// </summary>
    private System.Collections.IEnumerator SetupMultiplayerMatch()
    {
        float deadline = Time.time + clientsReadyTimeout;
        yield return new WaitUntil(() =>
            Time.time > deadline ||
            NetworkServer.connections.Values.All(c => c.isReady && c.identity != null));

        if (Time.time > deadline)
            Debug.LogWarning("[MyNetworkingManager] Timeout waiting for clients to be ready. Starting anyway.");

        // One extra frame so GameManager/SeatManager finish Awake/Start/RunOnlyOnce
        yield return null;

        var seatMgr = SeatManager.Instance;
        var gameMgr = GameManager.Instance;
        if (seatMgr == null || gameMgr == null)
        {
            Debug.LogError("[MyNetworkingManager] SeatManager or GameManager missing in GameScene.");
            yield break;
        }

        // Lobby team 0 takes even chairs, team 1 takes odd chairs. Exception: in a
        // 1v1 the opponent sits ACROSS the table (seat 2) instead of beside the host,
        // so GameManager must read teams from the lobby (PlayerNetworkSync.teamIndex)
        // rather than chair parity.
        bool oneVsOne = NetworkServer.connections.Values.Count(c => c.identity != null) == 2;
        int[] nextSeatByTeam = { 0, 1 };
        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn.identity == null) continue;
            var netSync = conn.identity.GetComponent<PlayerNetworkSync>();
            if (netSync == null) continue;

            int team = Mathf.Clamp(netSync.teamIndex, 0, 1);
            int seat = nextSeatByTeam[team];
            if (oneVsOne && team == 1) seat = 2; // face to face
            if (seat >= seatMgr.allChairs.Count) continue;
            nextSeatByTeam[team] = seat + 2;

            // Carry the lobby nickname into the match
            var pl = conn.identity.GetComponent<PlayerLocal>();
            if (pl != null && pl.player != null && !string.IsNullOrEmpty(netSync.playerName))
                pl.player.playerName = netSync.playerName;

            // Seat on the server; clients mirror it through the seatIndex SyncVar hook.
            seatMgr.RequestSeat(conn.identity.gameObject, seatMgr.allChairs[seat]);
            netSync.seatIndex = seat;
        }

        gameMgr.StartMultiplayerMatch();
    }

    /// <summary>
    /// Called by the lobby "Empezar" button (host only). Returns false with a
    /// user-facing message when the room can't start (needs 1v1 or 2v2).
    /// </summary>
    [Server]
    public bool StartMultiplayerGame(out string error)
    {
        error = null;

        int t0 = 0, t1 = 0;
        foreach (var conn in NetworkServer.connections.Values)
        {
            var sync = conn.identity != null ? conn.identity.GetComponent<PlayerNetworkSync>() : null;
            if (sync == null) continue;
            if (sync.teamIndex == 1) t1++;
            else t0++;
        }

        if (t0 + t1 < 2)
        {
            error = "No hay suficientes jugadores en la sala. Se necesita 1 vs 1 o 2 vs 2 para empezar.";
            return false;
        }

        if (t0 != t1)
        {
            error = $"Los equipos están desparejos ({t0} vs {t1}). Tiene que ser 1 vs 1 o 2 vs 2.";
            return false;
        }

        ServerChangeScene("GameScene");
        return true;
    }

    // ─────────────────────── Game state broadcasts ────────────────────────

    /// <summary>A ClientRpc reaches every client regardless of which object carries it,
    /// so broadcasts go through a single player object to avoid N-times duplication.</summary>
    private PlayerNetworkSync AnyPlayerSync()
    {
        foreach (var conn in NetworkServer.connections.Values)
        {
            var ns = conn.identity != null ? conn.identity.GetComponent<PlayerNetworkSync>() : null;
            if (ns != null) return ns;
        }
        return null;
    }

    /// <summary>Broadcasts score + rounds to all clients.</summary>
    public void BroadcastScores(int s1, int s2, int r1, int r2)
    {
        AnyPlayerSync()?.RpcSyncScores(s1, s2, r1, r2);
    }

    /// <summary>Shows a card on the table for all clients.</summary>
    public void BroadcastCardOnTable(int cardDbId, int cardValue, string cardSuit, int seatIndex, bool isBurned)
    {
        AnyPlayerSync()?.RpcBroadcastCardOnTable(cardDbId, cardValue, cardSuit, seatIndex, isBurned);
    }

    /// <summary>Sends the vira + deck placement to all clients.</summary>
    public void BroadcastVira(Card vira, int dealerSeatIndex)
    {
        if (vira == null) return;
        AnyPlayerSync()?.RpcSyncVira(vira.value, vira.suit, vira.dbId, dealerSeatIndex);
    }

    /// <summary>Mirrors a host HUD notification on every client.</summary>
    public void BroadcastHudEvent(string message, float duration)
    {
        AnyPlayerSync()?.RpcHudNotify(message, duration);
    }

    /// <summary>Mirrors the end-of-hand table cleanup animation on every client.</summary>
    public void BroadcastAnimateCardsToDeck()
    {
        AnyPlayerSync()?.RpcAnimateCardsToDeck();
    }

    /// <summary>Mirrors the "envido points at stake" HUD indicator on every client.</summary>
    public void BroadcastEnvidoStake(int points, bool visible)
    {
        AnyPlayerSync()?.RpcEnvidoStake(points, visible);
    }

    /// <summary>New hand: resets every client's local announcement state.</summary>
    public void BroadcastResetAnnouncements()
    {
        AnyPlayerSync()?.RpcResetAnnouncements();
    }

    /// <summary>An announcement was sung: mirrors the called-this-hand flag on clients.</summary>
    public void BroadcastAnnouncementCalled(int announceStateInt)
    {
        AnyPlayerSync()?.RpcAnnouncementCalled(announceStateInt);
    }

    /// <summary>Truco accepted: syncs owner team and level to every client.</summary>
    public void BroadcastTrucoState(int lastTrucoTeamIndex, int trucoLevel, bool trucoCalled)
    {
        AnyPlayerSync()?.RpcSyncTrucoState(lastTrucoTeamIndex, trucoLevel, trucoCalled);
    }
}
