using System;
using System.Collections.Generic;
using System.Linq;
using Code.GameLogic;
using Code.GameLogic.Announcement;
// using Mirror;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;

public enum AnnounceState
{
    Envido,
    Truco,
    Flor,
    ALey,
    None
}

namespace Code.Player
{
    public class AnnouncementManager : MonoBehaviour
    {
        [SerializeField] private GameObject _allAnnouncement;

        private readonly string[] _announcement =
            { "Envido Announcement", "Truco Announcement", "Flor Announcement", "A Ley Announcement" };

        private readonly Dictionary<string, Type> _announceComponents = new()
        {
            { "Envido Announcement", typeof(EnvidoAnnouncement) },
            { "Truco Announcement", typeof(TrucoAnnouncement) },
            { "Flor Announcement", typeof(FlorAnnouncement) },
            { "A Ley Announcement", typeof(ALeyAnnouncement) }
        };

        /*[SyncVar]*/ public AnnounceState _announceState = AnnounceState.None;
        public GameObject[] respondButtons;
        public string[] announcementPlayerNames = new string[2];
        private Dictionary<AnnounceState, bool> _announcementsCalledThisHand = new Dictionary<AnnounceState, bool>();

        [Header("UI Buttons (Legacy - Deprecated)")]
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button declineButton;
        [SerializeField] private Button moreAnnounceButton;

        private void Start()
        {
            InitializeAnnouncementsCalled();
            ResetState();
            respondButtons = new GameObject[3];
            foreach (var t in _announcement)
            {
                var announce = Instantiate(_allAnnouncement, transform);
                announce.name = t;
                announce.AddComponent(_announceComponents[t]);
            }

            InitializeRespondButtons();
        }

        private void InitializeAnnouncementsCalled()
        {
            _announcementsCalledThisHand[AnnounceState.Envido] = false;
            _announcementsCalledThisHand[AnnounceState.Truco] = false;
            _announcementsCalledThisHand[AnnounceState.Flor] = false;
            _announcementsCalledThisHand[AnnounceState.ALey] = false;
        }

        private void InitializeRespondButtons()
        {
            if (acceptButton != null)
            {
                respondButtons[0] = acceptButton.gameObject;
                acceptButton.onClick.AddListener(() => LocalButtonInteract("AcceptButton"));
                acceptButton.gameObject.SetActive(false);
            }

            if (declineButton != null)
            {
                respondButtons[1] = declineButton.gameObject;
                declineButton.onClick.AddListener(() => LocalButtonInteract("DeclineButton"));
                declineButton.gameObject.SetActive(false);
            }

            if (moreAnnounceButton != null)
            {
                respondButtons[2] = moreAnnounceButton.gameObject;
                moreAnnounceButton.onClick.AddListener(() => LocalButtonInteract("MoreAnnounceButton"));
                moreAnnounceButton.gameObject.SetActive(false);
            }
        }

        public void ResetState()
        {
            _announceState = AnnounceState.None;
            if (GameManager.Instance != null) GameManager.Instance.isAnnouncementPending = false;
            if (PlayerHUD.Instance != null) PlayerHUD.Instance.ShowResponseButtons(false);
            
            InitializeAnnouncementsCalled();
        }

        public bool WasAnnouncementCalledThisHand(AnnounceState state)
        {
            if (_announcementsCalledThisHand.TryGetValue(state, out bool called)) return called;
            return false;
        }

        private void GetOpponentPlayer(out GameObject opponentPlayer)
        {
            var playerLocal = FindAnyObjectByType<PlayerLocal>();
            if (playerLocal == null || playerLocal.player == null)
            {
                opponentPlayer = null;
                return;
            }

            var myTeam = playerLocal.player.team;
            var allNpcs = FindObjectsByType<NPCPlayer>(FindObjectsSortMode.None);

            foreach (var npc in allNpcs)
            {
                if (npc.team != myTeam)
                {
                    opponentPlayer = npc.gameObject;
                    return;
                }
            }

            opponentPlayer = null;
        }

