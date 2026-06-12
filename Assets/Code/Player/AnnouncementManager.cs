using System;
using System.Collections.Generic;
using System.Linq;
using Code.GameLogic;
using Code.GameLogic.Announcement;
using Code.Networking;
using Mirror;
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
        // Silla del cantor vigente: el que responde es siempre el rival "a la derecha"
        // (la silla siguiente en el orden de turnos). -1 = desconocida.
        public int currentAnnouncerSeat = -1;
        public GameObject[] respondButtons;
        public string[] announcementPlayerNames = new string[2];
        private Dictionary<AnnounceState, bool> _announcementsCalledThisHand = new Dictionary<AnnounceState, bool>();

        // Flor por jugador: _announcementsCalledThisHand[Flor] es global a la mano, pero
        // contestar la flor del rival con la propia ("con flor envido") exige saber si
        // ESTE jugador ya cantó la suya. _florSingers vive en el server (autoritativo);
        // _localFlorSung es la copia de esta máquina para la visibilidad del botón.
        private bool _localFlorSung;
        private readonly HashSet<string> _florSingers = new HashSet<string>();

        public bool LocalFlorSung => _localFlorSung;

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

            // En host/singleplayer los botones de canto los escucha PlayerTurnState, pero
            // la máquina de estados solo corre en el server: el cliente puro los engancha acá.
            if (NetworkClient.active && !NetworkServer.active)
                global::Code.Core.GameEventManager.OnAnnounceButtonClicked += SendAnnounceToClient;
        }

        private void OnDestroy()
        {
            global::Code.Core.GameEventManager.OnAcceptButtonClicked -= LocalAccept;
            global::Code.Core.GameEventManager.OnDeclineButtonClicked -= LocalDecline;
            global::Code.Core.GameEventManager.OnMoreButtonClicked -= LocalMore;
            global::Code.Core.GameEventManager.OnAnnounceButtonClicked -= SendAnnounceToClient;
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
            currentAnnouncerSeat = -1;
            _localFlorSung = false;
            _florSingers.Clear();
            foreach (var p in FindObjectsByType<Code.Player.Player>(FindObjectsSortMode.None))
                p.florBurned = false;
            UpdateEnvidoStakeUI(0, false);
            if (GameManager.Instance != null) GameManager.Instance.isAnnouncementPending = false;
            if (PlayerHUD.Instance != null) PlayerHUD.Instance.ShowResponseButtons(false);
            
            var announces = GetComponentsInChildren<Announce>();
            foreach (var a in announces)
            {
                a.ResetAnnounceState();
                if (a is EnvidoAnnouncement envido)
                {
                    envido.extraPoints = 0;
                    envido.pendingRaiseStones = 0;
                }
            }

            InitializeAnnouncementsCalled();

            // Los clientes guardan su propia copia de este estado (visibilidad de
            // botones): sin esto, "truco ya cantado" persiste entre manos en el cliente.
            if (NetworkServer.active)
                (NetworkManager.singleton as MyNetworkingManager)?.BroadcastResetAnnouncements();
        }

        /// <summary>Client-side: the server says this announcement was sung this hand.
        /// Updates the local flag and re-evaluates button visibility (e.g. flor blocks
        /// envido).</summary>
        public void MarkAnnouncementCalled(AnnounceState state)
        {
            if (state == AnnounceState.None) return;
            _announcementsCalledThisHand[state] = true;

            var pl = GetLocalPlayerLocal();
            if (PlayerHUD.Instance != null && pl != null && pl.player != null)
                PlayerHUD.Instance.RefreshActionButtons(pl.player.canPlayCard);
        }

        /// <summary>Client-side: mirrors the accepted-truco state so the HUD can decide
        /// between TRUCO/RETRUCO/VALE 9 and which team is allowed to raise.</summary>
        public void ApplyTrucoStateFromServer(bool trucoCalled, int trucoLevel)
        {
            _announcementsCalledThisHand[AnnounceState.Truco] = trucoCalled;
            var truco = GetComponentsInChildren<Announce>().OfType<TrucoAnnouncement>().FirstOrDefault();
            if (truco != null) truco.acceptAmount = trucoLevel;
        }

        /// <summary>Envido points that would be awarded if the standing call were
        /// accepted right now: next step of the table + accumulated extra stones.</summary>
        private int ProspectiveEnvidoStake()
        {
            var envido = GetComponentsInChildren<Announce>().OfType<EnvidoAnnouncement>().FirstOrDefault();
            if (envido == null) return 0;
            var table = envido.IncreasingAmount();
            int idx = Mathf.Min(envido.acceptAmount + 1, table.Length - 1);
            return table[idx] + envido.extraPoints;
        }

        /// <summary>Updates the local "envido at stake" HUD and mirrors it on clients.</summary>
        private void UpdateEnvidoStakeUI(int points, bool visible)
        {
            if (PlayerHUD.Instance != null) PlayerHUD.Instance.ShowEnvidoStake(visible, points);
            if (NetworkServer.active)
                (NetworkManager.singleton as MyNetworkingManager)?.BroadcastEnvidoStake(points, visible);
        }

        public bool WasAnnouncementCalledThisHand(AnnounceState state)
        {
            if (_announcementsCalledThisHand.TryGetValue(state, out bool called)) return called;
            return false;
        }

        /// <summary>The PlayerLocal owned by this machine (host or client). In multiplayer
        /// several PlayerLocal instances coexist, so FindAnyObjectByType is not enough.</summary>
        private static PlayerLocal GetLocalPlayerLocal()
        {
            PlayerLocal fallback = null;
            foreach (var p in UnityEngine.Object.FindObjectsByType<PlayerLocal>(FindObjectsSortMode.None))
            {
                if (p.isLocalPlayer) return p;
                fallback ??= p;
            }
            return fallback;
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
            // Bloquear si hay un canto pendiente, EXCEPTO si es "A Ley" (informativo) 
            // o si es "Flor" sobre un "Envido" (la Flor anula al Envido).
            bool isFlorOverEnvido = ButtonName == "FlorButton" && _announceState == AnnounceState.Envido;
            
            if (GameManager.Instance != null && GameManager.Instance.isAnnouncementPending 
                && _announceState != AnnounceState.ALey && !isFlorOverEnvido) 
                return;

            var playerLocal = GetLocalPlayerLocal();

            // La Flor sobre un Envido es una RESPUESTA: se canta aunque no sea tu turno.
            bool isALeyResponseCheck = ButtonName == "ALeyButton" && _announceState == AnnounceState.ALey;
            if (playerLocal != null && !playerLocal.player.canPlayCard && !isALeyResponseCheck && !isFlorOverEnvido)
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

            // Multiplayer pure client: the server owns the announcement state machine.
            if (targetState != AnnounceState.None && NetworkClient.active && !NetworkServer.active)
            {
                var localP = GetLocalPlayerLocal();
                var netSync = localP != null ? localP.GetComponent<PlayerNetworkSync>() : null;
                netSync?.CmdSendAnnouncement((int)targetState);

                // Marca local optimista para que el botón se oculte ya; se limpia cada
                // mano con RpcResetAnnouncements y el truco se re-sincroniza al aceptarse.
                _announcementsCalledThisHand[targetState] = true;

                if (targetState == AnnounceState.Flor) _localFlorSung = true;

                // Envido con flor en mano = quemar la flor. El server hace lo propio con
                // su copia (BurnFlor); acá solo se refleja para los botones del HUD.
                if (targetState == AnnounceState.Envido && localP != null
                    && localP.player != null && localP.player.haveFlower)
                {
                    localP.player.haveFlower = false;
                    localP.player.florBurned = true;
                }

                // Si la flor respondía a un envido, cerrar la barra de respuesta local.
                if (isFlorOverEnvido && PlayerHUD.Instance != null)
                    PlayerHUD.Instance.ShowResponseButtons(false);
                return;
            }

            if (targetState == AnnounceState.Envido)
            {
                // La flor propia ya no bloquea el envido: cantarlo la QUEMA (ver abajo).
                bool anyNpcHasFlor = GameManager.Instance != null && GameManager.Instance.npcs.Any(n => n.haveFlower);
                bool florAlreadySung = WasAnnouncementCalledThisHand(AnnounceState.Flor);

                if (anyNpcHasFlor || florAlreadySung)
                {
                    if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent("NO SE PUEDE CANTAR ENVIDO CON FLOR EN JUEGO", 2f);
                    return;
                }

                int score = 0;
                if (playerLocal != null && playerLocal.cardsHandler != null && DeckCreator.Instance != null)
                {
                    score = TrucoRules.CalculateEnvidoScore(playerLocal.cardsHandler.InitialHand, DeckCreator.Instance.cardVira);
                }

                if (score <= 0)
                {
                    if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent("NO TIENES ENVIDO", 1.5f);
                    return;
                }
            }

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
                // Truco re-raises (Retruco, Vale 9, Vale Partida) are valid after acceptance;
                // the team-ownership check above already blocked invalid calls.
                // A Ley can be sung in response to an opponent's A Ley.
                bool isALeyResponse = targetState == AnnounceState.ALey && _announceState == AnnounceState.ALey;
                // La flor del rival se contesta con la propia ("con flor envido"):
                // vale mientras este jugador no haya cantado la suya todavía.
                bool isFlorOverFlor = targetState == AnnounceState.Flor && !_localFlorSung
                    && playerLocal != null && playerLocal.player != null && playerLocal.player.haveFlower;
                if (targetState != AnnounceState.Truco && !isALeyResponse && !isFlorOverFlor)
                    return;
            }

            if (targetState == AnnounceState.Flor)
            {
                _localFlorSung = true;
                if (playerLocal != null && playerLocal.player != null)
                    _florSingers.Add(playerLocal.player.playerName);
            }
            else if (targetState == AnnounceState.Envido && playerLocal != null
                     && playerLocal.player != null && playerLocal.player.haveFlower)
            {
                BurnFlor(playerLocal.player);
            }

            StartCoroutine(SendAnnounceCoroutine(targetState, playerLocal));
        }

        private System.Collections.IEnumerator SendAnnounceCoroutine(AnnounceState targetState, PlayerLocal playerLocal)
        {
            // SI targetState es Flor y el estado anterior era Envido, la Flor anula al Envido.
            if (targetState == AnnounceState.Flor && _announceState == AnnounceState.Envido)
            {
                Debug.Log("[AnnouncementManager] FLOR anula ENVIDO (Jugador).");
                if (PlayerHUD.Instance != null)
                {
                    PlayerHUD.Instance.NotifyEvent("FLOR ANULA ENVIDO", 2f);
                    PlayerHUD.Instance.ShowResponseButtons(false); // cerrar la respuesta al envido
                }
                if (GameManager.Instance != null) GameManager.Instance.pendingEnvidoResolution = false;
                UpdateEnvidoStakeUI(0, false);
            }

            _announceState = targetState;
            if (targetState != AnnounceState.None)
            {
                _announcementsCalledThisHand[targetState] = true;
                if (NetworkServer.active)
                    (NetworkManager.singleton as MyNetworkingManager)?.BroadcastAnnouncementCalled((int)targetState);
            }

            if (targetState == AnnounceState.Envido)
                UpdateEnvidoStakeUI(ProspectiveEnvidoStake(), true);

            var trucoAnnounceForSfx = GetCurrentAnnounce() as Code.GameLogic.Announcement.TrucoAnnouncement;
            int trucoSfxLevel = (targetState == AnnounceState.Truco && trucoAnnounceForSfx != null)
                ? trucoAnnounceForSfx.acceptAmount + 1 : 1;
            PlayAnnounceSFX(targetState, trucoSfxLevel);

            string playerName = playerLocal != null && playerLocal.player != null ? playerLocal.player.playerName : "Tú";
            currentAnnouncerName = playerName;
            currentAnnouncerSeat = (playerLocal != null && SeatManager.Instance != null)
                ? SeatManager.Instance.GetPlayerSeatIndex(playerLocal.gameObject) : -1;

            string teamSuffix = "";
            if (playerLocal != null && playerLocal.player != null && playerLocal.player.team != null)
            {
                teamSuffix = $" ({playerLocal.player.team.teamName})";
                currentAnnouncerTeam = playerLocal.player.team;
            }

            string announceLabel = targetState.ToString().ToUpper();
            if (targetState == AnnounceState.Truco && trucoAnnounceForSfx != null)
            {
                announceLabel = trucoAnnounceForSfx.acceptAmount switch
                {
                    1 => "RETRUCO",
                    2 => "VALE 9",
                    3 => "VALE PARTIDA",
                    _ => "TRUCO"
                };
            }
            if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent($"¡{playerName.ToUpper()}{teamSuffix.ToUpper()} CANTA {announceLabel}!", 2.5f);
            
            // Si es "A Ley" o "Flor", no bloqueamos la partida, es solo informativo.
            if (targetState != AnnounceState.ALey && targetState != AnnounceState.Flor)
            {
                GameManager.Instance.isAnnouncementPending = true;
                if (playerLocal != null) playerLocal.player.canPlayCard = false;
            }
            else
            {
                // Para A Ley y Flor, permitimos seguir jugando inmediatamente
                GameManager.Instance.isAnnouncementPending = false;
                if (playerLocal != null) playerLocal.player.canPlayCard = true;
            }
            
            RpcAnnounceToAllClients();
            if (PlayerHUD.Instance != null) PlayerHUD.Instance.RefreshActionButtons(true);

            // Reducir espera para A Ley y Flor
            float waitTime = (targetState == AnnounceState.ALey || targetState == AnnounceState.Flor) ? 2.5f : 4.5f;
            yield return new WaitForSeconds(waitTime);

            GetOpponentPlayer(out var opponent);
            if (opponent != null)
            {
                var npc = opponent.GetComponent<NPCPlayer>();
                // El NPC aún puede reaccionar (ej: cantar su propia Flor) pero no con Quiero/No Quiero
                if (npc != null) npc.HandleOpponentAnnounce(targetState, gameObject);
            }
            else if (targetState == AnnounceState.Envido || targetState == AnnounceState.Truco)
            {
                // Multiplayer: el oponente es humano — mostrarle los botones de respuesta.
                // Si no hay nadie (no debería pasar), liberamos el bloqueo para no trabar la mano.
                if (!TryShowResponseToHumanOpponent(currentAnnouncerTeam))
                {
                    GameManager.Instance.isAnnouncementPending = false;
                }
            }

            // Si era informativo, nos aseguramos de que el estado vuelva a None tras la notificación
            if (targetState == AnnounceState.ALey || targetState == AnnounceState.Flor) _announceState = AnnounceState.None;
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

        /// <summary>
        /// Server-side entry point for human player announcements in multiplayer.
        /// Mirrors ReceiveAnnounceFromNPC flow but treats the caller as the announcer.
        /// </summary>
        public void ReceiveAnnounceFromHumanPlayer(AnnounceState state, GameObject playerObj)
        {
            // Flor: validación autoritativa contra la mano repartida (haveFlower solo
            // existe en la máquina del dueño) y un solo canto de flor por jugador.
            string announcerName = playerObj.GetComponent<PlayerLocal>()?.player?.playerName ?? playerObj.name;
            if (state == AnnounceState.Flor
                && (!PlayerHandHasFlor(playerObj) || _florSingers.Contains(announcerName)))
                return;

            if (state != AnnounceState.None && WasAnnouncementCalledThisHand(state))
            {
                // Truco re-raises (Retruco, Vale 9...) are legal after acceptance; the
                // ownership check below decides who may raise. Same exception as the
                // host path in SendAnnounceToClient.
                bool isALeyResponse = state == AnnounceState.ALey && _announceState == AnnounceState.ALey;
                // La flor del rival se contesta con la propia (ya validada arriba).
                if (state != AnnounceState.Truco && !isALeyResponse && state != AnnounceState.Flor) return;
            }

            // Con flor cantada no hay envido — validación autoritativa: se rechaza acá
            // aunque el botón del cliente esté desincronizado o el comando sea forzado.
            if (state == AnnounceState.Envido && WasAnnouncementCalledThisHand(AnnounceState.Flor))
            {
                var rejectedSync = playerObj.GetComponent<PlayerNetworkSync>();
                if (rejectedSync != null && rejectedSync.connectionToClient != null
                    && !(rejectedSync.connectionToClient is LocalConnectionToClient))
                {
                    rejectedSync.TargetHudNotify(rejectedSync.connectionToClient,
                        "NO SE PUEDE CANTAR ENVIDO CON FLOR EN JUEGO", 2f);
                }
                return;
            }

            if (state == AnnounceState.Truco && GameManager.Instance != null
                && GameManager.Instance.lastTrucoTeamIndex != 0)
            {
                // Only the team that does NOT own the current truco may raise it.
                var pl = playerObj.GetComponent<PlayerLocal>();
                var team = pl?.player?.team;
                int teamIdx = team != null ? GameManager.Instance.teams.IndexOf(team) + 1 : 0;
                if (teamIdx == GameManager.Instance.lastTrucoTeamIndex) return;

                // Vale Partida is the ceiling.
                var trucoAnnounce = GetComponentsInChildren<Announce>().OfType<TrucoAnnouncement>().FirstOrDefault();
                if (trucoAnnounce != null && trucoAnnounce.acceptAmount >= 3) return;
            }

            if (state == AnnounceState.Flor)
            {
                _florSingers.Add(announcerName);
            }
            else if (state == AnnounceState.Envido && PlayerHandHasFlor(playerObj))
            {
                // Envido con flor en mano = la flor se quema (no puntúa esta mano).
                var announcer = playerObj.GetComponent<PlayerLocal>()?.player;
                if (announcer != null) BurnFlor(announcer);
            }

            StartCoroutine(ReceiveAnnounceFromNPCCoroutine(state, playerObj));
        }

        public void ReceiveAnnounceFromNPC(AnnounceState state, GameObject npcObj)
        {
            // Bloquear si el canto ya se realizó en esta mano
            if (state != AnnounceState.None && WasAnnouncementCalledThisHand(state))
            {
                return;
            }
            StartCoroutine(ReceiveAnnounceFromNPCCoroutine(state, npcObj));
        }

        private System.Collections.IEnumerator ReceiveAnnounceFromNPCCoroutine(AnnounceState state, GameObject npcObj)
        {
            // SI el NPC canta Flor y el estado anterior era Envido, la Flor anula al Envido.
            if (state == AnnounceState.Flor && _announceState == AnnounceState.Envido)
            {
                Debug.Log("[AnnouncementManager] FLOR anula ENVIDO (NPC).");
                if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent("FLOR ANULA ENVIDO", 2f);
                if (GameManager.Instance != null) GameManager.Instance.pendingEnvidoResolution = false;
                UpdateEnvidoStakeUI(0, false);
            }

            // El "anunciante" puede ser un NPC (singleplayer) o un jugador humano remoto (multiplayer)
            var npcComp = npcObj.GetComponent<NPCPlayer>();
            var humanComp = npcObj.GetComponent<PlayerLocal>();
            var npcTeam = npcComp != null ? npcComp.team : humanComp?.player?.team;
            string npcName = npcComp != null
                ? npcComp.playerName
                : (humanComp?.player?.playerName ?? npcObj.name);
            currentAnnouncerName = npcName;
            currentAnnouncerSeat = SeatManager.Instance != null
                ? SeatManager.Instance.GetPlayerSeatIndex(npcObj) : -1;

            string teamSuffix = npcTeam != null ? $" ({npcTeam.teamName})" : "";

            currentAnnouncerTeam = npcTeam;

            if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent($"¡{npcName.ToUpper()}{teamSuffix.ToUpper()} CANTA {state.ToString().ToUpper()}!", 2.5f);

            PlayAnnounceSFX(state, 1);

            _announceState = state;
            if (state != AnnounceState.None)
            {
                _announcementsCalledThisHand[state] = true;
                if (NetworkServer.active)
                    (NetworkManager.singleton as MyNetworkingManager)?.BroadcastAnnouncementCalled((int)state);
            }

            // La Flor es informativa (igual que en SendAnnounceCoroutine): no bloquea la
            // mano ni pide Quiero/No Quiero; la contraflor se resuelve sola al cerrar la mano.
            GameManager.Instance.isAnnouncementPending = state != AnnounceState.Flor;

            if (state == AnnounceState.Envido)
                UpdateEnvidoStakeUI(ProspectiveEnvidoStake(), true);

            // 2.5s para que desaparezca el texto + 2s extra = 4.5s
            yield return new WaitForSeconds(4.5f);

            if (state == AnnounceState.Flor)
            {
                // Si mientras tanto se cantó otra cosa (ej: truco), no pisar ese estado.
                if (_announceState == AnnounceState.Flor) _announceState = AnnounceState.None;
                yield break;
            }

            var playerLocal = GetLocalPlayerLocal();
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
                else if (!TryShowResponseToHumanOpponent(npcTeam))
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
            string moreText = "RE-ENVIDAR";
            
            // Permitimos re-envidar tanto en Truco como en Envido
            bool showMore = _announceState == AnnounceState.Truco || _announceState == AnnounceState.Envido;
            bool showDecline = true;

            // Determinar texto del botón More si es Truco
            if (_announceState == AnnounceState.Truco)
            {
                var trucoAnnounce = GetCurrentAnnounce() as TrucoAnnouncement;
                int currentLevel = (trucoAnnounce != null) ? trucoAnnounce.acceptAmount : 0;
                moreText = (currentLevel + 1) switch
                {
                    1 => "RETRUCO",
                    2 => "VALE 9",
                    3 => "VALE PARTIDA",
                    _ => "RETRUCO"
                };
            }

            var allPlayers = FindObjectsByType<PlayerLocal>(FindObjectsSortMode.None);
            var playerLocal = allPlayers.FirstOrDefault(p => p.isLocalPlayer && p.gameObject.activeInHierarchy);
            bool hasFlor = playerLocal != null && playerLocal.player != null && playerLocal.player.haveFlower;

            bool disableAccept = false;
            if (_announceState == AnnounceState.Envido && !hasFlor)
            {
                int localEnvidoScore = 0;
                if (playerLocal != null && playerLocal.cardsHandler != null && DeckCreator.Instance != null)
                {
                    localEnvidoScore = TrucoRules.CalculateEnvidoScore(playerLocal.cardsHandler.InitialHand, DeckCreator.Instance.cardVira);
                }
                
                if (localEnvidoScore == 0)
                {
                    disableAccept = true;
                    showMore = false;
                }
            }

            if (_announceState == AnnounceState.Envido && hasFlor)
            {
                acceptText = "A LEY (FLOR)";
                declineText = "NO QUIERO";
                showMore = false;
            }
            else if (_announceState == AnnounceState.ALey)
            {
                acceptText = hasFlor ? "TENGO FLOR" : "PASAR";
                declineText = "PASAR";
                showMore = false;
                showDecline = hasFlor; // Si no tengo flor, solo muestro un botón de "PASAR"
            }
            else if (_announceState == AnnounceState.Flor)
            {
                if (hasFlor)
                {
                    acceptText = "CON FLOR / QUIERO";
                    declineText = "NO QUIERO";
                    showMore = true;
                }
                else
                {
                    acceptText = "QUIERO";
                    showDecline = false;
                    showMore = false;
                }
            }

            if (PlayerHUD.Instance != null)
            {
                bool showSlider = _announceState == AnnounceState.Envido && !disableAccept;
                string announcerText = !string.IsNullOrEmpty(currentAnnouncerName) ? currentAnnouncerName : "TE";
                string titleText = $"{announcerText.ToUpper()} CANTA {_announceState.ToString().ToUpper()}";
                
                // CRITICAL: Pasar el moreText calculado (RETRUCO, VALE 9, etc) al PlayerHUD
                PlayerHUD.Instance.ShowResponseButtons(true, acceptText, declineText, showMore, showSlider, titleText, disableAccept, showDecline, moreText);
            }
            else
            {
                // Fallback a legacy si no hay PlayerHUD
                if (respondButtons[0] != null) respondButtons[0].SetActive(!disableAccept);
                if (respondButtons[1] != null) respondButtons[1].SetActive(true);
                if (respondButtons[2] != null) respondButtons[2].SetActive(showMore);
            }
        }

        // Bridge methods for UI Toolkit events
        public void LocalAccept() => LocalButtonInteract("AcceptButton");
        public void LocalDecline() => LocalButtonInteract("DeclineButton");
        public void LocalMore() => LocalButtonInteract("MoreAnnounceButton");

        private void LocalButtonInteract(string buttonName)
        {
            // GetLocalPlayerLocal y no FindAnyObjectByType: en multiplayer conviven varios
            // PlayerLocal y el del rival tiene la mano oculta (envido siempre 0).
            var playerLocal = GetLocalPlayerLocal();
            
            bool hasFlor = false;
            string playerName = "Jugador";

            if (playerLocal != null && playerLocal.player != null)
            {
                hasFlor = playerLocal.player.haveFlower;
                playerName = playerLocal.player.playerName;
            }
            
            if ((buttonName == "AcceptButton" || buttonName == "MoreAnnounceButton") && _announceState == AnnounceState.Envido && !hasFlor)
            {
                int score = 0;
                if (playerLocal != null && playerLocal.cardsHandler != null && DeckCreator.Instance != null)
                {
                    score = TrucoRules.CalculateEnvidoScore(playerLocal.cardsHandler.InitialHand, DeckCreator.Instance.cardVira);
                }
                if (score <= 0)
                {
                    if (PlayerHUD.Instance != null) PlayerHUD.Instance.NotifyEvent("NO TIENES ENVIDO", 1.5f);
                    return;
                }
            }

            // Multiplayer pure client: send the response to the server and hide local UI.
            // The envido slider value travels along — the server can't read this HUD.
            if (NetworkClient.active && !NetworkServer.active)
            {
                var localP = GetLocalPlayerLocal();
                var netSync = localP != null ? localP.GetComponent<PlayerNetworkSync>() : null;
                int extraStones = PlayerHUD.Instance != null ? PlayerHUD.Instance.CurrentSliderValue : 0;
                netSync?.CmdRespondAnnouncement(buttonName, extraStones);

                if (PlayerHUD.Instance != null) PlayerHUD.Instance.ShowResponseButtons(false);
                return;
            }

            ButtonInteract(buttonName, null, playerName, hasFlor);

            if (PlayerHUD.Instance != null) PlayerHUD.Instance.ShowResponseButtons(false);
            else if (respondButtons != null)
            {
                foreach (var t in respondButtons) if (t != null) t.SetActive(false);
            }
        }

        // ─────────────── Multiplayer bridge (server-side entry points) ───────────────

        /// <summary>Server-side: a remote client pressed Quiero / No Quiero / re-raise.
        /// extraStones: slider value chosen on the client when re-raising envido.</summary>
        public void ReceiveResponseFromHumanPlayer(string buttonName, GameObject playerObj, int extraStones = 0)
        {
            var pl = playerObj.GetComponent<PlayerLocal>();
            string playerName = pl?.player?.playerName ?? playerObj.name;
            bool hasFlor = pl?.player?.haveFlower ?? false;
            ButtonInteract(buttonName, null, playerName, hasFlor, extraStones);
        }

        /// <summary>Client-side (via TargetRpc): the server asks this client to respond
        /// to an opponent's announcement.</summary>
        public void ShowResponseButtonsFromServer(AnnounceState state, string announcerName)
        {
            _announceState = state;
            currentAnnouncerName = announcerName;
            if (state != AnnounceState.None) _announcementsCalledThisHand[state] = true;
            ShowRespondButtons();
        }

        /// <summary>Server-side: shows the response UI to the opponent at the announcer's
        /// RIGHT — the next occupied seat in turn order (host UI directly, remote client
        /// via TargetRpc). Falls back to the first connected opponent when the announcer's
        /// seat is unknown. Returns false when no human opponent exists.</summary>
        private bool TryShowResponseToHumanOpponent(Team announcerTeam)
        {
            if (!NetworkServer.active || announcerTeam == null) return false;

            var seatMgr = SeatManager.Instance;
            if (currentAnnouncerSeat >= 0 && seatMgr != null && seatMgr.allChairs.Count > 0)
            {
                int count = seatMgr.allChairs.Count;
                for (int step = 1; step <= count; step++)
                {
                    var occupant = seatMgr.allChairs[(currentAnnouncerSeat + step) % count].occupant;
                    if (occupant == null) continue;

                    var p = occupant.GetComponent<Code.Player.Player>();
                    if (p == null || p.team == null || p.team.teamName == announcerTeam.teamName) continue;

                    var netSync = occupant.GetComponent<PlayerNetworkSync>();
                    if (netSync != null && ShowResponseTo(netSync)) return true;
                }
            }

            foreach (var conn in NetworkServer.connections.Values)
            {
                var identity = conn.identity;
                if (identity == null) continue;

                var p = identity.GetComponent<Code.Player.Player>();
                if (p == null || p.team == null || p.team.teamName == announcerTeam.teamName) continue;

                var netSync = identity.GetComponent<PlayerNetworkSync>();
                if (netSync != null && ShowResponseTo(netSync)) return true;
            }
            return false;
        }

        /// <summary>Server-side: opens the response bar on this player's machine.</summary>
        private bool ShowResponseTo(PlayerNetworkSync netSync)
        {
            var conn = netSync.connectionToClient;
            if (conn == null) return false;

            if (conn is LocalConnectionToClient)
                ShowRespondButtons();
            else
                netSync.TargetShowResponseButtons(conn, (int)_announceState, currentAnnouncerName);
            return true;
        }

        /// <summary>Server-side: does this player's dealt hand really contain flor?
        /// haveFlower no sirve acá: solo se setea en la máquina dueña de la mano.
        /// Una flor ya quemada cuenta como "no tiene".</summary>
        private static bool PlayerHandHasFlor(GameObject playerObj)
        {
            var pl = playerObj.GetComponent<PlayerLocal>();
            if (pl == null || pl.player == null || pl.player.florBurned) return false;

            var hand = pl.cardsHandler != null ? pl.cardsHandler.InitialHand : null;
            if (hand == null || hand.Count < 3 || DeckCreator.Instance == null) return false;

            return TrucoRules.IsFlor(hand, DeckCreator.Instance.cardVira);
        }

        /// <summary>Cantar envido teniendo flor la quema: deja de existir para los botones
        /// y para el puntaje de fin de mano (FlorAnnouncement saltea florBurned).</summary>
        private void BurnFlor(Code.Player.Player announcer)
        {
            announcer.haveFlower = false;
            announcer.florBurned = true;
            StartCoroutine(NotifyBurnedFlor(announcer.playerName));
        }

        private System.Collections.IEnumerator NotifyBurnedFlor(string playerName)
        {
            // Después del cartel "CANTA ENVIDO" (2.5s) para que no se pisen.
            yield return new WaitForSeconds(2.6f);
            if (PlayerHUD.Instance != null)
                PlayerHUD.Instance.NotifyEvent($"¡{playerName.ToUpper()} QUEMA SU FLOR!", 2f);
        }

        /// <summary>Server-side: re-enables the turn of whoever should be playing after an
        /// announcement resolves, both locally and on the owning client.</summary>
        private void RestoreTurnAfterAnnouncement()
        {
            var gm = GameManager.Instance;
            if (gm == null || SeatManager.Instance == null) return;
            if (gm.currentPlayerTurn < 0 || gm.currentPlayerTurn >= SeatManager.Instance.allChairs.Count) return;

            var occupant = SeatManager.Instance.allChairs[gm.currentPlayerTurn].occupant;
            if (occupant == null) return;

            var p = occupant.GetComponent<Code.Player.Player>();
            if (p == null) return;

            p.canPlayCard = true;
            if (NetworkServer.active)
                occupant.GetComponent<PlayerNetworkSync>()?.RpcSetTurn(true);
        }

        // [Command(requiresAuthority = false)]
        private void ButtonInteract(string buttonName, PlayerLocal dummy, string playerName, bool hasFlor, int extraStones = -1)
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
                    StartCoroutine(MoreAnnounceCoroutine(playerName, hasFlor, extraStones));
                    break;

                default:
                    break;
            }
        }

        private System.Collections.IEnumerator MoreAnnounceCoroutine(string playerName, bool hasFlor, int extraStones = -1)
        {
            var current = GetCurrentAnnounce();
            if (current != null)
            {
                // Si es Envido, ACUMULAMOS las piedras extra de este re-envido. Cuando el
                // re-envido viene de un cliente remoto, las piedras llegan por parámetro
                // (el slider del HUD local es el del server y no sirve).
                if (current is EnvidoAnnouncement envido)
                {
                    int stones = extraStones >= 0
                        ? extraStones
                        : (PlayerHUD.Instance != null ? PlayerHUD.Instance.CurrentSliderValue : 0);
                    envido.extraPoints += stones;
                    // Re-envidar acepta el re-envido anterior: solo este queda pendiente.
                    envido.pendingRaiseStones = stones;
                }

                current.IncreaseAcceptAmount();
                // Envido: NO recalcular el total acá. UpdateTotalScore encola la
                // resolución en el GameManager como si el re-envido ya estuviera
                // querido; si después responden "No quiero" se cobraría doble (los 2
                // del rechazo + el total pendiente a fin de mano). Solo se encola al
                // aceptar (AcceptAnnounceCoroutine). Truco sí lo necesita: subir la
                // apuesta implica aceptar el nivel anterior (currentHandValue).
                if (!(current is EnvidoAnnouncement))
                    current.UpdateTotalScore();
                PlayAnnounceSFX(_announceState, current.acceptAmount);

                if (_announceState == AnnounceState.Envido)
                    UpdateEnvidoStakeUI(ProspectiveEnvidoStake(), true);
            }

            // El emisor del re-envido es el jugador que presionó el botón (el humano)
            currentAnnouncerName = playerName;
            currentAnnouncerSeat = FindSeatByPlayerName(playerName);
            string teamSuffix = "";
            var playerLocal = FindAnyObjectByType<PlayerLocal>();
            if (playerLocal != null && playerLocal.player != null && playerLocal.player.team != null)
            {
                teamSuffix = $" ({playerLocal.player.team.teamName})";
            }

            string actionText = "RE-ENVIDA";
            if (_announceState == AnnounceState.Truco)
            {
                var trucoAnnounce = GetCurrentAnnounce() as TrucoAnnouncement;
                int level = (trucoAnnounce != null) ? trucoAnnounce.acceptAmount : 1;
                actionText = level switch
                {
                    1 => "PIDE RETRUCO",
                    2 => "PIDE VALE 9",
                    3 => "PIDE VALE PARTIDA",
                    _ => "PIDE RETRUCO"
                };
            }
            else if (_announceState == AnnounceState.Flor) actionText = "PIDE CONTRAFLOR";

            if (PlayerHUD.Instance != null)
            {
                PlayerHUD.Instance.NotifyEvent($"¡{playerName.ToUpper()}{teamSuffix.ToUpper()} {actionText.ToUpper()}!", 2.5f);
                PlayerHUD.Instance.ShowResponseButtons(false); // Ocultar botones de respuesta para el humano
            }

            GameManager.Instance.isAnnouncementPending = true;

            // Esperar a que la notificación de re-canto termine
            yield return new WaitForSeconds(4.5f);

            // El re-cantor pasa a ser el "anunciante" vigente (importante para resolver un No Quiero)
            var raiserTeam = FindTeamByPlayerName(playerName);
            if (raiserTeam != null) currentAnnouncerTeam = raiserTeam;

            // Notificar al oponente (NPC en singleplayer, humano en multiplayer)
            GetOpponentPlayer(out var opponent);
            if (opponent != null)
            {
                var npc = opponent.GetComponent<NPCPlayer>();
                if (npc != null) npc.HandleOpponentAnnounce(_announceState, gameObject);
            }
            else if (!TryShowResponseToHumanOpponent(currentAnnouncerTeam))
            {
                GameManager.Instance.isAnnouncementPending = false;
            }
        }

        private static int FindSeatByPlayerName(string playerName)
        {
            var seatMgr = SeatManager.Instance;
            if (seatMgr == null) return -1;
            for (int i = 0; i < seatMgr.allChairs.Count; i++)
            {
                var occ = seatMgr.allChairs[i].occupant;
                if (occ == null) continue;
                var p = occ.GetComponent<Code.Player.Player>();
                if (p != null && p.playerName == playerName) return i;
                var npc = occ.GetComponent<NPCPlayer>();
                if (npc != null && npc.playerName == playerName) return i;
            }
            return -1;
        }

        private Team FindTeamByPlayerName(string playerName)
        {
            if (GameManager.Instance == null) return null;
            foreach (var p in GameManager.Instance.allPlayers)
                if (p != null && p.playerName == playerName) return p.team;
            foreach (var n in GameManager.Instance.npcs)
                if (n != null && n.playerName == playerName) return n.team;
            return null;
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
                    // El equipo "dueño" del truco aceptado es siempre el del anunciante vigente
                    if (currentAnnouncerTeam != null)
                    {
                        GameManager.Instance.lastTrucoTeamIndex = GameManager.Instance.teams.IndexOf(currentAnnouncerTeam) + 1;
                    }
                    else
                    {
                        var playerLocal = GetLocalPlayerLocal();
                        if (playerName == playerLocal?.player?.playerName)
                        {
                            GetOpponentPlayer(out var opponent);
                            var npc = opponent?.GetComponent<NPCPlayer>();
                            if (npc != null && npc.team != null)
                                GameManager.Instance.lastTrucoTeamIndex = GameManager.Instance.teams.IndexOf(npc.team) + 1;
                        }
                        else if (playerLocal?.player?.team != null)
                        {
                            GameManager.Instance.lastTrucoTeamIndex = GameManager.Instance.teams.IndexOf(playerLocal.player.team) + 1;
                        }
                    }
                }

                current.IncreaseAcceptAmount();
                current.UpdateTotalScore();

                // Envido querido: todo lo apostado queda aceptado; mostrar el total final
                // en juego (se cobra al final de la mano).
                if (current is EnvidoAnnouncement acceptedEnvido && GameManager.Instance != null)
                {
                    acceptedEnvido.pendingRaiseStones = 0;
                    UpdateEnvidoStakeUI(GameManager.Instance.pendingEnvidoPoints, true);
                }

                // Truco querido: los clientes necesitan el dueño y el nivel para que su
                // botón muestre RETRUCO/VALE 9 y respete quién puede subir.
                if (current is TrucoAnnouncement acceptedTruco && NetworkServer.active && GameManager.Instance != null)
                {
                    (NetworkManager.singleton as MyNetworkingManager)?.BroadcastTrucoState(
                        GameManager.Instance.lastTrucoTeamIndex, acceptedTruco.acceptAmount, true);
                }
            }

            _announceState = AnnounceState.None;
            GameManager.Instance.isAnnouncementPending = false;

            // Restaurar permiso de juego a quien le toca (local y, en multiplayer, su cliente)
            RestoreTurnAfterAnnouncement();
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
                    int points = 1;
                    var envido = GetComponentsInChildren<Announce>().OfType<EnvidoAnnouncement>().FirstOrDefault();
                    if (envido != null)
                    {
                        int idx = envido.acceptAmount;
                        if (idx >= 0 && idx < envido.IncreasingAmount().Length)
                        {
                            points = envido.IncreasingAmount()[idx];
                        }

                        // "No quiero" paga lo último aceptado: base + las piedras de la
                        // cadena MENOS las del re-envido rechazado (nunca se aceptó).
                        if (idx > 0)
                            points += Mathf.Max(0, envido.extraPoints - envido.pendingRaiseStones);
                    }
                    GameManager.Instance.AddAnnouncementPoints(announcerTeam.teamName, points);
                }

                // Envido rechazado: ya se cobró, el indicador desaparece
                UpdateEnvidoStakeUI(0, false);
            }
            _announceState = AnnounceState.None;
            GameManager.Instance.isAnnouncementPending = false;

            // Restaurar permiso de juego a quien le toca (local y, en multiplayer, su cliente)
            RestoreTurnAfterAnnouncement();
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
