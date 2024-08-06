using AudioAPI.Extensions;
using LabExtended.API;

using LabExtended.Commands;
using LabExtended.Commands.Arguments;
using LabExtended.Commands.Interfaces;

using LabExtended.Utilities.Async;

using MEC;

using System.Collections.Generic;

using VoiceChat;

namespace AudioAPI.Commands.File
{
    public class PlayAtUrlCommand : CustomCommand
    {
        public static AudioSource TestSource;

        public override string Command => "aturl";
        public override string Description => "Plays audio from a URL at a specified player.";

        public override ArgumentDefinition[] BuildArgs()
        {
            return GetArgs(x =>
            {
                x.WithArg<string>("Url", "Url to the audio file.");
                x.WithOptional<VoiceChatChannel>("Channel", "The channel to send the audio via", VoiceChatChannel.Proximity);
            });
        }

        public override void OnCommand(ExPlayer sender, ICommandContext ctx, ArgumentCollection args)
        {
            base.OnCommand(sender, ctx, args);

            var url = args.Get<string>("Url");
            var channel = args.Get<VoiceChatChannel>("Channel");

            ctx.RespondOk("Downloading file ..");

            IEnumerator<float> AwaitSpawn(byte[] data)
            {
                if (TestSource is null)
                {
                    TestSource = new AudioSource();
                    TestSource.Spawn();
                }

                while (!TestSource.IsSpawned)
                    yield return Timing.WaitForOneFrame;

                TestSource.ProximityReceivers.Clear();

                if (channel is VoiceChatChannel.Proximity)
                    TestSource.ProximityReceivers.Add(sender);

                TestSource.Channel = channel;

                TestSource.Player.Receivers.Clear();
                TestSource.Player.AddAllReceivers();

                TestSource.Play(sender.Position, data);
            }

            AsyncMethods.GetByteArrayAsync(url).Await(data =>
            {
                sender.SendRemoteAdminMessage($"File downloaded, {data.Length} bytes");
                Timing.RunCoroutine(AwaitSpawn(data));
            });
        }
    }
}