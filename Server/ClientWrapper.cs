using GrandTheftMultiplayer.Server.Elements;
using GrandTheftMultiplayer.Shared.Math;

namespace VoiceChat
{
    public class ClientWrapper
    {
        public int ID;
        public Client Client;
        public Vector3 OldPostion;
        public Vector3 OldCamera;

        public ClientWrapper(Client client, int ID)
        {
            this.Client = client;

            OldPostion = new Vector3();
            OldCamera = new Vector3();
        }
    }
}
