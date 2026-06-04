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
        public SpriteRenderer cardImageRenderer; // Opción A: Usar SpriteRenderer
        public MeshRenderer cardMeshRenderer;    // Opción B: Usar Material/MeshRenderer

        public int cardValue;
        public string cardSuit;
        public int cardDbId;
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

            if (cardDbId > 0)
            {
                var db = Resources.Load<Code.Cards.CardDatabase>("CardDatabase");
                if (db != null)
                {
                    db.Initialize();
                    var data = db.GetCardById(cardDbId);
                    if (data != null && data.cardSprite != null)
                    {
                        // Opción A: Si usa SpriteRenderer
                        if (cardImageRenderer != null)
                        {
                            cardImageRenderer.sprite = data.cardSprite;
                        }

                        // Opción B: Si prefiere usar un Material (MeshRenderer)
                        if (cardMeshRenderer != null)
                        {
                            // Como el sprite es parte de un spritesheet, no podemos simplemente asignar la textura.
                            // Tenemos que calcular el Offset y Scale basándonos en las coordenadas del sprite.
                            Material mat = cardMeshRenderer.material; // Esto crea una instancia del material
                            mat.mainTexture = data.cardSprite.texture;

                            Rect spriteRect = data.cardSprite.textureRect;
                            float texWidth = data.cardSprite.texture.width;
                            float texHeight = data.cardSprite.texture.height;

                            Vector2 scale = new Vector2(spriteRect.width / texWidth, spriteRect.height / texHeight);
                            Vector2 offset = new Vector2(spriteRect.x / texWidth, spriteRect.y / texHeight);

                            mat.mainTextureScale = scale;
                            mat.mainTextureOffset = offset;
                        }
                        
                        // Ocultar textos si ya tenemos la imagen de la carta
                        if (numberText != null) numberText.gameObject.SetActive(false);
                        if (suitText != null) suitText.gameObject.SetActive(false);
                    }
                }
            }
        }

        // [Server]
        public void SetupCard(int value, string suit, int dbId = 0)
        {
            cardValue = value;
            cardSuit = suit;
            cardDbId = dbId;
            UpdateVisuals();
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
