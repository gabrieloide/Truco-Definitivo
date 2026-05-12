using UnityEngine;
using TMPro;
using Code.Player;
using Code.GameLogic;

namespace Code.Cards
{
    public class PhysicalCard3D : MonoBehaviour, IInteractable
    {
        [Header("UI References")]
        public TMP_Text numberText;
        public TMP_Text suitText;

        public int cardValue;
        public string cardSuit;
        public PlayerLocal owner;

        private void OnCardDataChanged(int oldVal, int newVal) { UpdateVisuals(); }
        private void OnCardDataChanged(string oldVal, string newVal) { UpdateVisuals(); }

        private void Start()
        {
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (numberText != null) numberText.text = cardValue.ToString();
            if (suitText != null) suitText.text = cardSuit;
        }

        // [Server]
        public void SetupCard(int value, string suit)
        {
            cardValue = value;
            cardSuit = suit;
        }

        public string GetInteractText()
        {
            return $"Jugar {cardValue} de {cardSuit}";
        }

        public void Interact(GameObject interactor)
        {
            Debug.Log($"[PhysicalCard3D] Interact llamado por {interactor.name}");
            PlayerLocal interactorPlayer = interactor.GetComponentInParent<PlayerLocal>();
            
            // Si no lo encontramos por jerarquía, lo buscamos en la escena (válido para Singleplayer)
            if (interactorPlayer == null)
            {
                interactorPlayer = FindAnyObjectByType<PlayerLocal>();
                Debug.Log($"[PhysicalCard3D] Buscando PlayerLocal en la escena... Encontrado: {(interactorPlayer != null ? interactorPlayer.name : "NULO")}");
            }

            if (interactorPlayer == null)
            {
                Debug.LogError("[PhysicalCard3D] ERROR: No se pudo identificar al jugador que interactúa.");
                return;
            }

            if (owner != null && interactorPlayer == owner)
            {
                if (GameManager.Instance != null && GameManager.Instance.isAnnouncementPending)
                {
                    Debug.LogWarning("[PhysicalCard3D] Esperando respuesta al canto...");
                    return;
                }

                if (!owner.player.canPlayCard)
                {
                    Debug.LogWarning($"[PhysicalCard3D] No es tu turno todavía. (Jugador: {owner.name})");
                    return;
                }

                Debug.Log($"[PhysicalCard3D] Carta {cardValue} de {cardSuit} jugada con éxito por {interactorPlayer.name}");
                
                // Logic to play the card
                Code.GameLogic.Architecture.ICommand playCommand = new Code.GameLogic.Architecture.PlayCardCommand(new Code.GameLogic.Card(cardValue, cardSuit), owner.gameObject);
                playCommand.Execute();
                
                Destroy(gameObject);
            }
            else
            {
                Debug.LogWarning($"[PhysicalCard3D] No puedes jugar esta carta. Dueño: {(owner != null ? owner.name : "NULO")}, Interactor: {interactorPlayer.name}. ¿Son el mismo objeto?: {interactorPlayer == owner}");
            }
        }
    }
}
