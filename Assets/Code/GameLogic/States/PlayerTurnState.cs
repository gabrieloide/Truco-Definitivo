using UnityEngine;

namespace Code.GameLogic.States
{
    public class PlayerTurnState : GameState
    {
        public override void EnterState()
        {
            Debug.Log("[PlayerTurnState] Esperando acción del jugador...");
            TableManager.OnCardPlaced += HandleCardPlaced;
            global::Code.Core.GameEventManager.OnAnnounceButtonClicked += HandleAnnounce;
        }

        public override void UpdateState()
        {
        }

        public override void ExitState()
        {
            Debug.Log("[PlayerTurnState] Saliendo del turno activo.");
            TableManager.OnCardPlaced -= HandleCardPlaced;
            global::Code.Core.GameEventManager.OnAnnounceButtonClicked -= HandleAnnounce;
        }

        private void HandleCardPlaced(Code.Cards.Card card, GameObject player)
        {
            Debug.Log($"[PlayerTurnState] Carta jugada por {player.name}. Finalizando turno en breve...");
            GameManager.Instance.StartCoroutine(DelayedEndTurn());
        }

        private System.Collections.IEnumerator DelayedEndTurn()
        {
            yield return new WaitForSeconds(2.5f);
            // Delega a GameManager para chequear si la baza terminó
            GameManager.Instance.EndTurn();
        }

        private void HandleAnnounce(string announceType)
        {
            if (announceType == "EnvidoButton")
            {
                Debug.Log("[PlayerTurnState] ¡Envido cantado! Transicionando a EnvidoPhaseState...");
                StateMachine.ChangeState(new EnvidoPhaseState());
            }
            // Add other announcements here (Truco, Flor, etc)
        }
    }
}
