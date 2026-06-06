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

            var existingCollider = GetComponent<Collider>();
            if (existingCollider == null)
            {
                var col = gameObject.AddComponent<BoxCollider>();
                var renderer = GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    // Convert world bounds to local size
                    col.size = transform.InverseTransformVector(renderer.bounds.size);
                    col.center = transform.InverseTransformPoint(renderer.bounds.center);
                    // Ensure a minimum thickness for the Z axis
                    if (Mathf.Abs(col.size.z) < 0.01f)
                    {
                        var size = col.size;
                        size.z = 0.01f;
                        col.size = size;
                    }
                }
                else
                {
                    col.size = new Vector3(1f, 1.5f, 0.01f);
                }
            }
            else if (existingCollider is MeshCollider meshCollider)
            {
                // Unity SILENTLY DISABLES MeshColliders that are triggers but not convex!
                if (meshCollider.isTrigger && !meshCollider.convex)
                {
                    meshCollider.convex = true;
                }
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
            var db = Resources.Load<Code.Cards.CardDatabase>("CardDatabase");
            if (db != null)
            {
                db.Initialize();
                var allCards = db.GetAllCards();

                if (string.IsNullOrEmpty(cardSuit) || cardValue == 0)
                {
                    Debug.Log($"[PhysicalCard3D] Mostrando reverso de la carta (suit='{cardSuit}', value={cardValue})");
                    ShowCardBack(allCards);
                    return;
                }

                Debug.Log($"[PhysicalCard3D] Iniciando UpdateVisuals para {cardValue} de {cardSuit}");

                // Buscar por valor y palo para que sea 100% robusto y no dependa de IDs
                var data = allCards.FirstOrDefault(c => c.suit.ToString() == cardSuit && c.value == cardValue);
                if (data != null)
                {
                    Debug.Log($"[PhysicalCard3D] Encontrado CardData para {cardValue} de {cardSuit}. cardSprite: {(data.cardSprite != null ? data.cardSprite.name : "NULL")}");
                    
                    if (data.cardSprite != null)
                    {
                        // Opción A: Si usa SpriteRenderer
                        if (cardImageRenderer != null)
                        {
                            cardImageRenderer.sprite = data.cardSprite;
                            Debug.Log("[PhysicalCard3D] Asignado sprite a cardImageRenderer");
                        }

                        // Opción B: Si prefiere usar un Material (MeshRenderer)
                        if (cardMeshRenderer != null)
                        {
                            Material mat = cardMeshRenderer.material; // Esto crea una instancia del material
                            Debug.Log($"[PhysicalCard3D] Obtenido material de cardMeshRenderer: {mat.name}, shader: {mat.shader.name}");

                            Rect spriteRect = data.cardSprite.rect;
                            float texWidth = data.cardSprite.texture.width;
                            float texHeight = data.cardSprite.texture.height;

                            Vector2 scale = new Vector2(spriteRect.width / texWidth, spriteRect.height / texHeight);
                            Vector2 offset = new Vector2(spriteRect.x / texWidth, spriteRect.y / texHeight);
                            Debug.Log($"[PhysicalCard3D] SpriteRect: {spriteRect}, Texture: {texWidth}x{texHeight}, Scale: {scale}, Offset: {offset}");

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
                                    Debug.Log($"[PhysicalCard3D] Asignada textura a propiedad {prop} (Escala: {scale}, Offset: {offset})");
                                }
                            }

                            if (textureAssigned)
                            {
                                // Para MK Toon, es necesario habilitar explícitamente el keyword de albedo map
                                mat.EnableKeyword("_MK_ALBEDO_MAP");
                                bool keywordEnabled = mat.IsKeywordEnabled("_MK_ALBEDO_MAP");
                                Debug.Log($"[PhysicalCard3D] Habilitado keyword _MK_ALBEDO_MAP. Estado activo: {keywordEnabled}");
                            }
                            else
                            {
                                Debug.LogWarning("[PhysicalCard3D] No se encontró ninguna propiedad de textura conocida en el material.");
                            }

                            // Limpiamos los tintes de color en las propiedades de color comunes
                            string[] colorProperties = { "_BaseColor", "_Color", "_AlbedoColor", "_ColorTint" };
                            foreach (string prop in colorProperties)
                            {
                                if (mat.HasProperty(prop))
                                {
                                    mat.SetColor(prop, Color.white);
                                    Debug.Log($"[PhysicalCard3D] Limpiado tinte de color en {prop} a blanco");
                                }
                            }
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[PhysicalCard3D] El CardData para {cardValue} de {cardSuit} tiene cardSprite = NULL");
                    }
                }
                else
                {
                    Debug.LogWarning($"[PhysicalCard3D] No se encontró CardData para {cardValue} de {cardSuit} en la base de datos.");
                }
            }
            else
            {
                Debug.LogError("[PhysicalCard3D] No se pudo cargar CardDatabase desde Resources.");
            }
        }

        private void ShowCardBack(System.Collections.Generic.List<CardData> allCards)
        {
            if (allCards != null && allCards.Count > 0 && allCards[0].cardSprite != null)
            {
                if (cardMeshRenderer != null)
                {
                    Material mat = cardMeshRenderer.material;
                    Texture2D tex = allCards[0].cardSprite.texture;

                    // El reverso con diseño (Back_1) está en la posición x: 80, y: 0, w: 80, h: 122 en un sheet de 960x610
                    Vector2 scale = new Vector2(80f / 960f, 122f / 610f);
                    Vector2 offset = new Vector2(80f / 960f, 0f);

                    string[] targetProperties = { "_BaseMap", "_MainTex", "_AlbedoMap", "_AlbedoTex", "_BaseColorMap", "_Albedo" };
                    bool textureAssigned = false;
                    foreach (string prop in targetProperties)
                    {
                        if (mat.HasProperty(prop))
                        {
                            mat.SetTexture(prop, tex);
                            mat.SetTextureScale(prop, scale);
                            mat.SetTextureOffset(prop, offset);
                            textureAssigned = true;
                        }
                    }

                    if (textureAssigned)
                    {
                        mat.EnableKeyword("_MK_ALBEDO_MAP");
                    }

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
                Debug.LogWarning("[PhysicalCard3D] No se pudo cargar la textura para el reverso porque la base de datos está vacía.");
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
