using GrandTheftMultiplayer.Server.API;
using GrandTheftMultiplayer.Server.Elements;
using System;
using VoiceChat;

namespace GTMP_Test_Resource
{
    public class Resource : Script
    {
        private VoiceServer _server;

        public Resource()
        {
            _server = new VoiceServer(1234, API);
            _server.OnTokenGenerated += _server_OnTokenGenerated;
            _server.OnVoicePlayerConnected += _server_OnVoicePlayerConnected;
            _server.OnVoicePlayerDisconnected += _server_OnVoicePlayerDisconnected;
        }

        private void _server_OnVoicePlayerDisconnected(Client player)
        {
            API.consoleOutput("Player " + player.name + " connected with voice");
        }

        private void _server_OnVoicePlayerConnected(Client player)
        {
            API.consoleOutput("Player " + player.name + " disconnected with voice");
        }

        private void _server_OnTokenGenerated(VoiceChat.EventArguments.VoiceConnectedEventArgs eventArgs)
        {
            API.consoleOutput("Player " + eventArgs.Client.name + " got token " + eventArgs.SecretToken);
        }
    }
}
