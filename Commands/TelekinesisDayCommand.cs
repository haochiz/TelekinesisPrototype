using Terraria;
using Terraria.ModLoader;

namespace TelekinesisPrototype.Commands;

public sealed class TelekinesisDayCommand : ModCommand
{
    public override string Command => "tkday";
    public override CommandType Type => CommandType.Chat;
    public override string Usage => "/tkday";
    public override string Description => "Sets the current world time to noon for prototype testing.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        Main.dayTime = true;
        Main.time = 27000.0;
        caller.Reply("Time set to noon.");
    }
}
