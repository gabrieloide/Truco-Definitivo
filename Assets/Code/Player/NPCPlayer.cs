using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Code.GameLogic;
using Code.Cards;

namespace Code.Player
{
    public class NPCPlayer : MonoBehaviour
    {
        public string playerName;
        public Team team;
        public GameObject card3DPrefab;
        public Transform handTransform;
        
        public List<Card> hand = new List<Card>();
        private List<GameObject> _visualCards = new List<GameObject>();
        public bool isMyTurn = false;

        public void ReceiveCards(List<Card> cards)
        {
            hand = cards;
            
            // Limpiamos mano anterior si hubiera
            foreach(var v in _visualCards) Destroy(v);
            _visualCards.Clear();

            // Spawneamos las cartas visualmente en el NPC
            for (int i = 0; i < hand.Count; i++)
            {
                Transform parent = handTransform != null ? handTransform : transform;
                Vector3 pos = new Vector3((i - 1) * 0.2f, 0, 0);
                
                GameObject c = Instantiate(card3DPrefab, parent);
                c.transform.localPosition = pos;
                c.transform.localRotation = Quaternion.Euler(0, 0, 180); // Boca abajo para nosotros, o ajusta según necesites
                
                _visualCards.Add(c);
            }
            
            Debug.Log($"[NPC {playerName}] He recibido {cards.Count} cartas físicas.");
        }

        private Coroutine _turnCoroutine;

        public void StartTurn()
        {
            if (isMyTurn) return;
            isMyTurn = true;
            
            if (_turnCoroutine != null) StopCoroutine(_turnCoroutine);
            _turnCoroutine = StartCoroutine(NPCLifeCycle());
        }

        private IEnumerator NPCLifeCycle()
        {
            yield return new WaitForSeconds(Random.Range(0.5f, 1.5f));

            bool announcementOccurred = false;

            if (GameManager.Instance.isAnnouncementPending)
            {
                announcementOccurred = true;
                while (GameManager.Instance.isAnnouncementPending) yield return new WaitForSeconds(0.5f);
            }

            // 1. Canto en primera ronda
            if (GameManager.Instance.round == 0)
            {
                var vira = FindAnyObjectByType<DeckCreator>().cardVira;
                if (NPCDecisionMaker.ShouldAnnounceFlor(hand, vira))
                {
                    Announce(AnnounceState.ALey);
                    announcementOccurred = true;
                    while (GameManager.Instance.isAnnouncementPending) yield return new WaitForSeconds(0.5f);
                }
                else if (NPCDecisionMaker.ShouldAnnounceEnvido(hand, vira))
                {
                    Announce(AnnounceState.Envido);
                    announcementOccurred = true;
                    while (GameManager.Instance.isAnnouncementPending) yield return new WaitForSeconds(0.5f);
                }
            }

            // 2. Truco
            var trucoAnnounce = FindAnyObjectByType<Code.GameLogic.Announcement.TrucoAnnouncement>();
            int trucoLevel = trucoAnnounce != null ? trucoAnnounce.acceptAmount : 0;
            if (NPCDecisionMaker.ShouldAnnounceTruco(hand, FindAnyObjectByType<DeckCreator>().cardVira) && trucoLevel == 0)
            {
                Announce(AnnounceState.Truco);
                announcementOccurred = true;
                while (GameManager.Instance.isAnnouncementPending) yield return new WaitForSeconds(0.5f);
            }

            if (GameManager.Instance.isAnnouncementPending)
            {
                announcementOccurred = true;
                while (GameManager.Instance.isAnnouncementPending) yield return new WaitForSeconds(0.5f);
            }

            if (announcementOccurred)
            {
                yield return new WaitForSeconds(1.5f);
            }

            // 3. Jugar carta
            if (isMyTurn) yield return PlayCard();
        }

        private void Announce(AnnounceState state)
        {
            Debug.Log($"[NPC {playerName}] Canta: {state}");
            var am = FindAnyObjectByType<AnnouncementManager>();
            if (am != null) am.ReceiveAnnounceFromNPC(state, gameObject);
        }

        public void HandleOpponentAnnounce(AnnounceState state, GameObject opponent)
        {
            StartCoroutine(RespondToAnnounce(state));
        }

        private IEnumerator RespondToAnnounce(AnnounceState state)
        {
            yield return new WaitForSeconds(Random.Range(1f, 2.5f));

            var vira = FindAnyObjectByType<DeckCreator>().cardVira;
            var am = FindAnyObjectByType<AnnouncementManager>();

            if (state == AnnounceState.Envido && NPCDecisionMaker.ShouldAnnounceFlor(hand, vira))
            {
                if (am != null) am.ReceiveAnnounceFromNPC(AnnounceState.ALey, gameObject);
                yield break;
            }

            bool accept = NPCDecisionMaker.ShouldAcceptAnnounce(state.ToString(), hand, vira);
            if (accept)
            {
                Debug.Log($"[NPC {playerName}] Dice: ¡QUIERO! ({state})");
                if (am != null) am.AcceptFromNPC(gameObject);
            }
            else
            {
                Debug.Log($"[NPC {playerName}] Dice: NO QUIERO ({state})");
                if (am != null) am.DeclineFromNPC(gameObject);
            }
        }

        public void ResetTurnState()
        {
            isMyTurn = false;
            if (_turnCoroutine != null) StopCoroutine(_turnCoroutine);
            _turnCoroutine = null;
        }

        private IEnumerator PlayCard()
        {
            if (hand.Count > 0 && _visualCards.Count > 0)
            {
                if (GameManager.Instance.isAnnouncementPending)
                {
                    // Si justo se inició un canto un milisegundo antes de que tire la carta
                    isMyTurn = true;
                    yield break;
                }

                Card cardToPlay = NPCDecisionMaker.SelectCardToPlay(hand, TableManager.Instance.CardsInTable, FindAnyObjectByType<DeckCreator>().cardVira);
                if (cardToPlay == null) cardToPlay = hand[0];
                
                hand.Remove(cardToPlay);

                GameObject visualCard = _visualCards[0];
                _visualCards.RemoveAt(0);
                Vector3? startPos = null;
                if (visualCard != null) 
                {
                    startPos = visualCard.transform.position;
                    Destroy(visualCard);
                }

                Debug.Log($"[NPC {playerName}] Juega: {cardToPlay.value} de {cardToPlay.suit} (valor real: {cardToPlay.realValue})");
                
                // IMPORTANTE: Liberar el turno ANTES de ejecutar el comando que avanza el juego
                isMyTurn = false;
                _turnCoroutine = null;

                var playCommand = new Code.GameLogic.Architecture.PlayCardCommand(cardToPlay, gameObject, startPos);
                playCommand.Execute();
            }
            else
            {
                isMyTurn = false;
                _turnCoroutine = null;
                GameManager.Instance.EndTurn();
            }
            yield return null;
        }
    }
}
