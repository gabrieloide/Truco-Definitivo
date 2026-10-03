using System;
using System.Collections.Generic;
using Code.Cards;
using Code.GameLogic;
using UnityEngine;

namespace Code.Networking
{
    /// <summary>
    /// Información guardada en el Servidor (Host) sobre un jugador que se desconectó
    /// durante una partida en curso. Permite restaurar su asiento, equipo y mano si vuelve a entrar.
    /// </summary>
    [Serializable]
    public class DisconnectedPlayerSession
    {
        public string playerId;
        public string playerName;
        public int seatIndex;
        public int teamIndex;
        public List<Card> cardsInHand = new List<Card>();
        public float disconnectTime;
        [NonSerialized] public Coroutine timeoutCoroutine;
    }
}
