using UnityEngine;

namespace Code.GameLogic.States
{
    public class PlayerTurnState : GameState
    {
        public override void EnterState()
        {
            TableManager.OnCardPlaced += HandleCardPlaced;
            global::Code.Core.GameEventManager.OnAnnounceButtonClicked += HandleAnnounce;
        }

        public override void UpdateState()
        {
        }

        public override void ExitState()
        {
            TableManager.OnCardPlaced -= HandleCardPlaced;
            global::Code.Core.GameEventManager.OnAnnounceButtonClicked -= HandleAnnounce;
        }

        private void HandleCardPlaced(Card card, GameObject player)
        {
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
            var announceManager = UnityEngine.Object.FindAnyObjectByType<global::Code.Player.AnnouncementManager>();
            if (announceManager != null)
            {
                announceManager.SendAnnounceToClient(announceType);
            }
            else
            {
                Debug.LogError("[PlayerTurnState] ERROR: AnnouncementManager no encontrado en la escena al cantar.");
            }
        }
    }
}
