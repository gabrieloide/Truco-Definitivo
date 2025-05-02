using System;
using System.Collections.Generic;
using Code.GameLogic;
using Code.GameLogic.Announcement;
using Mirror;
using UnityEngine;
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

        private void Start()
        {
            foreach (var t in _announcement)
            {
                var announce = Instantiate(_allAnnouncement, transform);
                announce.name = t;
                announce.AddComponent(_announceComponents[t]);
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
            var envido = gameObject.GetComponentInChildren<EnvidoAnnouncement>();

            if (envido.announceEnvidoButton.activeSelf)
                envido.announceEnvidoButton.SetActive(false);
        }

        [TargetRpc]
        private void RpcSendToOpponent(NetworkConnection conn)
        {
            var envido = gameObject.GetComponentInChildren<EnvidoAnnouncement>();
            var localPlayer = NetworkClient.localPlayer;

            foreach (var t in envido.envidoButtons)
            {
                t.SetActive(true);
            }

            Debug.Log($"i am the opponent player {localPlayer.GetComponent<PlayerLocal>().player.playerName}");
        }


        //Accept or Decline Buttons
        public void LocalButtonInteract(string buttonName)
        {
            var playerGameObject = NetworkClient.localPlayer.gameObject;
            var playerLocal = playerGameObject.GetComponent<PlayerLocal>();
            var envido = gameObject.GetComponentInChildren<EnvidoAnnouncement>();

            var opponent = GameManager.Instance.GetOpponentTeam(playerGameObject)[0].GetComponent<PlayerLocal>();

            Debug.Log(
                $"my name is {playerLocal.player.playerName} and the opponent is {opponent.player.playerName}");

            ButtonInteract(buttonName, opponent, playerGameObject.GetComponent<PlayerLocal>().player.playerName);

            for (int i = 0; i < envido.envidoButtons.Length; i++)
            {
                envido.envidoButtons[i].SetActive(false);
            }
        }

        [Command(requiresAuthority = false)]
        private void ButtonInteract(string buttonName, PlayerLocal localPlayer, string playerName)
        {
            switch (buttonName)
            {
                case "AcceptEnvidoButton":
                    AcceptAnnounce(localPlayer.connectionToClient, playerName);
                    break;

                case "DeclineEnvidoButton":
                    DeclineAnnounce();
                    Debug.Log($"{playerName} has declined the envido");
                    break;

                default:
                    Debug.Log("no button was pressed");
                    break;
            }
        }

        [TargetRpc]
        private void AcceptAnnounce(NetworkConnection conn, string playerAcceptedName)
        {
            Debug.Log($"the player {playerAcceptedName} has accepted the envido");
            var envido = gameObject.GetComponentInChildren<EnvidoAnnouncement>();

            for (int i = 0; i < envido.envidoButtons.Length; i++)
            {
                envido.envidoButtons[i].SetActive(true);
            }
        }


        [ClientRpc]
        private void DeclineAnnounce()
        {
        }
    }
}