using AudioAPI.Extensions;

using LabExtended.API;

using LabExtended.Commands;
using LabExtended.Commands.Arguments;

using LabExtended.Core;
using LabExtended.Commands.Interfaces;

using VoiceChat;

namespace AudioAPI.Commands.File
{
    public class PlayFileCommand : CustomCommand
    {
        public override string Command => "file";
        public override string Description => "Plays audio from a file.";

        public override ArgumentDefinition[] BuildArgs()
        {
            return GetArgs(x =>
            {
                x.WithArg<string>("Path", "Path to the audio file.");
                x.WithOptional<VoiceChatChannel>("Channel", "The channel to send the audio via", VoiceChatChannel.RoundSummary);
            });
        }

        public override void OnCommand(ExPlayer sender, ICommandContext ctx, ArgumentCollection args)
        {
            base.OnCommand(sender, ctx, args);

            var path = args.Get<string>("Path");
            var channel = args.Get<VoiceChatChannel>("Channel");

            if (!path.Contains("/") && !path.Contains("\\"))
                path = $"{ExLoader.Folder}/{path}";

            if (!System.IO.File.Exists(path))
            {
                ctx.RespondFail("This file does not exist.");
                return;
            }
            else
            {
                var data = System.IO.File.ReadAllBytes(path);

                var player = new AudioPlayer();

                player.Receivers.Clear();
                player.AddAllReceivers();

                player.Channel = channel;
                player.Source = sender;

                player.OnFinished += player.Dispose;
                player.Play(data);

                ctx.RespondOk($"Started playing file '{System.IO.Path.GetFileName(path)}'");
                return;
            }
        }
    }
}