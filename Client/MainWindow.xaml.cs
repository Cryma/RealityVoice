﻿using System;
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

            _listener.KeyDown += OnGlobalKeyDown;
            _listener.KeyUp += OnGlobalKeyUp;

            
            _voice = new Voice();
            _voice.OnStatusChanged += OnStatusChanged;
            _voice.OnPlayerJoined += OnPlayerJoined;

            _voice.Start();
            
        }

        private void OnGlobalKeyDown(object sender, RawKeyEventArgs e)
        {
            if (e.Key == Key.B && _voice?.SelectedSpeakMode == SpeakMode.PushToTalk)
            {
                _voice.IsSpeaking = true;
            }
        }

        private void OnGlobalKeyUp(object sender, RawKeyEventArgs e)
        {
            if (e.Key == Key.B && _voice?.SelectedSpeakMode == SpeakMode.PushToTalk)
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
            _voice.SelectedSpeakMode = SpeakMode.VoiceActivation;
        }

        private void OnSelectPushToTalk(object sender, RoutedEventArgs e)
        {
            if (_voice == null) return;
            _voice.SelectedSpeakMode = SpeakMode.PushToTalk;
        }

    }
}
