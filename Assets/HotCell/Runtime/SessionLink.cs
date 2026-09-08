using System;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace HotCell
{
    public sealed class SessionLink : MonoBehaviour
    {
        public Action<ulong> Joined, Left;
        public Action<ulong, InputFrame> InputReceived;
        public Action<ShiftFrame> SnapshotReceived;
        public Action<ulong, byte[]> VoiceReceived;
        public Action<int, byte[]> VoicePlayback;
        public Func<bool> CanJoin;
        public NetworkManager Net { get; private set; }
        public bool IsHost { get { return Net != null && Net.IsHost; } }
        public string Status { get; private set; }
        private const string Inputs = "hc.input.1", State = "hc.state.1", Voice = "hc.voice.1", Sound = "hc.sound.1";
        private const int InputBytes = 29, VoiceBytes = 400, SnapshotBytes = 32768;

        public bool Open(bool host, string address, ushort port)
        {
            if (Net != null) return false;
            Net = gameObject.AddComponent<NetworkManager>();
            UnityTransport transport = gameObject.AddComponent<UnityTransport>();
            transport.SetConnectionData(address, port, host ? "0.0.0.0" : null);
            Net.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = false,
                ForceSamePrefabs = false,
                ConnectionApproval = true,
                TickRate = 20
            };
            Net.ConnectionApprovalCallback = (request, response) =>
            {
                response.Approved = request.ClientNetworkId == NetworkManager.ServerClientId || (CanJoin != null && CanJoin());
                response.CreatePlayerObject = false;
                response.Pending = false;
                response.Reason = response.Approved ? "" : "Shift in progress or all six suits occupied.";
            };
            Net.OnClientConnectedCallback += OnJoin;
            Net.OnClientDisconnectCallback += OnLeave;
            bool started = host ? Net.StartHost() : Net.StartClient();
            Status = started ? (host ? "HOST READY" : "CONNECTING") : "NETWORK START FAILED";
            if (!started) return false;
            Net.CustomMessagingManager.RegisterNamedMessageHandler(Inputs, ReadInput);
            Net.CustomMessagingManager.RegisterNamedMessageHandler(State, ReadState);
            Net.CustomMessagingManager.RegisterNamedMessageHandler(Voice, ReadVoice);
            Net.CustomMessagingManager.RegisterNamedMessageHandler(Sound, ReadSound);
            return true;
        }

        private void OnJoin(ulong id) { Status = "CONNECTED"; Joined?.Invoke(id); }
        private void OnLeave(ulong id)
        {
            if (!IsHost) Status = "DISCONNECTED: " + Net.DisconnectReason;
            Left?.Invoke(id);
        }

        public void SendInput(InputFrame input)
        {
            if (Net == null || !Net.IsConnectedClient || Net.CustomMessagingManager == null || !ValidInput(input)) return;
            if (IsHost) { InputReceived?.Invoke(Net.LocalClientId, input); return; }
            using (FastBufferWriter writer = new FastBufferWriter(64, Allocator.Temp))
            {
                writer.WriteValueSafe(input.sequence);
                writer.WriteValueSafe(input.horizontal); writer.WriteValueSafe(input.vertical);
                writer.WriteValueSafe(input.yaw); writer.WriteValueSafe(input.pitch);
                writer.WriteValueSafe(input.use); writer.WriteValueSafe(input.reverse);
                writer.WriteValueSafe(input.lamp); writer.WriteValueSafe(input.wrist);
                writer.WriteValueSafe(input.clipboard); writer.WriteValueSafe(input.selectedSuit);
                Net.CustomMessagingManager.SendNamedMessage(Inputs, NetworkManager.ServerClientId, writer, NetworkDelivery.UnreliableSequenced);
            }
        }

        private void ReadInput(ulong id, FastBufferReader reader)
        {
            if (!IsHost || !Net.ConnectedClients.ContainsKey(id) || Remaining(reader) != InputBytes) return;
            try
            {
                InputFrame frame = new InputFrame();
                reader.ReadValueSafe(out frame.sequence);
                reader.ReadValueSafe(out frame.horizontal); reader.ReadValueSafe(out frame.vertical);
                reader.ReadValueSafe(out frame.yaw); reader.ReadValueSafe(out frame.pitch);
                reader.ReadValueSafe(out frame.use); reader.ReadValueSafe(out frame.reverse);
                reader.ReadValueSafe(out frame.lamp); reader.ReadValueSafe(out frame.wrist);
                reader.ReadValueSafe(out frame.clipboard); reader.ReadValueSafe(out frame.selectedSuit);
                if (ValidInput(frame)) InputReceived?.Invoke(id, frame);
            }
            catch (OverflowException) { }
        }

        public void SendState(ulong id, ShiftFrame snapshot)
        {
            if (!IsHost || Net.CustomMessagingManager == null || !Net.ConnectedClients.ContainsKey(id) || snapshot == null) return;
            if (id == Net.LocalClientId) { SnapshotReceived?.Invoke(snapshot); return; }
            string json = JsonUtility.ToJson(snapshot);
            int bytes = FastBufferWriter.GetWriteSize(json);
            if (bytes > SnapshotBytes)
            {
                Debug.LogError("HOT CELL snapshot exceeds the message limit.");
                return;
            }
            using (FastBufferWriter writer = new FastBufferWriter(bytes, Allocator.Temp))
            {
                writer.WriteValueSafe(json);
                Net.CustomMessagingManager.SendNamedMessage(State, id, writer, NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        private void ReadState(ulong id, FastBufferReader reader)
        {
            int bytes = Remaining(reader);
            if (IsHost || id != NetworkManager.ServerClientId || bytes < sizeof(int) || bytes > SnapshotBytes) return;
            try
            {
                // NGO 2.7 strings carry a uint character count. Check it before its
                // internal count-to-byte multiplication or allocation can overflow.
                int position = reader.Position;
                reader.ReadValueSafe(out uint characters);
                if (characters > (SnapshotBytes - sizeof(int)) / 2 || sizeof(int) + (long)characters * 2 != bytes) return;
                reader.Seek(position);
                reader.ReadValueSafe(out string json);
                ShiftFrame frame = JsonUtility.FromJson<ShiftFrame>(json);
                if (frame != null && frame.suits != null && frame.suits.Length <= 6 && frame.localSlot >= 0 && frame.localSlot < 6)
                    SnapshotReceived?.Invoke(frame);
            }
            catch (Exception error) when (error is OverflowException || error is ArgumentException) { }
        }

        public void SendVoice(byte[] pcm)
        {
            if (pcm == null || pcm.Length != VoiceBytes || Net == null || !Net.IsConnectedClient || Net.CustomMessagingManager == null) return;
            if (IsHost) { VoiceReceived?.Invoke(Net.LocalClientId, pcm); return; }
            using (FastBufferWriter writer = new FastBufferWriter(416, Allocator.Temp))
            {
                writer.WriteBytesSafe(pcm);
                Net.CustomMessagingManager.SendNamedMessage(Voice, NetworkManager.ServerClientId, writer, NetworkDelivery.Unreliable);
            }
        }

        private void ReadVoice(ulong id, FastBufferReader reader)
        {
            if (!IsHost || !Net.ConnectedClients.ContainsKey(id) || Remaining(reader) != VoiceBytes) return;
            byte[] pcm = new byte[VoiceBytes];
            reader.ReadBytesSafe(ref pcm, VoiceBytes);
            VoiceReceived?.Invoke(id, pcm);
        }

        public void RelayVoice(ulong recipient, int slot, byte[] pcm)
        {
            if (!IsHost || Net.CustomMessagingManager == null || !Net.ConnectedClients.ContainsKey(recipient)
                || slot < 0 || slot >= 6 || pcm == null || pcm.Length != VoiceBytes) return;
            if (recipient == Net.LocalClientId) { VoicePlayback?.Invoke(slot, pcm); return; }
            using (FastBufferWriter writer = new FastBufferWriter(416, Allocator.Temp))
            {
                writer.WriteValueSafe(slot);
                writer.WriteBytesSafe(pcm);
                Net.CustomMessagingManager.SendNamedMessage(Sound, recipient, writer, NetworkDelivery.Unreliable);
            }
        }

        private void ReadSound(ulong id, FastBufferReader reader)
        {
            if (IsHost || id != NetworkManager.ServerClientId || Remaining(reader) != VoiceBytes + sizeof(int)) return;
            reader.ReadValueSafe(out int slot);
            byte[] pcm = new byte[VoiceBytes];
            reader.ReadBytesSafe(ref pcm, VoiceBytes);
            if (slot >= 0 && slot < 6) VoicePlayback?.Invoke(slot, pcm);
        }

        private static int Remaining(FastBufferReader reader)
        {
            // NamedMessage has already read its hash; Length still includes that header.
            return reader.Length - reader.Position;
        }

        private static bool ValidInput(InputFrame input)
        {
            return input != null && Finite(input.horizontal) && Finite(input.vertical)
                && Finite(input.yaw) && Finite(input.pitch)
                && input.horizontal >= -1f && input.horizontal <= 1f
                && input.vertical >= -1f && input.vertical <= 1f
                && input.selectedSuit >= 1 && input.selectedSuit <= 6;
        }

        private static bool Finite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void OnDestroy()
        {
            if (Net == null) return;
            Net.OnClientConnectedCallback -= OnJoin;
            Net.OnClientDisconnectCallback -= OnLeave;
            Net.Shutdown();
        }
    }
}
