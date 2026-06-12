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
            var deckCreator = DeckCreator.Instance;
            if (deckCreator == null) return;
            var vira = deckCreator.cardVira;

            var allPlayers = FindObjectsByType<PlayerLocal>(FindObjectsSortMode.None);
            var playerLocal = allPlayers.FirstOrDefault(p => p.isLocalPlayer && p.gameObject.activeInHierarchy);
            if (playerLocal != null)
            {
                var cardsHandler = playerLocal.cardsHandler;
                if (cardsHandler != null && cardsHandler.InitialHand != null && cardsHandler.InitialHand.Count >= 3)
                {
                    // Una flor quemada (cantó envido teniéndola) no vuelve a la vida.
                    bool isFlor = TrucoRules.IsFlor(cardsHandler.InitialHand, vira) && !playerLocal.player.florBurned;
                    playerLocal.player.haveFlower = isFlor;
                    if (isFlor) Debug.Log($"[Flor] El jugador tiene FLOR.");
                }
                else
                {
                    playerLocal.player.haveFlower = false;
                }
                if (PlayerHUD.Instance != null) PlayerHUD.Instance.RefreshActionButtons(playerLocal.player.canPlayCard);
            }

            // Evaluar Flor para todos los NPCs para que los chequeos de Envido funcionen correctamente
            var allNpcs = FindObjectsByType<NPCPlayer>(FindObjectsSortMode.None);
            foreach (var npc in allNpcs)
            {
                if (npc.initialHand != null && npc.initialHand.Count >= 3)
                {
                    npc.haveFlower = TrucoRules.IsFlor(npc.initialHand, vira);
                    if (npc.haveFlower) Debug.Log($"[Flor] El NPC {npc.playerName} tiene FLOR.");
                }
                else
                {
                    npc.haveFlower = false;
                }
            }
        }


        private class FlorData
        {
            public int TeamIndex; // Índice en GameManager.teams (los nombres cambian en multiplayer)
            public int Points;
            public int ComparisonScore;
            public string PlayerName;
        }

        private bool _pointsAwardedThisHand = false;

        public override void ResetAnnounceState()
        {
            base.ResetAnnounceState();
            _pointsAwardedThisHand = false;
        }

        public override void UpdateTotalScore()
        {
            var deckCreator = DeckCreator.Instance;
            if (deckCreator == null) return;
            var vira = deckCreator.cardVira;

            List<FlorData> allFlores = new List<FlorData>();

            var gm = GameManager.Instance;
            if (gm == null || gm.teams.Count < 2) return;
            var allPlayers = FindObjectsByType<Code.Player.Player>(FindObjectsSortMode.None);
            var allNpcs = FindObjectsByType<NPCPlayer>(FindObjectsSortMode.None);

            foreach (var p in allPlayers)
            {
                // La flor quemada (envido cantado con flor en mano) no vale nada.
                if (p.florBurned) continue;

                var cardsHandler = p.GetComponent<CardsHandler>();
                if (cardsHandler == null) cardsHandler = p.GetComponentInChildren<CardsHandler>();

                if (cardsHandler != null && cardsHandler.InitialHand.Count >= 3)
                {
                    if (TrucoRules.IsFlor(cardsHandler.InitialHand, vira))
                    {
                        // Por índice de equipo y no por nombre: en multiplayer los
                        // equipos se renombran y "Team 1"/"Team 2" no matcheaba nunca,
                        // así que la flor jamás sumaba puntos.
                        int teamIdx = gm.GetTeamIndex(p.team);
                        allFlores.Add(new FlorData
                        {
                            TeamIndex = teamIdx >= 0 ? teamIdx : 0,
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
                    int teamIdx = gm.GetTeamIndex(npc.team);
                    allFlores.Add(new FlorData
                    {
                        TeamIndex = teamIdx >= 0 ? teamIdx : 1,
                        Points = TrucoRules.CalculateFlorPoints(npc.initialHand, vira),
                        ComparisonScore = TrucoRules.CalculateFlorComparisonScore(npc.initialHand, vira),
                        PlayerName = npc.playerName
                    });
                }
            }

            if (allFlores.Count == 0 || _pointsAwardedThisHand)
            {
                return;
            }

            _pointsAwardedThisHand = true;

            var team1Flores = allFlores.FindAll(f => f.TeamIndex == 0);
            var team2Flores = allFlores.FindAll(f => f.TeamIndex == 1);


            if (team1Flores.Count > 0 && team2Flores.Count == 0)
            {
                // Solo el Equipo 1 tiene Flor
                int totalPoints = 0;
                foreach (var f in team1Flores) totalPoints += f.Points;
                gm.AddAnnouncementPoints(gm.teams[0].teamName, totalPoints);
                if (PlayerHUD.Instance != null)
                    PlayerHUD.Instance.NotifyEvent($"¡FLOR! {gm.teams[0].teamName.ToUpper()} GANA +{totalPoints} PIEDRAS", 3.5f);
            }
            else if (team2Flores.Count > 0 && team1Flores.Count == 0)
            {
                // Solo el Equipo 2 tiene Flor
                int totalPoints = 0;
                foreach (var f in team2Flores) totalPoints += f.Points;
                gm.AddAnnouncementPoints(gm.teams[1].teamName, totalPoints);
                if (PlayerHUD.Instance != null)
                    PlayerHUD.Instance.NotifyEvent($"¡FLOR! {gm.teams[1].teamName.ToUpper()} GANA +{totalPoints} PIEDRAS", 3.5f);
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

                int winnerIdx;
                if (bestTeam1.ComparisonScore > bestTeam2.ComparisonScore)
                {
                    winnerIdx = 0;
                }
                else if (bestTeam2.ComparisonScore > bestTeam1.ComparisonScore)
                {
                    winnerIdx = 1;
                }
                else
                {
                    // Empate en puntaje de Flor: Gana el equipo que es Mano
                    winnerIdx = Mathf.Clamp(gm.ManoTeamIndex - 1, 0, 1);
                }
                string winnerTeam = gm.teams[winnerIdx].teamName;

                int totalPoints = 0;
                foreach (var f in allFlores) totalPoints += f.Points;

                // Primero agregar los puntos (muestra notificación genérica brevemente)
                gm.AddAnnouncementPoints(winnerTeam, totalPoints);

                // Luego sobreescribir con la notificación detallada de Contraflor para que
                // el jugador entienda por qué ganó/perdió (de lo contrario la notificación
                // genérica de AddAnnouncementPoints tapaba esta info)
                if (PlayerHUD.Instance != null)
                {
                    PlayerHUD.Instance.NotifyEvent(
                        $"¡CONTRAFLOR! {winnerTeam.ToUpper()} GANA +{totalPoints} PIEDRAS  " +
                        $"(T1:{bestTeam1.ComparisonScore} vs T2:{bestTeam2.ComparisonScore})", 4.0f);
                }
            }
        }
    }
}
