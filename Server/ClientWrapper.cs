using GrandTheftMultiplayer.Server.Elements;
using GrandTheftMultiplayer.Shared.Math;

namespace VoiceChat
{
    public class ClientWrapper
    {
        public int ID;
        public Client Client;
        public Vector3 OldPosition;
        public Vector3 OldCamera;

        public ClientWrapper(Client client, int id)
        {
            this.ID = id;
            this.Client = client;

            OldPosition = new Vector3();
            OldCamera = new Vector3();
        }
    }
}
