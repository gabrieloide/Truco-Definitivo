using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Mirror;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using ULobby = Unity.Services.Lobbies.Models.Lobby;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace Code.Networking
{
    /// <summary>
    /// Manages Unity Services initialization, relay allocation and lobby join codes.
    /// Call InitializeAsync() once, then CreateHostAsync() or JoinClientAsync(code).
    /// </summary>
    public class UnityServicesManager : MonoBehaviour
    {
        public static UnityServicesManager Instance { get; private set; }

        public string CurrentLobbyCode { get; private set; }
        public string CurrentLobbyId  { get; private set; }
        public bool   IsHost           { get; private set; }
        public string PlayerName       { get; private set; }

        private const string k_RelayCodeKey = "relayCode";
        private const int    k_MaxPlayers   = 4;
        // Must mirror the Relay SDK's own check (AllocationUtils.GetValidProtocols):
        // with WebGL as build target the SDK only accepts "wss", editor included,
        // so this cannot use "&& !UNITY_EDITOR". UnityRelayTransport picks the
        // matching network interface from RelayServerData.IsWebSocket.
#if UNITY_WEBGL
        private const string k_ConnectionType = "wss";
#else
        private const string k_ConnectionType = "dtls";
#endif

        private ULobby _currentLobby;
        private float _heartbeatTimer;
        private const float k_HeartbeatInterval = 15f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (_currentLobby != null && IsHost)
            {
                _heartbeatTimer += Time.deltaTime;
                if (_heartbeatTimer >= k_HeartbeatInterval)
                {
                    _heartbeatTimer = 0f;
                    _ = SendHeartbeatAsync();
                }
            }
        }

        // ─────────────────────── Initialization ───────────────────────────

        public async Task InitializeAsync(string playerName = null)
        {
            if (!string.IsNullOrWhiteSpace(playerName))
                PlayerName = playerName.Trim();

            if (UnityServices.State == ServicesInitializationState.Initialized) return;

            var options = new InitializationOptions();
            if (!string.IsNullOrEmpty(playerName))
            {
                // Auth profiles only allow [a-zA-Z0-9_-] up to 30 chars; the display
                // name keeps spaces/accents, only the profile id is sanitized.
                string profile = System.Text.RegularExpressions.Regex.Replace(playerName, "[^a-zA-Z0-9_-]", "");
                if (profile.Length > 30) profile = profile.Substring(0, 30);
                if (!string.IsNullOrEmpty(profile))
                    options.SetProfile(profile);
            }

            await UnityServices.InitializeAsync(options);

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            Debug.Log($"[UnityServicesManager] Initialized. PlayerId: {AuthenticationService.Instance.PlayerId}");
        }

        // ─────────────────────── HOST ─────────────────────────────────────

        /// <summary>
        /// Creates a relay allocation and a lobby. Returns the lobby join code to share.
        /// Configures UnityRelayTransport.HostRelayData before returning.
        /// </summary>
        public async Task<string> CreateHostAsync(string playerName = null)
        {
            await InitializeAsync(playerName);

            // The previous match's lobby may still be deleting (fire-and-forget on
            // disconnect): finish leaving first or the service rejects the creation
            // ("player is already a member of a lobby").
            await LeaveLobbyAsync();
            IsHost = true;

            // 1. Create relay allocation
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(k_MaxPlayers - 1);
            string relayJoinCode  = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            UnityRelayTransport.HostRelayData = allocation.ToRelayServerData(k_ConnectionType);

            Debug.Log($"[UnityServicesManager] Relay join code: {relayJoinCode}");

            // 2. Create lobby storing relay code in lobby data
            var options = new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
                {
                    { k_RelayCodeKey, new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) }
                }
            };

            _currentLobby   = await LobbyService.Instance.CreateLobbyAsync("TrucoRoom", k_MaxPlayers, options);
            CurrentLobbyCode = _currentLobby.LobbyCode;
            CurrentLobbyId   = _currentLobby.Id;

            Debug.Log($"[UnityServicesManager] Lobby created. Code: {CurrentLobbyCode}");
            return CurrentLobbyCode;
        }

        // ─────────────────────── CLIENT ───────────────────────────────────

        /// <summary>
        /// Joins a lobby by its code, reads the relay join code from lobby data, joins relay.
        /// Configures UnityRelayTransport.ClientRelayData before returning.
        /// </summary>
        public async Task JoinClientAsync(string lobbyCode, string playerName = null)
        {
            await InitializeAsync(playerName);

            // Leave any stale lobby membership from the previous match first.
            await LeaveLobbyAsync();
            IsHost = false;

            // 1. Join lobby
            _currentLobby   = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode.Trim().ToUpper());
            CurrentLobbyCode = _currentLobby.LobbyCode;
            CurrentLobbyId   = _currentLobby.Id;

            // 2. Read relay code from lobby data
            if (!_currentLobby.Data.TryGetValue(k_RelayCodeKey, out var relayData))
                throw new Exception("[UnityServicesManager] Lobby data missing relay code.");

            string relayJoinCode = relayData.Value;
            Debug.Log($"[UnityServicesManager] Joining relay: {relayJoinCode}");

            // 3. Join relay allocation
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
            UnityRelayTransport.ClientRelayData = joinAllocation.ToRelayServerData(k_ConnectionType);

            Debug.Log("[UnityServicesManager] Client relay configured.");
        }

        // ─────────────────────── Cleanup ──────────────────────────────────

        public async void LeaveLobby() => await LeaveLobbyAsync();

        public async Task LeaveLobbyAsync()
        {
            if (_currentLobby == null) return;
            var lobby = _currentLobby;
            // Clear first so a parallel call doesn't try to delete the same lobby twice
            _currentLobby   = null;
            CurrentLobbyCode = null;
            CurrentLobbyId   = null;

            try
            {
                if (IsHost)
                    await LobbyService.Instance.DeleteLobbyAsync(lobby.Id);
                else
                    await LobbyService.Instance.RemovePlayerAsync(lobby.Id, AuthenticationService.Instance.PlayerId);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UnityServicesManager] LeaveLobby error: {e.Message}");
            }
        }

        private async Task SendHeartbeatAsync()
        {
            try
            {
                await LobbyService.Instance.SendHeartbeatPingAsync(_currentLobby.Id);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UnityServicesManager] Heartbeat error: {e.Message}");
            }
        }

        private void OnDestroy()
        {
            if (_currentLobby != null) LeaveLobby();
        }
    }
}
