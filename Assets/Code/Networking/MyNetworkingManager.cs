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
            return;
        }


        FindAnyObjectByType<Lobby>().AddPlayerToLobby($"player{count}");
    }
}
