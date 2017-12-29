using System;

namespace RealityVoice
{
    public struct VoicePacket
    {

        public byte[] EncodedVoice;
        public int DataSize;
        public DateTime CreatedAt;

        public VoicePacket(byte[] encoded, int dataSize)
        {
            EncodedVoice = encoded;
            DataSize = dataSize;
            CreatedAt = DateTime.Now;
        }

    }
}