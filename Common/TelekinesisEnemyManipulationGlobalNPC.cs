using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TelekinesisPrototype.Common;

public sealed class TelekinesisEnemyManipulationGlobalNPC : GlobalNPC
{
    public override void PostAI(NPC npc)
    {
        if (Main.netMode != NetmodeID.SinglePlayer || Main.myPlayer < 0 || Main.myPlayer >= Main.maxPlayers)
            return;

        Player player = Main.LocalPlayer;
        if (player == null || !player.active)
            return;

        player.GetModPlayer<TelekinesisEnemyManipulationPlayer>().ApplyToNpc(npc);
    }
}
