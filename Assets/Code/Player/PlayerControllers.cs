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
            if (FindAnyObjectByType<PlayerHUD>() == null)
                return;
            _isPaused = !_isPaused;
            PlayerHUD.Instance.pauseMenu.SetActive(_isPaused);
        }
        

        
        
    }
}