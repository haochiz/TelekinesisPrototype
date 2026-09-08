using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace TelekinesisPrototype.Common;

public sealed class TelekinesisGlobalItem : GlobalItem
{
    private const int TelekineticPickupRangePixels = 60 * 16;

    private const float MaxLaunchOffset = 128f;

    public override void GrabRange(Item item, Player player, ref int grabRange)
    {
        if (player.whoAmI == Main.myPlayer && Main.netMode == Terraria.ID.NetmodeID.SinglePlayer)
            grabRange = Math.Max(grabRange, TelekineticPickupRangePixels);
    }

    public override void UseStyle(Item item, Player player, Rectangle heldItemFrame)
    {
        if (player.whoAmI != Main.myPlayer)
            return;

        TelekinesisPlayer tk = player.GetModPlayer<TelekinesisPlayer>();
        if (!tk.ShouldRelocateHeldItem(item))
            return;

        Vector2 offset = tk.GripPosition - player.MountedCenter;
        player.itemLocation += offset;

        // A remote gun's bullets leave the grip toward its auto-target rather than along the
        // player's cursor line, so rotate the visible weapon to match where it actually shoots.
        if (TelekinesisItemRules.IsBulletGun(item)) {
            NPC gunTarget = TelekinesisTargeting.FindNearestEnemy(tk.GripPosition);
            if (gunTarget != null) {
                Vector2 gunAim = gunTarget.Center - tk.GripPosition;
                if (gunAim.LengthSquared() > 0.001f)
                    player.itemRotation = MathF.Atan2(gunAim.Y * player.direction, gunAim.X * player.direction);
            }
        }
    }

    public override void UseItemHitbox(Item item, Player player, ref Rectangle hitbox, ref bool noHitbox)
    {
        if (player.whoAmI != Main.myPlayer)
            return;

        TelekinesisPlayer tk = player.GetModPlayer<TelekinesisPlayer>();
        if (!tk.ShouldUseRemoteMeleeHitbox(item))
            return;

        // Discard the vanilla player-anchored hitbox entirely. Remote melee collision is generated
        // only from the telekinetic hand/pivot and the current vanilla swing rotation.
        Rectangle drawHitbox = Item.GetDrawHitbox(item.type, player);
        Vector2 itemSize = Main.dedServ
            ? new Vector2(Math.Max(1, item.width), Math.Max(1, item.height))
            : new Vector2(Math.Max(1, drawHitbox.Width), Math.Max(1, drawHitbox.Height));

        float scale = player.GetAdjustedItemScale(item);
        float itemLength = Math.Max(8f, itemSize.Length() * scale);

        // Terraria's held-item sprites are diagonally oriented. Vanilla use styles encode that
        // sprite correction in itemRotation, so remove it to recover handle -> tip direction.
        float swingAngle = player.direction >= 0
            ? player.itemRotation - MathHelper.PiOver4
            : player.itemRotation - MathHelper.PiOver4 * 3f;

        Vector2 swingDirection = swingAngle.ToRotationVector2();
        Vector2 handle = tk.GripPosition;
        Vector2 tip = handle + swingDirection * itemLength;

        hitbox = Utils.CornerRectangle(handle, tip);

        // Give the line-shaped handle-to-tip rectangle physical blade/tool-head thickness without
        // letting the original player-centered rectangle influence remote collision.
        int thickness = Math.Clamp((int)(Math.Min(itemSize.X, itemSize.Y) * scale * 0.22f), 2, 10);
        hitbox.Inflate(thickness, thickness);
        noHitbox = false;
    }

    public override bool? CanMeleeAttackCollideWithNPC(Item item, Rectangle meleeAttackHitbox, Player player, NPC target)
    {
        if (player.whoAmI != Main.myPlayer)
            return null;

        TelekinesisPlayer tk = player.GetModPlayer<TelekinesisPlayer>();
        if (!tk.ShouldUseRemoteMeleeHitbox(item))
            return null;

        // Once remote control owns the melee hitbox, the physical player's position must not
        // participate in deciding whether that hitbox can connect. This fixes vanilla/player-
        // relative collision rejection when an NPC is pressed against a wall or closed door.
        if (!meleeAttackHitbox.Intersects(target.Hitbox))
            return false;

        // Preserve vanilla-like wall blocking, but anchor it at the telekinetic hand. A sword can
        // hit an NPC beside a wall from the open side, but cannot attack through solid terrain.
        Vector2 gripCheckPosition = tk.MeleeObstructionOrigin - new Vector2(2f, 2f);
        bool canHitFromGrip = Collision.CanHit(
            gripCheckPosition,
            4,
            4,
            target.position,
            target.width,
            target.height
        );

        if (!canHitFromGrip)
            return false;

        // tModLoader still performs Player.CanHit(target) after this hook, which is anchored to the
        // physical player. Suppress that final vanilla gate for every remote direct-melee item that
        // reached this point; our grip-based LOS above remains the wall-blocking authority.
        // Projectile-based weapons such as standard shortswords never enter this path.
        tk.TemporarilyBypassPhysicalBroadswordTileCheck(target);

        return true;
    }

