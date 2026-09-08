using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace TelekinesisPrototype.Common;

public sealed class TelekinesisGripDrawLayer : PlayerDrawLayer
{
    public override Position GetDefaultPosition() => PlayerDrawLayers.AfterLastVanillaLayer;

    public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
    {
        return drawInfo.shadow == 0f && drawInfo.drawPlayer.whoAmI == Main.myPlayer;
    }

    protected override void Draw(ref PlayerDrawSet drawInfo)
    {
        Player player = drawInfo.drawPlayer;
        TelekinesisPlayer tk = player.GetModPlayer<TelekinesisPlayer>();

        if (!tk.ShouldDrawTravellingItem(out int itemType))
            return;

        Item drawItem = player.HeldItem.type == itemType ? player.HeldItem : new Item(itemType);

        Main.instance.LoadItem(itemType);
        Texture2D texture = TextureAssets.Item[itemType].Value;
        Rectangle frame = player.GetItemDrawFrame(itemType);
        float scale = player.GetAdjustedItemScale(drawItem);

        Vector2 aim = Main.MouseWorld - tk.GripPosition;
        if (aim.LengthSquared() <= 0.001f)
            aim = new Vector2(player.direction, 0f);
        aim.Normalize();

        Vector2 worldPosition = tk.GripPosition;
        float rotation = aim.ToRotation() + MathHelper.PiOver4;
        bool rotatedMountHeldItemOverride = tk.ShouldOverrideRotatedMountHeldItemDraw(drawItem);

        // Vanilla shortswords are projectile-driven and do not provide a useful remote held-item
        // animation after we take over their projectile AI. Draw the actual inventory sword here and
        // move it outward/inward over the normal item animation so the remote attack visibly thrusts.
        if (tk.Mode == TelekinesisPlayer.GripMode.Active &&
            player.itemAnimation > 0 &&
            TelekinesisItemRules.IsStandardShortsword(drawItem)) {
            // Prefer the actual relocated shortsword damage projectile as the visual transform so
            // the visible blade and hitbox cannot drift apart.
            Projectile remoteStab = FindOwnedShortswordProjectile(player.whoAmI);
            if (remoteStab != null) {
                worldPosition = remoteStab.Center;
                rotation = remoteStab.rotation;
                aim = (rotation - MathHelper.PiOver4).ToRotationVector2();
            }
            else {
                int animationMax = Math.Max(1, player.itemAnimationMax);
                float progress = 1f - player.itemAnimation / (float)animationMax;
                float thrust = MathF.Sin(MathHelper.Pi * MathHelper.Clamp(progress, 0f, 1f));

                float itemLength = MathF.Sqrt(drawItem.width * drawItem.width + drawItem.height * drawItem.height) * scale;
                float thrustDistance = Math.Clamp(itemLength * 0.55f, 14f, 34f);
                worldPosition += aim * (6f + thrust * thrustDistance);
            }
        }
        else if (rotatedMountHeldItemOverride) {
            // Minecarts rotate the entire player draw cache on sloped track. A remotely held item
            // must not inherit that player rotation: its damage geometry already uses the unrotated
            // remote grip + itemRotation. Draw it ourselves at the grip and opt this DrawData out
            // of the later player-rotation transform.
            worldPosition = tk.GripPosition;
            rotation = player.itemRotation;
        }
        else {
            Vector2 travelAim = tk.GripTarget - tk.GripPosition;
            if (travelAim.LengthSquared() > 1f)
                rotation = travelAim.ToRotation() + MathHelper.PiOver4;
        }

        Vector2 position = worldPosition - Main.screenPosition;
        Vector2 origin = rotatedMountHeldItemOverride
            ? new Vector2(frame.Width * 0.5f - frame.Width * 0.5f * player.direction, frame.Height)
            : new Vector2(frame.Width * 0.5f, frame.Height * 0.5f);

        // A shortsword texture already points along its +rotation axis after the Pi/4 correction.
        // Mirroring it on leftward thrusts rotates the apparent blade by another 90 degrees, which
        // is why left thrusts looked downward and near-vertical thrusts could flip unpredictably.
        SpriteEffects effects = TelekinesisItemRules.IsStandardShortsword(drawItem)
            ? SpriteEffects.None
            : (rotatedMountHeldItemOverride
                ? (player.direction < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None)
                : (aim.X < 0f ? SpriteEffects.FlipHorizontally : SpriteEffects.None));

        DrawData drawData = new DrawData(
            texture,
            position,
            frame,
            Color.White,
            rotation,
            origin,
            scale,
            effects,
            0
        );

        if (rotatedMountHeldItemOverride)
            drawData.ignorePlayerRotation = true;

        drawInfo.DrawDataCache.Add(drawData);
    }
    private static Projectile FindOwnedShortswordProjectile(int owner)
    {
        for (int i = 0; i < Main.maxProjectiles; i++) {
            Projectile projectile = Main.projectile[i];
            if (projectile.active && projectile.owner == owner && projectile.aiStyle == Terraria.ID.ProjAIStyleID.ShortSword)
                return projectile;
        }

        return null;
    }

}
