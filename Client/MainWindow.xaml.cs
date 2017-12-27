using System;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using Lidgren.Network;
using MahApps.Metro.Controls;

namespace RealityVoice
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {

        private Voice _voice;
        private KeyboardListener _listener = new KeyboardListener();

        public MainWindow()
        {
            InitializeComponent();

            LoadSettings();

            _listener.KeyDown += OnGlobalKeyDown;
            _listener.KeyUp += OnGlobalKeyUp;

            
            _voice = new Voice();
            _voice.OnStatusChanged += OnStatusChanged;
            _voice.OnPlayerJoined += OnPlayerJoined;

            _voice.Start();
            
        }

        private void LoadSettings()
        {
            switch (Properties.Settings.Default.VoiceMode)
            {
                case VoiceMode.VoiceActivation:
                    VoiceActivationRadio.IsChecked = true;
                    OnSelectVoiceActivation(null, null);
                    break;
                case VoiceMode.PushToTalk:
                    PushToTalkRadio.IsChecked = true;
                    OnSelectPushToTalk(null, null);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnGlobalKeyDown(object sender, RawKeyEventArgs e)
        {
            if (e.Key == Key.B && _voice?.SelectedVoiceMode == VoiceMode.PushToTalk)
            {
                _voice.IsSpeaking = true;
            }
        }

        private void OnGlobalKeyUp(object sender, RawKeyEventArgs e)
        {
            if (e.Key == Key.B && _voice?.SelectedVoiceMode == VoiceMode.PushToTalk)
            {
                _voice.IsSpeaking = false;
            }
        }

        private void OnPlayerJoined(Player player)
        {
            Dispatcher.Invoke(() =>
            {
                PlayerLabel.Text = $"Players: {_voice.Players.Count}";
            });
        }

        private void OnStatusChanged(NetConnectionStatus status, string reason)
        {
            Dispatcher.Invoke(() =>
            {
                StatusLabel.Text = String.IsNullOrWhiteSpace(reason) ? $"Status: {status}" : $"Status: {status} - {reason}";
            });

            if (status == NetConnectionStatus.Disconnected)
            {
                HandleDisconnect();
            }
        }


        private void OnInteraction(object sender, RoutedEventArgs e)
        {
            if (_voice == null) return;

            if (_voice.IsConnected)
                HandleDisconnect();
            else
                HandleConnect();               
        }

        private void HandleConnect()
        {
            int port;
            if (!int.TryParse(PortField.Text, out port))
                return;

            if (!_voice.IsConnected)
                _voice.Connect(IPField.Text, port, SecretField.Password);

            Dispatcher.Invoke(() =>
            {
                InteractionButton.Content = "Disconnect";
            });
        }

        private void HandleDisconnect()
        {
            if(_voice.IsConnected)
                _voice.Disconnect();

            Dispatcher.Invoke(() =>
            {
                InteractionButton.Content = "Connect";
                PlayerLabel.Text = "";
            });
        }

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            int port;
            if (!int.TryParse(PortField.Text, out port))
                port = 1234;

            var voiceMode = _voice == null ? Properties.Settings.Default.VoiceMode : _voice.SelectedVoiceMode;

            Properties.Settings.Default.IP = IPField.Text;
            Properties.Settings.Default.Port = port;
            Properties.Settings.Default.VoiceMode = voiceMode;
            Properties.Settings.Default.Volume = Convert.ToInt32(VolumeSlider.Value);

            Properties.Settings.Default.Save();

            _voice?.Disconnect();
            _listener.Dispose();
        }

        private void PreviewPortInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTextAllowed(e.Text);
        }

        private bool IsTextAllowed(string text)
        {
            Regex regex = new Regex("[^0-9.-]+");
            return !regex.IsMatch(text);
        }

        private void OnSelectVoiceActivation(object sender, RoutedEventArgs e)
        {
            if (_voice == null) return;
            _voice.SelectedVoiceMode = VoiceMode.VoiceActivation;
        }

        private void OnSelectPushToTalk(object sender, RoutedEventArgs e)
        {
            if (_voice == null) return;
            _voice.SelectedVoiceMode = VoiceMode.PushToTalk;
        }

        private void OnSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Voice.Volume = Convert.ToInt32(e.NewValue);
        }
    }
}
