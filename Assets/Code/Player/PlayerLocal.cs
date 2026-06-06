using System;
using Code.Cards;
using Code.GameLogic;
// using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace Code.Player
{
    public class PlayerLocal : MonoBehaviour
    {
        public bool isLocalPlayer = true;
        public bool isServer = true;
        public Player player;
        [HideInInspector] public CardsHandler cardsHandler;
        [HideInInspector] public PlayerControllers playerControllers;
        [FormerlySerializedAs("announcementSystem")] [HideInInspector] public AnnouncementManager announcementManager;
        [HideInInspector] public CardInteraction selectedCardInteraction;


        private void Awake()
        {
            if (cardsHandler == null)
                cardsHandler = GetComponent<CardsHandler>();

            if (playerControllers == null)
                playerControllers = gameObject.AddComponent<PlayerControllers>();
            
            // Ensure interaction and movement are added if missing
            if (GetComponent<PlayerInteract>() == null)
                gameObject.AddComponent<PlayerInteract>();
            
            if (GetComponent<PlayerMovement3D>() == null)
                gameObject.AddComponent<PlayerMovement3D>();

            if (player == null)
                player = GetComponent<Player>();
            
            // Teams and names are now assigned by GameManager.RunOnlyOnce in singleplayer.
            // In multiplayer, this might need further adjustment, but for now we prioritize stability.
            if (player.playerName == null || player.playerName == "")
            {
                player.playerName = $"Player_{UnityEngine.Random.Range(100, 999)}";
            }

            cardsHandler.enabled = false;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (!isLocalPlayer)
                return;

            // Register player to game manager for solo mode
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddPlayerToServer(this);
            }
            else
            {
                Debug.LogError("[PlayerLocal] ERROR: No se encontró el GameManager al intentar registrarse.");
            }

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

        // [Command]
        private void CmdRequestPlayerFromServer()
        {

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


                string teamName = (localPlayer.player.team != null) ? localPlayer.player.team.teamName : "";
                RpcServerPlayerToClient(localPlayer, localPlayer.player.playerName, teamName);
            }
        }


        // [ClientRpc]
        private void RpcServerPlayerToClient(PlayerLocal localPlayer, string playerName, string teamName)
        {
            if (isServer)
                return;


            //debug player name and team name

            GameManager.Instance.serverPlayers.Add(localPlayer);
            player.playerName = playerName;
            
            if (player.team == null)
            {
                player.team = new Team(teamName);
            }
            else
            {
                player.team.teamName = teamName;
            }
        }

        private void InitializedHand()
        {
            var s = SceneManager.GetActiveScene();

            if (s.name != "GameScene") return;

            GameManager.Instance.isGameScene = true;
            cardsHandler.enabled = true;
        }

        // public override void OnStopClient()
        // {
        //     Destroy(gameObject);
        // }

        // [TargetRpc]
        public void RpcRequestChangeTurn(/*NetworkConnection conn,*/ bool turn)
        {
            player.canPlayCard = turn;
        }
    }
}
