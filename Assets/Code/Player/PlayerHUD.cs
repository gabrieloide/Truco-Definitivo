using System;
using TMPro;
using UnityEngine;

namespace Code.Player
{
    public class PlayerHUD : MonoBehaviour
    {
        public static PlayerHUD Instance { get; private set; }
        [SerializeField] private TMP_Text currentTurn;

        [SerializeField] private TMP_Text currentScore;
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
            }
            else
            {
                Instance = this;
            }
        }
        

        public void ChangeCurrentTurnText(bool yourTurn)
        {
            currentTurn.text = yourTurn ? "Is your turn" : "Is not your turn";
        }
    }
}