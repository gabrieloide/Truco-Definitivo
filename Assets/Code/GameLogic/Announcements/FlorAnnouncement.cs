using System;
using System.Collections.Generic;
using System.Linq;
using Code.Player;
using Code.Cards;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

namespace Code.GameLogic.Announcement
{
    public class FlorAnnouncement : Announce
    {
        public override GameObject AnnounceButton() => null; // Handled by PlayerHUD and UI Toolkit
        protected override AnnounceState AnnounceState() => global::AnnounceState.Flor;

        public override int[] IncreasingAmount() => new[] { 3, 3, 3 }; 

        // UI handling is now managed by PlayerHUD and AnnouncementManager

        public void CanDeclareFlower()
        {
            var allPlayers = FindObjectsByType<PlayerLocal>(FindObjectsSortMode.None);
            var playerLocal = allPlayers.FirstOrDefault(p => p.isLocalPlayer && p.gameObject.activeInHierarchy);
            if (playerLocal == null) return;

            var cardsHandler = playerLocal.cardsHandler;

            if (cardsHandler.InitialHand == null || cardsHandler.InitialHand.Count < 3)
            {
                playerLocal.player.haveFlower = false;
                if (PlayerHUD.Instance != null) PlayerHUD.Instance.RefreshActionButtons(playerLocal.player.canPlayCard);
                return;
            }

            var deckCreator = DeckCreator.Instance;
            if (deckCreator == null) return;

            bool isFlor = TrucoRules.IsFlor(cardsHandler.InitialHand, deckCreator.cardVira);
            playerLocal.player.haveFlower = isFlor;

            if (isFlor) Debug.Log($"[Flor] El jugador tiene FLOR.");

            if (PlayerHUD.Instance != null)
            {
                PlayerHUD.Instance.RefreshActionButtons(playerLocal.player.canPlayCard);
            }
        }


        private class FlorData
        {
            public string TeamName;
            public int Points;
            public int ComparisonScore;
            public string PlayerName;
        }

        public override void UpdateTotalScore()
        {
            var deckCreator = DeckCreator.Instance;
            if (deckCreator == null) return;
            var vira = deckCreator.cardVira;

            List<FlorData> allFlores = new List<FlorData>();

            var allPlayers = FindObjectsByType<Code.Player.Player>(FindObjectsSortMode.None);
            var allNpcs = FindObjectsByType<NPCPlayer>(FindObjectsSortMode.None);

            foreach (var p in allPlayers)
            {
                var cardsHandler = p.GetComponent<CardsHandler>();
                if (cardsHandler == null) cardsHandler = p.GetComponentInChildren<CardsHandler>();
                
                if (cardsHandler != null && cardsHandler.InitialHand.Count >= 3)
                {
                    if (TrucoRules.IsFlor(cardsHandler.InitialHand, vira))
                    {
                        allFlores.Add(new FlorData
                        {
                            TeamName = p.team != null ? p.team.teamName : "Team 1",
                            Points = TrucoRules.CalculateFlorPoints(cardsHandler.InitialHand, vira),
                            ComparisonScore = TrucoRules.CalculateFlorComparisonScore(cardsHandler.InitialHand, vira),
                            PlayerName = p.playerName
                        });
                    }
                }
            }

            foreach (var npc in allNpcs)
            {
                if (npc.initialHand.Count >= 3 && TrucoRules.IsFlor(npc.initialHand, vira))
                {
                    allFlores.Add(new FlorData
                    {
                        TeamName = npc.team != null ? npc.team.teamName : "Team 2",
                        Points = TrucoRules.CalculateFlorPoints(npc.initialHand, vira),
                        ComparisonScore = TrucoRules.CalculateFlorComparisonScore(npc.initialHand, vira),
                        PlayerName = npc.playerName
                    });
                }
            }

            if (allFlores.Count == 0)
            {
                return;
            }

            var team1Flores = allFlores.FindAll(f => f.TeamName == "Team 1");
            var team2Flores = allFlores.FindAll(f => f.TeamName == "Team 2");


            if (team1Flores.Count > 0 && team2Flores.Count == 0)
            {
                // Solo el Equipo 1 tiene Flor
                int totalPoints = 0;
                foreach (var f in team1Flores) totalPoints += f.Points;
                GameManager.Instance.AddAnnouncementPoints("Team 1", totalPoints);
            }
            else if (team2Flores.Count > 0 && team1Flores.Count == 0)
            {
                // Solo el Equipo 2 tiene Flor
                int totalPoints = 0;
                foreach (var f in team2Flores) totalPoints += f.Points;
                GameManager.Instance.AddAnnouncementPoints("Team 2", totalPoints);
            }
            else if (team1Flores.Count > 0 && team2Flores.Count > 0)
            {
                // CONTRAFLOR: Ambos equipos tienen Flor

                FlorData bestTeam1 = team1Flores[0];
                foreach (var f in team1Flores)
                {
                    if (f.ComparisonScore > bestTeam1.ComparisonScore) bestTeam1 = f;
                }

                FlorData bestTeam2 = team2Flores[0];
                foreach (var f in team2Flores)
                {
                    if (f.ComparisonScore > bestTeam2.ComparisonScore) bestTeam2 = f;
                }

                string winnerTeam = "";
                if (bestTeam1.ComparisonScore > bestTeam2.ComparisonScore)
                {
                    winnerTeam = "Team 1";
                }
                else if (bestTeam2.ComparisonScore > bestTeam1.ComparisonScore)
                {
                    winnerTeam = "Team 2";
                }
                else
                {
                    // Empate en puntaje de Flor: Gana el equipo que es Mano
                    int manoTeamIndex = GameManager.Instance.ManoTeamIndex;
                    winnerTeam = "Team " + manoTeamIndex;
                }

                int totalPoints = 0;
                foreach (var f in allFlores) totalPoints += f.Points;

                // Notificar los puntajes comparados de Contraflor en la pantalla
                if (PlayerHUD.Instance != null)
                {
                    PlayerHUD.Instance.NotifyEvent($"CONTRAFLOR: TEAM 1 ({bestTeam1.ComparisonScore}) VS TEAM 2 ({bestTeam2.ComparisonScore})", 4.0f);
                }

                GameManager.Instance.AddAnnouncementPoints(winnerTeam, totalPoints);
            }
        }
    }
}
