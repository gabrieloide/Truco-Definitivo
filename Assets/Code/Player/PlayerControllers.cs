using System;
using Code.GameLogic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Code.Player
{
    public class PlayerControllers : MonoBehaviour
    {
        private bool _isPaused = false;

        private void Start()
        {
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