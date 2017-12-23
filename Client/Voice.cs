using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using FragLabs.Audio.Engines;
using FragLabs.Audio.Engines.OpenAL;
using Lidgren.Network;
using System.Threading;

namespace RealityVoice
{
    class Voice
    {

        public const uint SampleRate = 24000;
        public const int StreamSize = 1600;

        public List<Player> Players = new List<Player>();

        private NetClient _client;
        public bool IsConnected;

        private byte[] _readBuffer;
        private Stream _capture;

        private Thread _updateThread;

        private float _lastVolume;

        #region Events

        public delegate void PlayerEvent(Player player);
        public delegate void StatusEvent(NetConnectionStatus status, string reason);

        public event PlayerEvent OnPlayerJoined;
        public event StatusEvent OnStatusChanged;

        private void InvokeOnPlayerJoined(Player player)
        {
            OnPlayerJoined?.Invoke(player);
        }

        private void InvokeOnStatusChanged(NetConnectionStatus status, string reason = "")
        {
            OnStatusChanged?.Invoke(status, reason);
        }

        #endregion

        public Voice()
        {
            OnStatusChanged += EventOnStatusChanged;
        }

        public void Start()
        {
            var config = new NetPeerConfiguration("rl-voice");

            _client = new NetClient(config);
            _client.Start();

            _updateThread = new Thread(Update);
            _updateThread.IsBackground = true;
        }

        public void Connect(string ip, int port, string token)
        {
            if (IsConnected) return;

            NetOutgoingMessage message = _client.CreateMessage(token);
            
            _client.Connect(ip, port, message);
            IsConnected = true;

            _readBuffer = new byte[StreamSize];

            _capture = OpenALHelper.CaptureDevices[0].OpenStream((int)SampleRate, OpenALAudioFormat.Mono16Bit, 10);
            _capture.BeginRead(_readBuffer, 0, _readBuffer.Length, CaptureCallback, null);
        }

        public void Disconnect()
        {
            if (!IsConnected) return;

            _client.Disconnect("");
        }

        public void Update()
        {
            if (IsConnected)
            {
                NetIncomingMessage msg;
                msg = _client.ReadMessage();
                if (msg != null)
                {
                    if (msg.MessageType == NetIncomingMessageType.StatusChanged)
                    {
                        var netConnection = (NetConnectionStatus)msg.ReadByte();
                        var reason = msg.ReadString();
                        InvokeOnStatusChanged(netConnection, reason);
                    }
                    else if (msg.MessageType == NetIncomingMessageType.Data)
                    {
                        byte type = msg.ReadByte();
                        if (type == 1)
                        {
                            int size = msg.ReadInt32();
                            byte[] readBuffer = msg.ReadBytes(size);
                            string name = msg.ReadString();

                            Player player = Players.Find(p => p.Name == name);
                            if (player == null) return;

                            Vector3 position = msg.ReadPositionFromMessage();
                            Vector3 direction = msg.ReadDirectionFromMessage();

                            player.UpdatePosition(position);
                            player.UpdateOrientation(direction);
                            player.PlayVoice(readBuffer);
                        }
                        else if (type == 2)
                        {
                            string name = msg.ReadString();
                            Player newPlayer = new Player(name);

                            Players.Add(newPlayer);
                            InvokeOnPlayerJoined(newPlayer);
                        }
                    }

                    _client.Recycle(msg);
                }
            }
            Thread.Sleep(10);
        }

        private void EventOnStatusChanged(NetConnectionStatus status, string reason)
        {
            if (status == NetConnectionStatus.Disconnected)
            {
                IsConnected = false;
                Players = new List<Player>();
            }
        }

        private void CaptureCallback(IAsyncResult ar)
        {
            if (!_capture.CanRead) return;
            if (!IsConnected) return;

            var read = _capture.EndRead(ar);

            float volume = 0;
            for (int i = 0; i < _readBuffer.Length; i += 2)
            {
                short sample = (short)((_readBuffer[i + 1] << 8) | _readBuffer[i + 0]);
                var sample32 = sample / 32768f;
                if (sample32 < 0) sample32 = -sample32;
                if (sample32 > volume) volume = sample32;
            }
            volume *= 10;

            if ((Math.Abs(volume - _lastVolume) > 0.03) || volume > 0.5)
            {
#if DEBUG
                Console.WriteLine(_readBuffer.Length);
#endif
                NetOutgoingMessage msg = _client.CreateMessage();
                msg.Write((byte)1);
                msg.Write(_readBuffer.Length);
                msg.Write(_readBuffer);

                _client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered);
            }

            _capture.BeginRead(_readBuffer, 0, _readBuffer.Length, CaptureCallback, null);
            _lastVolume = volume;
        }

    }
}
