using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TelekinesisPrototype.Commands;

public sealed class TelekinesisTestCommand : ModCommand
{
    public override string Command => "tktest";
    public override CommandType Type => CommandType.Chat;
    public override string Usage => "/tktest";
    public override string Description => "Gives a representative set of vanilla items and a Target Dummy for telekinesis prototype testing.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        Player player = caller.Player;
        var source = player.GetSource_GiftOrReward("TelekinesisPrototypeTestKit");

        player.QuickSpawnItem(source, ItemID.WoodenSword);
        player.QuickSpawnItem(source, ItemID.GoldBroadsword);
        player.QuickSpawnItem(source, ItemID.Katana);
        player.QuickSpawnItem(source, ItemID.ZombieArm);
        player.QuickSpawnItem(source, ItemID.IceBlade);
        player.QuickSpawnItem(source, ItemID.EnchantedSword);
        player.QuickSpawnItem(source, ItemID.Starfury);
        player.QuickSpawnItem(source, ItemID.CopperShortsword);
        player.QuickSpawnItem(source, ItemID.Spear);
        player.QuickSpawnItem(source, ItemID.Trident);
        player.QuickSpawnItem(source, ItemID.JoustingLance);
        player.QuickSpawnItem(source, ItemID.CopperPickaxe);
        player.QuickSpawnItem(source, ItemID.CopperAxe);
        player.QuickSpawnItem(source, ItemID.WoodenHammer);
        player.QuickSpawnItem(source, ItemID.DirtBlock, 999);
        player.QuickSpawnItem(source, ItemID.Wood, 999);
        player.QuickSpawnItem(source, ItemID.WoodPlatform, 999);
        player.QuickSpawnItem(source, ItemID.TargetDummy);

        caller.Reply("Telekinesis test kit spawned with representative melee weapons (including projectile swords and spears/lances), tools, building materials, and a Target Dummy.");
    }
}
