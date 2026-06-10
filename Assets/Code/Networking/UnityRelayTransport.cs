using System;
using System.Collections.Generic;
using Mirror;
using Unity.Collections;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using Unity.Networking.Transport.Utilities;
using UnityEngine;
using UTPConnection = Unity.Networking.Transport.NetworkConnection;

namespace Code.Networking
{
    /// <summary>
    /// Mirror transport backed by Unity Transport + Unity Relay.
    /// Before starting host/client, configure HostRelayData or ClientRelayData respectively.
    /// </summary>
    public class UnityRelayTransport : Transport
    {
        public static RelayServerData HostRelayData;
        public static RelayServerData ClientRelayData;

        // Fragmentation allows Mirror messages bigger than one MTU (e.g. spawn bursts).
        private const int k_PayloadCapacity = 16 * 1024;

        private NetworkDriver _serverDriver;
        private NetworkDriver _clientDriver;
        private NetworkPipeline _serverPipeline;
        private NetworkPipeline _clientPipeline;

        // Mirror reserves connectionId 0 for the host's local connection,
        // so remote ids must start at 1 and stay stable for the connection's lifetime.
        private readonly Dictionary<int, UTPConnection> _serverConns = new();
        private readonly List<int> _removedConnIds = new();
        private int _nextConnectionId = 1;

        private UTPConnection _clientConn;
        private bool _clientConnected;

        // ──────────────────────── Transport overrides ────────────────────────

        public override bool Available() => true;

        public override int GetMaxPacketSize(int channelId = Channels.Reliable) => k_PayloadCapacity;

        public override Uri ServerUri() => new Uri("relay://relay");

        public override void Shutdown()
        {
            ServerStop();
            ClientDisconnect();
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        // ──────────────────────── SERVER ─────────────────────────────────────

        public override bool ServerActive() => _serverDriver.IsCreated;

        public override void ServerStart()
        {
            _serverDriver = CreateDriver(ref HostRelayData);
            _serverPipeline = _serverDriver.CreatePipeline(
                typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));

            if (_serverDriver.Bind(NetworkEndpoint.AnyIpv4) != 0)
            {
                Debug.LogError("[UnityRelayTransport] Server bind failed.");
                _serverDriver.Dispose();
                return;
            }
            _serverDriver.Listen();
            Debug.Log($"[NetDiag] t={Time.realtimeSinceStartup:F1}s — Server listening via Relay (webSocket={HostRelayData.IsWebSocket == 1}).");
        }

        public override void ServerStop()
        {
            if (!_serverDriver.IsCreated) return;
            foreach (var c in _serverConns.Values)
                if (c.IsCreated) _serverDriver.Disconnect(c);
            // Flush: without this the disconnect packets never leave the queue and the
            // clients only notice the session ended via the 2-minute timeout.
            _serverDriver.ScheduleUpdate().Complete();
            _serverConns.Clear();
            _serverDriver.Dispose();
        }

        public override void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId)
        {
            if (!_serverDriver.IsCreated) return;
            if (!_serverConns.TryGetValue(connectionId, out var conn) || !conn.IsCreated) return;

            int beginErr = _serverDriver.BeginSend(_serverPipeline, conn, out var writer, segment.Count);
            if (beginErr != 0)
            {
                // Dropping a reliable message corrupts the Mirror stream — make it loud.
                Debug.LogError($"[UnityRelayTransport] ServerSend BeginSend failed ({beginErr}) for conn {connectionId}, {segment.Count} bytes. Message dropped!");
                return;
            }
            WriteSegment(ref writer, segment);
            int sent = _serverDriver.EndSend(writer);
            if (sent < 0)
                Debug.LogWarning($"[UnityRelayTransport] ServerSend failed ({sent}) for conn {connectionId}, {segment.Count} bytes.");
        }

        public override void ServerDisconnect(int connectionId)
        {
            if (!_serverDriver.IsCreated) return;
            if (_serverConns.TryGetValue(connectionId, out var conn) && conn.IsCreated)
                _serverDriver.Disconnect(conn);
        }

        public override string ServerGetClientAddress(int connectionId) => "relay";

        // ──────────────────────── CLIENT ─────────────────────────────────────

        public override bool ClientConnected() => _clientConnected;

        public override void ClientConnect(string address)
        {
            _clientConnected = false;
            _clientDriver = CreateDriver(ref ClientRelayData);
            _clientPipeline = _clientDriver.CreatePipeline(
                typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));

