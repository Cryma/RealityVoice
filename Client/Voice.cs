using System;
using System.Collections.Generic;
using System.IO;
using FragLabs.Audio.Engines;
using FragLabs.Audio.Engines.OpenAL;
using Lidgren.Network;
using System.Threading;
using System.Diagnostics;
using System.Linq;
using FragLabs.Audio.Codecs;
using FragLabs.Audio.Codecs.Opus;

namespace RealityVoice
{
    class Voice
    {

        public const uint SampleRate = 24000;
        public const int StreamSize = 960 * 2;

        public static int Volume = Properties.Settings.Default.Volume;

        public List<Player> Players = new List<Player>();

        public VoiceMode SelectedVoiceMode = VoiceMode.VoiceActivation;
        public bool IsSpeaking;

        private NetClient _client;
        public bool IsConnected;

        private byte[] _readBuffer;
        private Stream _capture;

        private List<VoicePacket> _packets = new List<VoicePacket>();

        private OpusDecoder _decoder;
        private OpusEncoder _encoder;
        private int _bytesPerSegment;

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

        #endregion

        public Voice()
        {
            OnStatusChanged += EventOnStatusChanged;

            _decoder = OpusDecoder.Create((int)SampleRate, 1);
            _encoder = OpusEncoder.Create((int)SampleRate, 1, Application.Voip);
            _encoder.Bitrate = 8192 * 2;
            _bytesPerSegment = _encoder.FrameByteCount(StreamSize);
        }

        public void Start()
        {
            var config = new NetPeerConfiguration("voice-chat");

            _client = new NetClient(config);
            _client.Start();
        }

        public void ChangeVolume(int volume)
        {
            Voice.Volume = volume;
            foreach(var player in Players)
                player.Playback.SetVolume(volume / 100f);
        }

        public void Connect(string ip, int port, string token)
        {
            if (IsConnected) return;

            var message = _client.CreateMessage(token);
            
            _client.Connect(ip, port, message);
            IsConnected = true;

            _readBuffer = new byte[StreamSize];

            _capture = OpenALHelper.CaptureDevices[0].OpenStream((int)SampleRate, OpenALAudioFormat.Mono16Bit, 50);
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

            //if (_updateThread.IsAlive)
            //{
            //    try
            //    {
            //        _updateThread.Join(1000);
            //        if (_updateThread.IsAlive)
            //            _updateThread.Abort();
            //    }
            //    catch (ThreadAbortException) { }
            //    catch(Exception ex)
            //    {
            //        Debug.WriteLine($"Error trying to stop update queue: {ex}");
            //    }
            //}
        }

