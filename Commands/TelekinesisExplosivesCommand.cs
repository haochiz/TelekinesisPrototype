using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TelekinesisPrototype.Commands;

public sealed class TelekinesisExplosivesCommand : ModCommand
{
    public override string Command => "tkexplosives";
    public override CommandType Type => CommandType.Chat;
    public override string Usage => "/tkexplosives";
    public override string Description => "Gives one Bomb and one Grenade for remote-steering testing.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        Player player = caller.Player;
        var source = player.GetSource_GiftOrReward("TelekinesisPrototypeExplosiveTestKit");

        player.QuickSpawnItem(source, ItemID.Bomb);
        player.QuickSpawnItem(source, ItemID.Grenade);

        caller.Reply("Added 1 Bomb and 1 Grenade. With TK REMOTE on, keep item-use held after throwing to steer them toward the cursor.");
    }
}