        public void SendAnnounceToClient(string ButtonName)
        {
            if (GameManager.Instance != null && GameManager.Instance.isAnnouncementPending) return;

            var playerLocal = FindAnyObjectByType<PlayerLocal>();
            
            // --- VALIDACIÓN DE TURNO ---
            // Solo puedes cantar si es tu turno o si se está respondiendo a un canto previo (isAnnouncementPending ya manejado arriba)
            if (playerLocal != null && !playerLocal.player.canPlayCard)
            {
                Debug.LogWarning("[AnnouncementManager] No puedes cantar fuera de tu turno.");
                if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent("NO ES TU TURNO", 1.5f);
                return;
            }

            AnnounceState targetState = ButtonName switch
            {
                "EnvidoButton" => AnnounceState.Envido,
                "TrucoButton" => AnnounceState.Truco,
                "FlorButton" => AnnounceState.Flor,
                "ALeyButton" => AnnounceState.ALey,
                _ => AnnounceState.None
            };

            // Validación de propiedad de Truco: No puedes cantar Truco si tu equipo cantó el anterior aceptado
            if (targetState == AnnounceState.Truco)
            {
                int myTeamIndex = (playerLocal != null && playerLocal.player != null && playerLocal.player.team != null) 
                    ? (GameManager.Instance.teams.IndexOf(playerLocal.player.team) + 1) : 0;

                if (GameManager.Instance.lastTrucoTeamIndex != 0 && GameManager.Instance.lastTrucoTeamIndex == myTeamIndex)
                {
                    Debug.LogWarning("[AnnouncementManager] Tu equipo ya tiene el Truco. No puedes re-cantar.");
                    if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent("TU EQUIPO TIENE EL TRUCO", 2f);
                    return;
                }
            }

            if (targetState != AnnounceState.None && WasAnnouncementCalledThisHand(targetState)) 
            {
                Debug.LogWarning($"[AnnouncementManager] El canto {targetState} ya se realizó en esta mano.");
                return;
            }

            _announceState = targetState;
            if (targetState != AnnounceState.None) _announcementsCalledThisHand[targetState] = true;

            string teamName = "TU EQUIPO";
            if (playerLocal != null && playerLocal.player != null && playerLocal.player.team != null)
                teamName = playerLocal.player.team.teamName;

            Debug.Log($"[Canto] El jugador canta {targetState}");
            if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent($"¡{teamName.ToUpper()} CANTA {targetState.ToString().ToUpper()}!");
            
            GameManager.Instance.isAnnouncementPending = true;

            // Al cantar, bloqueamos el permiso de jugar carta para evitar que juegue mientras se procesa el canto
            if (playerLocal != null) playerLocal.player.canPlayCard = false;

            GetOpponentPlayer(out var opponent);
            if (opponent != null)
            {
                var npc = opponent.GetComponent<NPCPlayer>();
                if (npc != null) npc.HandleOpponentAnnounce(_announceState, gameObject);
            }
            
            RpcAnnounceToAllClients();
            if (PlayerHUD.Instance != null) PlayerHUD.Instance.RefreshActionButtons(true);
        }

        private void RpcAnnounceToAllClients()
        {
            var allAnnouncements = GetComponentsInChildren<Announce>();
            foreach (var announce in allAnnouncements)
            {
                var btn = announce.AnnounceButton();
                if (btn != null && btn.activeSelf) btn.SetActive(false);
            }
        }

        public void ReceiveAnnounceFromNPC(AnnounceState state, GameObject npcObj)
        {
            var npcComp = npcObj.GetComponent<NPCPlayer>();
            var npcTeam = npcComp != null ? npcComp.team : null;
            string npcName = npcComp != null ? npcComp.playerName : npcObj.name;
            string teamName = npcTeam != null ? npcTeam.teamName : npcName;

            if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent($"¡{teamName.ToUpper()} CANTA {state.ToString().ToUpper()}!");

            _announceState = state;
            GameManager.Instance.isAnnouncementPending = true;

            var playerLocal = FindAnyObjectByType<PlayerLocal>();
            bool humanIsOpponent = playerLocal != null && playerLocal.player != null && playerLocal.player.team != npcTeam;

            if (humanIsOpponent)
            {
                ShowRespondButtons();
            }
            else
            {
                GetOpponentPlayerForNPC(npcObj, out var actualOpponent);
                if (actualOpponent != null)
                {
                    var opponentNpc = actualOpponent.GetComponent<NPCPlayer>();
                    if (opponentNpc != null) opponentNpc.HandleOpponentAnnounce(state, npcObj);
                }
                else
                {
                    GameManager.Instance.isAnnouncementPending = false;
                }
            }
        }

        private void GetOpponentPlayerForNPC(GameObject npcObject, out GameObject opponentPlayer)
        {
            var npcComp = npcObject.GetComponent<NPCPlayer>();
            var team = npcComp != null ? npcComp.team : null;
            
            var allNpcs = FindObjectsByType<NPCPlayer>(FindObjectsSortMode.None);
            foreach (var n in allNpcs)
            {
                if (n.gameObject != npcObject && n.team != team)
                {
                    opponentPlayer = n.gameObject;
                    return;
                }
            }
            
            // Si no hay NPCs oponentes, quizás el oponente es el humano (ya chequeado arriba, pero por seguridad)
            opponentPlayer = null;
        }

        // Métodos auxiliares para respuestas de NPC
        public void AcceptFromNPC(GameObject npcObj)
        {
            AcceptAnnounce(npcObj.name, false);
            GameManager.Instance.isAnnouncementPending = false; // Liberamos juego
        }

