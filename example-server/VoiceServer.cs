using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using GrandTheftMultiplayer.Server.API;
using GrandTheftMultiplayer.Server.Elements;
using GrandTheftMultiplayer.Shared.Math;
using Lidgren.Network;
using NLog;

namespace VoiceServer
{
    public class VoiceServer : Script
    {

        public static Logger Logger = LogManager.GetCurrentClassLogger();

        public Dictionary<NetConnection, Client> VoicePlayers = new Dictionary<NetConnection, Client>();

        private NetServer _server;

        private bool _started;

        public VoiceServer()
        {
            API.onPlayerFinishedDownload += OnPlayerFinishedDownload;
            API.onClientEventTrigger += OnClientEventTrigger;
            API.onUpdate += () =>
            {
                if (_started)
                    Update();
            };

            var config = new NetPeerConfiguration("rl-voice")
            {
                Port = 2424
            };
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);

            _server = new NetServer(config);
            Start();
        }

        private void OnClientEventTrigger(Client sender, string eventName, params object[] arguments)
        {
            if (eventName == "voiceCam")
            {
                sender.setData("campos", (Vector3) arguments[0]);
            }
        }

        private void OnPlayerFinishedDownload(Client player)
        {
            string token = RandomString(5);
            player.setData("token", token);
            player.sendChatMessage("Token: " + token);
            player.triggerEvent("voiceInit");
        }

        private void Start()
        {
            _server.Start();
            Logger.Info("Started Voice Server!");
            _started = true;
        }

        private void Update()
        {
            NetIncomingMessage msg;
            msg = _server.ReadMessage();
            if (msg == null) return;

            if (msg.MessageType == NetIncomingMessageType.ConnectionApproval)
            {
                string token = msg.ReadString();
                foreach (var c in API.shared.getAllPlayers())
                {
                    if (c.getData("token") == token)
                    {
                        PlayerJoined(msg.SenderConnection, c);
                        return;
                    }
                }
                Logger.Info("Player trying to connect with voice is invalid!");
                msg.SenderConnection.Deny("invalid token");
            }
            else if (msg.MessageType == NetIncomingMessageType.StatusChanged)
            {
                var status = (NetConnectionStatus)msg.ReadByte();
                Logger.Info("Voice Player Status Changed: " + status);
                if (status == NetConnectionStatus.Disconnected)
                {
                    PlayerDisconnected(msg.SenderConnection);
                }
            }
            else if (msg.MessageType == NetIncomingMessageType.Data)
            {
                byte type = msg.ReadByte();
                if (type == 1)
                {
                    int size = msg.ReadInt32();
                    byte[] encoded = msg.ReadBytes(size);

                    ReceivedVoice(encoded, size, VoicePlayers[msg.SenderConnection]);

                    return;
                }
            }
            _server.Recycle(msg);
        }

        private void ReceivedVoice(byte[] encoded, int size, Client sender)
        {
            foreach (var p in VoicePlayers)
            {
                if (p.Value.name == sender.name) continue;
                if (Vector3.Distance(sender.position, p.Value.position) > 20) continue;

                Vector3 relativePosition;

                relativePosition = sender.position - p.Value.position;

                Vector3 camPos = p.Value.getData("campos");
                
                NetOutgoingMessage m = _server.CreateMessage();
                m.Write((byte)1);
                // Size of byte[]
                m.Write(size);
                // encoded byte[]
                m.Write(encoded);
                // name of the player that is talking
                m.Write(sender.name);
                // relative position of the player
                m.Write(relativePosition.X);
                m.Write(relativePosition.Y);
                m.Write(relativePosition.Z);
                // receiving player camera
                m.Write(camPos.X);
                m.Write(camPos.Y);
                m.Write(camPos.Z);
                _server.SendMessage(m, VoicePlayers.First(o => o.Value == p.Value).Key,
                    NetDeliveryMethod.ReliableOrdered);
            }
        }

        private void PlayerDisconnected(NetConnection connection)
        {
            Client player = VoicePlayers[connection];

            VoicePlayers.Remove(connection);
        }

        private void PlayerJoined(NetConnection connection, Client client)
        {
            Logger.Info("Player {0} connected with Voice!", client.name);
            connection.Approve();
            VoicePlayers.Add(connection, client);

            API.shared.delay(500, true, () =>
            {
                // Bootstrap new player with existing players
                foreach (var p in VoicePlayers)
                {
                    Logger.Info("Sent existing player: " + p.Value.name + " to: " + client.name);
                    NetOutgoingMessage msg = _server.CreateMessage();
                    msg.Write((byte)2);
                    msg.Write(p.Value.name);
                    _server.SendMessage(msg, connection, NetDeliveryMethod.ReliableOrdered, 2);
                }

                // Send new player to old players
                NetOutgoingMessage msg1 = _server.CreateMessage();
                msg1.Write((byte)2);
                msg1.Write(client.name);
                _server.SendToAll(msg1, connection, NetDeliveryMethod.ReliableOrdered, 1);
            });
        }

        public void Shutdown()
        {
            _server?.Shutdown("Shutting down...");
            Logger.Info("Stopped Voice Server!");
        }

        static string RandomString(int length)
        {
            const string valid = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
            StringBuilder res = new StringBuilder();
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

    }
}
