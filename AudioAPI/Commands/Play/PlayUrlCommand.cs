using AudioAPI.Extensions;

using LabExtended.API;

using LabExtended.Commands;
using LabExtended.Commands.Arguments;
using LabExtended.Commands.Interfaces;

using LabExtended.Utilities.Async;

using VoiceChat;

namespace AudioAPI.Commands.File
{
    public class PlayUrlCommand : CustomCommand
    {
        public override string Command => "url";
        public override string Description => "Plays audio from a URL.";

        public override ArgumentDefinition[] BuildArgs()
        {
            return GetArgs(x =>
            {
                x.WithArg<string>("Url", "Url to the audio file.");
                x.WithOptional<VoiceChatChannel>("Channel", "The channel to send the audio via", VoiceChatChannel.RoundSummary);
            });
        }

        public override void OnCommand(ExPlayer sender, ICommandContext ctx, ArgumentCollection args)
        {
            base.OnCommand(sender, ctx, args);

            var url = args.Get<string>("Url");
            var channel = args.Get<VoiceChatChannel>("Channel");

            ctx.RespondOk("Downloading file ..");

            AsyncMethods.GetByteArrayAsync(url).Await(data =>
            {
                sender.SendRemoteAdminMessage($"File downloaded, {data.Length} bytes");

                var player = new AudioPlayer();

                player.Initialize(true);
                player.AddAllReceivers();

                player.Channel = channel;
                player.Source = sender;

                player.OnFinished += player.Dispose;
                player.Play(data);
            });
        }
    }
}