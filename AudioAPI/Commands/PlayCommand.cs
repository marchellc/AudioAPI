using AudioAPI.Commands.File;

using CommandSystem;

using LabExtended.Commands;

namespace AudioAPI.Commands
{
    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    public class PlayCommand : VanillaParentCommandBase
    {
        public override string Command => "play";
        public override string Description => "A set of commands for audio playback.";

        public override void LoadGeneratedCommands()
        {
            RegisterCommand(new PlayFileCommand());
            RegisterCommand(new PlayUrlCommand());
            RegisterCommand(new PlayAtUrlCommand());
        }
    }
}