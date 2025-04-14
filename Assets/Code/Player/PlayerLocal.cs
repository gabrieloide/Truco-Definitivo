using System;
using Code.Cards;
using Code.GameLogic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Code.Player
{
    public class PlayerLocal : NetworkBehaviour
    {
        [HideInInspector] public Player player;
        [HideInInspector] public CardsHandler cardsHandler;
        [HideInInspector] public PlayerControllers playerControllers;
        [HideInInspector] public AnnouncementSystem announcementSystem;
        public bool canPlayCard = false;

        private void Awake()
        {
            if (cardsHandler == null)
                cardsHandler = GetComponent<CardsHandler>();

            if (playerControllers == null)
                playerControllers = gameObject.AddComponent<PlayerControllers>();

            if (announcementSystem == null)
                announcementSystem = gameObject.AddComponent<AnnouncementSystem>();
            
            if (player == null)
                player = gameObject.AddComponent<Player>();


            cardsHandler.enabled = false;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (!isLocalPlayer)
                return;


            CmdRequestPlayerFromServer();
        }


        private void Update()
        {
            if (!isLocalPlayer)
                return;

            InitializedHand();

            if (FindAnyObjectByType<PlayerHUD>() == null)
                return;
            if (player == null)
                return;
            PlayerHUD.Instance.ChangeCurrentTurnText(canPlayCard);
        }

        [Command]
        private void CmdRequestPlayerFromServer()
        {
            Debug.Log("This message is to the server");
            foreach (var localPlayer in GameManager.Instance.serverPlayers)
            {
                RpcServerPlayerToClient(localPlayer, localPlayer.player.playerName, localPlayer.player.team.teamName);
                Debug.Log("[SERVER] Player name: " + localPlayer.player.playerName);
                Debug.Log("[SERVER] Team name: " + localPlayer.player.team.teamName);
            }
        }

        [ClientRpc]
        private void RpcServerPlayerToClient(PlayerLocal localPlayer, string playerName, string teamName)
        {
            if (isServer)
                return;

            Debug.Log("Adding players to the local");

            //debug player name and team name
            Debug.Log("Player name: " + playerName);
            Debug.Log("Team name: " + teamName);

            GameManager.Instance.serverPlayers.Add(localPlayer);
            //player.playerName = playerName;
            //player.team.teamName = teamName;
        }

        private void InitializedHand()
        {
            var s = SceneManager.GetActiveScene();

            if (s.name != "GameScene") return;

            GameManager.Instance.isGameScene = true;
            cardsHandler.enabled = true;
        }

        public override void OnStopClient()
        {
            Destroy(gameObject);
        }

        [Command]
        public void CmdIncreaseTurn(int cardPosition)
        {
            canPlayCard = false;
            LastCard();
            cardsHandler.RpcMoveCard(new Vector3(0, 0, 0), cardPosition);
        }

        [ClientRpc]
        private void LastCard()
        {
            GameManager.Instance.currentPlayerTurn++;

            if (GameManager.Instance.currentPlayerTurn == GameManager.Instance.playerCount)
            {
                TableManager.Instance.DetermineHighestCard();
            }

            if (GameManager.Instance.currentPlayerTurn == GameManager.Instance.playerCount)
                GameManager.Instance.currentPlayerTurn = 0;
        }

        [TargetRpc]
        public void RpcRequestChangeTurn(NetworkConnection conn, bool turn)
        {
            canPlayCard = turn;
        }
    }

    [Serializable]
    public class Team
    {
        public string teamName;
        public int teamScore;

        public Team(string teamName)
        {
            this.teamName = teamName;
        }
    }
}