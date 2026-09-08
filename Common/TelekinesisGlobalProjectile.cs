using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ModLoader;

namespace TelekinesisPrototype.Common;

public sealed class TelekinesisGlobalProjectile : GlobalProjectile
{
    public override bool InstancePerEntity => true;

    private bool _remoteShortsword;
    private bool _remoteSpear;
    private Vector2 _stabDirection;
    private int _knockbackDirection;

    private bool _remoteHeldProjectileSword;

    // The aim spoof below is a single-AI-call global edit, so its saved state is static rather than
    // per-projectile.
    private static bool _aimCursorSpoofed;
    private static int _savedMouseX;
    private static int _savedMouseY;
    private const float HeldProjectileSwordAimDistance = 100f;

    private bool _remoteSteerableExplosive;
    private bool _remoteExplosiveSteeringEnded;
    private const float RemoteExplosiveSpeed = 10f;
    private const float RemoteExplosiveAcceleration = 0.85f;


    public override void OnSpawn(Projectile projectile, IEntitySource source)
    {
        if (Main.netMode != NetmodeID.SinglePlayer || projectile.owner != Main.myPlayer)
            return;

        // Steer any vanilla/modded projectile marked explosive when it was directly thrown/used
        // as a consumable item. This covers Bomb/Grenade/Dynamite variants, Scarab Bomb, liquid
        // and dirt bombs, Bomb Fish, Beenades, etc., while excluding rockets/mines produced as
        // ammunition by a non-consumable launcher.
        if (projectile.type < 0 || projectile.type >= ProjectileID.Sets.Explosive.Length ||
            !ProjectileID.Sets.Explosive[projectile.type] ||
            source is not IEntitySource_WithStatsFromItem itemSource ||
            !itemSource.Item.consumable ||
            itemSource.Item.shoot != projectile.type)
            return;

        Player player = Main.player[projectile.owner];
        _remoteSteerableExplosive = player.GetModPlayer<TelekinesisPlayer>().RemoteControlEnabled;
    }

    public override void PostAI(Projectile projectile)
    {
        RestoreSpoofedAimCursor();

        if (_remoteHeldProjectileSword && projectile.owner == Main.myPlayer) {
            Player swordOwner = Main.player[projectile.owner];
            TelekinesisPlayer swordTk = swordOwner.GetModPlayer<TelekinesisPlayer>();

            if (swordTk.IsRemoteItemBeingUsed && TelekinesisItemRules.IsHeldProjectileSword(swordOwner.HeldItem)) {
                // Vanilla held-projectile AI keeps the blade anchored to the physical player and
                // spins it there. Translating the finished pose to the grip preserves the vanilla
                // animation, timing and reach while moving the whole weapon to the remote hand.
                projectile.position += swordTk.GripPosition - swordOwner.MountedCenter;

                // Vanilla ownerHitCheck is player-relative. Wall blocking for the remote blade is
                // enforced from the grip in CanHitNPC below instead.
                projectile.ownerHitCheck = false;
            }
        }

        if (_remoteSpear && projectile.owner == Main.myPlayer) {
            Player player = Main.player[projectile.owner];
            TelekinesisPlayer tk = player.GetModPlayer<TelekinesisPlayer>();

            if (tk.IsRemoteItemBeingUsed && TelekinesisItemRules.IsSpearLike(player.HeldItem)) {
                // Vanilla spear AI computes the normal thrust/extension from the physical player.
                // Translate that finished pose to the remote grip so its vanilla timing, reach and
                // weapon-specific spear behavior are preserved.
                projectile.position += tk.GripPosition - player.MountedCenter;

                // Vanilla spear ownerHitCheck is player-relative. Remote spear wall blocking is
                // instead enforced from the telekinetic grip in CanHitNPC below.
                projectile.ownerHitCheck = false;
            }
        }

        if (!_remoteSteerableExplosive || _remoteExplosiveSteeringEnded || projectile.owner != Main.myPlayer)
            return;

        // Steering belongs only to the single uninterrupted left-click hold that threw this
        // explosive. Use the raw physical left-button state rather than Player.controlUseItem,
        // which Terraria may temporarily clear during an item-use cycle even while the button is
        // still held. MouseLeft is a global button state, so the release is latched permanently:
        // without that latch, any later unrelated click (swinging a sword, using another item)
        // would resume steering a bomb that had already returned to vanilla ballistic motion.
        if (!PlayerInput.Triggers.Current.MouseLeft) {
            _remoteExplosiveSteeringEnded = true;
            return;
        }

        Player explosiveOwner = Main.player[projectile.owner];
        TelekinesisPlayer explosiveTk = explosiveOwner.GetModPlayer<TelekinesisPlayer>();

        // Only steering changes. Vanilla explosive AI, collisions, timeLeft, and explosion timing
        // continue to run normally.
        if (!explosiveTk.RemoteControlEnabled)
            return;

        Vector2 toCursor = Main.MouseWorld - projectile.Center;
        if (toCursor.LengthSquared() <= 4f) {
            projectile.velocity = Vector2.Lerp(projectile.velocity, Vector2.Zero, 0.25f);
            return;
        }

        toCursor.Normalize();
        Vector2 desiredVelocity = toCursor * RemoteExplosiveSpeed;
        projectile.velocity = MoveVelocityTowards(projectile.velocity, desiredVelocity, RemoteExplosiveAcceleration);
    }

