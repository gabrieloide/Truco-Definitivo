using UnityEngine;
using Code.GameLogic;

namespace Code.GameLogic.States
{
    public class DealingState : GameState
    {
        public override void EnterState()
        {
            Debug.Log("[DealingState] Repartiendo cartas...");
            
            var deckCreator = Object.FindAnyObjectByType<DeckCreator>();
            var gameManager = GameManager.Instance;
            
            if (deckCreator != null && gameManager != null)
            {
                // 1. Mezclar y seleccionar la Vira
                deckCreator.ShuffleAndSetVira();

                // 2. Repartir a todos los jugadores y NPCs
                var allPlayers = Object.FindObjectsByType<Code.Player.Player>(FindObjectsSortMode.None);
                foreach (var player in allPlayers)
                {
                    var cardsHandler = player.GetComponent<Code.Cards.CardsHandler>();
                    if (cardsHandler != null)
                    {
                        cardsHandler.TargetReceiveCards(deckCreator.DealCards(3));
                    }
                }

                var allNPCs = Object.FindObjectsByType<Code.Player.NPCPlayer>(FindObjectsSortMode.None);
                foreach (var npc in allNPCs)
                {
                    npc.ReceiveCards(deckCreator.DealCards(3));
                }

                // 3. Colocar el mazo y la vira en la mesa 3D
                gameManager.UpdateDeckAndVira();

                // 4. Iniciar el turno de la mano (el jugador a la derecha del repartidor)
                gameManager.StartTurn(gameManager.currentTrickStartSeatIndex);
            }

            // Transición automática a la fase de juego
            StateMachine.ChangeState(new PlayerTurnState());
        }

        public override void UpdateState() { }

        public override void ExitState()
        {
            Debug.Log("[DealingState] Reparto completado.");
        }
    }
}
