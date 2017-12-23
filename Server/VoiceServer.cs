using Lidgren.Network;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GrandTheftMultiplayer.Server.API;
using GrandTheftMultiplayer.Server.Elements;
using System.Threading;
using GrandTheftMultiplayer.Shared.Math;

namespace Server
{
    public class VoiceServer : IDisposable
    {
        private static Logger Logger = LogManager.GetCurrentClassLogger();

        private ConcurrentDictionary<NetConnection, ClientWrapper> _connectedPlayers = new ConcurrentDictionary<NetConnection, ClientWrapper>();
        private List<int> _usedIDs = new List<int>();

        private NetServer _server;
        private Thread _serverThread;
        private bool _shutDown;

        public VoiceServer(int port)
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
                Logger.Fatal(ex, "Network error while starting server");
                return;
            }
            catch(Exception ex)
            {
                Logger.Fatal(ex, "Error while starting server");
                return;
            }

            Logger.Info($"Voice server started on port {port}");

            _serverThread = new Thread(Update);
            _serverThread.IsBackground = true;
            _serverThread.Start();
        }

        private void Update()
        {
            if (_server == null)
                return;

            if (_server.Status != NetPeerStatus.Running)
                return;

            try
            {

                while (!_shutDown)
                {
                    var messages = new List<NetIncomingMessage>();
                    var messageCount = _server.ReadMessages(messages);
                    if (messageCount == 0)
                        return;

                    foreach(var message in messages)
                    {
                        try
                        {
                            if (message.MessageType == NetIncomingMessageType.ConnectionApproval)
                            {
                                var token = message.ReadString();

                                foreach (var player in API.shared.getAllPlayers())
                                {
                                    if (player.getData("voice_token") == token)
                                    {
                                        PlayerVoiceConnected(message.SenderConnection, player);
                                        return;
                                    }
                                }

                                Logger.Warn("Player tried to join with an invalid token");
                                message.SenderConnection.Deny("invalid token provided");
                            }
                            else if (message.MessageType == NetIncomingMessageType.StatusChanged)
                            {
                                var status = (NetConnectionStatus)message.ReadByte();
#if DEBUG
                                Logger.Debug("Voice player status changed");
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
                            Logger.Error(ex, "Error decoding data");
                        }
                        catch(Exception ex)
                        {
                            Logger.Error(ex, "Error reading network data");
                        }
                    }
                }
            }
            catch (ThreadAbortException) { } //No need to handle that
            catch (Exception ex)
            {
                Logger.Error(ex, "Excetion in netcode");
            }
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
                        break;
                    }
                }

                if(id == -1)
                {
                    Logger.Error("No valid ID");
                    return -1;
                }

                return id;
            }
        }

        private void BroadcastVoiceData(NetIncomingMessage message)
        {
            if (_connectedPlayers.ContainsKey(message.SenderConnection))
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

                outMessage.Write(relativePosition.X);
                outMessage.Write(relativePosition.Y);
                outMessage.Write(relativePosition.Z);

                outMessage.Write(cameraPosition.X);
                outMessage.Write(cameraPosition.Y);
                outMessage.Write(cameraPosition.Z);

                _server.SendMessage(outMessage, player.Key, NetDeliveryMethod.UnreliableSequenced);
            }
        }

        private void PlayerVoiceConnected(NetConnection connection, Client client)
        {
#if DEBUG
            Logger.Info($"{client.socialClubName} connected with voice");
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
                newPlayerMessage.Write(clientWrapper.ID);
                newPlayerMessage.Write((byte)0x00);
                newPlayerMessage.Write(client.name);
                _server.SendToAll(newPlayerMessage, connection, NetDeliveryMethod.ReliableUnordered, 1);
            });
            task.Start();
        }

        private void PlayerVoiceDisconnected(NetConnection connection)
        {
            if (!_connectedPlayers.ContainsKey(connection))
                return;

            _connectedPlayers.TryRemove(connection, out _);
        }

        public void Stop() => Dispose();

        public void Dispose()
        {
            _shutDown = true;
            if (_server == null)
                return;

            Logger.Info("Voice server stopping...");
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
                    Logger.Error(ex, "Error while trying to stop server");
                }
            }
            Logger.Info("Voice server stopped");
        }
    }
}
