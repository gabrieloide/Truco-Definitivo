using System;
using Code.Cards;
using Code.GameLogic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace Code.Player
{
    public class PlayerLocal : NetworkBehaviour
    {
        public Player player;
        [HideInInspector] public CardsHandler cardsHandler;
        [HideInInspector] public PlayerControllers playerControllers;
        [FormerlySerializedAs("announcementSystem")] [HideInInspector] public AnnouncementManager announcementManager;


        private void Awake()
        {
            if (cardsHandler == null)
                cardsHandler = GetComponent<CardsHandler>();

            if (playerControllers == null)
                playerControllers = gameObject.AddComponent<PlayerControllers>();

            if (player == null)
                player = GetComponent<Player>();
            

            player.playerName = $"player{GameManager.Instance.playerCount}";
            player.team.teamName = GameManager.Instance.teams[GameManager.Instance.playerCount].teamName;


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

            PlayerHUD.Instance.ChangeCurrentTurnText(player.canPlayCard);
        }

        [Command]
        private void CmdRequestPlayerFromServer()
        {
            Debug.Log("This message is to the server");

            // Check if GameManager.Instance is null
            if (GameManager.Instance == null)
            {
                Debug.LogError("GameManager.Instance is null");
                return;
            }

            // Check if serverPlayers is null
            if (GameManager.Instance.serverPlayers == null)
            {
                Debug.LogError("GameManager.Instance.serverPlayers is null");
                return;
            }

            foreach (var localPlayer in GameManager.Instance.serverPlayers)
            {
                // Check if localPlayer is null
                if (localPlayer == null)
                {
                    Debug.LogError("localPlayer is null");
                    continue;
                }

                // Check if player component is null
                if (localPlayer.player == null)
                {
                    Debug.LogError($"player component is null for localPlayer");
                    continue;
                }


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
            player.playerName = playerName;
            player.team.teamName = teamName;
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
            player.canPlayCard = false;
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
                GameManager.Instance.currentPlayerTurn = 0;
                GameManager.Instance.round++;
            }
        }
        
        [Command]
        public void CmdAddCardToTheTable(Card card)
        {
            TableManager.Instance.CardsInTable.Add(card);
        }

        [TargetRpc]
        public void RpcRequestChangeTurn(NetworkConnection conn, bool turn)
        {
            player.canPlayCard = turn;
        }
    }
}