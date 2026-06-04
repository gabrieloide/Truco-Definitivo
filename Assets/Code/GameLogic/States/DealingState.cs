using UnityEngine;
using Code.GameLogic;

namespace Code.GameLogic.States
{
    public class DealingState : GameState
    {
        public override void EnterState()
        {
            Debug.Log("[DealingState] Repartiendo cartas...");
            
            // 1. Mezclar y seleccionar la Vira
            var deckCreator = Object.FindAnyObjectByType<DeckCreator>();
            if (deckCreator != null)
            {
                deckCreator.ShuffleAndSetVira();
            }

            // TODO: Integrar con SeatManager y PlayerLocal para asignarles las cartas
            // var seatManager = Object.FindAnyObjectByType<SeatManager>();
            // foreach(var seat in seatManager.Seats) { ... repartir 3 cartas ... }

            // 2. Transición automática a la fase de juego
            StateMachine.ChangeState(new PlayerTurnState());
        }

        public override void UpdateState()
        {
            // No se requiere lógica por frame en el reparto.
        }

        public override void ExitState()
        {
            Debug.Log("[DealingState] Reparto completado.");
        }
    }
}
