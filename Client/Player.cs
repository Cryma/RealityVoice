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
            Playback.Listener.Orientation = new Orientation(orientation, new Vector3(0f, 1f, 0f));
        }

        public void PlayVoice(byte[] data, int count)
        {
            if(Playback.CanWrite)
                Playback.Write(data, 0, count);
        }

        private void CreatePlayback()
        {
            Playback = OpenALHelper.PlaybackDevices[0].OpenStream(Voice.SampleRate, OpenALAudioFormat.Mono16Bit);
            Playback.Listener.Position = new Vector3(0f, 0f, 0f);
            Playback.Listener.Velocity = new Vector3(0f, 0f, 0f);
            Playback.Listener.Orientation = new Orientation(new Vector3(0f, 0f, 1f), new Vector3(0f, 1f, 0f));

            Playback.ALPosition = new Vector3(0f, 0f, 0f);
            Playback.Velocity = new Vector3(0f, 0f, 0f);

            Playback.SetVolume(Voice.Volume / 100f);
        }

    }
}