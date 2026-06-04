using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.UI
{
    public class MainMenuController : MonoBehaviour
    {
        private UIDocument _uiDocument;
        private VisualElement _root;

        // Screens
        private VisualElement _screenMain;
        private VisualElement _screenPlay;
        private VisualElement _screenLobby;
        private VisualElement _screenSettings;
        private VisualElement _screenCredits;

        private List<VisualElement> _allScreens = new List<VisualElement>();

        private void OnEnable()
        {
            _uiDocument = GetComponent<UIDocument>();
            if (_uiDocument == null) return;

            _root = _uiDocument.rootVisualElement;
            if (_root == null) return;

            // Initialize Screens
            _screenMain = _root.Q<VisualElement>("screen-main");
            _screenPlay = _root.Q<VisualElement>("screen-play");
            _screenLobby = _root.Q<VisualElement>("screen-lobby");
            _screenSettings = _root.Q<VisualElement>("screen-settings");
            _screenCredits = _root.Q<VisualElement>("screen-credits");

            _allScreens = new List<VisualElement>
            {
                _screenMain, _screenPlay, _screenLobby, _screenSettings, _screenCredits
            };

            // Bind Main Menu Buttons
            var btnSingleplayer = _root.Q<Button>("btn-main-singleplayer");
            if (btnSingleplayer != null) btnSingleplayer.clicked += HandleSingleplayer;

            var btnPlay = _root.Q<Button>("btn-main-play");
            if (btnPlay != null) btnPlay.clicked += () => ShowScreen(_screenPlay);

            var btnSettings = _root.Q<Button>("btn-main-settings");
            if (btnSettings != null) btnSettings.clicked += () => ShowScreen(_screenSettings);

            var btnCredits = _root.Q<Button>("btn-main-credits");
            if (btnCredits != null) btnCredits.clicked += () => ShowScreen(_screenCredits);

            var btnQuit = _root.Q<Button>("btn-main-quit");
            if (btnQuit != null) btnQuit.clicked += HandleQuit;

            // Bind Play/Connection Buttons
            var btnPlayHost = _root.Q<Button>("btn-play-host");
            if (btnPlayHost != null) btnPlayHost.clicked += HandleHostRoom;

            var btnPlayJoin = _root.Q<Button>("btn-play-join");
            if (btnPlayJoin != null) btnPlayJoin.clicked += HandleJoinRoom;

            var btnPlayBack = _root.Q<Button>("btn-play-back");
            if (btnPlayBack != null) btnPlayBack.clicked += () => ShowScreen(_screenMain);

            // Bind Settings Buttons
            var btnSettingsBack = _root.Q<Button>("btn-settings-back");
            if (btnSettingsBack != null) btnSettingsBack.clicked += () => ShowScreen(_screenMain);

            // Bind Credits Buttons
            var btnCreditsBack = _root.Q<Button>("btn-credits-back");
            if (btnCreditsBack != null) btnCreditsBack.clicked += () => ShowScreen(_screenMain);

            // Bind Lobby Buttons
            var btnLobbyBack = _root.Q<Button>("btn-lobby-back");
            if (btnLobbyBack != null) btnLobbyBack.clicked += HandleLeaveLobby;
            
            var btnLobbyReady = _root.Q<Button>("btn-lobby-ready");
            if (btnLobbyReady != null) btnLobbyReady.clicked += HandleReadyState;

            var btnLobbyStart = _root.Q<Button>("btn-lobby-start");
            if (btnLobbyStart != null) btnLobbyStart.clicked += HandleStartGame;

            // Initial State
            ShowScreen(_screenMain);
        }

        private void ShowScreen(VisualElement targetScreen)
        {
            if (targetScreen == null) return;

            foreach (var screen in _allScreens)
            {
                if (screen != null)
                {
                    screen.style.display = (screen == targetScreen) ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
        }

        // --- Logic Handlers ---

        private void HandleSingleplayer()
        {
            Debug.Log("[MainMenu] Starting Singleplayer...");
            UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
        }

        private void HandleHostRoom()
        {
            Debug.Log("[MainMenu] Hosting Room...");
            // TODO: Call NetworkManager to host server/host match
            // After successful creation, transition to lobby:
            ShowScreen(_screenLobby);
        }

        private void HandleJoinRoom()
        {
            var codeInput = _root.Q<TextField>("input-room-code");
            string code = codeInput != null ? codeInput.value : "";
            Debug.Log($"[MainMenu] Joining Room with code: {code}");
            // TODO: Call NetworkManager to join by code
            // After successful join, transition to lobby:
            ShowScreen(_screenLobby);
        }

        private void HandleLeaveLobby()
        {
            Debug.Log("[MainMenu] Leaving Lobby...");
            // TODO: Call NetworkManager to disconnect
            ShowScreen(_screenPlay);
        }

        private void HandleReadyState()
        {
            Debug.Log("[MainMenu] Player is Ready!");
            // TODO: Toggle ready state over network
        }

        private void HandleStartGame()
        {
            Debug.Log("[MainMenu] Starting Game!");
            // TODO: Call NetworkManager to load GameScene
        }

        private void HandleQuit()
        {
            Debug.Log("[MainMenu] Quitting Application...");
            Application.Quit();
        }
    }
}
