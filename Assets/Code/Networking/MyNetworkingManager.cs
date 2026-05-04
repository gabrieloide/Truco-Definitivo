using System;
using Code.Cards;
using Code.GameLogic;
using Code.Networking;
using Code.Player;
using Mirror;
using UnityEngine;

public class MyNetworkingManager : NetworkManager
{
    //Marian franco
    public override void OnStartHost()
    {
        base.OnStartHost();
        Debug.Log("OnStartHost");
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        SceneChanger.Instance.ChangeScene("LobbyScene");
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        base.OnServerAddPlayer(conn);
        var count = GameManager.Instance.serverPlayers.Count;

        var player = conn.identity.gameObject.AddComponent<Player>();
        var playerLocal = conn.identity.gameObject.GetComponent<PlayerLocal>();
        
        
        

        GameManager.Instance.AddPlayerToServer(playerLocal);

        if (playerLocal == null)
        {
            Debug.Log("Player controller is null");
            return;
        }

        Debug.Log("This is the client");

        FindAnyObjectByType<Lobby>().AddPlayerToLobby($"player{count}");
    }
}