using System.ComponentModel;

using VoiceChat.Codec.Enums;

namespace AudioAPI.Core
{
    public class Config
    {
        [Description("The amount of head samples.")]
        public int HeadSamples { get; set; } = 1920;

        [Description("The type of application to use on the opus encoder.")]
        public OpusApplicationType OpusType { get; set; } = OpusApplicationType.Voip;
    }
}
