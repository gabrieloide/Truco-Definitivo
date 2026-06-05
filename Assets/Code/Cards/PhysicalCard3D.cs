using UnityEngine;
using Code.Player;
using Code.GameLogic;
using System.Linq;

namespace Code.Cards
{
    public class PhysicalCard3D : MonoBehaviour, IInteractable
    {
        [Header("Visual References")]
        public SpriteRenderer cardImageRenderer; // Opción A: Usar SpriteRenderer
        public MeshRenderer cardMeshRenderer;    // Opción B: Usar Material/MeshRenderer

        public int cardValue;
        public string cardSuit;
        public int cardDbId;
        public PlayerLocal owner;
        public Card cardReference;
        public JuicyCardAnimator animator { get; private set; }

        private void Awake()
        {
            animator = GetComponent<JuicyCardAnimator>();
            if (animator == null)
            {
                animator = gameObject.AddComponent<JuicyCardAnimator>();
            }
        }

        private void OnCardDataChanged(int oldVal, int newVal) { UpdateVisuals(); }
        private void OnCardDataChanged(string oldVal, string newVal) { UpdateVisuals(); }

        private void Start()
        {
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (string.IsNullOrEmpty(cardSuit) || cardValue == 0) return;

            var db = Resources.Load<Code.Cards.CardDatabase>("CardDatabase");
            if (db != null)
            {
                db.Initialize();
                // Buscar por valor y palo para que sea 100% robusto y no dependa de IDs
                var data = db.GetAllCards().FirstOrDefault(c => c.suit.ToString() == cardSuit && c.value == cardValue);
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
                        Material mat = cardMeshRenderer.material; // Esto crea una instancia del material

                        Rect spriteRect = data.cardSprite.textureRect;
                        float texWidth = data.cardSprite.texture.width;
                        float texHeight = data.cardSprite.texture.height;

                        Vector2 scale = new Vector2(spriteRect.width / texWidth, spriteRect.height / texHeight);
                        Vector2 offset = new Vector2(spriteRect.x / texWidth, spriteRect.y / texHeight);

                        // Diagnóstico de propiedades de textura en el editor
                        #if UNITY_EDITOR
                        string[] texProps = mat.GetTexturePropertyNames();
                        #endif

                        // Intentamos asignar a todas las propiedades de textura comunes (incluido MK Toon)
                        string[] targetProperties = { "_BaseMap", "_MainTex", "_AlbedoMap", "_AlbedoTex", "_BaseColorMap", "_Albedo" };
                        bool textureAssigned = false;
                        foreach (string prop in targetProperties)
                        {
                            if (mat.HasProperty(prop))
                            {
                                mat.SetTexture(prop, data.cardSprite.texture);
                                mat.SetTextureScale(prop, scale);
                                mat.SetTextureOffset(prop, offset);
                                textureAssigned = true;
                            }
                        }

                        if (textureAssigned)
                        {
                            // Para MK Toon, es necesario habilitar explícitamente el keyword de albedo map
                            mat.EnableKeyword("_MK_ALBEDO_MAP");
                        }
                        else
                        {
                        }

                        // Limpiamos los tintes de color en las propiedades de color comunes
                        string[] colorProperties = { "_BaseColor", "_Color", "_AlbedoColor", "_ColorTint" };
                        foreach (string prop in colorProperties)
                        {
                            if (mat.HasProperty(prop))
                            {
                                mat.SetColor(prop, Color.white);
                            }
                        }
                    }
                }
                else
                {
                }
            }
            else
            {
                Debug.LogError("[PhysicalCard3D] No se pudo cargar CardDatabase desde Resources.");
            }
        }

        // [Server]
        public void SetupCard(Card card, int value, string suit, int dbId = 0)
        {
            cardReference = card;
            cardValue = value;
            cardSuit = suit;
            cardDbId = dbId;
            UpdateVisuals();
        }

        // [Server]
        public void SetupCard(int value, string suit, int dbId = 0)
        {
            SetupCard(null, value, suit, dbId);
        }

        public string GetInteractText()
        {
            var deckCreator = FindAnyObjectByType<Code.GameLogic.DeckCreator>();
            if (deckCreator != null && deckCreator.cardVira != null)
            {
                int realValue = Code.GameLogic.TrucoRules.GetCardRealValue(cardReference, deckCreator.cardVira);
                if (realValue == 100) return $"Jugar Perico ({cardValue} de {cardSuit})";
                if (realValue == 99) return $"Jugar Perica ({cardValue} de {cardSuit})";
            }
            return $"Jugar {cardValue} de {cardSuit}";
        }

        public void Interact(GameObject interactor)
        {
            PlayerLocal interactorPlayer = interactor.GetComponentInParent<PlayerLocal>();
            
            // Si no lo encontramos por jerarquía, lo buscamos en la escena (válido para Singleplayer)
            if (interactorPlayer == null)
            {
                interactorPlayer = FindAnyObjectByType<PlayerLocal>();
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
                    return;
                }

                if (!owner.player.canPlayCard)
                {
                    return;
                }

                
                // Logic to play the card
                Card cardToPlay = cardReference;
                if (cardToPlay == null)
                {
                    cardToPlay = new Code.GameLogic.Card(cardValue, cardSuit) { dbId = cardDbId };
                }
                Code.GameLogic.Architecture.ICommand playCommand = new Code.GameLogic.Architecture.PlayCardCommand(cardToPlay, owner.gameObject, transform.position);
                playCommand.Execute();
                
                Destroy(gameObject);
            }
            else
            {
            }
        }
    }
}
