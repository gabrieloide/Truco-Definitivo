using System;
using System.Linq;
using Code.GameLogic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;
using Code.Scripts.Audio;

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
        private Label _responseTitleLabel;
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
        private Button _infoButton;
        private Button _cameraButton;

        [Header("Card Action Menu")]
        private VisualElement _cardActionMenu;
        private Button _cardPlayBtn;
        private Button _cardBurnBtn;

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
            if (_notificationLabel == null) 
            {
                _notificationLabel = _root?.Q<Label>("notification-label");
                if (_notificationLabel == null)
                {
                    Debug.LogError("[PlayerHUD] Could not find notification-label in UI Root.");
                    return;
                }
            }

            _notificationLabel.text = message;
            _notificationLabel.AddToClassList("visible");

            // Auto-hide after duration
            CancelInvoke("HideNotification");
            Invoke("HideNotification", duration);
        }

        private void HideNotification()
        {
            if (_notificationLabel != null)
                _notificationLabel.RemoveFromClassList("visible");
        }

        private void Update()
        {
            if (_cardActionMenu == null) return;
            
            bool shouldShow = false;
            
            if (Application.isMobilePlatform)
            {
                var allPlayers = FindObjectsByType<PlayerLocal>(FindObjectsSortMode.None);
                var playerLocal = allPlayers.FirstOrDefault(p => p.isLocalPlayer && p.gameObject.activeInHierarchy);
                if (playerLocal != null && playerLocal.selectedCardInteraction != null)
                {
                    shouldShow = true;
                }
            }
            
            _cardActionMenu.style.display = shouldShow ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnCardPlayClicked()
        {
            var allPlayers = FindObjectsByType<PlayerLocal>(FindObjectsSortMode.None);
            var playerLocal = allPlayers.FirstOrDefault(p => p.isLocalPlayer && p.gameObject.activeInHierarchy);
            if (playerLocal != null && playerLocal.selectedCardInteraction != null)
            {
                playerLocal.selectedCardInteraction.PlayCardToTable(false);
                playerLocal.selectedCardInteraction = null;
            }
        }

        private void OnCardBurnClicked()
        {
            var allPlayers = FindObjectsByType<PlayerLocal>(FindObjectsSortMode.None);
            var playerLocal = allPlayers.FirstOrDefault(p => p.isLocalPlayer && p.gameObject.activeInHierarchy);
            if (playerLocal != null && playerLocal.selectedCardInteraction != null)
            {
                playerLocal.selectedCardInteraction.PlayCardToTable(true);
                playerLocal.selectedCardInteraction = null;
            }
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
            _responseTitleLabel = _root.Q<Label>("response-title-label");
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
                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlaySFX("slider_tick_pop");
                    }
                });
            }

            // Hierarchy Modal
            _hierarchyModal = _root.Q<VisualElement>("hierarchy-modal");
            _closeHierarchyButton = _root.Q<Button>("close-hierarchy-button");
            SetupButton(_closeHierarchyButton, HideCardHierarchy);

            // Pause Modal
            _pauseModal = _root.Q<VisualElement>("pause-modal");
            _resumeButton = _root.Q<Button>("resume-button");
            _quitButton = _root.Q<Button>("quit-button");
            SetupButton(_resumeButton, TogglePauseMenu);
            SetupButton(_quitButton, ShowQuitConfirm);

            // Quit Confirm Modal
            _quitConfirmModal = _root.Q<VisualElement>("quit-confirm-modal");
            _confirmYesButton = _root.Q<Button>("confirm-yes-button");
            _confirmNoButton = _root.Q<Button>("confirm-no-button");
            SetupButton(_confirmYesButton, ConfirmQuitGame);
            SetupButton(_confirmNoButton, CancelQuit);

            // Info Button (to open Hierarchy)
            _infoButton = _root.Q<Button>("info-button");
            SetupButton(_infoButton, ShowCardHierarchy);

            // Camera Button
            _cameraButton = _root.Q<Button>("camera-button");
            SetupButton(_cameraButton, ToggleCamera);

            // Card Action Menu
            _cardActionMenu = _root.Q<VisualElement>("card-action-menu");
            _cardPlayBtn = _root.Q<Button>("card-play-btn");
            _cardBurnBtn = _root.Q<Button>("card-burn-btn");

            SetupButton(_cardPlayBtn, OnCardPlayClicked);
            SetupButton(_cardBurnBtn, OnCardBurnClicked);

            // 2. Suscribirse a los eventos de los botones
            SetupButton(_aleyButton, OnALeyClicked);
            SetupButton(_envidoButton, OnEnvidoClicked);
            SetupButton(_trucoButton, OnTrucoClicked);
            SetupButton(_florButton, OnFlorClicked);
            SetupButton(_pauseButton, OnPauseClicked);

            SetupButton(_acceptButton, OnAcceptClicked);
            SetupButton(_declineButton, OnDeclineClicked);
            SetupButton(_moreButton, OnMoreClicked);

            // Ocultar los botones inicialmente
            SetActionsVisible(false);
            ShowResponseButtons(false);
        }

        private void OnDisable()
        {
            CleanupButton(_aleyButton, OnALeyClicked);
            CleanupButton(_envidoButton, OnEnvidoClicked);
            CleanupButton(_trucoButton, OnTrucoClicked);
            CleanupButton(_florButton, OnFlorClicked);
            CleanupButton(_pauseButton, OnPauseClicked);

            CleanupButton(_acceptButton, OnAcceptClicked);
            CleanupButton(_declineButton, OnDeclineClicked);
            CleanupButton(_moreButton, OnMoreClicked);
            
            CleanupButton(_closeHierarchyButton, HideCardHierarchy);
            CleanupButton(_resumeButton, TogglePauseMenu);
            CleanupButton(_quitButton, ShowQuitConfirm);
            CleanupButton(_confirmYesButton, ConfirmQuitGame);
            CleanupButton(_confirmNoButton, CancelQuit);
            CleanupButton(_infoButton, ShowCardHierarchy);
            CleanupButton(_cameraButton, ToggleCamera);

            CleanupButton(_cardPlayBtn, OnCardPlayClicked);
            CleanupButton(_cardBurnBtn, OnCardBurnClicked);

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

            // Flor solo si el jugador realmente la tiene Y es la primera ronda Y no se cantó
            var allPlayers = FindObjectsByType<PlayerLocal>(FindObjectsSortMode.None);
            var playerLocal = allPlayers.FirstOrDefault(p => p.isLocalPlayer && p.gameObject.activeInHierarchy);
            bool hasFlor = playerLocal != null && playerLocal.player != null && playerLocal.player.haveFlower;

            bool hasEnvido = false;
            if (playerLocal != null && playerLocal.cardsHandler != null && DeckCreator.Instance != null)
            {
                int score = TrucoRules.CalculateEnvidoScore(playerLocal.cardsHandler.InitialHand, DeckCreator.Instance.cardVira);
                hasEnvido = score > 0;
            }

            // Lógica para visibilidad de botones durante tu turno
            SetButtonVisible(_aleyButton, isFirstRound && !WasCalled(AnnounceState.ALey));
            
            bool showEnvido = isFirstRound && !WasCalled(AnnounceState.Envido);
            SetButtonVisible(_envidoButton, showEnvido);
            if (showEnvido && _envidoButton != null)
            {
                _envidoButton.SetEnabled(hasEnvido);
            }

            SetButtonVisible(_trucoButton, isFirstRound && !WasCalled(AnnounceState.Truco));
            SetButtonVisible(_florButton, hasFlor && isFirstRound && !WasCalled(AnnounceState.Flor));

            bool WasCalled(AnnounceState state)
            {
                if (announceManager == null) return false;
                return announceManager.WasAnnouncementCalledThisHand(state);
            }
        }
        public void ShowResponseButtons(bool visible, string acceptText = "QUIERO", string declineText = "NO QUIERO", bool showMore = false, bool showSlider = false, string title = "", bool disableAccept = false, bool showDecline = true)
        {
            if (_responseBar != null)
                _responseBar.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            if (visible)
            {
                if (_acceptButton != null)
                {
                    _acceptButton.Q<Label>().text = acceptText;
                    _acceptButton.SetEnabled(!disableAccept);
                }
                if (_declineButton != null)
                {
                    _declineButton.Q<Label>().text = declineText;
                    _declineButton.style.display = showDecline ? DisplayStyle.Flex : DisplayStyle.None;
                }
                if (_moreButton != null) _moreButton.style.display = showMore ? DisplayStyle.Flex : DisplayStyle.None;
                
                if (_sliderContainer != null) _sliderContainer.style.display = showSlider ? DisplayStyle.Flex : DisplayStyle.None;

                if (_responseTitleLabel != null)
                {
                    if (string.IsNullOrEmpty(title))
                    {
                        _responseTitleLabel.style.display = DisplayStyle.None;
                    }
                    else
                    {
                        _responseTitleLabel.text = title.ToUpper();
                        _responseTitleLabel.style.display = DisplayStyle.Flex;
                    }
                }

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
            global::Code.Core.GameEventManager.EmitAnnounceButtonClicked("ALeyButton");
        }

        private void OnEnvidoClicked()
        {
            global::Code.Core.GameEventManager.EmitAnnounceButtonClicked("EnvidoButton");
        }

        private void OnTrucoClicked()
        {
            global::Code.Core.GameEventManager.EmitAnnounceButtonClicked("TrucoButton");
        }

        private void OnFlorClicked()
        {
            global::Code.Core.GameEventManager.EmitAnnounceButtonClicked("FlorButton");
        }

        private void OnAcceptClicked()
        {
            global::Code.Core.GameEventManager.EmitAcceptButtonClicked();
        }

        private void OnDeclineClicked()
        {
            global::Code.Core.GameEventManager.EmitDeclineButtonClicked();
        }

        private void OnMoreClicked()
        {
            global::Code.Core.GameEventManager.EmitMoreButtonClicked();
        }

        private void OnPauseClicked()
        {
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
            
            bool isPauseVisible = _pauseModal.ClassListContains("visible");
            bool isConfirmVisible = _quitConfirmModal != null && _quitConfirmModal.ClassListContains("visible");
            bool isPaused = isPauseVisible || isConfirmVisible;


            if (isPaused)
            {
                _pauseModal.RemoveFromClassList("visible");
                if (_quitConfirmModal != null) _quitConfirmModal.RemoveFromClassList("visible");
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySFX("modal_close_whoosh");
                }
            }
            else
            {
                _pauseModal.AddToClassList("visible");
                if (_quitConfirmModal != null) _quitConfirmModal.RemoveFromClassList("visible");
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySFX("modal_open_whoosh");
                }
            }
        }

        private void ShowQuitConfirm()
        {
            if (_pauseModal != null) _pauseModal.RemoveFromClassList("visible");
            if (_quitConfirmModal != null) _quitConfirmModal.AddToClassList("visible");
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX("modal_open_whoosh");
            }
        }

        private void CancelQuit()
        {
            if (_quitConfirmModal != null) _quitConfirmModal.RemoveFromClassList("visible");
            if (_pauseModal != null) _pauseModal.AddToClassList("visible");
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX("modal_open_whoosh");
            }
        }

        private void ConfirmQuitGame()
        {
            
            // Destruir GameManager si existe para reiniciar la partida la próxima vez
            if (GameManager.Instance != null)
            {
                Destroy(GameManager.Instance.gameObject);
            }

            // Destruir el jugador local (PlayerLocal) si existe
            var localPlayer = FindAnyObjectByType<PlayerLocal>();
            if (localPlayer != null)
            {
                Destroy(localPlayer.gameObject);
            }

            // Destruir el DebugCommands si existe
            var debugCommands = FindAnyObjectByType<DebugCommands>();
            if (debugCommands != null)
            {
                Destroy(debugCommands.gameObject);
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

            if (GameManager.Instance != null)
            {
                Destroy(GameManager.Instance.gameObject);
            }

            var localPlayer = FindAnyObjectByType<PlayerLocal>();
            if (localPlayer != null)
            {
                Destroy(localPlayer.gameObject);
            }

            var debugCommands = FindAnyObjectByType<DebugCommands>();
            if (debugCommands != null)
            {
                Destroy(debugCommands.gameObject);
            }

            Destroy(gameObject);

            SceneChanger.Instance.ChangeScene("LobbyScene");
            SceneManager.LoadScene("LobbyScene");
        }

        private void UpdateLocalPlayerTeam()
        {
            if (_teamLabel != null)
            {
                _teamLabel.text = "TEAM 1    -    TEAM 2";
                _teamLabel.style.display = DisplayStyle.Flex;
            }
        }

        public void ShowCardHierarchy()
        {
            if (_hierarchyModal != null)
            {
                _hierarchyModal.AddToClassList("visible");
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySFX("modal_open_whoosh");
                }
            }
        }

        private void HideCardHierarchy()
        {
            if (_hierarchyModal != null)
            {
                _hierarchyModal.RemoveFromClassList("visible");
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySFX("modal_close_whoosh");
                }
            }
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
        }

        private void OnButtonHover(PointerEnterEvent evt)
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX("ui_button_hover_pop");
            }
        }

        private void SetupButton(Button button, System.Action onClickAction)
        {
            if (button == null) return;
            
            button.clicked -= onClickAction;
            button.clicked += onClickAction;
            
            button.clicked -= PlayClickSound;
            button.clicked += PlayClickSound;
            
            button.UnregisterCallback<PointerEnterEvent>(OnButtonHover);
            button.RegisterCallback<PointerEnterEvent>(OnButtonHover);
        }

        private void CleanupButton(Button button, System.Action onClickAction)
        {
            if (button == null) return;
            button.clicked -= onClickAction;
            button.clicked -= PlayClickSound;
            button.UnregisterCallback<PointerEnterEvent>(OnButtonHover);
        }

        private void PlayClickSound()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX("ui_button_click_press");
            }
        }
    }
}
