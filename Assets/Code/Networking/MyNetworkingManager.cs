using System;
using Code.Cards;
using Code.GameLogic;
using Code.Networking;
using Mirror;
using UnityEngine;

public class MyNetworkingManager : NetworkManager
{
    public override void OnStartHost()
    {
        base.OnStartHost();
        Debug.Log("OnStartHost");
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        base.OnServerAddPlayer(conn);
        var count = GameManager.Instance.serverPlayers.Count + 1;

        var player = new Player(
            playerName: $"player{count}",
            turnNumber: count,
            conn: conn
            );

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
}