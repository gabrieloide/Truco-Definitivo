using System;
using System.Collections.Generic;
using Code.Cards;
using DG.Tweening;
using Mirror;
using UnityEngine;
using UnityEngine.Serialization;
using Code.Scripts.Audio;
using Code.Domain;

namespace Code.GameLogic
{
    public class TableManager : MonoBehaviour
    {
        public static event Action<Card, GameObject> OnCardPlaced;
        public static event Action OnTrickEvaluated;

        public readonly List<Card> CardsInTable = new List<Card>();
        public static TableManager Instance;

        [Header("3D Card Spawning")]
        public GameObject card3DPrefab;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        [Header("3D Card Transforms")]
        public Transform viraPosition;
        public GameObject deckVisualPrefab; // Prefab for the stack of cards
        public float viraHeightOffset = 0.02f; 
        public float deckHeightOffset = 0.01f; 

        [Header("Placement Offsets (X, Z)")]
        [Tooltip("Offset del mazo respecto al punto central de la mesa o silla del repartidor.")]
        public Vector2 deckOffset = new Vector2(0.25f, -0.1f);
        [Tooltip("Distancia de la vira respecto a la posición del mazo.")]
        public Vector2 viraOffset = new Vector2(0f, 0.15f);

        private GameObject _currentViraObj;
        private GameObject _currentDeckObj;

        public Vector3 CurrentDeckPosition
        {
            get
            {
                if (_currentDeckObj != null) return _currentDeckObj.transform.position;
                if (viraPosition != null) return viraPosition.position;
                return transform.position;
            }
        }
        
        [Header("Table Configuration")]
        
        // Track how many cards each player has played on their spot
        private Dictionary<GameObject, int> _cardsPerPlayer = new Dictionary<GameObject, int>();
        private List<GameObject> _spawnedCards = new List<GameObject>();
        private bool _isDeckPlacedOnce = false;

        // [Server]
        public void SpawnDeck3D(int dealerSeatIndex = -1)
        {
            Transform anchor = transform;
            if (dealerSeatIndex != -1 && SeatManager.Instance != null && SeatManager.Instance.allChairs.Count > dealerSeatIndex)
            {
                var chair = SeatManager.Instance.allChairs[dealerSeatIndex];
                if (chair != null && chair.cardDestination != null) anchor = chair.cardDestination;
            }
            if (anchor == null) anchor = this.transform;

            Vector3 basePos = anchor.position;
            Quaternion baseRot = anchor.rotation;

            // Offset para el Mazo usando la variable del Inspector
            Vector3 worldDeckOffset = (anchor.right * deckOffset.x) + (anchor.forward * deckOffset.y);
            Vector3 deckPos = basePos + worldDeckOffset + (anchor.up * deckHeightOffset);
            Quaternion deckRot = baseRot * Quaternion.Euler(0, UnityEngine.Random.Range(-5f, 5f), 0);

            if (_currentDeckObj == null && DeckCreator.Instance != null)
            {
                _currentDeckObj = DeckCreator.Instance.gameObject;
            }

            if (_currentDeckObj != null)
            {
                if (!_isDeckPlacedOnce)
                {
                    _currentDeckObj.transform.position = deckPos;
                    _currentDeckObj.transform.rotation = deckRot;
                    _isDeckPlacedOnce = true;
                }
                else
                {
                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlaySFX("cards_sweep_shove");
                    }
                    _currentDeckObj.transform.DOJump(deckPos, 0.5f, 1, 0.6f).SetEase(DG.Tweening.Ease.InOutQuad);
                    _currentDeckObj.transform.DORotateQuaternion(deckRot, 0.6f).SetEase(DG.Tweening.Ease.InOutQuad);
                }
            }
        }