        public void DeclineFromNPC(GameObject npcObj)
        {
            DeclineAnnounce(npcObj.name, false);
            GameManager.Instance.isAnnouncementPending = false; // Liberamos juego
        }

        private void ShowRespondButtons()
        {
            string acceptText = "QUIERO";
            string declineText = "NO QUIERO";
            bool showMore = _announceState == AnnounceState.Envido || _announceState == AnnounceState.Truco;

            var playerLocal = FindAnyObjectByType<PlayerLocal>();
            bool hasFlor = playerLocal != null && playerLocal.player != null && playerLocal.player.haveFlower;

            if (_announceState == AnnounceState.Envido && hasFlor)
            {
                acceptText = "A LEY (FLOR)";
                declineText = "NO QUIERO";
                showMore = false;
            }
            else if (_announceState == AnnounceState.ALey)
            {
                acceptText = hasFlor ? "TENGO FLOR" : "NO QUIERO";
                declineText = "NO TENGO";
                showMore = false;
            }

            if (PlayerHUD.Instance != null)
            {
                PlayerHUD.Instance.ShowResponseButtons(true, acceptText, declineText, showMore);
            }
            else
            {
                // Fallback a legacy si no hay PlayerHUD
                foreach (var t in respondButtons) if (t != null) t.SetActive(true);
            }
        }

        // Bridge methods for UI Toolkit events
        public void LocalAccept() => LocalButtonInteract("AcceptButton");
        public void LocalDecline() => LocalButtonInteract("DeclineButton");
        public void LocalMore() => LocalButtonInteract("MoreAnnounceButton");

        private void LocalButtonInteract(string buttonName)
        {
            // Liberamos el juego al responder
            GameManager.Instance.isAnnouncementPending = false;

            var playerLocal = GetComponent<PlayerLocal>();
            if (playerLocal == null) playerLocal = GetComponentInParent<PlayerLocal>();
            if (playerLocal == null) playerLocal = FindAnyObjectByType<PlayerLocal>();
            
            bool hasFlor = false;
            string playerName = "Jugador";

            if (playerLocal != null && playerLocal.player != null)
            {
                hasFlor = playerLocal.player.haveFlower;
                playerName = playerLocal.player.playerName;
            }
            
            ButtonInteract(buttonName, null, playerName, hasFlor);

            if (PlayerHUD.Instance != null) PlayerHUD.Instance.ShowResponseButtons(false);
            else if (respondButtons != null)
            {
                foreach (var t in respondButtons) if (t != null) t.SetActive(false);
            }
        }

        // [Command(requiresAuthority = false)]
        private void ButtonInteract(string buttonName, PlayerLocal dummy, string playerName, bool hasFlor)
        {
            switch (buttonName)
            {
                case "AcceptButton":
                    AcceptAnnounce(playerName, hasFlor);
                    break;

                case "DeclineButton":
                    // Primero manejamos las consecuencias de rechazar SEGÚN el estado actual
                    if (_announceState == AnnounceState.Truco)
                    {
                        Debug.Log("[AnnouncementManager] Truco RECHAZADO. Finalizando mano.");
                        GetOpponentPlayer(out var npcObj);
                        if (npcObj != null)
                        {
                            var npc = npcObj.GetComponent<NPCPlayer>();
                            if (npc != null && npc.team != null)
                            {
                                // Si se rechaza el Truco, el equipo que cantó gana los puntos acumulados hasta ese momento (mínimo 1)
                                GameManager.Instance.ResolveHandWinner(npc.team.teamName, GameManager.Instance.currentHandValue);
                            }
                        }
                    }
                    else if (_announceState == AnnounceState.Envido)
                    {
                        Debug.Log("[AnnouncementManager] Envido RECHAZADO. Sumando 1 punto al oponente.");
                        GetOpponentPlayer(out var npcObj);
                        if (npcObj != null)
                        {
                            var npc = npcObj.GetComponent<NPCPlayer>();
                            if (npc != null && npc.team != null)
                            {
                                GameManager.Instance.AddAnnouncementPoints(npc.team.teamName, 1);
                            }
                        }
                    }

                    // Luego limpiamos el estado
                    DeclineAnnounce(playerName, hasFlor);
                    break;

                case "MoreAnnounceButton":
                    MoreAnnounce();
                    GameManager.Instance.isAnnouncementPending = true;
                    var current = GetCurrentAnnounce();
                    if (current != null) current.IncreaseAcceptAmount();
                    break;

                default:
                    Debug.Log("no button was pressed");
                    break;
            }
        }

