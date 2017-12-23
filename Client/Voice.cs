using System;
using System.Collections.Generic;
using System.IO;
using FragLabs.Audio.Engines;
using FragLabs.Audio.Engines.OpenAL;
using Lidgren.Network;
using System.Threading;
using System.Diagnostics;

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
            var config = new NetPeerConfiguration("voice-chat");

            _client = new NetClient(config);
            _client.Start();
        }

        public void Connect(string ip, int port, string token)
        {
            if (IsConnected) return;

            var message = _client.CreateMessage(token);
            
            _client.Connect(ip, port, message);
            IsConnected = true;

            _readBuffer = new byte[StreamSize];

            _capture = OpenALHelper.CaptureDevices[0].OpenStream((int)SampleRate, OpenALAudioFormat.Mono16Bit, 10);
            _capture.BeginRead(_readBuffer, 0, _readBuffer.Length, CaptureCallback, null);

            if(_updateThread == null || _updateThread.ThreadState == System.Threading.ThreadState.Aborted || _updateThread.ThreadState == System.Threading.ThreadState.Stopped)
            {
                _updateThread = new Thread(Update);
                _updateThread.IsBackground = true;
            }
            if (!_updateThread.IsAlive)
                _updateThread.Start();
        }

        public void Disconnect()
        {
            if (!IsConnected) return;

            _client.Disconnect("");
            IsConnected = false;

            if (_updateThread.IsAlive)
            {
                try
                {
                    _updateThread.Join(1000);
                    if (_updateThread.IsAlive)
                        _updateThread.Abort();
                }
                catch (ThreadAbortException) { }
                catch(Exception ex)
                {
                    Debug.WriteLine($"Error trying to stop update queue: {ex}");
                }
            }
        }

        public void Update()
        {
            if (!IsConnected)
                return;

            var message = _client.ReadMessage();
            if (message != null)
            {
                if (message.MessageType == NetIncomingMessageType.StatusChanged)
                {
                    var netConnection = (NetConnectionStatus)message.ReadByte();
                    var reason = message.ReadString();
                    InvokeOnStatusChanged(netConnection, reason);
                }
                else if (message.MessageType == NetIncomingMessageType.Data)
                {
                    byte type = message.ReadByte();
                    if(type == 0)
                    {
                        var id = message.ReadInt32();
                        var name = message.ReadString();
                        var newPlayer = new Player(name, id);

                        Players.Add(newPlayer);
                        InvokeOnPlayerJoined(newPlayer);
                    }
                    if (type == 1)
                    {
                        int size = message.ReadInt32();
                        byte[] readBuffer = message.ReadBytes(size);
                        var id = message.ReadInt32();

                        var player = Players.Find(p => p.ID == id);
                        if (player == null) return;

                        var toUpdate = message.ReadByte();

                        if(toUpdate == 1)
                        {
                            var position = message.ReadVector();
                            var direction = message.ReadVector();
                            player.UpdatePosition(position);
                            player.UpdateOrientation(direction);
                        }
                        else if(toUpdate == 2)
                        {
                            var position = message.ReadVector();
                            player.UpdatePosition(position);
                        }
                        else if(toUpdate == 3)
                        {
                            var direction = message.ReadVector();
                            player.UpdateOrientation(direction);
                        }
                        player.PlayVoice(readBuffer);
                    }
                }

                _client.Recycle(message);
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
                var message = _client.CreateMessage();
                message.Write((byte)0x01);
                message.Write(_readBuffer.Length);
                message.Write(_readBuffer);

                _client.SendMessage(message, NetDeliveryMethod.ReliableOrdered);
            }

            _capture.BeginRead(_readBuffer, 0, _readBuffer.Length, CaptureCallback, null);
            _lastVolume = volume;
        }

    }
}
