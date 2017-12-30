using System;

namespace VoiceChat
{
    public struct VoicePacket
    {

        public byte[] Data;
        public int DataSize;

        public VoicePacket(byte[] data, int dataSize)
        {
            Data = data;
            DataSize = dataSize;
        }

    }
}