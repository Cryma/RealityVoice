using System;

namespace VoiceChat
{
    public struct VoicePacket
    {

        public byte[] DecodedVoice;
        public int DataSize;

        public VoicePacket(byte[] decoded, int dataSize)
        {
            DecodedVoice = decoded;
            DataSize = dataSize;
        }

    }
}