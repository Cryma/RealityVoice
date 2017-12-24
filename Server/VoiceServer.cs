using GrandTheftMultiplayer.Server.API;
using GrandTheftMultiplayer.Server.Constant;
using GrandTheftMultiplayer.Server.Elements;
using GrandTheftMultiplayer.Shared.Math;
using Lidgren.Network;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VoiceChat.EventArguments;

namespace VoiceChat
{
    public class VoiceServer : IDisposable
    {

        public delegate void OnTokenGeneratedHandler(VoiceConnectedEventArgs eventArgs);
        public delegate void OnVoicePlayerConnectedHandler(Client player);
        public delegate void OnVoicePlayerDisconnectedHandler(Client player);

        public event OnTokenGeneratedHandler OnTokenGenerated;
        public event OnVoicePlayerConnectedHandler OnVoicePlayerConnected;
        public event OnVoicePlayerDisconnectedHandler OnVoicePlayerDisconnected;

        private ConcurrentDictionary<NetConnection, ClientWrapper> _connectedPlayers = new ConcurrentDictionary<NetConnection, ClientWrapper>();
        private List<int> _usedIDs = new List<int>();

        private NetServer _server;
        private Thread _serverThread;
        private bool _shutDown;

        public VoiceServer(int port, API API)
        {
            var config = new NetPeerConfiguration("voice-chat")
            {
                Port = port
            };
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);

            try
            {
                _server = new NetServer(config);
                _server.Start();
            }
            catch(NetException ex)
            {
                API.shared.consoleOutput(LogCat.Error, $"Network error while starting server: {ex}");
                return;
            }
            catch(Exception ex)
            {
                API.shared.consoleOutput(LogCat.Error, $"Error while starting server: {ex}");
                return;
            }

            API.shared.consoleOutput(LogCat.Info, $"Voice server started on port {port}");

            _serverThread = new Thread(Update);
            _serverThread.IsBackground = true;
            _serverThread.Start();

            API.onPlayerConnected += GameOnPlayerConnected;
            API.onClientEventTrigger += GameOnClientEventTrigger;
        }

        private void GameOnClientEventTrigger(Client sender, string eventName, params object[] arguments)
        {
            if (eventName == "voiceCam") //synced entity data??
            {
                sender.setData("campos", (Vector3)arguments[0]);
            }
        }


        private void GameOnPlayerConnected(Client player)
        {
            string secretToken = RandomString(5);
            player.setData("voice_token", secretToken);
            player.triggerEvent("voiceInit");

            OnTokenGenerated?.Invoke(new VoiceConnectedEventArgs()
            {
                Client = player,
                SecretToken = secretToken
            });
        }

        private void Update()
        {
            API.shared.consoleOutput("Message thread started");

            if (_server == null)
                return;

            if (_server.Status != NetPeerStatus.Running)
            {
                API.shared.consoleOutput("Server not started");
                return;
            }

            try
            {

                while (!_shutDown)
                {
                    Thread.Sleep(10);

                    var messages = new List<NetIncomingMessage>();
                    var messageCount = _server.ReadMessages(messages);
                    if (messageCount == 0)
                        continue;

                    foreach(var message in messages)
                    {
                        try
                        {
                            if (message.MessageType == NetIncomingMessageType.ConnectionApproval)
                            {
                                bool playerFound = false;
                                var token = message.ReadString();

                                foreach (var player in API.shared.getAllPlayers())
                                {
                                    if (player.getData("voice_token") == token)
                                    {
                                        PlayerVoiceConnected(message.SenderConnection, player);
                                        playerFound = true;
                                        break;
                                    }
                                }

                                if (!playerFound)
                                {
                                    API.shared.consoleOutput(LogCat.Warn, "Player tried to join with an invalid token");
                                    message.SenderConnection.Deny("invalid token provided");
                                }
                            }
                            else if (message.MessageType == NetIncomingMessageType.StatusChanged)
                            {
                                var status = (NetConnectionStatus)message.ReadByte();
#if DEBUG
                                API.shared.consoleOutput(LogCat.Debug, "Voice player status changed");
#endif
                                if (status == NetConnectionStatus.Disconnected)
                                    PlayerVoiceDisconnected(message.SenderConnection);
                            }
                            else if (message.MessageType == NetIncomingMessageType.Data)
                            {
                                var type = message.ReadByte();
                                if (type == 0x01)
                                    BroadcastVoiceData(message);
                            }
                            _server.Recycle(message);
                        }
                        catch(IndexOutOfRangeException ex)
                        {
                            API.shared.consoleOutput(LogCat.Error, $"Error decoding data: {ex}");
                        }
                        catch(Exception ex)
                        {
                            API.shared.consoleOutput(LogCat.Error, $"Error reading network data: {ex}");
                        }
                    }
                }
            }
            catch (ThreadAbortException) { } //No need to handle that
            catch (Exception ex)
            {
                API.shared.consoleOutput(LogCat.Error, $"Excetion in netcode: {ex}");
            }
            API.shared.consoleOutput("Message thread stopped");
        }

