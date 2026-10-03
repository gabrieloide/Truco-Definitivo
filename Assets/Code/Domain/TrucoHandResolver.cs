using System.Collections.Generic;

namespace Code.Domain
{
    /// <summary>
    /// Motor de reglas puro para resolver el ganador de una mano (hasta 3 bazas)
    /// en el Truco Venezolano. Desacoplado de GameObjects, red y escena.
    /// </summary>
    public static class TrucoHandResolver
    {
        /// <summary>
        /// Determina el estado de la mano actual basándose en los ganadores de cada baza.
        /// </summary>
        /// <param name="trickWinners">Lista de ganadores por baza (0 = Parda/Empate, 1 = Equipo 1, 2 = Equipo 2).</param>
        /// <param name="manoTeamIndex">Equipo que posee la Mano (1 o 2) para desempatar pardas.</param>
        /// <param name="team1RoundsWon">Bazas ganadas por el Equipo 1.</param>
        /// <param name="team2RoundsWon">Bazas ganadas por el Equipo 2.</param>
        /// <returns>0 si la mano sigue en juego, 1 si ganó el Equipo 1, 2 si ganó el Equipo 2.</returns>
        public static int EvaluateHandWinner(IReadOnlyList<int> trickWinners, int manoTeamIndex, int team1RoundsWon, int team2RoundsWon)
        {
            if (trickWinners == null) return 0;

            // Regla 1: Un equipo gana 2 bazas directamente
            if (team1RoundsWon >= 2) return 1;
            if (team2RoundsWon >= 2) return 2;

            int currentRound = trickWinners.Count;

            // Regla 2: Resolución de Empates (Pardas)
            if (currentRound == 1)
            {
                // Parda en 1era baza: la mano continúa a la 2da
                return 0;
            }
            else if (currentRound == 2)
            {
                // Baza 1 parda y baza 2 definida -> gana quien ganó la 2da
                if (trickWinners[0] == 0 && trickWinners[1] != 0) return trickWinners[1];

                // Baza 1 ganada y baza 2 parda -> gana quien ganó la 1ra
                if (trickWinners[0] != 0 && trickWinners[1] == 0) return trickWinners[0];

                // Baza 1 parda y baza 2 parda -> muerte súbita (la 3ra no se juega); desempata la Mano
                if (trickWinners[0] == 0 && trickWinners[1] == 0) return manoTeamIndex;
            }
            else if (currentRound >= 3)
            {
                // Baza 3 definida -> gana quien ganó la 3ra
                if (trickWinners[2] != 0) return trickWinners[2];

                // Baza 3 parda -> gana quien ganó la 1ra
                if (trickWinners[0] != 0) return trickWinners[0];

                // Todo pardo -> desempata la Mano
                return manoTeamIndex;
            }

            return 0;
        }
    }
}
