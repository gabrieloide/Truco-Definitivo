using System;
using System.Collections.Generic;
using System.Linq;
using Code.GameLogic;
using Code.Networking;
using Code.Player;
using Code.Persistence;
using Mirror;
using UnityEngine;

public class MyNetworkingManager : NetworkManager
{
    [Header("Network Settings")]
    [SerializeField] private int targetPlayerCount = 2;
    [SerializeField] private float clientsReadyTimeout = 30f;

    // ─────────────────────── Reconnection Management ──────────────────────
    private readonly Dictionary<string, DisconnectedPlayerSession> _pendingReconnections = new Dictionary<string, DisconnectedPlayerSession>();
    private readonly Dictionary<int, DisconnectedPlayerSession> _pendingBySeat = new Dictionary<int, DisconnectedPlayerSession>();

    public override void OnStartHost()
    {
        base.OnStartHost();
        ClearPendingReconnections();
        // Each room starts with the default team names (the manager is DDOL,
        // so names from a previous room would leak into the new one).
        _lobbyTeamNames[0] = "EQUIPO 1";
        _lobbyTeamNames[1] = "EQUIPO 2";
        Debug.Log("[MyNetworkingManager] Host started.");
    }

    public override void OnStopServer()
    {
        ClearPendingReconnections();
        base.OnStopServer();
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

        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "GameScene")
        {
            CaptureDisconnectedPlayerSession(conn);
        }

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

        // DebugCommands is DontDestroyOnLoad too — it must not leak into the menu.
        var debugCommands = FindAnyObjectByType<DebugCommands>();
        if (debugCommands != null) Destroy(debugCommands.gameObject);

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

