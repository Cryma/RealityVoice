using System;

namespace RealityVoice
{
    public struct VoicePacket
    {

        public byte[] Data;
        public int DataSize;
        public DateTime CreatedAt;

        public VoicePacket(byte[] data, int dataSize)
        {
            Data = data;
            DataSize = dataSize;
            CreatedAt = DateTime.Now;
        }

    }
}