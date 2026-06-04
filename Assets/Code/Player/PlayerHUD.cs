using System;
using Code.GameLogic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;

namespace Code.Player
{
    public class PlayerHUD : MonoBehaviour
    {
        public static PlayerHUD Instance { get; private set; }
        
        [Header("Legacy UI References")]
        // Se removieron las referencias al Canvas viejo (pauseMenu, currentTurn, currentScore, etc)

        [Header("UI Toolkit References")]
        private UIDocument _uiDocument;
        private VisualElement _root;
        private Label _scoreLabel;
        private Label _teamLabel;
        private Label _turnLabel;
        private VisualElement _t1DotsContainer;
        private VisualElement _t2DotsContainer;
        private Button _envidoButton;
        private Button _trucoButton;
        private Button _florButton;
        private Button _aleyButton;
        private Button _pauseButton;

        [Header("Response Buttons")]
        private VisualElement _responseBar;
        private Button _acceptButton;
        private Button _declineButton;
        private Button _moreButton;

        [Header("Slider Elements")]
        private VisualElement _sliderContainer;
        private SliderInt _stonesSlider;
        private Label _sliderLabel;
        
        [Header("Hierarchy & Pause")]
        private VisualElement _hierarchyModal;
        private Button _closeHierarchyButton;
        private VisualElement _pauseModal;
        private Button _resumeButton;
        private Button _quitButton;
        
        private VisualElement _quitConfirmModal;
        private Button _confirmYesButton;
        private Button _confirmNoButton;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            InitializeUI();

