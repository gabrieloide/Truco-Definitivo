using System.Collections.Generic;
using Code.Cards;
using Code.GameLogic;
using Code.Player;
using Mirror;
using UnityEngine;

namespace Code.Networking
{
    /// <summary>
    /// NetworkBehaviour companion to PlayerLocal.
    /// Must be on the same prefab as PlayerLocal.
    /// Handles Commands (client→server) and TargetRpcs/ClientRpcs (server→client).
    /// </summary>
    public class PlayerNetworkSync : NetworkBehaviour
    {
        private PlayerLocal _playerLocal;
        private CardsHandler _cardsHandler;

        // Seat assignment is state (SyncVar), not an event: late-joining or still-loading
        // clients receive it with the spawn payload instead of missing a dropped RPC.
        [SyncVar(hook = nameof(OnSeatIndexChanged))]
        public int seatIndex = -1;

        // Lobby identity: nickname chosen in the menu and team slot (0 or 1).
        // SyncVars so every client can render the lobby roster.
        [SyncVar(hook = nameof(OnPlayerNameChanged))]
        public string playerName = "";

        [SyncVar]
        public int teamIndex = -1;

        private void Awake()
        {
            _playerLocal  = GetComponent<PlayerLocal>();
            _cardsHandler = GetComponent<CardsHandler>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (seatIndex >= 0) ApplySeatOnClient(seatIndex);
        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            string nick = UnityServicesManager.Instance != null ? UnityServicesManager.Instance.PlayerName : null;
            if (!string.IsNullOrWhiteSpace(nick)) CmdSetPlayerName(nick);
        }

        private void OnPlayerNameChanged(string _, string newName) => ApplyPlayerName(newName);

        private void ApplyPlayerName(string newName)
        {
            if (_playerLocal != null && _playerLocal.player != null && !string.IsNullOrEmpty(newName))
                _playerLocal.player.playerName = newName;
        }

        [Command]
        public void CmdSetPlayerName(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return;
            playerName = newName.Trim();
            ApplyPlayerName(playerName);
        }

        /// <summary>Client→server: move this player to the opposite lobby team if it has room.</summary>
        [Command]
        public void CmdSwitchTeam()
        {
            int target = teamIndex == 0 ? 1 : 0;
            int occupied = 0;
            foreach (var sync in FindObjectsByType<PlayerNetworkSync>(FindObjectsSortMode.None))
                if (sync != this && sync.teamIndex == target)
                    occupied++;

            if (occupied >= 2) return; // target team is full
            teamIndex = target;
        }

        private void OnSeatIndexChanged(int _, int newSeat)
        {
            if (newSeat >= 0) ApplySeatOnClient(newSeat);
        }

        private void ApplySeatOnClient(int idx)
        {
            // Host already seated this player through the server path.
            if (NetworkServer.active) return;
            StartCoroutine(ApplySeatWhenReady(idx));
        }

        private System.Collections.IEnumerator ApplySeatWhenReady(int idx)
        {
            // SeatManager/chairs may not exist yet right after scene load.
            float deadline = Time.time + 15f;
            while (Time.time < deadline)
            {
                var seatMgr = SeatManager.Instance;
                if (seatMgr != null && idx < seatMgr.allChairs.Count && seatMgr.allChairs.Count > 0)
                {
                    if (seatMgr.allChairs[idx].occupant != gameObject)
                        seatMgr.RequestSeat(gameObject, seatMgr.allChairs[idx]);
                    yield break;
                }
                yield return null;
            }
            Debug.LogWarning($"[PlayerNetworkSync] Could not apply seat {idx}: SeatManager/chairs never became available.");
        }

        // ──────────────────────── SERVER → all clients ────────────────────

        [ClientRpc]
        public void RpcSetTurn(bool canPlay)
        {
            if (_playerLocal == null || _playerLocal.player == null) return;
            _playerLocal.player.canPlayCard = canPlay;

            // Pure clients never receive GameManager.OnTurnStarted (server-only event),
            // so the turn label and HUD action buttons (Envido/Truco/Flor) are driven
            // from here. UpdateTurnState hides the label when it is not your turn.
            if (!NetworkServer.active && isLocalPlayer)
                PlayerHUD.Instance?.UpdateTurnState(canPlay);
        }

        [ClientRpc]
        public void RpcSyncScores(int s1, int s2, int r1, int r2)
        {
            if (NetworkServer.active) return; // host already applied them
            if (GameManager.Instance == null || GameManager.Instance.teams.Count < 2) return;
            GameManager.Instance.teams[0].teamScore = s1;
            GameManager.Instance.teams[1].teamScore = s2;
            GameManager.Instance.teams[0].roundsWon = r1;
            GameManager.Instance.teams[1].roundsWon = r2;
            PlayerHUD.Instance?.UpdateScore(s1, s2, r1, r2);
        }

