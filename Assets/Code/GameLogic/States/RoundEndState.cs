using UnityEngine;

namespace Code.GameLogic.States
{
    public class RoundEndState : GameState
    {
        public override void EnterState()
        {
            Debug.Log("[RoundEndState] Fin de la ronda. Sumando puntos.");
            // Sumar puntos globales, verificar si alguien llegó a 24/30 y resetear la mesa.
        }

        public override void UpdateState()
        {
            
        }

        public override void ExitState()
        {
            
        }
    }
}
