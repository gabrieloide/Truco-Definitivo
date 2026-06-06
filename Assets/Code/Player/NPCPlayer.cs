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
        public List<Card> initialHand = new List<Card>();
        private List<GameObject> _visualCards = new List<GameObject>();
        public bool isMyTurn = false;

        public void ClearCards()
        {
            hand.Clear();
            initialHand.Clear();
            foreach (var v in _visualCards) if (v != null) Destroy(v);
            _visualCards.Clear();
        }

        public void ReceiveSingleCard(Card card)
        {
            hand.Add(card);
            initialHand.Add(card);

            int i = _visualCards.Count;
            Transform parent = handTransform != null ? handTransform : transform;
            Vector3 localPos = new Vector3((i - 1) * 0.2f, 0, 0);
            Quaternion localRot = Quaternion.Euler(0, 0, 180); // Boca abajo para nosotros

            GameObject c = Instantiate(card3DPrefab, parent);
            _visualCards.Add(c);

            var juicyAnimator = c.GetComponent<JuicyCardAnimator>();
            if (juicyAnimator == null) juicyAnimator = c.AddComponent<JuicyCardAnimator>();

            Vector3 startWorldPos;
            if (TableManager.Instance != null)
            {
                startWorldPos = TableManager.Instance.CurrentDeckPosition;
                startWorldPos.y -= 0.015f; // Animación sale desde debajo del mazo
            }
            else
            {
                startWorldPos = parent.position;
            }

            juicyAnimator.AnimateToHand(startWorldPos, localPos, localRot, 0.5f, 0f, () =>
            {
                if (Code.Scripts.Audio.AudioManager.Instance != null)
                {
                    Code.Scripts.Audio.AudioManager.Instance.PlaySFX("card_deal_swoosh");
                }
            });
        }

        public void ReceiveCards(List<Card> cards)
        {
            ClearCards();
            for (int i = 0; i < cards.Count; i++)
            {
                ReceiveSingleCard(cards[i]);
            }
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
                var vira = DeckCreator.Instance.cardVira;
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
            if (NPCDecisionMaker.ShouldAnnounceTruco(hand, DeckCreator.Instance.cardVira) && trucoLevel == 0)
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

            var vira = DeckCreator.Instance.cardVira;
            var am = FindAnyObjectByType<AnnouncementManager>();

            if (state == AnnounceState.Envido && NPCDecisionMaker.ShouldAnnounceFlor(hand, vira))
            {
                if (am != null) am.ReceiveAnnounceFromNPC(AnnounceState.ALey, gameObject);
                yield break;
            }

            bool accept = NPCDecisionMaker.ShouldAcceptAnnounce(state.ToString(), hand, vira);
            if (accept)
            {
                if (am != null) am.AcceptFromNPC(gameObject);
            }
            else
            {
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

                Card cardToPlay = NPCDecisionMaker.SelectCardToPlay(hand, TableManager.Instance.CardsInTable, DeckCreator.Instance.cardVira);
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