        [ClientRpc]
        public void RpcBroadcastCardOnTable(int cardDbId, int cardValue, string cardSuit, int seatIndex, bool isBurned)
        {
            if (NetworkServer.active) return;
            if (TableManager.Instance == null || SeatManager.Instance == null) return;
            if (seatIndex < 0 || seatIndex >= SeatManager.Instance.allChairs.Count) return;
            // Burned cards arrive scrubbed (value 0, no suit): SpawnCard3D lays them
            // face-down and shows the card back.
            var card = new Card { dbId = cardDbId, value = cardValue, suit = cardSuit, isBurned = isBurned };
            var chair = SeatManager.Instance.allChairs[seatIndex];
            if (chair?.occupant != null)
            {
                TableManager.Instance.SpawnCard3D(card, chair.occupant);

                // A remote player's placeholder hand loses one card back; the local
                // player's own card was already hidden by PlayCardToTable.
                var occupantLocal = chair.occupant.GetComponent<PlayerLocal>();
                if (occupantLocal == null || !occupantLocal.isLocalPlayer)
                    chair.occupant.GetComponent<CardsHandler>()?.RemoveRenderedCard(cardDbId, fallbackToLast: true);
            }
        }

        /// <summary>Server→clients: vira card + deck position so pure clients can render them
        /// and compute their local envido score.</summary>
        [ClientRpc]
        public void RpcSyncVira(int value, string suit, int dbId, int dealerSeatIndex)
        {
            if (NetworkServer.active) return;
            var vira = new Card { value = value, suit = suit, dbId = dbId };
            vira.realValue = TrucoRules.GetCardRealValue(vira, vira);

            if (DeckCreator.Instance != null)
                DeckCreator.Instance.cardVira = vira;

            if (TableManager.Instance != null)
            {
                TableManager.Instance.SpawnDeck3D(dealerSeatIndex);
                TableManager.Instance.SpawnVira3D(vira, dealerSeatIndex);
            }

            // Con mano y vira conocidas, el cliente ya puede evaluar su propia Flor —
            // el server solo lo hace en su máquina, así que haveFlower quedaba en false
            // acá y nunca aparecían las opciones de Flor.
            Object.FindAnyObjectByType<Code.GameLogic.Announcement.FlorAnnouncement>()?.CanDeclareFlower();
        }

        /// <summary>Server→clients: clear this player's rendered hand before a new deal.</summary>
        [ClientRpc]
        public void RpcClearHand()
        {
            if (NetworkServer.active) return; // the server already cleared the authoritative hand
            _cardsHandler?.ClearCards();
        }

        /// <summary>Server→clients: this player was dealt a card. Remote viewers render a
        /// hidden placeholder (value 0, no suit) so opponents' hands are visible as card
        /// backs without leaking their values. The owner gets the real hand via TargetRpc.</summary>
        [ClientRpc]
        public void RpcDealHiddenCard()
        {
            if (NetworkServer.active) return; // host renders the real cards server-side
            if (isLocalPlayer) return;        // own hand arrives via TargetReceiveHand
            _cardsHandler?.ReceiveSingleCard(new Card { value = 0, suit = "", dbId = -1 });
        }

        /// <summary>Server→clients: an announcement was sung — mirror the "called this
        /// hand" flag so HUD buttons hide (e.g. envido is unavailable once flor is sung).</summary>
        [ClientRpc]
        public void RpcAnnouncementCalled(int announceStateInt)
        {
            if (NetworkServer.active) return; // the server already marked it
            var manager = Object.FindAnyObjectByType<AnnouncementManager>();
            manager?.MarkAnnouncementCalled((AnnounceState)announceStateInt);
        }

        /// <summary>Server→this client only: HUD notification (e.g. a rejected action).</summary>
        [TargetRpc]
        public void TargetHudNotify(NetworkConnectionToClient target, string message, float duration)
        {
            PlayerHUD.Instance?.NotifyEventLocal(message, duration);
        }

        /// <summary>Server→clients: new hand started — reset local announcement state
        /// (called-this-hand flags, truco level) so HUD buttons reappear.</summary>
        [ClientRpc]
        public void RpcResetAnnouncements()
        {
            if (NetworkServer.active) return; // the server already ran ResetState
            if (GameManager.Instance != null) GameManager.Instance.lastTrucoTeamIndex = 0;
            Object.FindAnyObjectByType<AnnouncementManager>()?.ResetState();
        }

