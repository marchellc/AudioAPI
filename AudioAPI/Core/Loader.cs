using AudioAPI.Core;

using LabExtended.Core;

using PluginAPI.Core.Attributes;

namespace AudioAPI
{
    public class Loader
    {
        public static Loader Instance;

        [PluginConfig]
        public Config Config;

        [PluginEntryPoint("AudioAPI", "1.0.0", "An audio API.", "marchellcx")]
        public void Load()
        {
            Instance = this;
            ExLoader.Info("Audio API", "Loaded!");
        }
    }
}