        // [Server]
        public void SpawnVira3D(Card card, int dealerSeatIndex = -1)
        {
            if (card3DPrefab == null) return;
            
            // Clean up old vira
            if (_currentViraObj != null) Destroy(_currentViraObj);

            Transform anchor = transform;
            if (dealerSeatIndex != -1 && SeatManager.Instance != null && SeatManager.Instance.allChairs.Count > dealerSeatIndex)
            {
                var chair = SeatManager.Instance.allChairs[dealerSeatIndex];
                if (chair != null && chair.cardDestination != null) anchor = chair.cardDestination;
            }
            if (anchor == null) anchor = this.transform;

            Vector3 basePos = anchor.position;
            Quaternion baseRot = anchor.rotation;

            // Calculamos posición del mazo primero (base para la vira)
            Vector3 worldDeckOffset = (anchor.right * deckOffset.x) + (anchor.forward * deckOffset.y);
            Vector3 deckPos = basePos + worldDeckOffset + (anchor.up * deckHeightOffset);
            
            // La vira se posiciona relativa al mazo usando viraOffset
            Vector3 worldViraOffset = (anchor.right * viraOffset.x) + (anchor.forward * viraOffset.y);
            Vector3 viraPos = deckPos + worldViraOffset + (anchor.up * (viraHeightOffset - deckHeightOffset));
            
            Quaternion flatRot = baseRot * Quaternion.Euler(90f, 180f, 0f);
            
            _currentViraObj = Instantiate(card3DPrefab, deckPos, baseRot);
            
            var physicalCard = _currentViraObj.GetComponent<Code.Cards.PhysicalCard3D>();
            if (physicalCard != null) physicalCard.SetupCard(card.value, card.suit);

            var juicyAnimator = _currentViraObj.GetComponent<Code.Cards.JuicyCardAnimator>();
            if (juicyAnimator == null) juicyAnimator = _currentViraObj.AddComponent<Code.Cards.JuicyCardAnimator>();

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("vira_slide_swoosh");

            juicyAnimator.AnimateViraReveal(
                startPos: deckPos,
                targetPos: viraPos,
                targetRot: flatRot,
                duration: 0.7f,
                onImpact: () =>
                {
                    if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("vira_flip_chirp");
                    if (JuiceVFXManager.Instance != null)
                    {
                        JuiceVFXManager.Instance.ShakeCamera(0.12f, 0.03f);
                        JuiceVFXManager.Instance.PlayImpactParticles(viraPos);
                    }
                }
            );
        }

