using FragLabs.Audio.Engines.OpenAL;
using Lidgren.Network;

namespace RealityVoice
{
    public static class Helper
    {

        public static Vector3 ReadPositionFromMessage(this NetIncomingMessage message)
        {
            float x = message.ReadFloat();
            float y = message.ReadFloat();
            float z = message.ReadFloat();

            return new Vector3
            {
                X = x,
                Y = z,
                Z = y
            };
        }

        public static Vector3 ReadDirectionFromMessage(this NetIncomingMessage message)
        {
            float x = message.ReadFloat();
            float y = message.ReadFloat();
            float z = message.ReadFloat();

            return new Vector3
            {
                X = x,
                Y = z,
                Z = y
            };
        }

    }
}