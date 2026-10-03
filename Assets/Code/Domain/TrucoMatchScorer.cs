using System;
using System.Collections.Generic;
using Code.Player;

namespace Code.Domain
{
    /// <summary>
    /// Modelo de dominio que gestiona los puntos de la partida, los tantos en juego (Truco/Envido/Flor)
    /// y el estado de victoria sin dependencias de Monobehaviour ni vistas.
    /// </summary>
    public class TrucoMatchScorer
    {
        public int MaxPoints { get; set; } = 15;
        public int CurrentHandValue { get; set; } = 1;
        public int LastTrucoTeamIndex { get; set; } = 0;
        public bool IsMatchEnded { get; private set; } = false;

        public readonly List<int> TrickWinners = new List<int>();

        // Estado pendiente de resolución de Envido al final de la mano
        public bool PendingEnvidoResolution { get; set; } = false;
        public string PendingEnvidoWinnerTeam { get; set; } = "";
        public int PendingEnvidoPoints { get; set; } = 0;
        public int PendingEnvidoScoreTeam1 { get; set; } = 0;
        public int PendingEnvidoScoreTeam2 { get; set; } = 0;

        public TrucoMatchScorer(int maxPoints = 15)
        {
            MaxPoints = maxPoints;
        }

        /// <summary>
        /// Reinicia las variables temporales al arrancar una nueva mano.
        /// </summary>
        public void ResetForNewHand(IList<Team> teams)
        {
            CurrentHandValue = 1;
            LastTrucoTeamIndex = 0;
            TrickWinners.Clear();

            if (teams != null)
            {
                foreach (var team in teams)
                {
                    if (team != null) team.roundsWon = 0;
                }
            }
        }

        /// <summary>
        /// Registra el ganador de una baza (0 = parda, 1 = equipo 1, 2 = equipo 2).
        /// </summary>
        public void RecordTrickWinner(int winnerTeamIndex, IList<Team> teams)
        {
            TrickWinners.Add(winnerTeamIndex);
            if (winnerTeamIndex > 0 && teams != null && winnerTeamIndex <= teams.Count)
            {
                teams[winnerTeamIndex - 1].roundsWon++;
            }
        }

        /// <summary>
        /// Evalúa si la mano ya quedó definida según las reglas de Truco Venezolano.
        /// </summary>
        public int EvaluateHandWinner(int manoTeamIndex, IList<Team> teams)
        {
            int t1Wins = (teams != null && teams.Count > 0) ? teams[0].roundsWon : 0;
            int t2Wins = (teams != null && teams.Count > 1) ? teams[1].roundsWon : 0;
            return TrucoHandResolver.EvaluateHandWinner(TrickWinners, manoTeamIndex, t1Wins, t2Wins);
        }

        /// <summary>
        /// Suma puntos directos a un equipo dado por su nombre.
        /// </summary>
        public Team AddPoints(string teamName, int points, IList<Team> teams)
        {
            if (teams == null) return null;
            foreach (var team in teams)
            {
                if (team != null && team.teamName == teamName)
                {
                    team.teamScore += points;
                    return team;
                }
            }
            return null;
        }

        /// <summary>
        /// Guarda el resultado del Envido para acreditar sus puntos al finalizar la mano.
        /// </summary>
        public void StorePendingEnvido(string winnerTeam, int points, int scoreT1, int scoreT2)
        {
            PendingEnvidoResolution = true;
            PendingEnvidoWinnerTeam = winnerTeam;
            PendingEnvidoPoints = points;
            PendingEnvidoScoreTeam1 = scoreT1;
            PendingEnvidoScoreTeam2 = scoreT2;
        }

        /// <summary>
        /// Limpia el Envido pendiente (ej. si la Flor lo anuló).
        /// </summary>
        public void ClearPendingEnvido()
        {
            PendingEnvidoResolution = false;
            PendingEnvidoWinnerTeam = "";
            PendingEnvidoPoints = 0;
            PendingEnvidoScoreTeam1 = 0;
            PendingEnvidoScoreTeam2 = 0;
        }

        /// <summary>
        /// Comprueba si algún equipo alcanzó o superó el puntaje máximo de la partida.
        /// </summary>
        public bool CheckForMatchWinner(IList<Team> teams, out Team matchWinner)
        {
            matchWinner = null;
            if (IsMatchEnded) return true;
            if (teams == null) return false;

            foreach (var team in teams)
            {
                if (team != null && team.teamScore >= MaxPoints)
                {
                    IsMatchEnded = true;
                    matchWinner = team;
                    return true;
                }
            }

            return false;
        }
    }
}