    public override bool PreAI(Projectile projectile)
    {
        // A spoofed aim cursor must never outlive the single AI call it was made for, even if the
        // projectile that set it was killed before its PostAI ran.
        RestoreSpoofedAimCursor();

        if (projectile.owner != Main.myPlayer)
            return true;

        Player player = Main.player[projectile.owner];
        TelekinesisPlayer tk = player.GetModPlayer<TelekinesisPlayer>();

        if (projectile.aiStyle == ProjAIStyleID.HeldProjectile) {
            if (!_remoteHeldProjectileSword) {
                if (!tk.IsRemoteItemBeingUsed || !TelekinesisItemRules.IsHeldProjectileSword(player.HeldItem))
                    return true;

                _remoteHeldProjectileSword = true;
            }

            if (tk.IsRemoteItemBeingUsed && TelekinesisItemRules.IsHeldProjectileSword(player.HeldItem))
                SpoofAimCursorForRemoteHeldProjectile(player, tk);

            // Vanilla held-projectile AI runs normally; PostAI only moves the finished pose.
            return true;
        }

        if (projectile.aiStyle == ProjAIStyleID.Spear) {
            if (!_remoteSpear && tk.IsRemoteItemBeingUsed && TelekinesisItemRules.IsSpearLike(player.HeldItem))
                _remoteSpear = true;

            // Let vanilla spear AI run normally. PostAI only translates the completed vanilla
            // spear pose from the player to the remote grip.
            return true;
        }

        if (projectile.aiStyle != ProjAIStyleID.ShortSword)
            return true;

        if (!_remoteShortsword) {
            if (!tk.IsRemoteItemBeingUsed || !TelekinesisItemRules.IsStandardShortsword(player.HeldItem))
                return true;

            _remoteShortsword = true;
            _stabDirection = projectile.velocity;
            if (_stabDirection.LengthSquared() <= 0.001f)
                _stabDirection = Main.MouseWorld - player.MountedCenter;
            if (_stabDirection.LengthSquared() <= 0.001f)
                _stabDirection = new Vector2(player.direction, 0f);
            _stabDirection.Normalize();
            _knockbackDirection = Math.Abs(_stabDirection.X) > 0.05f
                ? Math.Sign(_stabDirection.X)
                : 0;
        }

        if (!tk.IsRemoteItemBeingUsed || player.itemAnimation <= 0) {
            projectile.Kill();
            return false;
        }

        Item item = player.HeldItem;
        int animationMax = Math.Max(1, player.itemAnimationMax);
        float progress = 1f - player.itemAnimation / (float)animationMax;
        float thrust = MathF.Sin(MathHelper.Pi * MathHelper.Clamp(progress, 0f, 1f));

        float itemLength = MathF.Sqrt(item.width * item.width + item.height * item.height);
        float reach = Math.Clamp(itemLength * player.GetAdjustedItemScale(item) * 0.95f, 34f, 72f);

        projectile.Center = tk.GripPosition + _stabDirection * (8f + thrust * reach);
        projectile.velocity = Vector2.Zero;
        projectile.rotation = _stabDirection.ToRotation() + MathHelper.PiOver4;
        projectile.direction = _stabDirection.X >= 0f ? 1 : -1;
        projectile.spriteDirection = projectile.direction;
        projectile.ownerHitCheck = false;
        projectile.tileCollide = false;
        projectile.timeLeft = 2;

        return false;
    }

