using UnityEngine;

namespace Code.GameLogic.States
{
    public class EnvidoPhaseState : GameState
    {
        public override void EnterState()
        {
            Debug.Log("[EnvidoPhaseState] Fase de Envido iniciada.");
            // Aquí pausamos el juego de cartas normal y mostramos UI de "Quiero / No Quiero"
        }

        public override void UpdateState()
        {
            
        }

        public override void ExitState()
        {
            Debug.Log("[EnvidoPhaseState] Fase de Envido resuelta.");
        }
    }
}
