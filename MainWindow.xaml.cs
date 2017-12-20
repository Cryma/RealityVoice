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

        private Voice _voice;

        public MainWindow()
        {
            InitializeComponent();
            
            _voice = new Voice();
            _voice.OnStatusChanged += OnStatusChanged;
            _voice.OnPlayerJoined += OnPlayerJoined;

            _voice.Start();
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
            {
                HandleDisconnect();
            }
            else
            {
                HandleConnect();               
            }
        }

        private void HandleConnect()
        {
            if (!_voice.IsConnected)
                _voice.Connect("127.0.0.1", 2424, SecretField.Password);

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
            _voice?.Disconnect();
        }
    }
}
