using UnityEngine;
using Code.GameLogic;

namespace Code.GameLogic.States
{
    public class DealingState : GameState
    {
        public override void EnterState()
        {
            
            var gameManager = GameManager.Instance;
            if (gameManager != null)
            {
                gameManager.StartCoroutine(DealCardsRoutine());
            }
        }

        private System.Collections.IEnumerator DealCardsRoutine()
        {
            var deckCreator = DeckCreator.Instance;
            var gameManager = GameManager.Instance;
            
            if (deckCreator != null && gameManager != null)
            {
                // 1. Mezclar y seleccionar la Vira
                deckCreator.ShuffleAndSetVira();

                // 2. Colocar SOLAMENTE el mazo en la mesa para robar de él
                if (TableManager.Instance != null)
                {
                    TableManager.Instance.SpawnDeck3D(gameManager.dealerIndex);
                }

                // 3. Limpiar las manos de todos los jugadores y NPCs
                var allPlayers = Object.FindObjectsByType<Code.Player.Player>(FindObjectsSortMode.None);
                var allNPCs = Object.FindObjectsByType<Code.Player.NPCPlayer>(FindObjectsSortMode.None);

                foreach (var player in allPlayers)
                {
                    var cardsHandler = player.GetComponent<Code.Cards.CardsHandler>();
                    if (cardsHandler != null) cardsHandler.ClearCards();
                }
                foreach (var npc in allNPCs)
                {
                    npc.ClearCards();
                }

                // Pre-calcular a quién le toca cada asiento
                int totalSeats = SeatManager.Instance.allChairs.Count;
                var seatOccupants = new System.Collections.Generic.Dictionary<int, object>();
                
                foreach (var player in allPlayers)
                {
                    int seat = SeatManager.Instance.GetPlayerSeatIndex(player.gameObject);
                    if (seat != -1) seatOccupants[seat] = player;
                }
                foreach (var npc in allNPCs)
                {
                    int seat = SeatManager.Instance.GetPlayerSeatIndex(npc.gameObject);
                    if (seat != -1) seatOccupants[seat] = npc;
                }

                // 4. Reparto secuencial: 3 vueltas, empezando por la Mano
                int manoSeatIndex = (gameManager.dealerIndex + 1) % totalSeats;
                
                for (int round = 0; round < 3; round++)
                {
                    for (int offset = 0; offset < totalSeats; offset++)
                    {
                        int currentSeat = (manoSeatIndex + offset) % totalSeats;
                        
                        if (seatOccupants.TryGetValue(currentSeat, out object occupant))
                        {
                            var cards = deckCreator.DealCards(1);
                            if (cards.Count > 0)
                            {
                                if (occupant is Code.Player.Player p)
                                {
                                    var ch = p.GetComponent<Code.Cards.CardsHandler>();
                                    if (ch != null) ch.ReceiveSingleCard(cards[0]);
                                }
                                else if (occupant is Code.Player.NPCPlayer npc)
                                {
                                    npc.ReceiveSingleCard(cards[0]);
                                }
                                
                                yield return new UnityEngine.WaitForSeconds(0.25f);
                            }
                        }
                    }
                }

                yield return new UnityEngine.WaitForSeconds(0.4f);

                // 5. Mostrar la Vira al final
                if (TableManager.Instance != null)
                {
                    TableManager.Instance.SpawnVira3D(deckCreator.cardVira, gameManager.dealerIndex);
                }

                yield return new UnityEngine.WaitForSeconds(0.8f);

                // Evaluar si alguien tiene flor
                var flor = Object.FindAnyObjectByType<Code.GameLogic.Announcement.FlorAnnouncement>();
                if (flor != null) flor.CanDeclareFlower();

                // 6. Iniciar el turno
                gameManager.StartTurn(gameManager.currentTrickStartSeatIndex);
            }

            // Transición automática a la fase de juego
            StateMachine.ChangeState(new PlayerTurnState());
        }

        public override void UpdateState() { }

        public override void ExitState()
        {
        }
    }
}
