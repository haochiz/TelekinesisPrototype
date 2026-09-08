using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TelekinesisPrototype.Common;

public sealed class TelekinesisStatusSystem : ModSystem
{
    private const float StatusScale = 0.72f;
    private const float RightMargin = 16f;
    private const float BottomMargin = 18f;

    public override void PostDrawInterface(SpriteBatch spriteBatch)
    {
        if (Main.gameMenu || Main.netMode != NetmodeID.SinglePlayer || Main.myPlayer < 0 || Main.myPlayer >= Main.maxPlayers)
            return;

        Player player = Main.LocalPlayer;
        if (player == null || !player.active)
            return;

        TelekinesisPlayer tk = player.GetModPlayer<TelekinesisPlayer>();
        TelekinesisLevitationPlayer levitation = player.GetModPlayer<TelekinesisLevitationPlayer>();
        TelekinesisEnemyManipulationPlayer manipulation = player.GetModPlayer<TelekinesisEnemyManipulationPlayer>();

        string remoteKey = GetFirstAssignedKey(TelekinesisPrototypeMod.ToggleRemoteControlKeybind);
        string levitationKey = GetFirstAssignedKey(TelekinesisPrototypeMod.ToggleLevitationKeybind);
        string manipulationKey = GetFirstAssignedKey(TelekinesisPrototypeMod.ManipulateEnemyKeybind);
        string duration = levitation.LevitationInfinite ? "INF" : $"{levitation.LevitationRemainingSeconds:0.0}s";

        string remoteText = $"REMOTE [{remoteKey}] {(tk.RemoteControlEnabled ? "ON" : "OFF")}";
        string levitationText = $"LEV [{levitationKey}] {(levitation.LevitationEnabled ? "ON" : "OFF")} {duration}";
        string manipulationText = $"MANIP [{manipulationKey}] {(manipulation.IsManipulating ? "ACTIVE" : "READY")}";
        const string separator = "   |   ";

        Color remoteColor = tk.RemoteControlEnabled ? Color.Cyan : Color.Gray;
        Color levitationColor = levitation.LevitationEnabled ? Color.Cyan : Color.Gray;
        Color manipulationColor = manipulation.IsManipulating ? Color.Cyan : Color.Gray;
        Color separatorColor = Color.DarkGray;

        var font = FontAssets.MouseText.Value;
        float remoteWidth = font.MeasureString(remoteText).X * StatusScale;
        float separatorWidth = font.MeasureString(separator).X * StatusScale;
        float levitationWidth = font.MeasureString(levitationText).X * StatusScale;
        float manipulationWidth = font.MeasureString(manipulationText).X * StatusScale;
        float totalWidth = remoteWidth + separatorWidth + levitationWidth + separatorWidth + manipulationWidth;

        // Draw one compact status strip in the lower-right, away from Terraria's inventory/chest
        // UI (left/top), buffs and minimap (right/top), chat (left/bottom), and boss bars (center/bottom).
        // PostDrawInterface coordinates here already correspond to the screen-space used by this
        // SpriteBatch. Dividing by Main.UIScale incorrectly moves the anchor toward screen center
        // at UI scales above 100%.
        float x = Main.screenWidth - RightMargin - totalWidth;
        float y = Main.screenHeight - BottomMargin - font.LineSpacing * StatusScale;

        DrawSegment(spriteBatch, remoteText, ref x, y, remoteColor);
        DrawSegment(spriteBatch, separator, ref x, y, separatorColor);
        DrawSegment(spriteBatch, levitationText, ref x, y, levitationColor);
        DrawSegment(spriteBatch, separator, ref x, y, separatorColor);
        DrawSegment(spriteBatch, manipulationText, ref x, y, manipulationColor);
    }

    private static void DrawSegment(SpriteBatch spriteBatch, string text, ref float x, float y, Color color)
    {
        Utils.DrawBorderString(spriteBatch, text, new Vector2(x, y), color, StatusScale);
        x += FontAssets.MouseText.Value.MeasureString(text).X * StatusScale;
    }

    private static string GetFirstAssignedKey(ModKeybind keybind)
    {
        var keys = keybind?.GetAssignedKeys();
        return keys != null && keys.Count > 0 ? keys[0] : "Unbound";
    }
}