        private Announce GetCurrentAnnounce()
        {
            var announces = GetComponentsInChildren<Announce>();
            return _announceState switch
            {
                AnnounceState.Envido => announces.OfType<EnvidoAnnouncement>().FirstOrDefault(),
                AnnounceState.Truco => announces.OfType<TrucoAnnouncement>().FirstOrDefault(),
                AnnounceState.Flor => announces.OfType<FlorAnnouncement>().FirstOrDefault(),
                AnnounceState.ALey => announces.OfType<ALeyAnnouncement>().FirstOrDefault(),
                _ => null
            };
        }

        // [TargetRpc]
        private void AcceptAnnounce(/*NetworkConnection conn,*/ string playerName, bool hasFlor)
        {
            string displayWinner = GetTeamNameByPlayerName(playerName);
            string msg = $"¡{displayWinner.ToUpper()} QUIERE!";
            if (_announceState == AnnounceState.ALey) msg = $"¡{displayWinner.ToUpper()} TIENE FLOR!";
            if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent(msg);

            Debug.Log($"[AnnouncementManager] Canto ACEPTADO por {playerName}");
            GameManager.Instance.isAnnouncementPending = false;
            
            var current = GetCurrentAnnounce();
            if (current != null)
            {
                if (current is TrucoAnnouncement)
                {
                    var playerLocal = FindAnyObjectByType<PlayerLocal>();
                    if (playerName == playerLocal?.player?.playerName)
                    {
                        GetOpponentPlayer(out var opponent);
                        var npc = opponent?.GetComponent<NPCPlayer>();
                        if (npc != null && npc.team != null)
                        {
                            GameManager.Instance.lastTrucoTeamIndex = GameManager.Instance.teams.IndexOf(npc.team) + 1;
                        }
                    }
                    else
                    {
                        if (playerLocal?.player?.team != null)
                        {
                            GameManager.Instance.lastTrucoTeamIndex = GameManager.Instance.teams.IndexOf(playerLocal.player.team) + 1;
                        }
                    }
                }

                if (current is TrucoAnnouncement && current.acceptAmount == 0) current.IncreaseAcceptAmount();
                current.UpdateTotalScore();
            }

            // Restaurar permiso de juego al humano si es su turno
            var player = FindAnyObjectByType<PlayerLocal>();
            if (player != null && SeatManager.Instance.GetPlayerSeatIndex(player.gameObject) == GameManager.Instance.currentPlayerTurn)
            {
                player.player.canPlayCard = true;
                Debug.Log("[AnnouncementManager] Turno restaurado al jugador humano.");
            }

            if (_announceState == AnnounceState.ALey)
            {
                Debug.Log($"[A Ley] El jugador {playerName} responde que {(hasFlor ? "SÍ" : "NO")} tiene Flor.");
            }
            
            _announceState = AnnounceState.None;
        }

        // [ClientRpc]
        private void DeclineAnnounce(string playerName, bool hasFlor)
        {
            string displayWinner = GetTeamNameByPlayerName(playerName);
            string msg = $"¡{displayWinner.ToUpper()} NO QUIERO!";
            if (_announceState == AnnounceState.ALey) msg = $"¡{displayWinner.ToUpper()} NO TIENE FLOR!";
            if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent(msg);

            Debug.Log($"[AnnouncementManager] Canto RECHAZADO por {playerName}");
            GameManager.Instance.isAnnouncementPending = false;
            
            if (_announceState == AnnounceState.ALey)
            {
                Debug.Log($"[A Ley] El jugador {playerName} responde que {(hasFlor ? "SÍ" : "NO")} tiene Flor.");
            }

            // Restaurar permiso de juego al humano si es su turno
            var player = FindAnyObjectByType<PlayerLocal>();
            if (player != null && SeatManager.Instance.GetPlayerSeatIndex(player.gameObject) == GameManager.Instance.currentPlayerTurn)
            {
                player.player.canPlayCard = true;
            }
            
            _announceState = AnnounceState.None;
        }

        private string GetTeamNameByPlayerName(string playerName)
        {
            var allPlayers = FindObjectsByType<Code.Player.Player>(FindObjectsSortMode.None);
            foreach (var p in allPlayers)
            {
                if (p.playerName == playerName && p.team != null) return p.team.teamName;
            }
            
            var allNpcs = FindObjectsByType<NPCPlayer>(FindObjectsSortMode.None);
            foreach (var n in allNpcs)
            {
                if (n.playerName == playerName && n.team != null) return n.team.teamName;
            }
            
            return playerName;
        }

        // [TargetRpc]
        private void MoreAnnounce(/*NetworkConnection conn*/)
        {
            var current = GetCurrentAnnounce();
            if (current != null) current.UpdateTotalScore();
            
            if (PlayerHUD.Instance != null)
            {
                PlayerHUD.Instance.ShowResponseButtons(true, "QUIERO", "NO QUIERO", true);
            }
            else
            {
                foreach (var t in respondButtons)
                {
                    if (t != null) t.SetActive(true);
                }
            }
        }
    }
}
