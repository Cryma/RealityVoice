using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using FragLabs.Audio.Engines;
using FragLabs.Audio.Engines.OpenAL;
using Lidgren.Network;
using MahApps.Metro.Controls;

namespace VoiceApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {

        public NetClient Client;

        private bool _isConnected = false;
        private Timer updateTimer;

        private byte[] _readBuffer;
        private Stream _capture;

        private Dictionary<string, PlaybackStream> _playbacks = new Dictionary<string, PlaybackStream>();

        private float lastVolume = 0f;

        private readonly int streamSize = 1600;
        private readonly uint sampleRate = 24000;

        public MainWindow()
        {
            InitializeComponent();

            var config = new NetPeerConfiguration("rl-voice");
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);

            updateTimer = new Timer(0.001);
            updateTimer.AutoReset = false;
            updateTimer.Elapsed += OnTick;
            updateTimer.Start();

            Client = new NetClient(config);
            Client.Start();

            StatusLabel.Text = "Status: " + Client.ConnectionStatus;
            PlayerLabel.Text = "Players: " + _playbacks.Count;

            SecretField.Password = "Jqed09UDcQH0rhCfOBuUhYqgdFVUZz0G";
        }

        private void OnTick(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            if (_isConnected)
            {
                NetIncomingMessage msg;
                msg = Client.ReadMessage();
                if (msg != null)
                {
                    if (msg.MessageType == NetIncomingMessageType.StatusChanged)
                    {
                        var netConnection = (NetConnectionStatus)msg.ReadByte();
                        if (netConnection == NetConnectionStatus.Disconnected)
                        {
                            string reason = msg.ReadString();
                            if (String.IsNullOrWhiteSpace(reason))
                            {
                                this.Dispatcher.Invoke(() =>
                                {
                                    StatusLabel.Text = "Status: " + netConnection;
                                });
                            }
                            else
                            {
                                this.Dispatcher.Invoke(() =>
                                {
                                    StatusLabel.Text = "Status: " + netConnection + " - Reason: " + reason;
                                });
                            }
                            _isConnected = false;
                        }
                        else
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                StatusLabel.Text = "Status: " + netConnection;
                            });
                        }
                    }
                    else if (msg.MessageType == NetIncomingMessageType.Data)
                    {
                        byte type = msg.ReadByte();
                        if (type == 1)
                        {
                            int size = msg.ReadInt32();
                            byte[] readBuffer = msg.ReadBytes(size);
                            string player = msg.ReadString();

                            float x = msg.ReadFloat();
                            float y = msg.ReadFloat();
                            float z = msg.ReadFloat();

                            float x1 = msg.ReadFloat();
                            float y1 = msg.ReadFloat();
                            float z1 = msg.ReadFloat();

                            Vector3 pos = new Vector3() {X = x, Y = y, Z = z};
                            Vector3 cam = new Vector3() {X = x1, Y = y1, Z = z1};

                            if (_playbacks.ContainsKey(player))
                            {
                                var p = _playbacks[player];

                                //Dispatcher.Invoke(() =>
                                //{
                                //    StatusLabel.Text = $"X: {pos.X} Y: {pos.Y} Z: {pos.Z}";
                                //    //StatusLabel.Text = $"X: {x} Y: {y} Z: {z}";
                                //});

                                p.Listener.Position = pos;
                                p.Listener.Orientation = new FragLabs.Audio.Engines.OpenAL.Orientation()
                                {
                                    At = cam,
                                    Up = new Vector3() { X = 0.0f, Y = 1.0f, Z = 0.0f }
                                };

                                if (p.CanWrite)
                                    p.Write(readBuffer, 0, streamSize);
                            }
                        } else if (type == 2)
                        {
                            string name = msg.ReadString();
                            CreatePlayback(name);
                        }
                    }

                    Client.Recycle(msg);
                }
            }

            updateTimer.Start();
        }

        private void CreatePlayback(string player)
        {
            var playback = OpenALHelper.PlaybackDevices[0].OpenStream(sampleRate, OpenALAudioFormat.Mono16Bit);
            playback.Listener.Position = new Vector3() { X = 0.0f, Y = 0.0f, Z = 0.0f };
            playback.Listener.Velocity = new Vector3() { X = 0.0f, Y = 0.0f, Z = 0.0f };
            playback.Listener.Orientation = new FragLabs.Audio.Engines.OpenAL.Orientation()
            {
                At = new Vector3() { X = 0.0f, Y = 0.0f, Z = 1.0f },
                Up = new Vector3() { X = 0.0f, Y = 1.0f, Z = 0.0f }
            };

            playback.ALPosition = new Vector3() { X = 0.0f, Y = 0.0f, Z = 0.0f };
            playback.Velocity = new Vector3() { X = 0.0f, Y = 0.0f, Z = 0.0f };

            _playbacks.Add(player, playback);
            this.Dispatcher.Invoke(() =>
            {
                PlayerLabel.Text = "Players: " + _playbacks.Count;
            });
        }

        private void OnInteraction(object sender, RoutedEventArgs e)
        {
            if (_isConnected)
            {
                _isConnected = false;

                Client.Disconnect("disconnect");
                StatusLabel.Text = "Status: " + Client.ConnectionStatus;

                _playbacks = new Dictionary<string, PlaybackStream>();
                PlayerLabel.Text = "Players: " + _playbacks.Count;
            }
            else
            {
                _readBuffer = new byte[streamSize];

                _capture = OpenALHelper.CaptureDevices[0].OpenStream((int)sampleRate, OpenALAudioFormat.Mono16Bit, 10);
                _capture.BeginRead(_readBuffer, 0, _readBuffer.Length, ReadCallback, null);

                NetOutgoingMessage hail = Client.CreateMessage();
                hail.Write(SecretField.Password);

                Client.Connect("127.0.0.1", 2424, hail);

                _isConnected = true;
            }
        }

        private void ReadCallback(IAsyncResult ar)
        {
            if (!_capture.CanRead) return;
            if (!_isConnected) return;

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

            if ((Math.Abs(volume - lastVolume) > 0.03) || volume > 0.5)
            {
                Console.WriteLine(_readBuffer.Length);
                NetOutgoingMessage msg = Client.CreateMessage();
                msg.Write((byte)1);
                msg.Write(_readBuffer.Length);
                msg.Write(_readBuffer);

                Client.SendMessage(msg, NetDeliveryMethod.ReliableOrdered);
            }

            _capture.BeginRead(_readBuffer, 0, _readBuffer.Length, ReadCallback, null);
            lastVolume = volume;
        }

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            if (Client != null)
            {
                Client.Disconnect("close");
                Client.Shutdown("close");
            }
        }
    }
}
