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
using Code.Scripts.Audio;

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
        public Code.Player.Team currentAnnouncerTeam;
        public string currentAnnouncerName;
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

            // Suscribirse a los eventos de la UI Toolkit
            global::Code.Core.GameEventManager.OnAcceptButtonClicked += LocalAccept;
            global::Code.Core.GameEventManager.OnDeclineButtonClicked += LocalDecline;
            global::Code.Core.GameEventManager.OnMoreButtonClicked += LocalMore;
        }

        private void OnDestroy()
        {
            global::Code.Core.GameEventManager.OnAcceptButtonClicked -= LocalAccept;
            global::Code.Core.GameEventManager.OnDeclineButtonClicked -= LocalDecline;
            global::Code.Core.GameEventManager.OnMoreButtonClicked -= LocalMore;
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
            currentAnnouncerTeam = null;
            currentAnnouncerName = "";
            if (GameManager.Instance != null) GameManager.Instance.isAnnouncementPending = false;
            if (PlayerHUD.Instance != null) PlayerHUD.Instance.ShowResponseButtons(false);
            
            var announces = GetComponentsInChildren<Announce>();
            foreach (var a in announces)
            {
                a.acceptAmount = 0;
            }
            
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
            var allNpcs = GameManager.Instance.npcs;

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
            
            if (playerLocal != null && !playerLocal.player.canPlayCard)
            {
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

            if (targetState == AnnounceState.Truco)
            {
                int myTeamIndex = (playerLocal != null && playerLocal.player != null && playerLocal.player.team != null) 
                    ? (GameManager.Instance.teams.IndexOf(playerLocal.player.team) + 1) : 0;

                if (GameManager.Instance.lastTrucoTeamIndex != 0 && GameManager.Instance.lastTrucoTeamIndex == myTeamIndex)
                {
                    if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent("TU EQUIPO TIENE EL TRUCO", 2f);
                    return;
                }
            }

            if (targetState != AnnounceState.None && WasAnnouncementCalledThisHand(targetState)) 
            {
                return;
            }

            StartCoroutine(SendAnnounceCoroutine(targetState, playerLocal));
        }

        private System.Collections.IEnumerator SendAnnounceCoroutine(AnnounceState targetState, PlayerLocal playerLocal)
        {
            _announceState = targetState;
            if (targetState != AnnounceState.None) _announcementsCalledThisHand[targetState] = true;

            PlayAnnounceSFX(targetState, 1);

            string playerName = playerLocal != null && playerLocal.player != null ? playerLocal.player.playerName : "Tú";
            currentAnnouncerName = playerName;

            string teamSuffix = "";
            if (playerLocal != null && playerLocal.player != null && playerLocal.player.team != null)
            {
                teamSuffix = $" ({playerLocal.player.team.teamName})";
                currentAnnouncerTeam = playerLocal.player.team;
            }

            if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent($"¡{playerName.ToUpper()}{teamSuffix.ToUpper()} CANTA {targetState.ToString().ToUpper()}!", 2.5f);
            
            GameManager.Instance.isAnnouncementPending = true;

            if (playerLocal != null) playerLocal.player.canPlayCard = false;
            
            RpcAnnounceToAllClients();
            if (PlayerHUD.Instance != null) PlayerHUD.Instance.RefreshActionButtons(true);

            // 2.5s para que desaparezca el texto + 2s extra = 4.5s
            yield return new WaitForSeconds(4.5f);

            GetOpponentPlayer(out var opponent);
            if (opponent != null)
            {
                var npc = opponent.GetComponent<NPCPlayer>();
                if (npc != null) npc.HandleOpponentAnnounce(_announceState, gameObject);
            }
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
            StartCoroutine(ReceiveAnnounceFromNPCCoroutine(state, npcObj));
        }

        private System.Collections.IEnumerator ReceiveAnnounceFromNPCCoroutine(AnnounceState state, GameObject npcObj)
        {
            var npcComp = npcObj.GetComponent<NPCPlayer>();
            var npcTeam = npcComp != null ? npcComp.team : null;
            string npcName = npcComp != null ? npcComp.playerName : npcObj.name;
            currentAnnouncerName = npcName;

            string teamSuffix = npcTeam != null ? $" ({npcTeam.teamName})" : "";

            currentAnnouncerTeam = npcTeam;

            if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent($"¡{npcName.ToUpper()}{teamSuffix.ToUpper()} CANTA {state.ToString().ToUpper()}!", 2.5f);

            PlayAnnounceSFX(state, 1);

            _announceState = state;
            GameManager.Instance.isAnnouncementPending = true;

            // 2.5s para que desaparezca el texto + 2s extra = 4.5s
            yield return new WaitForSeconds(4.5f);

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
            
            var allNpcs = GameManager.Instance.npcs;
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
            StartCoroutine(AcceptAnnounceCoroutine(npcObj.name, false));
        }

        public void DeclineFromNPC(GameObject npcObj)
        {
            StartCoroutine(DeclineAnnounceCoroutine(npcObj.name, false, _announceState, currentAnnouncerTeam));
        }

        private void ShowRespondButtons()
        {
            string acceptText = "QUIERO";
            string declineText = "NO QUIERO";
            bool showMore = _announceState == AnnounceState.Envido || _announceState == AnnounceState.Truco;

            var allPlayers = FindObjectsByType<PlayerLocal>(FindObjectsSortMode.None);
            var playerLocal = allPlayers.FirstOrDefault(p => p.isLocalPlayer && p.gameObject.activeInHierarchy);
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
                bool showSlider = _announceState == AnnounceState.Envido;
                string announcerText = !string.IsNullOrEmpty(currentAnnouncerName) ? currentAnnouncerName : "TE";
                string titleText = $"{announcerText.ToUpper()} CANTA {_announceState.ToString().ToUpper()}";
                PlayerHUD.Instance.ShowResponseButtons(true, acceptText, declineText, showMore, showSlider, titleText);
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
                    StartCoroutine(AcceptAnnounceCoroutine(playerName, hasFlor));
                    break;

                case "DeclineButton":
                    StartCoroutine(DeclineAnnounceCoroutine(playerName, hasFlor, _announceState, currentAnnouncerTeam));
                    break;

                case "MoreAnnounceButton":
                    StartCoroutine(MoreAnnounceCoroutine(playerName, hasFlor));
                    break;

                default:
                    break;
            }
        }

        private System.Collections.IEnumerator MoreAnnounceCoroutine(string playerName, bool hasFlor)
        {
            var current = GetCurrentAnnounce();
            if (current != null)
            {
                current.IncreaseAcceptAmount();
                current.UpdateTotalScore();
                PlayAnnounceSFX(_announceState, current.acceptAmount);
            }

            // El emisor del re-envido es el jugador que presionó el botón (el humano)
            currentAnnouncerName = playerName;
            string teamSuffix = "";
            var playerLocal = FindAnyObjectByType<PlayerLocal>();
            if (playerLocal != null && playerLocal.player != null && playerLocal.player.team != null)
            {
                teamSuffix = $" ({playerLocal.player.team.teamName})";
            }

            string actionText = "RE-ENVIDA";
            if (_announceState == AnnounceState.Truco) actionText = "PIDE RETRUCO";
            else if (_announceState == AnnounceState.Flor) actionText = "PIDE CONTRAFLOR";

            if (PlayerHUD.Instance != null)
            {
                PlayerHUD.Instance.NotifyEvent($"¡{playerName.ToUpper()}{teamSuffix.ToUpper()} {actionText.ToUpper()}!", 2.5f);
                PlayerHUD.Instance.ShowResponseButtons(false); // Ocultar botones de respuesta para el humano
            }

            GameManager.Instance.isAnnouncementPending = true;

            // Esperar a que la notificación de re-canto termine
            yield return new WaitForSeconds(4.5f);

            // Notificar al oponente (NPC)
            GetOpponentPlayer(out var opponent);
            if (opponent != null)
            {
                var npc = opponent.GetComponent<NPCPlayer>();
                if (npc != null) npc.HandleOpponentAnnounce(_announceState, gameObject);
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

        private System.Collections.IEnumerator AcceptAnnounceCoroutine(string playerName, bool hasFlor)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX("confirm_quiero_positive");
            }
            string displayWinner = GetTeamNameByPlayerName(playerName);
            string msg = $"¡{displayWinner.ToUpper()} QUIERE!";
            if (_announceState == AnnounceState.ALey) msg = $"¡{displayWinner.ToUpper()} TIENE FLOR!";
            if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent(msg, 2.5f);

            
            // 2.5s para que desaparezca el texto + 2s extra = 4.5s
            yield return new WaitForSeconds(4.5f);
            
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

                current.IncreaseAcceptAmount();
                current.UpdateTotalScore();
            }

            if (_announceState == AnnounceState.ALey)
            {
            }
            
            _announceState = AnnounceState.None;
            GameManager.Instance.isAnnouncementPending = false;

            // Restaurar permiso de juego al humano si es su turno
            var player = FindAnyObjectByType<PlayerLocal>();
            if (player != null && SeatManager.Instance.GetPlayerSeatIndex(player.gameObject) == GameManager.Instance.currentPlayerTurn)
            {
                player.player.canPlayCard = true;
            }
        }

        private System.Collections.IEnumerator DeclineAnnounceCoroutine(string playerName, bool hasFlor, AnnounceState previousState, Team announcerTeam)
        {
            if (AudioManager.Instance != null)
            {
                if (previousState == AnnounceState.Truco)
                {
                    AudioManager.Instance.PlaySFX("fold_go_to_deck_slide");
                }
                else
                {
                    AudioManager.Instance.PlaySFX("decline_noquiero_neg");
                }
            }
            string displayWinner = GetTeamNameByPlayerName(playerName);
            string msg = $"¡{displayWinner.ToUpper()} NO QUIERO!";
            if (previousState == AnnounceState.ALey) msg = $"¡{displayWinner.ToUpper()} NO TIENE FLOR!";
            if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent(msg, 2.5f);

            
            // 2.5s para que desaparezca el texto + 2s extra = 4.5s
            yield return new WaitForSeconds(4.5f);
            
            if (previousState == AnnounceState.ALey)
            {
            }

            // Procesar las consecuencias AHORA que ha pasado el tiempo
            if (previousState == AnnounceState.Truco)
            {
                if (announcerTeam != null)
                {
                    GameManager.Instance.ResolveHandWinner(announcerTeam.teamName, GameManager.Instance.currentHandValue);
                }
            }
            else if (previousState == AnnounceState.Envido)
            {
                if (announcerTeam != null)
                {
                    GameManager.Instance.AddAnnouncementPoints(announcerTeam.teamName, 1);
                }
            }

            _announceState = AnnounceState.None;
            GameManager.Instance.isAnnouncementPending = false;

            // Restaurar permiso de juego al humano si es su turno
            var player = FindAnyObjectByType<PlayerLocal>();
            if (player != null && SeatManager.Instance.GetPlayerSeatIndex(player.gameObject) == GameManager.Instance.currentPlayerTurn)
            {
                player.player.canPlayCard = true;
            }
        }

        private string GetTeamNameByPlayerName(string playerName)
        {
            var allPlayers = GameManager.Instance.allPlayers;
            foreach (var p in allPlayers)
            {
                if (p.playerName == playerName)
                {
                    return p.team != null ? $"{p.playerName} ({p.team.teamName})" : p.playerName;
                }
            }
            
            var allNpcs = GameManager.Instance.npcs;
            foreach (var n in allNpcs)
            {
                if (n.playerName == playerName)
                {
                    return n.team != null ? $"{n.playerName} ({n.team.teamName})" : n.playerName;
                }
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
                bool showSlider = _announceState == AnnounceState.Envido;
                string announcerText = !string.IsNullOrEmpty(currentAnnouncerName) ? currentAnnouncerName : "TE";
                string titleText = $"{announcerText.ToUpper()} RE-ENVIDA";
                if (_announceState == AnnounceState.Truco) titleText = $"{announcerText.ToUpper()} PIDE RETRUCO";
                else if (_announceState == AnnounceState.Flor) titleText = $"{announcerText.ToUpper()} PIDE CONTRAFLOR";
                
                PlayerHUD.Instance.ShowResponseButtons(true, "QUIERO", "NO QUIERO", true, showSlider, titleText);
            }
            else
            {
                foreach (var t in respondButtons)
                {
                    if (t != null) t.SetActive(true);
                }
            }
        }

        private void PlayAnnounceSFX(AnnounceState state, int level)
        {
            if (AudioManager.Instance == null) return;

            string sfxId = "";
            switch (state)
            {
                case AnnounceState.Envido:
                    sfxId = level switch
                    {
                        <= 1 => "canto_envido_wood",
                        2 => "canto_realenvido_wood",
                        _ => "canto_faltaenvido_sweep"
                    };
                    break;
                case AnnounceState.Truco:
                    sfxId = level switch
                    {
                        <= 1 => "canto_truco_warn",
                        2 => "canto_retruco_raise",
                        _ => "canto_valecuatro_siren"
                    };
                    break;
                case AnnounceState.Flor:
                    sfxId = level switch
                    {
                        <= 1 => "canto_flor_bell",
                        2 => "canto_contraflor_chime",
                        _ => "canto_contraflor_resto_blast"
                    };
                    break;
                case AnnounceState.ALey:
                    sfxId = "canto_aley_gong";
                    break;
            }

            if (!string.IsNullOrEmpty(sfxId))
            {
                AudioManager.Instance.PlaySFX(sfxId);
            }
        }
    }
}
