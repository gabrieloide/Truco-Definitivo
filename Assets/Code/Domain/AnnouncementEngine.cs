using System;
using System.Collections.Generic;
using Code.Cards;

namespace Code.Domain
{
    /// <summary>
    /// Motor de reglas de cantos de Truco Venezolano (Puro C#, sin dependencias de UnityEngine o Mirror).
    /// Altamente testeable y desacoplado del ciclo de vida de MonoBehaviour.
    /// </summary>
    public class AnnouncementEngine
    {
        // Valores de Truco: [0] = Normal (1), [1] = Truco (3), [2] = Retruco (6), [3] = Vale 9 (9), [4] = Vale Partida (30)
        public static readonly int[] TrucoStakes = { 1, 3, 6, 9, 30 };
        public static readonly string[] TrucoTierNames = { "Normal", "Truco", "Retruco", "Vale 9", "Vale Partida" };

        public AnnounceState CurrentAnnouncement { get; private set; } = AnnounceState.None;
        public int CurrentTrucoTier { get; private set; } = 0;
        public int LastTrucoCallerSeat { get; private set; } = -1;
        public int LastTrucoCallerTeam { get; private set; } = -1;

        public int EnvidoExtraPoints { get; private set; } = 0;
        public int PendingEnvidoRaise { get; private set; } = 0;
        public int EnvidoCallerSeat { get; private set; } = -1;

        public bool IsEnvidoResolved { get; private set; } = false;
        public bool IsFlorResolved { get; private set; } = false;

        public void ResetHand()
        {
            CurrentAnnouncement = AnnounceState.None;
            CurrentTrucoTier = 0;
            LastTrucoCallerSeat = -1;
            LastTrucoCallerTeam = -1;
            EnvidoExtraPoints = 0;
            PendingEnvidoRaise = 0;
            EnvidoCallerSeat = -1;
            IsEnvidoResolved = false;
            IsFlorResolved = false;
        }

        #region Truco Logic

        /// <summary>
        /// Determina si un equipo puede cantar o subir el Truco.
        /// </summary>
        public bool CanCallTruco(int teamIndex, int currentTier)
        {
            if (currentTier >= TrucoStakes.Length - 1) return false; // Ya en Vale Partida
            if (currentTier > 0 && LastTrucoCallerTeam == teamIndex) return false; // No te puedes autosubir
            return true;
        }

        /// <summary>
        /// Devuelve los puntos en juego si el nivel actual de Truco es aceptado.
        /// </summary>
        public int GetTrucoAcceptedPoints(int tier)
        {
            int clamped = Math.Clamp(tier, 0, TrucoStakes.Length - 1);
            return TrucoStakes[clamped];
        }

        /// <summary>
        /// Devuelve los puntos otorgados al rival si el Truco es rechazado (No Quiero).
        /// </summary>
        public int GetTrucoDeclinedPoints(int tier)
        {
            if (tier <= 1) return 1; // Truco rechazado = 1 punto
            return TrucoStakes[tier - 1]; // Retruco rechazado = 3 puntos, Vale 9 rechazado = 6, etc.
        }

        public void RegisterTrucoCall(int seatIndex, int teamIndex, int newTier)
        {
            CurrentAnnouncement = AnnounceState.Truco;
            CurrentTrucoTier = newTier;
            LastTrucoCallerSeat = seatIndex;
            LastTrucoCallerTeam = teamIndex;
        }

        #endregion

        #region Envido Logic

        /// <summary>
        /// El Envido solo se puede cantar en la primera baza (round 0) y si no ha sido resuelto aún.
        /// </summary>
        public bool CanCallEnvido(int currentRound, bool isEnvidoAlreadyCalled, bool hasLiveFlor)
        {
            if (currentRound > 0) return false;
            if (isEnvidoAlreadyCalled) return false;
            if (hasLiveFlor) return false; // Flor anula envido
            return true;
        }

        public void RegisterEnvidoCall(int seatIndex)
        {
            CurrentAnnouncement = AnnounceState.Envido;
            EnvidoCallerSeat = seatIndex;
            EnvidoExtraPoints = 0;
            PendingEnvidoRaise = 0;
        }

        public void AddEnvidoRaise(int additionalStones)
        {
            EnvidoExtraPoints += additionalStones;
            PendingEnvidoRaise = additionalStones;
        }

        /// <summary>
        /// Puntos si el Envido es querido (2 base + piedras extra de re-envidos).
        /// </summary>
        public int GetEnvidoAcceptedPoints()
        {
            return 2 + EnvidoExtraPoints;
        }

        /// <summary>
        /// Puntos si el Envido es rechazado (1 si no hubo re-envido, o lo acumulado antes del último raise).
        /// </summary>
        public int GetEnvidoDeclinedPoints()
        {
            if (PendingEnvidoRaise == 0) return 1; // Rechazó el envido inicial
            int previousAccepted = EnvidoExtraPoints - PendingEnvidoRaise;
            return Math.Max(1, 2 + previousAccepted);
        }

        public void MarkEnvidoResolved()
        {
            IsEnvidoResolved = true;
            if (CurrentAnnouncement == AnnounceState.Envido)
            {
                CurrentAnnouncement = AnnounceState.None;
            }
        }

        #endregion

        #region Flor Logic

        /// <summary>
        /// Determina si un jugador puede declarar flor.
        /// </summary>
        public bool CanDeclareFlor(int currentRound, bool hasFlorHand, bool isFlorBurned)
        {
            if (currentRound > 0) return false;
            if (isFlorBurned) return false;
            return hasFlorHand;
        }

        public void MarkFlorResolved()
        {
            IsFlorResolved = true;
            if (CurrentAnnouncement == AnnounceState.Flor)
            {
                CurrentAnnouncement = AnnounceState.None;
            }
        }

        #endregion

        #region Turn & Seat Helpers

        /// <summary>
        /// En partidas de Truco, ante un canto, quien debe responder es el jugador
        /// del equipo rival inmediatamente siguiente en turno ("a la derecha" / rotación horaria).
        /// </summary>
        public static int GetNextResponderSeat(int callerSeat, int totalSeats)
        {
            if (totalSeats <= 0) return -1;
            return (callerSeat + 1) % totalSeats;
        }

        #endregion
    }
}
