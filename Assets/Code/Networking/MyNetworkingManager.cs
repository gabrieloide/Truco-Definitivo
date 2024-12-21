using System;
using Code.Cards;
using Code.GameLogic;
using Code.Networking;
using Mirror;
using Unity.VisualScripting;
using UnityEngine;

public class MyNetworkingManager : NetworkManager
{
    private NetworkHUD _networkHUD;

    public override void Start()
    {
        base.Start();
        _networkHUD = GetComponent<NetworkHUD>();
    }

    public override void OnStartHost()
    {
        base.OnStartHost();
        Debug.Log("OnStartHost");
    }

    public override void OnStopHost()
    {
        base.OnStopHost();
        _networkHUD.StopHost();
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        base.OnServerAddPlayer(conn);
        int count = GameManager.Instance.serverPlayers.Count + 1;

        Player player = new Player(
            playerName: $"player{count}",
            turnNumber: count,
            playerId: conn.connectionId);

        var playerController = conn.identity.gameObject.GetComponent<PlayerController>();
        GameManager.Instance.AddPlayerToServer(playerController);
        if (playerController == null)
        {
            Debug.Log("Player controller is null");
            return;
        }

        playerController.AssignPlayer(player);
        FindAnyObjectByType<Lobby>().AddPlayerToLobby($"player{count}");
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        Debug.Log("A player has disconnected");
    }
}