            GameManager.OnTurnStarted += HandleTurnStarted;
            GameManager.OnScoreChanged += UpdateScore;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "GameScene")
            {
                InitializeUI();
                
                if (GameManager.Instance != null && GameManager.Instance.teams.Count >= 2)
                {
                    UpdateScore(GameManager.Instance.teams[0].teamScore, GameManager.Instance.teams[1].teamScore);
                }
            }
        }

        private void Start()
        {
            if (GameManager.Instance != null && GameManager.Instance.teams.Count >= 2)
            {
                UpdateScore(GameManager.Instance.teams[0].teamScore, GameManager.Instance.teams[1].teamScore);
            }
        }

        private Label _notificationLabel;

        public void NotifyEvent(string message, float duration = 3f)
        {
            Debug.Log($"[PlayerHUD] NotifyEvent called: {message}");
            if (_notificationLabel == null) 
            {
                Debug.LogWarning("[PlayerHUD] _notificationLabel is null, searching again...");
                _notificationLabel = _root?.Q<Label>("notification-label");
                if (_notificationLabel == null)
                {
                    Debug.LogError("[PlayerHUD] Could not find notification-label in UI Root.");
                    return;
                }
            }

            _notificationLabel.text = message;
            _notificationLabel.style.display = DisplayStyle.Flex;
            _notificationLabel.style.opacity = 1f;

            // Auto-hide after duration
            CancelInvoke("HideNotification");
            Invoke("HideNotification", duration);
        }

        private void HideNotification()
        {
            if (_notificationLabel != null)
                _notificationLabel.style.display = DisplayStyle.None;
        }

        private void InitializeUI()
        {
            _uiDocument = GetComponent<UIDocument>();
            if (_uiDocument == null) _uiDocument = GetComponentInChildren<UIDocument>();

            if (_uiDocument == null) return;

            _root = _uiDocument.rootVisualElement;
            if (_root == null) return;

            _scoreLabel = _root.Q<Label>("score-label");
            _teamLabel = _root.Q<Label>("team-label");
            _turnLabel = _root.Q<Label>("turn-label");
            
            // Tricks dots
            _t1DotsContainer = _root.Q<VisualElement>("t1-dots");
            _t2DotsContainer = _root.Q<VisualElement>("t2-dots");

            // Notification Label (assuming it exists or creating a fallback)
            _notificationLabel = _root.Q<Label>("notification-label");
            if (_notificationLabel == null)
            {
                // Si no existe en el UXML, lo buscamos por un nombre genérico o lo ignoramos
                _notificationLabel = _root.Q<Label>("event-label");
            }

            if (_notificationLabel != null) _notificationLabel.text = "";

            UpdateLocalPlayerTeam();

            _aleyButton = _root.Q<Button>("aley-button");
            _envidoButton = _root.Q<Button>("envido-button");
            _trucoButton = _root.Q<Button>("truco-button");
            _florButton = _root.Q<Button>("flor-button");
            _pauseButton = _root.Q<Button>("pause-button");

            // Response Buttons
            _responseBar = _root.Q<VisualElement>("hud-response-bar");
            _acceptButton = _root.Q<Button>("accept-button");
            _declineButton = _root.Q<Button>("decline-button");
            _moreButton = _root.Q<Button>("more-button");

            // Slider Elements
            _sliderContainer = _root.Q<VisualElement>("hud-slider-container");
            _stonesSlider = _root.Q<SliderInt>("stones-slider");
            _sliderLabel = _root.Q<Label>("slider-value-label");

            if (_stonesSlider != null)
            {
                _stonesSlider.RegisterValueChangedCallback(evt => {
                    if (_sliderLabel != null) _sliderLabel.text = $"+{evt.newValue} {(evt.newValue == 1 ? "PIEDRA" : "PIEDRAS")}";
                });
            }

            // Hierarchy Modal
            _hierarchyModal = _root.Q<VisualElement>("hierarchy-modal");
            _closeHierarchyButton = _root.Q<Button>("close-hierarchy-button");
            if (_closeHierarchyButton != null)
            {
                _closeHierarchyButton.clicked -= HideCardHierarchy;
                _closeHierarchyButton.clicked += HideCardHierarchy;
            }

            // Pause Modal
            _pauseModal = _root.Q<VisualElement>("pause-modal");
            _resumeButton = _root.Q<Button>("resume-button");
            _quitButton = _root.Q<Button>("quit-button");
            if (_resumeButton != null) 
            {
                _resumeButton.clicked -= TogglePauseMenu;
                _resumeButton.clicked += TogglePauseMenu;
            }
            if (_quitButton != null) 
            {
                _quitButton.clicked -= ShowQuitConfirm;
                _quitButton.clicked += ShowQuitConfirm;
            }

            // Quit Confirm Modal
            _quitConfirmModal = _root.Q<VisualElement>("quit-confirm-modal");
            if (_quitConfirmModal != null) _quitConfirmModal.style.display = DisplayStyle.None;
            
            _confirmYesButton = _root.Q<Button>("confirm-yes-button");
            if (_confirmYesButton != null) 
            {
                _confirmYesButton.clicked -= ConfirmQuitGame;
                _confirmYesButton.clicked += ConfirmQuitGame;
            }

            _confirmNoButton = _root.Q<Button>("confirm-no-button");
            if (_confirmNoButton != null) 
            {
                _confirmNoButton.clicked -= CancelQuit;
                _confirmNoButton.clicked += CancelQuit;
            }

            // Info Button (to open Hierarchy)
            var infoButton = _root.Q<Button>("info-button");
            if (infoButton != null) 
            {
                infoButton.clicked -= ShowCardHierarchy;
                infoButton.clicked += ShowCardHierarchy;
            }

            // Camera Button
            var cameraButton = _root.Q<Button>("camera-button");
            if (cameraButton != null) 
            {
                cameraButton.clicked -= ToggleCamera;
                cameraButton.clicked += ToggleCamera;
            }

            // 2. Suscribirse a los eventos de los botones (limpiando primero por si InitializeUI se llama múltiples veces)
            if (_aleyButton != null) { _aleyButton.clicked -= OnALeyClicked; _aleyButton.clicked += OnALeyClicked; }
            if (_envidoButton != null) { _envidoButton.clicked -= OnEnvidoClicked; _envidoButton.clicked += OnEnvidoClicked; }
            if (_trucoButton != null) { _trucoButton.clicked -= OnTrucoClicked; _trucoButton.clicked += OnTrucoClicked; }
            if (_florButton != null) { _florButton.clicked -= OnFlorClicked; _florButton.clicked += OnFlorClicked; }
            if (_pauseButton != null) { _pauseButton.clicked -= OnPauseClicked; _pauseButton.clicked += OnPauseClicked; }

            if (_acceptButton != null) { _acceptButton.clicked -= OnAcceptClicked; _acceptButton.clicked += OnAcceptClicked; }
            if (_declineButton != null) { _declineButton.clicked -= OnDeclineClicked; _declineButton.clicked += OnDeclineClicked; }
            if (_moreButton != null) { _moreButton.clicked -= OnMoreClicked; _moreButton.clicked += OnMoreClicked; }

            // Ocultar los botones inicialmente
            SetActionsVisible(false);
            ShowResponseButtons(false);
        }

        private void OnDisable()
        {
            if (_aleyButton != null) _aleyButton.clicked -= OnALeyClicked;
            if (_envidoButton != null) _envidoButton.clicked -= OnEnvidoClicked;
            if (_trucoButton != null) _trucoButton.clicked -= OnTrucoClicked;
            if (_florButton != null) _florButton.clicked -= OnFlorClicked;
            if (_pauseButton != null) _pauseButton.clicked -= OnPauseClicked;

            if (_acceptButton != null) _acceptButton.clicked -= OnAcceptClicked;
            if (_declineButton != null) _declineButton.clicked -= OnDeclineClicked;
            if (_moreButton != null) _moreButton.clicked -= OnMoreClicked;
            if (_closeHierarchyButton != null) _closeHierarchyButton.clicked -= HideCardHierarchy;
            if (_resumeButton != null) _resumeButton.clicked -= TogglePauseMenu;
            if (_quitButton != null) _quitButton.clicked -= ShowQuitConfirm;
            if (_confirmYesButton != null) _confirmYesButton.clicked -= ConfirmQuitGame;
            if (_confirmNoButton != null) _confirmNoButton.clicked -= CancelQuit;

            var infoButton = _root?.Q<Button>("info-button");
            if (infoButton != null) infoButton.clicked -= ShowCardHierarchy;

            var cameraButton = _root?.Q<Button>("camera-button");
            if (cameraButton != null) cameraButton.clicked -= ToggleCamera;

            GameManager.OnTurnStarted -= HandleTurnStarted;
            GameManager.OnScoreChanged -= UpdateScore;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void HandleTurnStarted(int index, GameObject occupant)
        {
            UpdateLocalPlayerTeam();
            var localPlayer = occupant.GetComponent<PlayerLocal>();
            string playerName = "Unknown";
            
            if (localPlayer != null && localPlayer.player != null) playerName = localPlayer.player.playerName;
            else if (occupant.GetComponent<NPCPlayer>() != null) playerName = occupant.GetComponent<NPCPlayer>().playerName;
            else playerName = occupant.name;

            UpdateTurnState(localPlayer != null, playerName);

            // Mostrar aviso visual del turno
            if (localPlayer != null)
            {
                NotifyEvent("¡TU TURNO!", 1.5f);
            }
            else
            {
                NotifyEvent($"TURNO DE: {playerName.ToUpper()}", 1.5f);
            }
        }

        public void UpdateScore(int team1, int team2)
        {
            UpdateScore(team1, team2, 0, 0);
        }

        public void UpdateScore(int team1, int team2, int roundsTeam1, int roundsTeam2)
        {
            if (_scoreLabel != null)
                _scoreLabel.text = $"{team1} | {team2}";
            
            UpdateDots(_t1DotsContainer, roundsTeam1);
            UpdateDots(_t2DotsContainer, roundsTeam2);
        }

        private void UpdateDots(VisualElement container, int score)
        {
            if (container == null) return;
            
            int index = 0;
            foreach (var child in container.Children())
            {
                if (index < score) child.AddToClassList("filled");
                else child.RemoveFromClassList("filled");
                index++;
            }
        }

        public void UpdateTurnState(bool isYourTurn, string playerName = "")
        {
            string turnText = isYourTurn ? "IT'S YOUR TURN" : $"TURN OF: {playerName.ToUpper()}";
            Color turnColor = isYourTurn ? Color.green : Color.white;

            if (_turnLabel != null)
            {
                _turnLabel.text = turnText;
                _turnLabel.style.color = new StyleColor(turnColor);
            }

            RefreshActionButtons(isYourTurn);
        }

        public void RefreshActionButtons(bool canPlay)
        {
            if (!canPlay)
            {
                SetButtonVisible(_aleyButton, false);
                SetButtonVisible(_envidoButton, false);
                SetButtonVisible(_trucoButton, false);
                SetButtonVisible(_florButton, false);
                return;
            }

            // Restricción: Solo se puede cantar en la primera ronda (Round 0)
            bool isFirstRound = GameManager.Instance != null && GameManager.Instance.round == 0;
            var announceManager = FindAnyObjectByType<AnnouncementManager>();

            // Lógica para visibilidad de botones durante tu turno
            SetButtonVisible(_aleyButton, isFirstRound && !WasCalled(AnnounceState.ALey));
            SetButtonVisible(_envidoButton, isFirstRound && !WasCalled(AnnounceState.Envido));
            SetButtonVisible(_trucoButton, isFirstRound && !WasCalled(AnnounceState.Truco));

            // Flor solo si el jugador realmente la tiene Y es la primera ronda Y no se cantó
            var playerLocal = FindAnyObjectByType<PlayerLocal>();
            bool hasFlor = playerLocal != null && playerLocal.player != null && playerLocal.player.haveFlower;
            SetButtonVisible(_florButton, hasFlor && isFirstRound && !WasCalled(AnnounceState.Flor));

            bool WasCalled(AnnounceState state)
            {
                if (announceManager == null) return false;
                return announceManager.WasAnnouncementCalledThisHand(state);
            }
        }
        public void ShowResponseButtons(bool visible, string acceptText = "QUIERO", string declineText = "NO QUIERO", bool showMore = false, bool showSlider = false)
        {
            if (_responseBar != null)
                _responseBar.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            if (visible)
            {
                if (_acceptButton != null) _acceptButton.Q<Label>().text = acceptText;
                if (_declineButton != null) _declineButton.Q<Label>().text = declineText;
                if (_moreButton != null) _moreButton.style.display = showMore ? DisplayStyle.Flex : DisplayStyle.None;
                
                if (_sliderContainer != null) _sliderContainer.style.display = showSlider ? DisplayStyle.Flex : DisplayStyle.None;

                // Hide normal action buttons when responding
                SetActionsVisible(false);
            }
        }

        private void SetButtonVisible(Button button, bool visible)
        {
            if (button != null)
            {
                button.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void SetActionsVisible(bool visible) => RefreshActionButtons(visible);

        // Callbacks de los botones
        
        private void OnALeyClicked()
        {
            Debug.Log("[PlayerHUD] ¡A Ley! presionado en UI Toolkit.");
            Code.Core.GameEventManager.EmitAnnounceButtonClicked("ALeyButton");
        }

        private void OnEnvidoClicked()
        {
            Debug.Log("[PlayerHUD] ¡Envido! presionado en UI Toolkit.");
            Code.Core.GameEventManager.EmitAnnounceButtonClicked("EnvidoButton");
        }

        private void OnTrucoClicked()
        {
            Debug.Log("[PlayerHUD] ¡Quiero Truco! presionado en UI Toolkit.");
            Code.Core.GameEventManager.EmitAnnounceButtonClicked("TrucoButton");
        }

        private void OnFlorClicked()
        {
            Debug.Log("[PlayerHUD] ¡Flor! presionado en UI Toolkit.");
            Code.Core.GameEventManager.EmitAnnounceButtonClicked("FlorButton");
        }

        private void OnAcceptClicked()
        {
            Debug.Log("[PlayerHUD] Accept clicked");
            Code.Core.GameEventManager.EmitAcceptButtonClicked();
        }

        private void OnDeclineClicked()
        {
            Debug.Log("[PlayerHUD] Decline clicked");
            Code.Core.GameEventManager.EmitDeclineButtonClicked();
        }

        private void OnMoreClicked()
        {
            Debug.Log("[PlayerHUD] More clicked");
            Code.Core.GameEventManager.EmitMoreButtonClicked();
        }

        private void OnPauseClicked()
        {
            Debug.Log("[PlayerHUD] Click en botón de pausa de la UI (II).");
            if (PlayerHUD.Instance != null)
            {
                PlayerHUD.Instance.TogglePauseMenu();
            }
            else
            {
                TogglePauseMenu();
            }
        }

        public void TogglePauseMenu()
        {
            if (_pauseModal == null) 
            {
                Debug.LogError("[PlayerHUD] _pauseModal es nulo en TogglePauseMenu.");
                return;
            }
            
            bool isPauseModalFlex = _pauseModal.style.display == DisplayStyle.Flex;
            bool isConfirmModalFlex = _quitConfirmModal != null && _quitConfirmModal.style.display == DisplayStyle.Flex;
            bool isPaused = isPauseModalFlex || isConfirmModalFlex;

            Debug.Log($"[PlayerHUD] TogglePauseMenu llamado. isPaused = {isPaused} (pauseModal={isPauseModalFlex}, confirmModal={isConfirmModalFlex})");

            if (isPaused)
            {
                Debug.Log("[PlayerHUD] Ocultando menús de pausa.");
                _pauseModal.style.display = DisplayStyle.None;
                if (_quitConfirmModal != null) _quitConfirmModal.style.display = DisplayStyle.None;
            }
            else
            {
                Debug.Log("[PlayerHUD] Mostrando menú de pausa.");
                _pauseModal.style.display = DisplayStyle.Flex;
                if (_quitConfirmModal != null) _quitConfirmModal.style.display = DisplayStyle.None;
            }
        }

        private void ShowQuitConfirm()
        {
            if (_pauseModal != null) _pauseModal.style.display = DisplayStyle.None;
            if (_quitConfirmModal != null) _quitConfirmModal.style.display = DisplayStyle.Flex;
        }

        private void CancelQuit()
        {
            if (_quitConfirmModal != null) _quitConfirmModal.style.display = DisplayStyle.None;
            if (_pauseModal != null) _pauseModal.style.display = DisplayStyle.Flex;
        }

        private void ConfirmQuitGame()
        {
            Debug.Log("[PlayerHUD] Cargando MainMenu y limpiando estado...");
            
            // Destruir GameManager si existe para reiniciar la partida la próxima vez
            if (GameManager.Instance != null)
            {
                Destroy(GameManager.Instance.gameObject);
            }

            // Destruir también al jugador local / PlayerHUD que tiene DontDestroyOnLoad
            // para que no aparezca el HUD en el MainMenu.
            Destroy(gameObject);

            // Cargar MainMenu
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        public void ChangeScoreText()
        {
            if (GameManager.Instance != null && GameManager.Instance.teams.Count >= 2)
            {
                UpdateScore(GameManager.Instance.teams[0].teamScore, GameManager.Instance.teams[1].teamScore);
            }
        }

        public void PauseMenuButton() =>
            FindAnyObjectByType<PlayerControllers>().PauseMenu(new InputAction.CallbackContext());

        public void ChangeCurrentTurnText(bool yourTurn)
        {
            // Handled via events
        }

        public void LeaveNetworkButton()
        {
            if (NetworkServer.active && NetworkClient.isConnected)
            {
                NetworkManager.singleton.StopHost();
            }
            else if (NetworkClient.isConnected)
            {
                NetworkManager.singleton.StopClient();
            }
            else if (NetworkServer.active)
            {
                NetworkManager.singleton.StopServer();
            }

            SceneChanger.Instance.ChangeScene("LobbyScene");
            SceneManager.LoadScene("LobbyScene");
        }

        private void UpdateLocalPlayerTeam()
        {
            if (_teamLabel == null) return;
            
            var playerLocal = FindAnyObjectByType<PlayerLocal>();
            if (playerLocal != null && playerLocal.player != null && playerLocal.player.team != null)
            {
                _teamLabel.text = playerLocal.player.team.teamName;
                _teamLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _teamLabel.style.display = DisplayStyle.None;
            }
        }

        public void ShowCardHierarchy()
        {
            if (_hierarchyModal != null)
                _hierarchyModal.style.display = DisplayStyle.Flex;
        }

        private void HideCardHierarchy()
        {
            if (_hierarchyModal != null)
                _hierarchyModal.style.display = DisplayStyle.None;
        }

        private void ToggleCamera()
        {
            if (NetworkClient.localPlayer != null)
            {
                var camManager = NetworkClient.localPlayer.GetComponentInChildren<CameraManager>(true);
                if (camManager != null)
                {
                    camManager.ToggleAlternativeCamera();
                    return;
                }
            }

            var camManagers = FindObjectsByType<CameraManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var cm in camManagers)
            {
                if (cm.isLocalPlayer)
                {
                    cm.ToggleAlternativeCamera();
                    return;
                }
            }
            Debug.LogWarning("[PlayerHUD] No se encontró ningún CameraManager con isLocalPlayer = true, ni en el localPlayer ni en la escena.");
        }
    }
}