        // [Server]
        public void SpawnCard3D(Card card, GameObject player, Vector3? customStartPos = null, bool isSpecialCard = false)
        {
            if (card3DPrefab == null)
            {
                return;
            }

            // Determine how many cards this player has already played in this trick
            if (!_cardsPerPlayer.ContainsKey(player))
            {
                _cardsPerPlayer.Add(player, 0);
            }

            int cardsCount = _cardsPerPlayer[player];
            _cardsPerPlayer[player]++;

            // Find which seat this player is occupying
            int seatIndex = SeatManager.Instance.GetPlayerSeatIndex(player);
            
            // Get base position for this seat directly from the chair
            Transform basePos = transform;
            if (seatIndex != -1 && SeatManager.Instance != null && SeatManager.Instance.allChairs.Count > seatIndex)
            {
                var chair = SeatManager.Instance.allChairs[seatIndex];
                if (chair != null && chair.cardDestination != null)
                {
                    basePos = chair.cardDestination;
                }
            }

            // Stack upwards: base position + height offset per card
            float heightOffset = (cardsCount - 1) * 0.025f; // Slight height increase for stacking
            Vector3 targetPos = basePos.position + Vector3.up * heightOffset;
            
            // Apply a 90-degree rotation on X so the card lays flat on the table (face up)
            Quaternion targetRot = basePos.rotation * Quaternion.Euler(90f, 0f, 0f);
            
            if (card.isBurned)
            {
                // Face down
                targetRot = basePos.rotation * Quaternion.Euler(-90f, 0f, 0f);
            }
            
            // Start from custom pos or player position for animation
            Vector3 startPos = customStartPos.HasValue ? customStartPos.Value : player.transform.position + Vector3.up * 1.5f;
            
            GameObject cardObj = Instantiate(card3DPrefab, startPos, player.transform.rotation);
            
            var physicalCard = cardObj.GetComponent<Code.Cards.PhysicalCard3D>();
            if (physicalCard != null)
            {
                if (card.isBurned)
                {
                    physicalCard.SetupCard(0, ""); // No pintar la cara
                }
                else
                {
                    physicalCard.SetupCard(card.value, card.suit);
                }
            }
            
            bool isMega = isSpecialCard || CheckIsMegaSlam(card);
            float duration = isMega ? 0.38f : 0.58f;

            // Animate card to table using JuicyCardAnimator
            var juicyAnimator = cardObj.GetComponent<Code.Cards.JuicyCardAnimator>();
            if (juicyAnimator == null)
            {
                juicyAnimator = cardObj.AddComponent<Code.Cards.JuicyCardAnimator>();
            }

            juicyAnimator.AnimatePlayToTable(
                targetPos,
                targetRot,
                duration: duration,
                onImpact: () =>
                {
                    if (JuiceVFXManager.Instance != null)
                    {
                        if (isMega)
                        {
                            // Golpe seco y calibrado SOLO para cartas altas (sin mareos ni exageraciones)
                            JuiceVFXManager.Instance.ShakeCamera(0.18f, 0.07f);
                            JuiceVFXManager.Instance.PlayImpactParticles(targetPos, new Color(1f, 0.85f, 0.35f));
                            TriggerTableSlamShockwave(targetPos, cardObj);
                        }
                        else
                        {
                            // Cartas normales: NO sacuden la cámara, aterrizaje limpio y suave
                            JuiceVFXManager.Instance.PlayImpactParticles(targetPos);
                        }
                    }
                    else if (isMega)
                    {
                        TriggerTableSlamShockwave(targetPos, cardObj);
                    }

                    if (AudioManager.Instance != null)
                    {
                        // Golpe seco limpio sobre la mesa
                        AudioManager.Instance.PlaySFX("card_slam_thud");
                    }
                },
                onComplete: null,
                isMegaSlam: isMega
            );
            
            _spawnedCards.Add(cardObj);
        }