        /// <summary>Server→clients: a truco was accepted — sync owner team and level.</summary>
        [ClientRpc]
        public void RpcSyncTrucoState(int lastTrucoTeamIndex, int trucoLevel, bool trucoCalled)
        {
            if (NetworkServer.active) return;
            if (GameManager.Instance != null) GameManager.Instance.lastTrucoTeamIndex = lastTrucoTeamIndex;
            Object.FindAnyObjectByType<AnnouncementManager>()?.ApplyTrucoStateFromServer(trucoCalled, trucoLevel);
        }

        /// <summary>Server→clients: update the "envido points at stake" HUD indicator.</summary>
        [ClientRpc]
        public void RpcEnvidoStake(int points, bool visible)
        {
            if (NetworkServer.active) return; // host updates its HUD directly
            PlayerHUD.Instance?.ShowEnvidoStake(visible, points);
        }

        /// <summary>Server→clients: end of hand — sweep table cards and vira into the deck.</summary>
        [ClientRpc]
        public void RpcAnimateCardsToDeck()
        {
            if (NetworkServer.active) return; // the host runs the original animation
            TableManager.Instance?.AnimateCardsToDeck();
        }

        /// <summary>Server→clients: mirror of host-side PlayerHUD.NotifyEvent.</summary>
        [ClientRpc]
        public void RpcHudNotify(string message, float duration)
        {
            if (NetworkServer.active) return; // host already displayed it
            PlayerHUD.Instance?.NotifyEventLocal(message, duration);
        }

        // ──────────────────────── SERVER → this client only ───────────────

        [TargetRpc]
        public void TargetReceiveHand(NetworkConnectionToClient target, List<CardNetData> cards)
        {
            if (NetworkServer.active) return; // host hand is dealt by the server loop itself
            if (_cardsHandler == null) return;
            _cardsHandler.ClearCards();
            foreach (var nd in cards)
            {
                var card = nd.ToCard();
                _cardsHandler.ReceiveSingleCard(card);
            }
        }

        [TargetRpc]
        public void TargetSyncPlayerInfo(NetworkConnectionToClient target, string playerName, int teamIndex)
        {
            if (_playerLocal == null || _playerLocal.player == null) return;
            _playerLocal.player.playerName = playerName;
            if (GameManager.Instance != null && teamIndex >= 0 && teamIndex < GameManager.Instance.teams.Count)
                _playerLocal.player.team = GameManager.Instance.teams[teamIndex];
        }

        /// <summary>Server→this client: an opponent announced; show the response UI.</summary>
        [TargetRpc]
        public void TargetShowResponseButtons(NetworkConnectionToClient target, int announceStateInt, string announcerName)
        {
            var manager = Object.FindAnyObjectByType<AnnouncementManager>();
            if (manager == null) return;
            manager.ShowResponseButtonsFromServer((AnnounceState)announceStateInt, announcerName);
        }

        // ──────────────────────── CLIENT → server ─────────────────────────

        [Command]
        public void CmdPlayCard(int cardDbId, int cardValue, string cardSuit, bool isBurned)
        {
            if (!isServer) return;

            var playerComp = GetComponent<Code.Player.Player>();
            if (playerComp == null || !playerComp.canPlayCard) return;

            // Find the card in this player's dealt hand
            Card card = null;
            if (_cardsHandler != null)
            {
                foreach (var c in _cardsHandler.InitialHand)
                {
                    if (c.dbId == cardDbId) { card = c; break; }
                }
            }

            if (card == null)
                card = new Card { dbId = cardDbId, value = cardValue, suit = cardSuit };

            card.isBurned = isBurned;
            TableManager.Instance?.PlaceCard(card, gameObject);
        }

        [Command]
        public void CmdSendAnnouncement(int announceStateInt)
        {
            if (!isServer) return;
            var manager = Object.FindAnyObjectByType<AnnouncementManager>();
            if (manager == null) return;

            var state = (AnnounceState)announceStateInt;
            manager.ReceiveAnnounceFromHumanPlayer(state, gameObject);
        }

        /// <summary>Client→server: response to an announcement (Quiero / No quiero / re-raise).
        /// extraStones: slider value when re-raising envido (ignored otherwise).</summary>
        [Command]
        public void CmdRespondAnnouncement(string buttonName, int extraStones)
        {
            if (!isServer) return;
            var manager = Object.FindAnyObjectByType<AnnouncementManager>();
            if (manager == null) return;
            manager.ReceiveResponseFromHumanPlayer(buttonName, gameObject, Mathf.Max(0, extraStones));
        }
    }

    /// <summary>
    /// Serializable card representation for TargetRpc (Card has non-serializable fields).
    /// </summary>
    public struct CardNetData
    {
        public int    DbId;
        public int    Value;
        public string Suit;

        public Card ToCard() => new Card { dbId = DbId, value = Value, suit = Suit };

        public static CardNetData From(Card c) => new CardNetData { DbId = c.dbId, Value = c.value, Suit = c.suit };
    }
}