        public void Update()
        {
            try
            {
                while (IsConnected)
                {
                    Thread.Sleep(10);

                    var messages = new List<NetIncomingMessage>();
                    var messageCount = _client.ReadMessages(messages);

                    if (messageCount != 0)
                    {

                        foreach (var message in messages)
                        {
                            if (message.MessageType == NetIncomingMessageType.StatusChanged)
                            {
                                var netConnection = (NetConnectionStatus)message.ReadByte();
                                var reason = message.ReadString();

                                OnStatusChanged?.Invoke(netConnection, reason);
                            }
                            else if (message.MessageType == NetIncomingMessageType.Data)
                            {
                                byte type = message.ReadByte();
                                if (type == 0)
                                {
                                    var id = message.ReadInt32();
                                    var name = message.ReadString();
                                    var newPlayer = new Player(name, id);

                                    Players.Add(newPlayer);
                                    InvokeOnPlayerJoined(newPlayer);
                                }
                                if (type == 1)
                                {
                                    List<VoicePacket> packets = new List<VoicePacket>();

                                    var packetAmount = message.ReadInt32();


                                    for (var i = 0; i < packetAmount; i++)
                                    {
                                        int size = message.ReadInt32();
                                        int dataSize = message.ReadInt32();
                                        byte[] encoded = message.ReadBytes(size);
                                        byte[] decoded = _decoder.Decode(encoded, dataSize, out var len);

                                        packets.Add(new VoicePacket(decoded, len));
                                    }

                                    var id = message.ReadInt32();

                                    var player = Players.Find(p => p.ID == id);
                                    if (player == null)
                                        continue;

                                    var toUpdate = message.ReadByte();

                                    if (toUpdate == 1)
                                    {
                                        var position = message.ReadVector();
                                        var direction = message.ReadVector();
                                        player.UpdatePosition(position);
                                        player.UpdateOrientation(direction);
                                    }
                                    else if (toUpdate == 2)
                                    {
                                        var position = message.ReadVector();
                                        player.UpdatePosition(position);
                                    }
                                    else if (toUpdate == 3)
                                    {
                                        var direction = message.ReadVector();
                                        player.UpdateOrientation(direction);
                                    }

                                    for (var i = 0; i < packetAmount; i++)
                                    {
                                        player.PlayVoice(packets[i].EncodedVoice, packets[i].DataSize);
                                    }
                                }
                            }

                            _client.Recycle(message);
                        }

                    }

                    if(_packets.Count > 4 || (_packets.Count > 1 && _packets.Last().CreatedAt.AddMilliseconds(50) < DateTime.Now))
                        SendPackets();
                }
            }
            catch (ThreadAbortException) { }
            //catch (Exception ex)
            //{
            //    Debug.WriteLine($"Exception in netcode: {ex}");
            //}
        }

        public static byte[] Combine(byte[] first, byte[] second)
        {
            byte[] ret = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, ret, 0, first.Length);
            Buffer.BlockCopy(second, 0, ret, first.Length, second.Length);
            return ret;
        }


        private void EventOnStatusChanged(NetConnectionStatus status, string reason)
        {
            if (status == NetConnectionStatus.Disconnected)
            {
                IsConnected = false;
                Players = new List<Player>();
            }
        }

        private void HandleVoiceActivation()
        {
            float volume = 0;
            for (int i = 0; i < _readBuffer.Length; i += 2)
            {
                short sample = (short)((_readBuffer[i + 1] << 8) | _readBuffer[i + 0]);
                var sample32 = sample / 32768f;
                if (sample32 < 0) sample32 = -sample32;
                if (sample32 > volume) volume = sample32;
            }
            volume *= 10;

            IsSpeaking = (Math.Abs(volume - _lastVolume) > 0.03) || volume > 0.5;

            _lastVolume = volume;
        }

        private void CaptureCallback(IAsyncResult ar)
        {
            if (!_capture.CanRead) return;
            if (!IsConnected) return;

            var read = _capture.EndRead(ar);

            if(SelectedVoiceMode == VoiceMode.VoiceActivation)
                HandleVoiceActivation();

            if (IsSpeaking)
            {
#if DEBUG
                Console.WriteLine(_readBuffer.Length);
#endif
                
                int length;
                byte[] encoded = _encoder.Encode(_readBuffer, _readBuffer.Length, out length);

                _packets.Add(new VoicePacket(encoded, length));

                _capture.BeginRead(_readBuffer, 0, _readBuffer.Length, CaptureCallback, null);
                return;
                
            }

            _capture.BeginRead(_readBuffer, 0, _readBuffer.Length, CaptureCallback, null);
        }

        private void SendPackets()
        {
            if (_packets.Count < 1) return;

            var message = _client.CreateMessage();
            message.Write((byte)0x01);

            message.Write(_packets.Count);


            foreach (var packet in _packets.ToList())
            {
                message.Write(packet.EncodedVoice.Length);
                message.Write(packet.DataSize);
                message.Write(packet.EncodedVoice);
            }

            _client.SendMessage(message, NetDeliveryMethod.ReliableOrdered);

            _packets = new List<VoicePacket>();
        }

    }
}
