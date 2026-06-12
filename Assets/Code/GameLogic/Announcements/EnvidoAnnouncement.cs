using System;
using System.Collections.Generic;
using Code.Player;
using Code.Cards;
using Mirror;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Code.GameLogic.Announcement
{
    public class EnvidoAnnouncement : Announce
    {
        public override GameObject AnnounceButton() => GameObject.Find("EnvidoButton");
        protected override AnnounceState AnnounceState() => global::AnnounceState.Envido;

        // Índices por acceptAmount: 0 = envido rechazado (1 piedra), 1+ = envido
        // querido (2 piedras fijas del que lo cantó). Los re-envidos NO agregan
        // puntos base propios: solo aportan las piedras extra que ponga cada cantor
        // (acumuladas en extraPoints). Ej: envido → re-envido +3 → quiero = 2+3 = 5.
        public override int[] IncreasingAmount() => new[] { 1, 2, 2, 2, 2, 2 };

        // UI handling is now managed by PlayerHUD and AnnouncementManager
        public int extraPoints = 0; // Piedras extra acumuladas de todos los re-envidos

        // Piedras del último re-envido todavía no aceptado. Re-envidar por encima de un
        // re-envido implica aceptarlo, así que esto siempre es solo el último de la
        // cadena. Un "No quiero" paga lo último aceptado: extraPoints - este valor.
        public int pendingRaiseStones = 0;
        
        public override void UpdateTotalScore()
        {
            if (global::AnnounceState.Envido != global::AnnounceState.Envido) return; // Legacy check

            
            var deckCreator = DeckCreator.Instance;
            if (deckCreator == null) return;

            var vira = deckCreator.cardVira;
            
            // Calculamos puntos base + puntos extra del slider
            int basePoints = IncreasingAmount()[acceptAmount];
            int pointsToAward = basePoints + extraPoints;

            if (extraPoints > 0) Debug.Log($"[Envido] Resolviendo Envido: {basePoints} base + {extraPoints} extra = {pointsToAward} totales.");

            // Calculate scores for everyone
            int bestScoreTeam1 = -1;
            int bestScoreTeam2 = -1;

            var gm = GameManager.Instance;
            if (gm == null || gm.teams.Count < 2) return;

            // Get all players and NPCs
            var allPlayers = FindObjectsByType<Code.Player.Player>(FindObjectsSortMode.None);
            var allNpcs = FindObjectsByType<NPCPlayer>(FindObjectsSortMode.None);

            foreach (var p in allPlayers)
            {
                int score = 0;
                var cardsHandler = p.GetComponent<CardsHandler>();
                if (cardsHandler == null) cardsHandler = p.GetComponentInChildren<CardsHandler>();

                if (cardsHandler != null)
                {
                    // Una flor quemada (cantó envido teniéndola) no anula el envido:
                    // esas cartas tantean normal.
                    score = TrucoRules.CalculateEnvidoScore(cardsHandler.InitialHand, vira, p.florBurned);
                }

                // Si alguien tiene Flor (viva), el Envido se anula automáticamente
                if (score == -1)
                {
                    return;
                }

                // Por índice de equipo y no por nombre: en multiplayer los equipos se
                // renombran desde el lobby y "Team 1"/"Team 2" no matcheaba nunca
                // (ambos puntajes quedaban en -1 y el ganador no cobraba).
                int teamIdx = gm.GetTeamIndex(p.team);
                if (teamIdx == 0) bestScoreTeam1 = Mathf.Max(bestScoreTeam1, score);
                else if (teamIdx == 1) bestScoreTeam2 = Mathf.Max(bestScoreTeam2, score);
            }

            foreach (var npc in allNpcs)
            {
                int score = TrucoRules.CalculateEnvidoScore(npc.initialHand, vira);

                // Si alguien tiene Flor, el Envido se anula automáticamente
                if (score == -1)
                {
                    return;
                }

                int teamIdx = gm.GetTeamIndex(npc.team);
                if (teamIdx == 0) bestScoreTeam1 = Mathf.Max(bestScoreTeam1, score);
                else if (teamIdx == 1) bestScoreTeam2 = Mathf.Max(bestScoreTeam2, score);
            }


            string winnerTeam;
            if (bestScoreTeam1 > bestScoreTeam2) winnerTeam = gm.teams[0].teamName;
            else if (bestScoreTeam2 > bestScoreTeam1) winnerTeam = gm.teams[1].teamName;
            else
            {
                // Tie: Mano wins.
                winnerTeam = gm.teams[Mathf.Clamp(gm.ManoTeamIndex - 1, 0, 1)].teamName;
            }

            // GUARDAR EL RESULTADO PENDIENTE EN EL GAMEMANAGER EN LUGAR DE RESOLVERLO AHORA
            GameManager.Instance.pendingEnvidoResolution = true;
            GameManager.Instance.pendingEnvidoWinnerTeam = winnerTeam;
            GameManager.Instance.pendingEnvidoPoints = pointsToAward;
            GameManager.Instance.pendingEnvidoScoreTeam1 = bestScoreTeam1;
            GameManager.Instance.pendingEnvidoScoreTeam2 = bestScoreTeam2;
            
            // Opcional: Podríamos mostrar un mensaje muy sutil de "Envido Aceptado" sin revelar los puntos aún
            if (PlayerHUD.Instance != null)
            {
                PlayerHUD.Instance.NotifyEvent("ENVIDO ACEPTADO (Se resuelve al final de la mano)", 2.0f);
            }
        }

        Card[] TestingEnvido(PlayerLocal[] local)
        {
            Card[] allCards = new Card[6];
            int playerIndex = 0; 

            for (int i = 0; i < allCards.Length; i++)
            {
                playerIndex = i == 2 ? 0 : 1;
                // local might not have enough players depending on testing setup
                PlayerLocal owner = (local.Length > playerIndex) ? local[playerIndex] : null;
                
                allCards[i] = new Card();
                allCards[i].cardOwner = owner;
                if (owner != null) allCards[i].ownerObj = owner.gameObject;
                allCards[i].suit = "Gold";
                allCards[i].value = i + 1;
            }
            
            return  allCards;

        }
        public void GetHighestEnvido()
        {
            
            var announcementManager = FindAnyObjectByType<AnnouncementManager>();
            var cardPlayer = new Card[3, 3];
            var cardsInTable = TestingEnvido(GameManager.Instance.serverPlayers.ToArray());

            for (var i = 0; i < announcementManager.announcementPlayerNames.Length; i++)
            {
                var currentPlayerName = announcementManager.announcementPlayerNames[i];
                var cardIndex = 0;

                foreach (var card in cardsInTable)
                {
                    if (card.cardOwner != null && card.cardOwner.player != null && card.cardOwner.player.playerName == currentPlayerName)
                    {
                        if (cardIndex < 3)
                        {
                            cardPlayer[i, cardIndex] = card;
                            cardIndex++;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            
            for (int i = 0; i < cardPlayer.GetLength(0); i++)
            {
                for (int j = 0; j < cardPlayer.GetLength(1); j++)
                {
                    if (cardPlayer[i, j] != null && cardPlayer[i, j].cardOwner != null && cardPlayer[i, j].cardOwner.player != null)
                    {
                    }
                }
            }
        }
    }
}