        private int GetID()
        {
            lock(_usedIDs)
            {
                int id = -1;
                for (int i = 0; i < int.MaxValue; i++)
                {
                    if(!_usedIDs.Contains(i))
                    {
                        id = i;
                        _usedIDs.Add(id);
                        break;
                    }
                }

                if(id == -1)
                {
                    API.shared.consoleOutput(LogCat.Error, "No valid ID");
                    return -1;
                }

                return id;
            }
        }

        private void BroadcastVoiceData(NetIncomingMessage message)
        {
            if (!_connectedPlayers.ContainsKey(message.SenderConnection))
                return;

            int size = message.ReadInt32();
            byte[] voiceData = message.ReadBytes(size);

            var sender = _connectedPlayers[message.SenderConnection];

            foreach (var player in _connectedPlayers)
            {
                var client = player.Value.Client;

                if (client.name == sender.Client.name) continue;
                if (client.position.DistanceTo(sender.Client.position) > 20) continue;

                var relativePosition = sender.Client.position - player.Value.Client.position;
                var cameraPosition = player.Value.Client.hasData("campos") ? (Vector3)player.Value.Client.getData("campos") : new Vector3();

                var outMessage = _server.CreateMessage();
                outMessage.Write((byte)0x01);
                outMessage.Write(size);
                outMessage.Write(voiceData);

                outMessage.Write(player.Value.ID);
            
                var positionChanged = relativePosition != player.Value.OldPosition;
                var cameraChanged = cameraPosition != player.Value.OldCamera;

                if (positionChanged && cameraChanged)
                    outMessage.Write((byte)0x01);
                else if (positionChanged)
                    outMessage.Write((byte)0x02);
                else if (cameraChanged)
                    outMessage.Write((byte)0x03);
                else
                {
                    outMessage.Write((byte)0x00);
                    _server.SendMessage(outMessage, player.Key, NetDeliveryMethod.UnreliableSequenced);
                    continue;
                }

                if (positionChanged)
                {
                    outMessage.Write(relativePosition.X);
                    outMessage.Write(relativePosition.Y);
                    outMessage.Write(relativePosition.Z);

                    player.Value.OldPosition = relativePosition;
                }

                if (cameraChanged)
                {
                    outMessage.Write(cameraPosition.X);
                    outMessage.Write(cameraPosition.Y);
                    outMessage.Write(cameraPosition.Z);

                    player.Value.OldCamera = cameraPosition;
                }

                _server.SendMessage(outMessage, player.Key, NetDeliveryMethod.UnreliableSequenced);
            }
        }

        private void PlayerVoiceConnected(NetConnection connection, Client client)
        {
#if DEBUG
            API.shared.consoleOutput(LogCat.Info, $"{client.socialClubName} connected with voice");
#endif
            var id = GetID();
            if (id == -1)
                return;

            var clientWrapper = new ClientWrapper(client, id);

            connection.Approve();
            _connectedPlayers.AddOrUpdate(connection, clientWrapper, (con, cli) => clientWrapper);

            var task = new Task(delegate
            {
                Thread.Sleep(500); //Why the delay?

                foreach(var voicePlayer in _connectedPlayers)
                {
                    var message = _server.CreateMessage();
                    message.Write((byte)0x00);
                    message.Write(voicePlayer.Value.ID);
                    message.Write(voicePlayer.Value.Client.name);
                    _server.SendMessage(message, connection, NetDeliveryMethod.ReliableUnordered);
                }

                var newPlayerMessage = _server.CreateMessage();
                newPlayerMessage.Write((byte)0x00);
                newPlayerMessage.Write(clientWrapper.ID);
                newPlayerMessage.Write(client.name);
                _server.SendToAll(newPlayerMessage, connection, NetDeliveryMethod.ReliableUnordered, 1);
            });
            task.Start();

            OnVoicePlayerConnected?.Invoke(client);
        }

        private void PlayerVoiceDisconnected(NetConnection connection)
        {
            if (!_connectedPlayers.ContainsKey(connection))
                return;

            ClientWrapper client;
            if (_connectedPlayers.TryRemove(connection, out client))
            {
                if (_usedIDs.Contains(client.ID))
                    _usedIDs.Add(client.ID);
                OnVoicePlayerDisconnected?.Invoke(client.Client);
            }
        }

        static string RandomString(int length)
        {
            const string valid = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
            var res = new StringBuilder();
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                byte[] uintBuffer = new byte[sizeof(uint)];

                while (length-- > 0)
                {
                    rng.GetBytes(uintBuffer);
                    uint num = BitConverter.ToUInt32(uintBuffer, 0);
                    res.Append(valid[(int)(num % (uint)valid.Length)]);
                }
            }

            return res.ToString();
        }

        public void Stop() => Dispose();

        public void Dispose()
        {
            _shutDown = true;
            if (_server == null)
                return;

            API.shared.consoleOutput(LogCat.Info, "Voice server stopping...");
            _server.Shutdown("Server is shutting down");
            if (_serverThread.IsAlive)
            {
                try
                {
                    _serverThread.Join(1000);
                    if (_serverThread.IsAlive)
                        _serverThread.Abort();
                }
                catch (Exception ex)
                {
                    API.shared.consoleOutput(LogCat.Error, "Error while trying to stop server");
                }
            }
            API.shared.consoleOutput(LogCat.Info, "Voice server stopped");
        }
    }
}