    public override void ModifyShootStats(Item item, Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
    {
        if (player.whoAmI != Main.myPlayer)
            return;

        bool remoteShortsword = TelekinesisItemRules.IsStandardShortsword(item);
        bool remoteSpear = TelekinesisItemRules.IsSpearLike(item);
        bool remoteHeldProjectileSword = TelekinesisItemRules.IsHeldProjectileSword(item);
        bool remoteGun = TelekinesisItemRules.IsBulletGun(item);
        bool remoteProjectileMelee = TelekinesisItemRules.IsBasicMelee(item) &&
                                     item.shoot > Terraria.ID.ProjectileID.None &&
                                     !remoteShortsword;
        if (!remoteShortsword && !remoteSpear && !remoteProjectileMelee &&
            !remoteHeldProjectileSword && !remoteGun)
            return;

        TelekinesisPlayer tk = player.GetModPlayer<TelekinesisPlayer>();
        if (!tk.IsRemoteItemBeingUsed)
            return;

        if (remoteShortsword || remoteSpear) {
            position = tk.GripPosition;

            // Spears and shortswords keep vanilla mouse-facing direction, but originate from the
            // telekinetic grip instead of the physical player.
            Vector2 aim = Main.MouseWorld - player.MountedCenter;
            if (aim.LengthSquared() > 0.001f) {
                float speed = velocity.Length();
                aim.Normalize();
                velocity = aim * speed;
            }
            return;
        }

        // Held-projectile swords and guns are both launched from the grip and auto-aimed at the
        // nearest visible enemy, falling back to the vanilla cursor direction when there is none.
        if (remoteHeldProjectileSword || remoteGun) {
            position = tk.GripPosition;

            float targetRange = remoteHeldProjectileSword ? TelekinesisTargeting.HeldProjectileSwordRange : -1f;
            NPC autoTarget = TelekinesisTargeting.FindNearestEnemy(tk.GripPosition, targetRange);
            if (autoTarget == null)
                return;

            Vector2 autoAim = autoTarget.Center - tk.GripPosition;
            Vector2 cursorAim = Main.MouseWorld - player.MountedCenter;
            if (autoAim.LengthSquared() <= 0.001f)
                return;

            // Rotating rather than replacing velocity preserves vanilla projectile speed and any
            // intentional spread or multishot angular offset.
            if (cursorAim.LengthSquared() <= 0.001f) {
                autoAim.Normalize();
                velocity = autoAim * velocity.Length();
                return;
            }

            velocity = velocity.RotatedBy(autoAim.ToRotation() - cursorAim.ToRotation());
            return;
        }

        // Projectile-emitting direct-melee weapons are only relocated/auto-aimed when their
        // vanilla projectile is actually a conventional shot launched from near the player in the
        // cursor direction. This deliberately leaves unusual spawn patterns (for example projectiles
        // created far from the player) alone so they can be handled separately if encountered.
        if (!IsCursorAimedPlayerLaunchedProjectile(player, position, velocity)) {
            RetargetCursorPointProjectile(player, tk, ref position, velocity);
            return;
        }

        Vector2 vanillaAim = Main.MouseWorld - player.MountedCenter;
        position = tk.GripPosition;

        NPC target = TelekinesisTargeting.FindNearestEnemy(tk.GripPosition);
        if (target == null || vanillaAim.LengthSquared() <= 0.001f)
            return;

        Vector2 targetAim = target.Center - tk.GripPosition;
        if (targetAim.LengthSquared() <= 0.001f)
            return;

        // Rotate rather than replace velocity so vanilla projectile speed and any intentional
        // spread/multi-shot angular offset are preserved.
        float rotationDelta = targetAim.ToRotation() - vanillaAim.ToRotation();
        velocity = velocity.RotatedBy(rotationDelta);
    }

    private static bool IsCursorAimedPlayerLaunchedProjectile(Player player, Vector2 position, Vector2 velocity)
    {
        const float MinimumAimAlignment = 0.75f;

        if (velocity.LengthSquared() <= 0.001f ||
            Vector2.DistanceSquared(position, player.MountedCenter) > MaxLaunchOffset * MaxLaunchOffset)
            return false;

        Vector2 vanillaAim = Main.MouseWorld - player.MountedCenter;
        if (vanillaAim.LengthSquared() <= 0.001f)
            return false;

        Vector2 shotDirection = Vector2.Normalize(velocity);
        Vector2 cursorDirection = Vector2.Normalize(vanillaAim);
        return Vector2.Dot(shotDirection, cursorDirection) >= MinimumAimAlignment;
    }

    // Starfury-style weapons do not shoot a line out of the player: vanilla spawns the star far
    // away (above the cursor) with a velocity that converges on the cursor point itself. Shifting
    // the whole spawn by cursor -> target keeps vanilla's fall angle, speed and per-star spread and
    // simply lands the shot on the enemy instead of on the cursor.
    private static void RetargetCursorPointProjectile(Player player, TelekinesisPlayer tk, ref Vector2 position, Vector2 velocity)
    {
        if (!IsCursorConvergingRemoteSpawn(player, position, velocity))
            return;

        // Falling stars pass through terrain, so grip line of sight is deliberately not required.
        NPC pointTarget = TelekinesisTargeting.FindNearestEnemy(tk.GripPosition, requireLineOfSight: false);
        if (pointTarget == null)
            return;

        position += pointTarget.Center - Main.MouseWorld;
    }

    private static bool IsCursorConvergingRemoteSpawn(Player player, Vector2 position, Vector2 velocity)
    {
        const float MinimumConvergenceAlignment = 0.85f;

        if (velocity.LengthSquared() <= 0.001f ||
            Vector2.DistanceSquared(position, player.MountedCenter) <= MaxLaunchOffset * MaxLaunchOffset)
            return false;

        Vector2 spawnToCursor = Main.MouseWorld - position;
        if (spawnToCursor.LengthSquared() <= 0.001f)
            return false;

        return Vector2.Dot(Vector2.Normalize(velocity), Vector2.Normalize(spawnToCursor)) >= MinimumConvergenceAlignment;
    }
}