            _clientDriver.Bind(NetworkEndpoint.AnyIpv4);
            _clientConn = _clientDriver.Connect(ClientRelayData.Endpoint);
            Debug.Log($"[NetDiag] t={Time.realtimeSinceStartup:F1}s — Client connecting to Relay (webSocket={ClientRelayData.IsWebSocket == 1}).");
        }

        public override void ClientDisconnect()
        {
            if (!_clientDriver.IsCreated) return;
            if (_clientConn.IsCreated) _clientDriver.Disconnect(_clientConn);
            // Flush so the host learns about the departure immediately (see ServerStop).
            _clientDriver.ScheduleUpdate().Complete();
            _clientDriver.Dispose();
            _clientConnected = false;
        }

        public override void ClientSend(ArraySegment<byte> segment, int channelId)
        {
            if (!_clientDriver.IsCreated || !_clientConn.IsCreated) return;
            int beginErr = _clientDriver.BeginSend(_clientPipeline, _clientConn, out var writer, segment.Count);
            if (beginErr != 0)
            {
                Debug.LogError($"[UnityRelayTransport] ClientSend BeginSend failed ({beginErr}), {segment.Count} bytes. Message dropped!");
                return;
            }
            WriteSegment(ref writer, segment);
            int sent = _clientDriver.EndSend(writer);
            if (sent < 0)
                Debug.LogWarning($"[UnityRelayTransport] ClientSend failed ({sent}), {segment.Count} bytes.");
        }

        // ──────────────────────── Update loop ────────────────────────────────

        private void Update()
        {
            PollServer();
            PollClient();
        }

        private void PollServer()
        {
            if (!_serverDriver.IsCreated) return;
            _serverDriver.ScheduleUpdate().Complete();

            UTPConnection newConn;
            while ((newConn = _serverDriver.Accept()) != default)
            {
                int id = _nextConnectionId++;
                _serverConns[id] = newConn;
                Debug.Log($"[NetDiag] t={Time.realtimeSinceStartup:F1}s — Server accepted connection {id}.");
                OnServerConnected?.Invoke(id);
            }

            _removedConnIds.Clear();
            foreach (var kvp in _serverConns)
            {
                int id = kvp.Key;
                var conn = kvp.Value;
                if (!conn.IsCreated)
                {
                    _removedConnIds.Add(id);
                    continue;
                }

                NetworkEvent.Type evt;
                while ((evt = _serverDriver.PopEventForConnection(conn, out var stream)) != NetworkEvent.Type.Empty)
                {
                    if (evt == NetworkEvent.Type.Data)
                    {
                        OnServerDataReceived?.Invoke(id, ReadStream(stream), Channels.Reliable);
                    }
                    else if (evt == NetworkEvent.Type.Disconnect)
                    {
                        Debug.LogWarning($"[NetDiag] t={Time.realtimeSinceStartup:F1}s — Conn {id} disconnected. Reason: {ReadDisconnectReason(ref stream)} (Timeout = no data for 2 min, ClosedByRemote = other side closed, Default = local close)");
                        _removedConnIds.Add(id);
                        OnServerDisconnected?.Invoke(id);
                        break;
                    }
                }
            }

            foreach (var id in _removedConnIds)
                _serverConns.Remove(id);
        }

        private void PollClient()
        {
            if (!_clientDriver.IsCreated || !_clientConn.IsCreated) return;
            _clientDriver.ScheduleUpdate().Complete();

            NetworkEvent.Type evt;
            while ((evt = _clientDriver.PopEventForConnection(_clientConn, out var stream)) != NetworkEvent.Type.Empty)
            {
                switch (evt)
                {
                    case NetworkEvent.Type.Connect:
                        _clientConnected = true;
                        Debug.Log($"[NetDiag] t={Time.realtimeSinceStartup:F1}s — Client connected to host.");
                        OnClientConnected?.Invoke();
                        break;
                    case NetworkEvent.Type.Data:
                        OnClientDataReceived?.Invoke(ReadStream(stream), Channels.Reliable);
                        break;
                    case NetworkEvent.Type.Disconnect:
                        Debug.LogWarning($"[NetDiag] t={Time.realtimeSinceStartup:F1}s — Disconnected from server. Reason: {ReadDisconnectReason(ref stream)} (Timeout = no data for 2 min, ClosedByRemote = host closed, Default = local close)");
                        _clientConnected = false;
                        OnClientDisconnected?.Invoke();
                        break;
                }
            }
        }

        // ──────────────────────── Helpers ────────────────────────────────────

        /// <summary>UTP requires the driver's network interface to match the relay
        /// connection type: "ws"/"wss" need WebSocketNetworkInterface, "udp"/"dtls"
        /// need the default UDP interface.</summary>
        private static NetworkDriver CreateDriver(ref RelayServerData relayData)
        {
            var settings = new NetworkSettings();
            settings.WithRelayParameters(ref relayData);
            settings.WithFragmentationStageParameters(payloadCapacity: k_PayloadCapacity);
            // Default reliable window is 32 in-flight packets; spawn/deal bursts can
            // fill it and a dropped reliable message corrupts the Mirror stream.
            settings.WithReliableStageParameters(windowSize: 64);
            // 2 minutes without receiving ANYTHING from the peer (heartbeats included)
            // before dropping the link. Heartbeats flow automatically every 500 ms while
            // both apps run, so this only fires when the other app truly stalls or the
            // network path dies — never because a player just sits idle.
            settings.WithNetworkConfigParameters(disconnectTimeoutMS: 120000, heartbeatTimeoutMS: 500);
            return relayData.IsWebSocket == 1
                ? NetworkDriver.Create(new WebSocketNetworkInterface(), settings)
                : NetworkDriver.Create(settings);
        }

        /// <summary>Disconnect events carry a 1-byte reason — surface it for debugging.</summary>
        private static Unity.Networking.Transport.Error.DisconnectReason ReadDisconnectReason(ref DataStreamReader stream)
        {
            return stream.Length >= 1
                ? (Unity.Networking.Transport.Error.DisconnectReason)stream.ReadByte()
                : Unity.Networking.Transport.Error.DisconnectReason.Default;
        }

        private static void WriteSegment(ref DataStreamWriter writer, ArraySegment<byte> segment)
        {
            var buf = new NativeArray<byte>(segment.Count, Allocator.Temp);
            NativeArray<byte>.Copy(segment.Array, segment.Offset, buf, 0, segment.Count);
            writer.WriteBytes(buf);
            buf.Dispose();
        }

        private static ArraySegment<byte> ReadStream(DataStreamReader stream)
        {
            var buf = new NativeArray<byte>(stream.Length, Allocator.Temp);
            stream.ReadBytes(buf);
            var bytes = buf.ToArray();
            buf.Dispose();
            return new ArraySegment<byte>(bytes);
        }
    }
}