        /// <summary>
        /// Determina si una carta jugada es una CARTA ALTA / BRAVA genuina que merece impacto en la mesa:
        /// 1. Triunfos y Cartas Bravas: Perico (100), Perica (99), As de Espadas (20), As de Bastos (19), 7 de Espadas (18), 7 de Oros (17).
        /// 2. Matar una carta brava rival ya presente en la mesa con otra carta alta.
        /// (Las cartas normales NO activan el slam ni el shake de cámara).
        /// </summary>
        public bool CheckIsMegaSlam(Card card)
        {
            if (card == null || card.isBurned) return false;

            // Asegurar que el realValue esté calculado
            if (card.realValue == 0 && DeckCreator.Instance != null && DeckCreator.Instance.cardVira != null)
            {
                card.realValue = TrucoRules.GetCardRealValue(card, DeckCreator.Instance.cardVira);
            }

            // 1. Cartas Bravas / Triunfos Legendarios
            if (card.realValue >= 17)
            {
                return true;
            }

            // 2. Matar una carta rival alta (>= 16) con una carta superior
            if (CardsInTable != null && CardsInTable.Count > 0)
            {
                int maxTableVal = 0;
                foreach (var c in CardsInTable)
                {
                    if (c != card && !c.isBurned && c.realValue > maxTableVal)
                    {
                        maxTableVal = c.realValue;
                    }
                }
                if (maxTableVal >= 16 && card.realValue > maxTableVal)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Efecto sísmico en la mesa: hace que las cartas ya jugadas, el mazo, la vira
        /// y cualquier prop decorativo (latas, vasos, etc.) salten en el aire y vuelvan a caer.
        /// </summary>
        public void TriggerTableSlamShockwave(Vector3 impactPoint, GameObject impactCardObj)
        {
            // 1. Sacudir cartas ya jugadas en la mesa
            foreach (var c in _spawnedCards)
            {
                if (c == null || c == impactCardObj) continue;

                Vector3 curPos = c.transform.position;
                float dist = Vector3.Distance(curPos, impactPoint);
                float jumpHeight = Mathf.Clamp(0.075f - (dist * 0.02f), 0.035f, 0.075f);
                float jumpDuration = UnityEngine.Random.Range(0.24f, 0.32f);

                float rotY = UnityEngine.Random.Range(-7f, 7f);
                float rotZ = UnityEngine.Random.Range(-3f, 3f);

                c.transform.DOKill(false);
                c.transform.DOJump(curPos, jumpHeight, 1, jumpDuration).SetEase(DG.Tweening.Ease.OutQuad);
                c.transform.DORotate(c.transform.eulerAngles + new Vector3(0, rotY, rotZ), jumpDuration).SetEase(DG.Tweening.Ease.OutQuad);
            }

            // 2. Sacudir el Mazo
            if (_currentDeckObj != null && _currentDeckObj != impactCardObj)
            {
                Vector3 deckPos = _currentDeckObj.transform.position;
                _currentDeckObj.transform.DOKill(false);
                _currentDeckObj.transform.DOJump(deckPos, 0.05f, 1, 0.25f).SetEase(DG.Tweening.Ease.OutQuad);
                _currentDeckObj.transform.DORotate(_currentDeckObj.transform.eulerAngles + new Vector3(0, UnityEngine.Random.Range(-4f, 4f), 0), 0.25f).SetEase(DG.Tweening.Ease.OutQuad);
            }

            // 3. Sacudir la Vira
            if (_currentViraObj != null && _currentViraObj != impactCardObj)
            {
                Vector3 viraPos = _currentViraObj.transform.position;
                _currentViraObj.transform.DOKill(false);
                _currentViraObj.transform.DOJump(viraPos, 0.045f, 1, 0.23f).SetEase(DG.Tweening.Ease.OutQuad);
            }

            // 4. Sacudir objetos decorativos de la mesa (latas de cerveza, etc.)
            var untagged = GameObject.FindGameObjectsWithTag("Untagged");
            foreach (var go in untagged)
            {
                if (go != null && go.name.StartsWith("BeerCan"))
                {
                    float dist = Vector3.Distance(go.transform.position, impactPoint);
                    if (dist < 2.0f)
                    {
                        Vector3 canPos = go.transform.position;
                        go.transform.DOKill(false);
                        go.transform.DOJump(canPos, 0.035f, 1, 0.22f).SetEase(DG.Tweening.Ease.OutQuad);
                        go.transform.DORotate(go.transform.eulerAngles + new Vector3(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(-5f, 5f), UnityEngine.Random.Range(-3f, 3f)), 0.22f).SetEase(DG.Tweening.Ease.OutQuad);
                    }
                }
            }

            // Si hay scripts TablePropBounce explícitos
            var bounceProps = FindObjectsByType<TablePropBounce>(FindObjectsSortMode.None);
            foreach (var bp in bounceProps)
            {
                if (bp != null) bp.Bounce(impactPoint);
            }
        }

        public void AnimateCardsToDeck(System.Action onComplete = null)
        {
            // Clients track their own spawned table cards — mirror the cleanup there
            if (NetworkServer.active)
                (NetworkManager.singleton as MyNetworkingManager)?.BroadcastAnimateCardsToDeck();

            if (_currentDeckObj == null || (_spawnedCards.Count == 0 && _currentViraObj == null))
            {
                ClearTable();
                onComplete?.Invoke();
                return;
            }

            Vector3 deckPos = _currentDeckObj.transform.position;
            float duration = 0.5f;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX("cards_sweep_shove");
            }

            int cardsAnimating = _spawnedCards.Count;
            if (_currentViraObj != null) cardsAnimating++;

            if (cardsAnimating == 0)
            {
                ClearTable();
                onComplete?.Invoke();
                return;
            }

            System.Action onCardComplete = () =>
            {
                cardsAnimating--;
                if (cardsAnimating <= 0)
                {
                    ClearTable();
                    onComplete?.Invoke();
                }
            };

            foreach (var cardObj in _spawnedCards)
            {
                if (cardObj != null)
                {
                    cardObj.transform.DOMove(deckPos, duration).SetEase(DG.Tweening.Ease.InBack).OnComplete(() => onCardComplete());
                }
                else
                {
                    onCardComplete();
                }
            }

            if (_currentViraObj != null)
            {
                _currentViraObj.transform.DOMove(deckPos, duration).SetEase(DG.Tweening.Ease.InBack).OnComplete(() => onCardComplete());
            }
        }

        public void ClearTable()
        {
            // Destroy physical cards
            foreach (var cardObj in _spawnedCards)
            {
                if (cardObj != null) Destroy(cardObj);
            }
            if (_currentViraObj != null) Destroy(_currentViraObj);
            // Deck is purposely NOT destroyed so it can be animated to the next dealer

            _spawnedCards.Clear();
            _cardsPerPlayer.Clear();
            CardsInTable.Clear();
        }

        public void PlaceCard(Card card, GameObject player, Vector3? startPos = null)
        {
            card.ownerObj = player;
            
            // Calculate/re-evaluate realValue based on the current Vira card
            var deckCreator = DeckCreator.Instance;
            if (deckCreator != null && deckCreator.cardVira != null)
            {
                card.realValue = TrucoRules.GetCardRealValue(card, deckCreator.cardVira);
                Debug.Log($"[TableManager] PlaceCard: {card.value} de {card.suit}. RealValue={card.realValue}. Vira={deckCreator.cardVira.value} de {deckCreator.cardVira.suit}");
            }

            if (card.isBurned)
            {
                card.realValue = -1;
            }

            CardsInTable.Add(card);
            bool isMega = CheckIsMegaSlam(card);
            if (isMega) Debug.Log($"[TableManager] ¡MEGA SLAM A LA MESA! {card.value} de {card.suit} (RealValue={card.realValue})");

            SpawnCard3D(card, player, startPos, isMega);

            // On the host, a remote player's rendered hand must lose the played card.
            // No fallback: the local player's card was already removed by PlayCardToTable.
            var ownerHandler = player.GetComponent<CardsHandler>();
            ownerHandler?.RemoveRenderedCard(card.dbId, fallbackToLast: false);

            // Broadcast to non-host clients in multiplayer. A burned card travels with
            // its identity scrubbed: the value must not even reach the other clients.
            if (NetworkServer.active)
            {
                int seatIdx = SeatManager.Instance?.GetPlayerSeatIndex(player) ?? -1;
                var netMgr = NetworkManager.singleton as MyNetworkingManager;
                if (card.isBurned)
                    netMgr?.BroadcastCardOnTable(-1, 0, "", seatIdx, true);
                else
                    netMgr?.BroadcastCardOnTable(card.dbId, card.value, card.suit, seatIdx, false);
            }
            
            // Disable turn state immediately for local player to avoid double clicks
            var playerLocal = player.GetComponent<Code.Player.PlayerLocal>();
            if (playerLocal != null && playerLocal.player != null)
            {
                playerLocal.player.canPlayCard = false;

                // Solo el HUD de ESTA máquina. PlayerHUD.Instance es el del host, así que
                // sin el chequeo de isLocalPlayer la carta de cualquiera de los otros 3
                // jugadores le apagaba al host su cartel de turno y sus botones de canto.
                if (playerLocal.isLocalPlayer && global::Code.Player.PlayerHUD.Instance != null)
                {
                    global::Code.Player.PlayerHUD.Instance.UpdateTurnState(false, playerLocal.player.playerName);
                }
            }

            // Disparar evento de dominio
            OnCardPlaced?.Invoke(card, player);
            int placedSeatIdx = SeatManager.Instance != null ? SeatManager.Instance.GetPlayerSeatIndex(player) : -1;
            TrucoEvents.EmitCardPlayed(placedSeatIdx, card);
        }

        private string GetPlayerName(Card card)
        {
            if (card == null || card.ownerObj == null) return "Unknown";
            var pLocal = card.ownerObj.GetComponent<Code.Player.PlayerLocal>();
            if (pLocal != null && pLocal.player != null) return pLocal.player.playerName;
            var pNpc = card.ownerObj.GetComponent<Code.Player.NPCPlayer>();
            if (pNpc != null) return pNpc.playerName;
            return card.ownerObj.name;
        }

        /// <summary>Índice de equipo (0/1) del dueño de la carta, o -1 si no se puede
        /// resolver. Comparar por índice y no por nombre evita que dos dueños
        /// desconocidos ("Unknown Team") pasen por compañeros.</summary>
        private int GetTeamIndexOf(Card card)
        {
            if (card == null || card.ownerObj == null || GameManager.Instance == null) return -1;

            var pComp = card.ownerObj.GetComponent<Code.Player.Player>();
            if (pComp != null && pComp.team != null) return GameManager.Instance.GetTeamIndex(pComp.team);

            var npc = card.ownerObj.GetComponent<Code.Player.NPCPlayer>();
            if (npc != null && npc.team != null) return GameManager.Instance.GetTeamIndex(npc.team);

            return -1;
        }

        private string GetTeamName(Card card)
        {
            if (card == null || card.ownerObj == null) return "Unknown";
            var pLocal = card.ownerObj.GetComponent<Code.Player.PlayerLocal>();
            if (pLocal != null && pLocal.player != null && pLocal.player.team != null) return pLocal.player.team.teamName;
            var pNpc = card.ownerObj.GetComponent<Code.Player.NPCPlayer>();
            if (pNpc != null && pNpc.team != null) return pNpc.team.teamName;
            return "Unknown Team";
        }

        // [Server]
        public void DetermineHighestCard()
        {
            if (CardsInTable == null || CardsInTable.Count == 0)
            {
                return;
            }

            int highestValue = int.MinValue;
            List<Card> highestCards = new List<Card>();

            foreach (var card in CardsInTable)
            {
                if (card.realValue > highestValue)
                {
                    highestValue = card.realValue;
                    highestCards.Clear();
                    highestCards.Add(card);
                }
                else if (card.realValue == highestValue)
                {
                    highestCards.Add(card);
                }
            }

            if (highestCards.Count == 0) return;

            if (highestCards.Count > 1)
            {
                // Parda solo si las cartas más altas son de EQUIPOS distintos. Con 4
                // jugadores es normal que empaten dos del MISMO equipo (ahí gana el que
                // la tiró primero, que es highestCards[0]). Se compara por índice de
                // equipo y no por nombre: un dueño que no se pueda resolver cuenta como
                // equipo desconocido (-1) y no se fusiona con otro desconocido.
                int firstTeam = GetTeamIndexOf(highestCards[0]);
                bool differentTeams = false;
                foreach (var c in highestCards)
                {
                    int t = GetTeamIndexOf(c);
                    if (t < 0 || firstTeam < 0 || t != firstTeam)
                    {
                        differentTeams = true;
                        break;
                    }
                }

                if (differentTeams)
                {
                    GameManager.Instance.HandleTrickResult(null); // Null means tie
                    RpcHighestCard(highestCards[0], null); // Simplified RPC
                    OnTrickEvaluated?.Invoke();
                    return;
                }
            }

            Card winningCard = highestCards[0];
            
            GameManager.Instance.HandleTrickResult(winningCard.ownerObj);

            RpcHighestCard(winningCard, null);
            OnTrickEvaluated?.Invoke();
        }

        // [ClientRpc]
        private void RpcHighestCard(Card highestCard, Card lowestCard)
        {
            if (highestCard == null)
            {
                return;
            }
            Debug.Log($"Highest Card: {highestCard.value} of {highestCard.suit}, " +
                      $"Owner={GetPlayerName(highestCard)}, Team={GetTeamName(highestCard)}");
        }
    }
}
