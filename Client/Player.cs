using FragLabs.Audio.Engines;
using FragLabs.Audio.Engines.OpenAL;

namespace RealityVoice
{
    public class Player
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public PlaybackStream Playback { get; set; }

        public Player(string name, int id)
        {
            this.Name = name;
            this.ID = id;

            CreatePlayback();
        }

        public void UpdatePosition(Vector3 position)
        {
            Playback.Listener.Position = position;
        }

        public void UpdateOrientation(Vector3 orientation)
        {
            Playback.Listener.Orientation = new Orientation()
            {
                At = orientation,
                Up = new Vector3() { X = 0.0f, Y = 1.0f, Z = 0.0f }
            };
        }

        public void PlayVoice(byte[] data)
        {
            if(Playback.CanWrite)
                Playback.Write(data, 0, Voice.StreamSize);
        }

        private void CreatePlayback()
        {
            Playback = OpenALHelper.PlaybackDevices[0].OpenStream(Voice.SampleRate, OpenALAudioFormat.Mono16Bit);
            Playback.Listener.Position = new Vector3() { X = 0.0f, Y = 0.0f, Z = 0.0f };
            Playback.Listener.Velocity = new Vector3() { X = 0.0f, Y = 0.0f, Z = 0.0f };
            Playback.Listener.Orientation = new Orientation()
            {
                At = new Vector3() { X = 0.0f, Y = 0.0f, Z = 1.0f },
                Up = new Vector3() { X = 0.0f, Y = 1.0f, Z = 0.0f }
            };

            Playback.ALPosition = new Vector3() { X = 0.0f, Y = 0.0f, Z = 0.0f };
            Playback.Velocity = new Vector3() { X = 0.0f, Y = 0.0f, Z = 0.0f };
        }

    }
}