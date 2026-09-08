using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TelekinesisPrototype.Commands;

public sealed class TelekinesisExtractinatorCommand : ModCommand
{
    public override string Command => "tkextractinator";
    public override CommandType Type => CommandType.Chat;
    public override string Usage => "/tkextractinator";
    public override string Description => "Gives one Extractinator for prototype testing.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        Player player = caller.Player;
        var source = player.GetSource_GiftOrReward("TelekinesisPrototypeExtractinatorTest");
        player.QuickSpawnItem(source, ItemID.Extractinator);
        caller.Reply("Added 1 Extractinator.");
    }
}
