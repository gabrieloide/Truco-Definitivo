using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Code.Networking;
using Mirror;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.UI
{
    public class MainMenuController : MonoBehaviour
    {
        public static int SingleplayerMaxPoints = 12;

        private UIDocument _uiDocument;
        private VisualElement _root;

        // Screens
        private VisualElement _screenMain;
        private VisualElement _screenSingleplayerSetup;
        private VisualElement _screenPlay;
        private VisualElement _screenLobby;
        private VisualElement _screenSettings;
        private VisualElement _screenCredits;

        private VisualElement[] _allScreens;

        // Lobby UI labels
        private Label _lblRoomCode;
        private Label _lblT1P1, _lblT1P2, _lblT2P1, _lblT2P2;
        private Button _btnLobbyStart;
        private Button _btnLobbyReady;
        private Label _lblPlayStatus;
        private Label _lblLobbyStatus;

        private float _lobbyRefreshTimer;
        private Coroutine _statusRoutine;

        private void OnEnable()
        {
            _uiDocument = GetComponent<UIDocument>();
            if (_uiDocument == null) return;

            _root = _uiDocument.rootVisualElement;
            if (_root == null) return;

            // Screens
            _screenMain             = _root.Q<VisualElement>("screen-main");
            _screenSingleplayerSetup = _root.Q<VisualElement>("screen-singleplayer-setup");
            _screenPlay             = _root.Q<VisualElement>("screen-play");
            _screenLobby            = _root.Q<VisualElement>("screen-lobby");
            _screenSettings         = _root.Q<VisualElement>("screen-settings");
            _screenCredits          = _root.Q<VisualElement>("screen-credits");

            _allScreens = new[]
            {
                _screenMain, _screenSingleplayerSetup, _screenPlay,
                _screenLobby, _screenSettings, _screenCredits
            };

            // Lobby labels
            _lblRoomCode  = _root.Q<Label>("lbl-room-code");
            _lblT1P1      = _root.Q<Label>("lbl-t1-p1");
            _lblT1P2      = _root.Q<Label>("lbl-t1-p2");
            _lblT2P1      = _root.Q<Label>("lbl-t2-p1");
            _lblT2P2      = _root.Q<Label>("lbl-t2-p2");
            _btnLobbyStart = _root.Q<Button>("btn-lobby-start");
            _btnLobbyReady = _root.Q<Button>("btn-lobby-ready");
            _lblPlayStatus  = _root.Q<Label>("lbl-play-status");
            _lblLobbyStatus = _root.Q<Label>("lbl-lobby-status");

            // Main Menu
            Bind("btn-main-singleplayer", () => ShowScreen(_screenSingleplayerSetup));
            Bind("btn-main-play",         () => ShowScreen(_screenPlay));
            Bind("btn-main-settings",     () => ShowScreen(_screenSettings));
            Bind("btn-main-credits",      () => ShowScreen(_screenCredits));

            var btnQuit = _root.Q<Button>("btn-main-quit");
            if (btnQuit != null) btnQuit.style.display = DisplayStyle.None;

            // Singleplayer Setup
            Bind("btn-single-start", HandleSingleplayerStart);
            Bind("btn-single-back",  () => ShowScreen(_screenMain));

            // Play / Connection
            Bind("btn-play-host", HandleHostRoom);
            Bind("btn-play-join", HandleJoinRoom);
            Bind("btn-play-back", () => ShowScreen(_screenMain));

            // Settings
            Bind("btn-settings-back", () => ShowScreen(_screenMain));

            // Credits
            Bind("btn-credits-back", () => ShowScreen(_screenMain));

            // Lobby
            Bind("btn-lobby-back",   HandleLeaveLobby);
            Bind("btn-lobby-ready",  HandleReadyState);
            Bind("btn-lobby-start",  HandleStartGame);
            Bind("btn-lobby-switch", HandleSwitchTeam);
            Bind("btn-copy-code",    HandleCopyRoomCode);

            // El código en sí también copia al clickearlo
            _lblRoomCode?.RegisterCallback<ClickEvent>(_ => HandleCopyRoomCode());

            // Only host sees Start button
            if (_btnLobbyStart != null)
                _btnLobbyStart.style.display = DisplayStyle.None;

            // Nickname persistente entre sesiones (en WebGL sobrevive a recargar la página)
            var nickField = _root.Q<TextField>("input-nickname");
            if (nickField != null)
            {
                string savedNick = PlayerPrefs.GetString("playerNickname", "");
                if (!string.IsNullOrEmpty(savedNick)) nickField.value = savedNick;

                nickField.RegisterValueChangedCallback(evt =>
                {
                    PlayerPrefs.SetString("playerNickname", evt.newValue ?? "");
                    PlayerPrefs.Save();
                });
            }

            ShowScreen(_screenMain);
        }

        private void Bind(string name, Action callback)
        {
            var btn = _root.Q<Button>(name);
            if (btn != null) btn.clicked += callback;
        }

        private void ShowScreen(VisualElement target)
        {
            if (target == null) return;
            foreach (var s in _allScreens)
                if (s != null)
                    s.style.display = s == target ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // ─────────────────────── Singleplayer ─────────────────────────────

        private void HandleSingleplayerStart()
        {
            var dropdown = _root.Q<DropdownField>("dropdown-single-points");
            if (dropdown != null && int.TryParse(dropdown.value, out int pts))
                SingleplayerMaxPoints = pts;

            UnityEngine.SceneManagement.SceneManager.LoadScene("GameScene");
        }

        // ─────────────────────── Multiplayer HOST ─────────────────────────

        private async void HandleHostRoom()
        {
            string playerName = GetNickname();
            if (string.IsNullOrWhiteSpace(playerName))
            {
                ShowStatus(_lblPlayStatus, "Ingresá tu nombre antes de crear una sala.");
                return;
            }

            SetButtonsInteractable(false);

            try
            {
                // Ensure UnityServicesManager exists in scene
                EnsureServicesManager();

                string lobbyCode = await UnityServicesManager.Instance.CreateHostAsync(playerName);

                // Configure and start Mirror host using relay transport
                var netMgr = NetworkManager.singleton;
                if (netMgr == null)
                    throw new Exception("NetworkManager.singleton is null — is the NetworkManager GameObject active in the MainMenu scene?");
                netMgr.StartHost();

                ShowLobbyScreen(lobbyCode, isHost: true);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MainMenuController] Host error: {e.Message}");
                ShowStatus(_lblPlayStatus, "No se pudo crear la sala. Revisá tu conexión e intentá de nuevo.");
                SetButtonsInteractable(true);
            }
        }

        // ─────────────────────── Multiplayer CLIENT ───────────────────────

        private async void HandleJoinRoom()
        {
            string playerName = GetNickname();
            if (string.IsNullOrWhiteSpace(playerName))
            {
                ShowStatus(_lblPlayStatus, "Ingresá tu nombre antes de unirte a una sala.");
                return;
            }

            var codeInput = _root.Q<TextField>("input-room-code");
            string code   = codeInput != null ? codeInput.value.Trim() : "";

            if (string.IsNullOrEmpty(code))
            {
                ShowStatus(_lblPlayStatus, "Ingresá el código de la sala a la que querés unirte.");
                return;
            }

            SetButtonsInteractable(false);

            try
            {
                EnsureServicesManager();

                await UnityServicesManager.Instance.JoinClientAsync(code, playerName);

                // Start Mirror client — address is unused (relay handles routing)
                var netMgr = NetworkManager.singleton;
                if (netMgr == null)
                    throw new Exception("NetworkManager.singleton is null — is the NetworkManager GameObject active in the MainMenu scene?");
                netMgr.StartClient();

                ShowLobbyScreen(code, isHost: false);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MainMenuController] Join error: {e.Message}");
                ShowStatus(_lblPlayStatus, "Ese código no funciona: la sala no existe o ya se cerró. Revisalo e intentá de nuevo.");
                SetButtonsInteractable(true);
            }
        }

        // ─────────────────────── Lobby screen ─────────────────────────────

        private void ShowLobbyScreen(string code, bool isHost)
        {
            ShowScreen(_screenLobby);

            if (_lblRoomCode  != null) _lblRoomCode.text  = $"CÓDIGO: {code}";
            if (_btnLobbyStart != null) _btnLobbyStart.style.display = isHost ? DisplayStyle.Flex : DisplayStyle.None;
            if (_btnLobbyReady != null) _btnLobbyReady.style.display = isHost ? DisplayStyle.None : DisplayStyle.Flex;

            // Only the host decides the match settings
            var dropdown = _root.Q<DropdownField>("dropdown-points");
            if (dropdown != null) dropdown.SetEnabled(isHost);

            RefreshLobbyPlayers();
            SetButtonsInteractable(true);
        }

        private void Update()
        {
            // Poll the lobby roster: player objects (PlayerNetworkSync) spawn/leave
            // asynchronously and their SyncVars update without events.
            if (_screenLobby == null || _screenLobby.style.display != DisplayStyle.Flex) return;

            _lobbyRefreshTimer += Time.deltaTime;
            if (_lobbyRefreshTimer < 0.25f) return;
            _lobbyRefreshTimer = 0f;
            RefreshLobbyPlayers();
        }

        private void RefreshLobbyPlayers()
        {
            var team1 = new List<string>();
            var team2 = new List<string>();

            foreach (var sync in FindObjectsByType<PlayerNetworkSync>(FindObjectsSortMode.None))
            {
                string displayName = string.IsNullOrEmpty(sync.playerName) ? "Jugador" : sync.playerName;
                if (sync.isLocalPlayer) displayName += " (Tú)";
                if (sync.teamIndex == 1) team2.Add(displayName);
                else team1.Add(displayName);
            }

            SetPlayerSlot(_lblT1P1, team1, 0);
            SetPlayerSlot(_lblT1P2, team1, 1);
            SetPlayerSlot(_lblT2P1, team2, 0);
            SetPlayerSlot(_lblT2P2, team2, 1);
        }

        private static void SetPlayerSlot(Label lbl, List<string> names, int idx)
        {
            if (lbl == null) return;
            lbl.text = idx < names.Count ? names[idx] : "Esperando...";
        }

        private void HandleSwitchTeam()
        {
            var localPlayer = NetworkClient.localPlayer;
            var sync = localPlayer != null ? localPlayer.GetComponent<PlayerNetworkSync>() : null;
            if (sync == null)
            {
                ShowStatus(_lblLobbyStatus, "Todavía no estás conectado a la sala.");
                return;
            }

            int target = sync.teamIndex == 0 ? 1 : 0;
            int occupied = FindObjectsByType<PlayerNetworkSync>(FindObjectsSortMode.None)
                .Count(s => s != sync && s.teamIndex == target);
            if (occupied >= 2)
            {
                ShowStatus(_lblLobbyStatus, $"El Equipo {target + 1} está completo.");
                return;
            }

            sync.CmdSwitchTeam();
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void CopyToClipboardJS(string text);
#endif

        private static void CopyToClipboard(string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // GUIUtility.systemCopyBuffer no llega al portapapeles del navegador en WebGL
            CopyToClipboardJS(text);
#else
            GUIUtility.systemCopyBuffer = text;
#endif
        }

        private void HandleCopyRoomCode()
        {
            string code = UnityServicesManager.Instance != null ? UnityServicesManager.Instance.CurrentLobbyCode : null;
            if (string.IsNullOrEmpty(code))
            {
                ShowStatus(_lblLobbyStatus, "Todavía no hay código de sala para copiar.");
                return;
            }

            CopyToClipboard(code);
            ShowStatus(_lblLobbyStatus, $"Código {code} copiado al portapapeles.");
        }

        private void HandleLeaveLobby()
        {
            if (NetworkServer.active) NetworkManager.singleton?.StopHost();
            else if (NetworkClient.active) NetworkManager.singleton?.StopClient();

            UnityServicesManager.Instance?.LeaveLobby();
            ShowScreen(_screenPlay);
        }

        private void HandleReadyState()
        {
            // For now, ready state is just visual. Could add SyncVar later.
        }

        private void HandleStartGame()
        {
            // Host only
            if (!NetworkServer.active) return;

            // Apply the points selected in the lobby (GameManager reads this on load)
            var dropdown = _root.Q<DropdownField>("dropdown-points");
            if (dropdown != null && int.TryParse(dropdown.value, out int pts))
                SingleplayerMaxPoints = pts;

            var netMgr = NetworkManager.singleton as MyNetworkingManager;
            if (netMgr == null) return;

            if (!netMgr.StartMultiplayerGame(out string error))
                ShowStatus(_lblLobbyStatus, error);
        }

        // ─────────────────────── Helpers ──────────────────────────────────

        private string GetNickname()
        {
            var field = _root.Q<TextField>("input-nickname");
            return field != null && field.value != null ? field.value.Trim() : "";
        }

        private void ShowStatus(Label lbl, string message, float duration = 4f)
        {
            if (lbl == null)
            {
                Debug.LogWarning($"[MainMenuController] {message}");
                return;
            }

            lbl.text = message;
            lbl.style.display = DisplayStyle.Flex;

            if (_statusRoutine != null) StopCoroutine(_statusRoutine);
            _statusRoutine = StartCoroutine(HideStatusAfter(lbl, duration));
        }

        private IEnumerator HideStatusAfter(Label lbl, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            lbl.style.display = DisplayStyle.None;
        }

        private void SetButtonsInteractable(bool enabled)
        {
            var hostBtn = _root.Q<Button>("btn-play-host");
            var joinBtn = _root.Q<Button>("btn-play-join");
            if (hostBtn != null) hostBtn.SetEnabled(enabled);
            if (joinBtn != null) joinBtn.SetEnabled(enabled);
        }

        private static void EnsureServicesManager()
        {
            if (UnityServicesManager.Instance != null) return;
            var go = new GameObject("UnityServicesManager");
            go.AddComponent<UnityServicesManager>();
        }
    }
}