        // The newcomer needs the current team names (it missed earlier renames).
        BroadcastTeamNames(_lobbyTeamNames[0], _lobbyTeamNames[1]);
    }

    /// <summary>Host only: swaps the two lobby players shown on the same row (one per
    /// team). Row order matches the lobby UI: players sorted by netId (join order)
    /// within each team. With only one player on the row, that player just moves to
    /// the other team. This replaces the old self-service team switch, which couldn't
    /// work with a full 2v2 room (both teams full = nobody could move).</summary>
    [Server]
    public void SwapLobbyRow(int row)
    {
        if (row < 0 || row > 1) return;

        var team0 = new List<PlayerNetworkSync>();
        var team1 = new List<PlayerNetworkSync>();
        foreach (var sync in FindObjectsByType<PlayerNetworkSync>(FindObjectsSortMode.None).OrderBy(s => s.netId))
        {
            if (sync.teamIndex == 1) team1.Add(sync);
            else team0.Add(sync);
        }

        var left  = row < team0.Count ? team0[row] : null;
        var right = row < team1.Count ? team1[row] : null;
        if (left == null && right == null) return;

        if (left  != null) left.teamIndex  = 1;
        if (right != null) right.teamIndex = 0;
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

        ApplyTeamNamesForMatch(oneVsOne);

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

    /// <summary>Baza actual (0/1/2) a todos los clientes: el HUD la usa para el
    /// "solo se canta en la primera". Sin esto el cliente creía estar siempre en la
    /// primera baza y dejaba cantar Envido/Flor en la segunda y la tercera.</summary>
    public void BroadcastRound(int round)
    {
        AnyPlayerSync()?.RpcSyncRound(round);
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

    /// <summary>An announcement is pending: mirrors who is currently thinking/responding to all clients.</summary>
    public void BroadcastWaitingResponse(int responderSeat, string responderName)
    {
        AnyPlayerSync()?.RpcSyncWaitingResponse(responderSeat, responderName);
    }

    /// <summary>Match over: every pure client shows the rematch/exit modal.</summary>
    public void BroadcastMatchEnded(string winnerText)
    {
        AnyPlayerSync()?.RpcMatchEnded(winnerText);
    }

    /// <summary>Host only: reloads GameScene for everyone. The scene objects
    /// (GameManager, SeatManager, deck) come back fresh — scores reset — while the
    /// Mirror player objects persist and get re-seated by SetupMultiplayerMatch.</summary>
    [Server]
    public void RestartMatch()
    {
        // Si el rival ya abandonó, no hay revancha posible.
        int connected = NetworkServer.connections.Values.Count(c => c.identity != null);
        if (connected < 2)
        {
            PlayerHUD.Instance?.NotifyEvent("NO HAY RIVALES PARA LA REVANCHA", 4f);
            return;
        }

        ServerChangeScene("GameScene");
    }

    // ─────────────────────── Team names ───────────────────────────────────

    /// <summary>Lobby team names, editable from the lobby (2v2). Server-authoritative;
    /// in 1v1 the players' nicknames override them when the match starts.</summary>
    private readonly string[] _lobbyTeamNames = { "EQUIPO 1", "EQUIPO 2" };

    /// <summary>Server: validates and stores a lobby team rename, then mirrors it.</summary>
    [Server]
    public void SetTeamName(int teamIdx, string name)
    {
        if (teamIdx < 0 || teamIdx > 1) return;

        name = string.IsNullOrWhiteSpace(name) ? $"EQUIPO {teamIdx + 1}" : name.Trim();
        if (name.Length > 16) name = name.Substring(0, 16);

        _lobbyTeamNames[teamIdx] = name;
        BroadcastTeamNames(_lobbyTeamNames[0], _lobbyTeamNames[1]);
    }

    public void BroadcastTeamNames(string team1, string team2)
    {
        AnyPlayerSync()?.RpcSyncTeamNames(team1, team2);
    }

    /// <summary>Server: resolves the names used during the match (player nicknames in
    /// 1v1, lobby names otherwise), applies them to GameManager and mirrors them.</summary>
    private void ApplyTeamNamesForMatch(bool oneVsOne)
    {
        string[] names = { _lobbyTeamNames[0], _lobbyTeamNames[1] };

        if (oneVsOne)
        {
            foreach (var conn in NetworkServer.connections.Values)
            {
                var sync = conn.identity != null ? conn.identity.GetComponent<PlayerNetworkSync>() : null;
                if (sync == null || string.IsNullOrWhiteSpace(sync.playerName)) continue;
                names[Mathf.Clamp(sync.teamIndex, 0, 1)] = sync.playerName;
            }
        }

        var gameMgr = GameManager.Instance;
        if (gameMgr != null && gameMgr.teams.Count >= 2)
        {
            gameMgr.teams[0].teamName = names[0];
            gameMgr.teams[1].teamName = names[1];
        }

        BroadcastTeamNames(names[0], names[1]);
        PlayerHUD.Instance?.RefreshTeamLabel();
    }

    // ─────────────────────── Reconnection Handlers ────────────────────────

    public void ClearPendingReconnections()
    {
        foreach (var session in _pendingReconnections.Values)
        {
            if (session.timeoutCoroutine != null)
                StopCoroutine(session.timeoutCoroutine);
        }
        _pendingReconnections.Clear();
        _pendingBySeat.Clear();
    }

    private void CaptureDisconnectedPlayerSession(NetworkConnectionToClient conn)
    {
        if (conn == null || conn.identity == null) return;

        var netSync = conn.identity.GetComponent<PlayerNetworkSync>();
        var cardsHandler = conn.identity.GetComponent<Code.Cards.CardsHandler>();
        if (netSync == null || netSync.seatIndex < 0) return;

        string idKey = !string.IsNullOrEmpty(netSync.playerId) ? netSync.playerId : netSync.playerName;
        if (string.IsNullOrEmpty(idKey)) idKey = $"Seat_{netSync.seatIndex}";

        var session = new DisconnectedPlayerSession
        {
            playerId = idKey,
            playerName = netSync.playerName,
            seatIndex = netSync.seatIndex,
            teamIndex = netSync.teamIndex,
            cardsInHand = cardsHandler != null ? cardsHandler.GetCurrentCardsInHand() : new List<Card>(),
            disconnectTime = Time.time
        };

        if (_pendingBySeat.TryGetValue(session.seatIndex, out var existing) && existing.timeoutCoroutine != null)
        {
            StopCoroutine(existing.timeoutCoroutine);
        }

        _pendingReconnections[session.playerId] = session;
        _pendingBySeat[session.seatIndex] = session;

        string discMsg = $"¡{session.playerName.ToUpper()} SE HA DESCONECTADO! ESPERANDO RECONEXIÓN (60s)...";
        if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent(discMsg, 5f);
        BroadcastHudEvent(discMsg, 5f);

        session.timeoutCoroutine = StartCoroutine(ReconnectionTimeoutRoutine(session, 60f));
    }

    private System.Collections.IEnumerator ReconnectionTimeoutRoutine(DisconnectedPlayerSession session, float waitSeconds)
    {
        yield return new WaitForSeconds(waitSeconds);

        if (_pendingBySeat.ContainsKey(session.seatIndex))
        {
            _pendingBySeat.Remove(session.seatIndex);
            _pendingReconnections.Remove(session.playerId);

            string expiredMsg = $"TIEMPO DE ESPERA AGOTADO: {session.playerName.ToUpper()} HA ABANDONADO.";
            if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent(expiredMsg, 5f);
            BroadcastHudEvent(expiredMsg, 5f);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.HandlePlayerAbandoned(session.seatIndex, session.teamIndex);
            }
        }
    }

    public void CheckAndHandleReconnectingPlayer(NetworkConnectionToClient conn, PlayerNetworkSync netSync, string pId, string playerName)
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "GameScene") return;

        DisconnectedPlayerSession session = null;
        if (!string.IsNullOrEmpty(pId) && _pendingReconnections.TryGetValue(pId, out var s1))
        {
            session = s1;
        }
        else if (!string.IsNullOrEmpty(playerName))
        {
            foreach (var kvp in _pendingReconnections)
            {
                if (string.Equals(kvp.Value.playerName, playerName, StringComparison.OrdinalIgnoreCase))
                {
                    session = kvp.Value;
                    break;
                }
            }
        }

        if (session == null && _pendingBySeat.Count > 0)
        {
            session = _pendingBySeat.Values.FirstOrDefault();
        }

        if (session == null)
        {
            Debug.Log($"[MyNetworkingManager] Cliente {playerName} ({pId}) conectado en GameScene pero sin sesión pendiente de reconexión.");
            return;
        }

        Debug.Log($"[MyNetworkingManager] ¡RECONEXIÓN EXITOSA! Restaurando a {session.playerName} en silla {session.seatIndex} (Equipo {session.teamIndex}).");

        if (session.timeoutCoroutine != null)
        {
            StopCoroutine(session.timeoutCoroutine);
        }

        _pendingReconnections.Remove(session.playerId);
        _pendingBySeat.Remove(session.seatIndex);

        var seatMgr = SeatManager.Instance;
        var gameMgr = GameManager.Instance;
        if (seatMgr == null || gameMgr == null) return;

        // 1. Restaurar asiento y SyncVars
        netSync.seatIndex = session.seatIndex;
        netSync.teamIndex = session.teamIndex;
        netSync.playerName = session.playerName;
        netSync.playerId = session.playerId;

        if (session.seatIndex >= 0 && session.seatIndex < seatMgr.allChairs.Count)
        {
            var chair = seatMgr.allChairs[session.seatIndex];
            chair.occupant = conn.identity.gameObject;
            chair.isOccupied = true;
            seatMgr.RequestSeat(conn.identity.gameObject, chair);
        }

        // 2. Restaurar cartas en el servidor
        var cardsHandler = conn.identity.GetComponent<Code.Cards.CardsHandler>();
        if (cardsHandler != null)
        {
            cardsHandler.ClearCards();
            foreach (var c in session.cardsInHand)
            {
                cardsHandler.ReceiveSingleCard(c);
            }
        }

        // 3. Re-añadir al GameManager
        var playerLocal = conn.identity.GetComponent<PlayerLocal>();
        if (playerLocal != null)
        {
            if (playerLocal.player != null)
            {
                playerLocal.player.playerName = session.playerName;
                if (session.teamIndex >= 0 && session.teamIndex < gameMgr.teams.Count)
                    playerLocal.player.team = gameMgr.teams[session.teamIndex];
            }
            gameMgr.AddPlayerToServer(playerLocal);
        }

        // 4. Enviar mano al cliente reconectado
        var cardDataList = session.cardsInHand.Select(c => CardNetData.From(c)).ToList();
        netSync.TargetReceiveHand(conn, cardDataList);

        for (int i = 0; i < session.cardsInHand.Count; i++)
        {
            netSync.RpcDealHiddenCard();
        }

        // 5. Sincronizar estado completo
        int s1Score = gameMgr.teams.Count > 0 ? gameMgr.teams[0].teamScore : 0;
        int s2Score = gameMgr.teams.Count > 1 ? gameMgr.teams[1].teamScore : 0;
        int r1Won = gameMgr.teams.Count > 0 ? gameMgr.teams[0].roundsWon : 0;
        int r2Won = gameMgr.teams.Count > 1 ? gameMgr.teams[1].roundsWon : 0;

        var vira = DeckCreator.Instance != null ? DeckCreator.Instance.cardVira : null;
        int viraVal = vira != null ? vira.value : 0;
        string viraS = vira != null ? vira.suit : "";
        int viraDb = vira != null ? vira.dbId : -1;

        netSync.TargetSyncReconnectedState(
            conn,
            gameMgr.round,
            s1Score,
            s2Score,
            r1Won,
            r2Won,
            gameMgr.currentPlayerTurn,
            viraVal,
            viraS,
            viraDb,
            gameMgr.dealerIndex
        );

        // 6. Sincronizar cartas en mesa si hay en esta baza
        if (TableManager.Instance != null)
        {
            foreach (var tableCard in TableManager.Instance.CardsInTable)
            {
                int cardSeat = -1;
                GameObject cardPlayerObj = tableCard.ownerObj ?? (tableCard.cardOwner != null ? tableCard.cardOwner.gameObject : null);
                if (cardPlayerObj != null)
                    cardSeat = seatMgr.GetPlayerSeatIndex(cardPlayerObj);

                if (cardSeat >= 0)
                {
                    netSync.TargetSyncCardOnTable(conn, tableCard.dbId, tableCard.value, tableCard.suit, cardSeat, tableCard.isBurned);
                }
            }
        }

        // 7. Notificar a todos
        string reconMsg = $"¡{session.playerName.ToUpper()} SE HA RECONECTADO A LA PARTIDA!";
        if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent(reconMsg, 4f);
        BroadcastHudEvent(reconMsg, 4f);
    }
}
