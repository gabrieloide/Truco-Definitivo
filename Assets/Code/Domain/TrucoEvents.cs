using System;
using Code.GameLogic;
using UnityEngine;

namespace Code.Domain
{
    /// <summary>
    /// Event Bus desacoplado para el ciclo de vida del Truco.
    /// Permite que la Vista (PlayerHUD, Audio, VFX) escuche el Dominio sin acoplamientos rígidos.
    /// </summary>
    public static class TrucoEvents
    {
        // ==========================================
        // Eventos de Flujo de Partida y Bazas
        // ==========================================
        public static event Action<int, Card> OnCardPlayed;
        public static event Action<int, GameObject> OnTurnChanged;
        public static event Action<int, Card> OnHandStarted;
        public static event Action<int, int, int> OnHandEnded; // winningTeam, score1, score2
        public static event Action<int> OnMatchEnded; // winningTeam

        // ==========================================
        // Eventos de Puntuación
        // ==========================================
        public static event Action<int, int> OnScoreChanged; // teamIndex, newScore
        public static event Action<int, int, int, int> OnScoresUpdated; // team1Score, team2Score, rounds1, rounds2

        // ==========================================
        // Eventos de Cantos (Announcements)
        // ==========================================
        public static event Action<AnnounceState, int, int> OnAnnouncementRaised; // state, announcerSeat, stake
        public static event Action<AnnounceState, ResponseType, int, int> OnAnnouncementResolved; // state, response, pointsAwarded, winningTeam
        public static event Action<AnnounceState, int, string> OnResponseRequired; // state, responderSeat, responderName

        // ==========================================
        // Eventos de Presentación / UI / Audio
        // ==========================================
        public static event Action<string, float> OnNotificationMessage; // message, duration
        public static event Action<string> OnAudioRequested; // sfxId

        // ==========================================
        // Emisores Seguros (Safe Emitters)
        // ==========================================
        public static void EmitCardPlayed(int seatIndex, Card card)
        {
            OnCardPlayed?.Invoke(seatIndex, card);
        }

        public static void EmitTurnChanged(int seatIndex, GameObject playerObj)
        {
            OnTurnChanged?.Invoke(seatIndex, playerObj);
        }

        public static void EmitHandStarted(int handNumber, Card vira)
        {
            OnHandStarted?.Invoke(handNumber, vira);
        }

        public static void EmitHandEnded(int winningTeam, int team1Score, int team2Score)
        {
            OnHandEnded?.Invoke(winningTeam, team1Score, team2Score);
        }

        public static void EmitMatchEnded(int winningTeam)
        {
            OnMatchEnded?.Invoke(winningTeam);
        }

        public static void EmitScoreChanged(int teamIndex, int newScore)
        {
            OnScoreChanged?.Invoke(teamIndex, newScore);
        }

        public static void EmitScoresUpdated(int team1Score, int team2Score, int rounds1, int rounds2)
        {
            OnScoresUpdated?.Invoke(team1Score, team2Score, rounds1, rounds2);
        }

        public static void EmitAnnouncementRaised(AnnounceState state, int announcerSeat, int stake)
        {
            OnAnnouncementRaised?.Invoke(state, announcerSeat, stake);
        }

        public static void EmitAnnouncementResolved(AnnounceState state, ResponseType response, int pointsAwarded, int winningTeam)
        {
            OnAnnouncementResolved?.Invoke(state, response, pointsAwarded, winningTeam);
        }

        public static void EmitResponseRequired(AnnounceState state, int responderSeat, string responderName)
        {
            OnResponseRequired?.Invoke(state, responderSeat, responderName);
        }

        public static void EmitNotification(string message, float duration = 2.5f)
        {
            OnNotificationMessage?.Invoke(message, duration);
        }

        public static void EmitAudio(string sfxId)
        {
            OnAudioRequested?.Invoke(sfxId);
        }
    }
}
