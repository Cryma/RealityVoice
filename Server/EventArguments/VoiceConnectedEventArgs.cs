using GrandTheftMultiplayer.Server.Elements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VoiceChat.EventArguments
{
    public class VoiceConnectedEventArgs : EventArgs
    {
        public Client Client { get; set; }
        public string SecretToken { get; set; }
    }
}
