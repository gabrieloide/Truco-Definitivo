using UnityEngine;
using Code.GameLogic;
using Code.Networking;

namespace Code.Player
{
    /// <summary>
    /// Servicio de enrutamiento para cantos y respuestas en la mesa.
    /// Resuelve a quién le toca responder (orden antihorario hacia la derecha),
    /// nombres visibles y pertenencia de equipo.
    /// </summary>
    public static class AnnouncementRouter
    {
        /// <summary>
        /// Obtiene el equipo de un GameObject (jugador humano o NPC).
        /// </summary>
        public static Team TeamOf(GameObject obj)
        {
            if (obj == null) return null;
            var p = obj.GetComponent<Code.Player.Player>();
            if (p != null && p.team != null) return p.team;
            var npc = obj.GetComponent<NPCPlayer>();
            return npc != null ? npc.team : null;
        }

        /// <summary>
        /// Determina la silla del rival que debe responder el canto.
        /// Busca en orden de turnos (antihorario / incremento de índice) al primer ocupante
        /// del equipo contrario. Retorna -1 si no se encuentra.
        /// </summary>
        public static int GetResponderSeat(Team announcerTeam, int currentAnnouncerSeat)
        {
            var seatMgr = SeatManager.Instance;
            var gm = GameManager.Instance;
            if (seatMgr == null || gm == null || announcerTeam == null) return -1;

            int count = seatMgr.allChairs.Count;
            if (count == 0) return -1;

            int announcerTeamIdx = gm.GetTeamIndex(announcerTeam);
            int start = currentAnnouncerSeat >= 0 ? currentAnnouncerSeat : 0;

            for (int step = 1; step <= count; step++)
            {
                int seat = (start + step) % count;
                var occupant = seatMgr.allChairs[seat].occupant;
                if (occupant == null) continue;

                var team = TeamOf(occupant);
                if (team == null) continue;
                if (gm.GetTeamIndex(team) == announcerTeamIdx) continue;

                return seat;
            }
            return -1;
        }

        /// <summary>
        /// Obtiene el nombre visual adecuado del jugador que debe responder.
        /// </summary>
        public static string IdentifyResponderName(GameObject occupant)
        {
            if (occupant == null) return "";

            var localComp = occupant.GetComponent<PlayerLocal>();
            if (localComp != null && localComp.player != null && !string.IsNullOrEmpty(localComp.player.playerName))
                return localComp.player.playerName;

            var playerComp = occupant.GetComponent<Code.Player.Player>();
            if (playerComp != null && !string.IsNullOrEmpty(playerComp.playerName))
                return playerComp.playerName;

            var netSync = occupant.GetComponent<PlayerNetworkSync>();
            if (netSync != null && !string.IsNullOrEmpty(netSync.playerName))
                return netSync.playerName;

            var npcComp = occupant.GetComponent<NPCPlayer>();
            if (npcComp != null)
            {
                // Limpiar sufijos típicos de clonación de Unity
                string n = occupant.name.Replace("(Clone)", "").Trim();
                return string.IsNullOrEmpty(n) ? "Rival" : n;
            }

            return occupant.name;
        }

        /// <summary>
        /// Comprueba si el ocupante es compañero del jugador local.
        /// </summary>
        public static bool IsTeammateOfLocal(GameObject occupant, PlayerLocal localPlayer, GameManager gm)
        {
            if (occupant == null || localPlayer == null || localPlayer.player == null || gm == null)
                return false;

            var localTeam = localPlayer.player.team;
            var responderTeam = TeamOf(occupant);
            if (localTeam == null || responderTeam == null) return false;

            int localTeamIdx = gm.GetTeamIndex(localTeam);
            int respTeamIdx = gm.GetTeamIndex(responderTeam);
            return localTeamIdx >= 0 && localTeamIdx == respTeamIdx;
        }
    }
}
