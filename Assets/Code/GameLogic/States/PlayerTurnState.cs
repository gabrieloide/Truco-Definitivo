using UnityEngine;

namespace Code.GameLogic.States
{
    public class PlayerTurnState : GameState
    {
        public override void EnterState()
        {
            Debug.Log("[PlayerTurnState] Esperando acción del jugador...");
            // Aquí habilitaríamos la UI para el jugador local si es su turno
        }

        public override void UpdateState()
        {
            // Aquí podríamos poner un temporizador de turno (ej. 15 segundos para jugar)
        }

        public override void ExitState()
        {
            Debug.Log("[PlayerTurnState] Turno finalizado.");
        }
    }
}