    public override bool? CanHitNPC(Projectile projectile, NPC target)
    {
        if ((!_remoteSpear && !_remoteHeldProjectileSword) || projectile.owner != Main.myPlayer)
            return null;

        Player player = Main.player[projectile.owner];
        TelekinesisPlayer tk = player.GetModPlayer<TelekinesisPlayer>();
        if (!tk.IsRemoteItemBeingUsed)
            return null;

        Vector2 gripCheckPosition = tk.GripPosition - new Vector2(2f, 2f);
        return Collision.CanHit(
            gripCheckPosition,
            4,
            4,
            target.position,
            target.width,
            target.height
        );
    }

    public override bool ShouldUpdatePosition(Projectile projectile)
    {
        if (_remoteShortsword)
            return false;

        return true;
    }
    public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
    {
        if (!_remoteShortsword && !_remoteHeldProjectileSword)
            return;

        int direction = _knockbackDirection;
        if (direction == 0) {
            Player player = Main.player[projectile.owner];
            TelekinesisPlayer tk = player.GetModPlayer<TelekinesisPlayer>();
            direction = target.Center.X >= tk.GripPosition.X ? 1 : -1;
        }

        modifiers.HitDirectionOverride = direction;
    }

    public override bool PreDraw(Projectile projectile, ref Color lightColor)
    {
        // The prototype draws the real inventory shortsword at the telekinetic grip so that the
        // visible blade follows the same thrust animation as the relocated damage projectile.
        return !_remoteShortsword;
    }

    // Vanilla held-projectile AI re-derives its pose from the cursor, so remote auto-aim cannot be
    // applied by editing velocity: the AI would overwrite it. Instead the cursor that the AI reads
    // is moved for exactly one AI call, so vanilla itself produces the desired aim.
    private static void SpoofAimCursorForRemoteHeldProjectile(Player player, TelekinesisPlayer tk)
    {
        NPC target = TelekinesisTargeting.FindNearestEnemy(tk.GripPosition, TelekinesisTargeting.HeldProjectileSwordRange);
        if (target == null)
            return;

        Vector2 aim = target.Center - tk.GripPosition;
        if (aim.LengthSquared() <= 0.001f)
            return;

        aim.Normalize();

        // The AI aims out from the physical player and PostAI then translates the result to the
        // grip, so the spoofed cursor has to sit along the desired direction from the player.
        Vector2 desiredCursorWorld = player.MountedCenter + aim * HeldProjectileSwordAimDistance;
        Vector2 worldDelta = desiredCursorWorld - Main.MouseWorld;
        Vector2 zoom = Main.GameViewMatrix.Zoom;

        _savedMouseX = Main.mouseX;
        _savedMouseY = Main.mouseY;
        _aimCursorSpoofed = true;

        Main.mouseX += (int)(worldDelta.X * zoom.X);
        Main.mouseY += (int)(worldDelta.Y * zoom.Y);
    }

    public static void RestoreSpoofedAimCursor()
    {
        if (!_aimCursorSpoofed)
            return;

        Main.mouseX = _savedMouseX;
        Main.mouseY = _savedMouseY;
        _aimCursorSpoofed = false;
    }

    private static Vector2 MoveVelocityTowards(Vector2 current, Vector2 target, float maxDelta)
    {
        Vector2 delta = target - current;
        float distance = delta.Length();

        if (distance <= maxDelta || distance <= 0.001f)
            return target;

        return current + delta / distance * maxDelta;
    }

}
