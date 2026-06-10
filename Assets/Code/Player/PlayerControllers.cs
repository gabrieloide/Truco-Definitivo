using System;
using Code.GameLogic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Code.Player
{
    public class PlayerControllers : MonoBehaviour
    {
        private bool _isPaused = false;

        // Coroutine Start: in the multiplayer lobby GameManager doesn't exist until
        // GameScene loads, so the pause binding waits for it instead of throwing.
        private System.Collections.IEnumerator Start()
        {
            while (GameManager.Instance == null || GameManager.Instance.playerInput == null)
                yield return null;

            GameManager.Instance.playerInput.Player.Pause.performed += PauseMenu;
        }

        public void PauseMenu(InputAction.CallbackContext context)
        {
            if (PlayerHUD.Instance != null)
            {
                PlayerHUD.Instance.TogglePauseMenu();
            }
        }
        

        
        
    }
}