using System;
using System.Collections.Generic;
using Code.GameLogic;
using Code.GameLogic.Announcement;
using Mirror;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

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
    public class AnnouncementManager : NetworkBehaviour
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

        [SyncVar] public AnnounceState _announceState = AnnounceState.None;
        public GameObject[] respondButtons;
        public string[] announcementPlayerNames = new string[2];

        [Header("UI Buttons")]
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button declineButton;
        [SerializeField] private Button moreAnnounceButton;

        private void Start()
        {
            respondButtons = new GameObject[3];
            foreach (var t in _announcement)
            {
                var announce = Instantiate(_allAnnouncement, transform);
                announce.name = t;
                announce.AddComponent(_announceComponents[t]);
            }

            InitializeRespondButtons();
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


        private void GetOpponentPlayer(out GameObject opponentPlayer)
        {
            var player = NetworkClient.localPlayer.GetComponent<PlayerLocal>();
            opponentPlayer = GameManager.Instance.GetOpponentTeam(player.gameObject)[0];
            if (GameManager.Instance.round != 0)
            {
                Debug.Log("You can only announce 'Envido' in the first round");
                return;
            }

            if (player == null || !player.player.canPlayCard)
            {
                Debug.LogError("You can't announce envido");
                return;
            }

            Debug.Log("Declaring");
        }


        /// <summary>
        /// Send from the local player to the target player that you want to declare an announcement and send to the
        /// all players to hide the buttons 
        /// </summary>
        public void SendAnnounceToClient(string ButtonName)
        {
            GetOpponentPlayer(out var opponent);
            Debug.Log($"The {ButtonName} state has started");
            if (opponent == null)
            {
                Debug.Log($"the opponents is null {opponent}");
                return;
            }

            _announceState = ButtonName switch
            {
                "EnvidoButton" => AnnounceState.Envido,
                "TrucoButton" => AnnounceState.Truco,
                "FlorButton" => AnnounceState.Flor,
                "ALeyButton" => AnnounceState.ALey,
                _ => _announceState
            };

            CmdAnnounceToServer(opponent);
        }

        [Command(requiresAuthority = false)]
        private void CmdAnnounceToServer(GameObject opponent)
        {
            RpcAnnounceToAllClients();
            RpcSendToOpponent(opponent.GetComponent<PlayerLocal>().connectionToClient);
        }

        [ClientRpc]
        private void RpcAnnounceToAllClients()
        {
            var allAnnouncements = GetComponentsInChildren<Announce>();
            foreach (var announce in allAnnouncements)
            {
                if (announce.AnnounceButton().activeSelf)
                    announce.AnnounceButton().SetActive(false);
            }

            Debug.Log($"The {_announceState} has started");
        }

        [TargetRpc]
        private void RpcSendToOpponent(NetworkConnection conn)
        {
            foreach (var t in respondButtons)
            {
                t.SetActive(true);
            }
        }

        /// <summary>
        /// Accept announcement button 
        /// </summary>
        private void LocalButtonInteract(string buttonName)
        {
            var playerGameObject = NetworkClient.localPlayer.gameObject;

            var opponent = GameManager.Instance.GetOpponentTeam(playerGameObject)[0].GetComponent<PlayerLocal>();

            ButtonInteract(buttonName, opponent, playerGameObject.GetComponent<PlayerLocal>().player.playerName);

            foreach (var t in respondButtons)
            {
                t.SetActive(false);
            }
        }

        [Command(requiresAuthority = false)]
        private void ButtonInteract(string buttonName, PlayerLocal localPlayer, string playerName)
        {
            switch (buttonName)
            {
                case "AcceptButton":
                    AcceptAnnounce(localPlayer.connectionToClient, playerName);
                    
                    break;

                case "DeclineButton":
                    DeclineAnnounce(playerName);

                    break;
                case "MoreAnnounceButton":
                    MoreAnnounce(localPlayer.connectionToClient);
                    gameObject.GetComponentInChildren<Announce>().IncreaseAcceptAmount();
                    break;

                default:
                    Debug.Log("no button was pressed");
                    break;
            }
        }

        [TargetRpc]
        private void AcceptAnnounce(NetworkConnection conn, string playerName)
        {
            Debug.Log($"the player {playerName} has accepted the announce");
            gameObject.GetComponentInChildren<Announce>().UpdateTotalScore();
        }

        [ClientRpc]
        private void DeclineAnnounce(string playerName)
        {
            Debug.Log($"{playerName} has declined the envido");
        }

        [TargetRpc]
        private void MoreAnnounce(NetworkConnection conn)
        {
            gameObject.GetComponentInChildren<Announce>().UpdateTotalScore();
            
            foreach (var t in respondButtons)
            {
                t.SetActive(true);
            }
        }
    }
}