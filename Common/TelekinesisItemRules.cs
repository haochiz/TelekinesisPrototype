using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace TelekinesisPrototype.Common;

internal static class TelekinesisItemRules
{
    public static bool IsStandardShortsword(Item item)
    {
        return !item.IsAir &&
               item.damage > 0 &&
               item.CountsAsClass(DamageClass.Melee) &&
               item.useStyle == ItemUseStyleID.Rapier &&
               item.shoot > ProjectileID.None;
    }

    public static bool IsSpearLike(Item item)
    {
        if (item.IsAir || item.damage <= 0 || !item.CountsAsClass(DamageClass.Melee) || item.shoot <= ProjectileID.None)
            return false;

        return ContentSamples.ProjectilesByType.TryGetValue(item.shoot, out Projectile sampleProjectile) &&
               sampleProjectile.aiStyle == ProjAIStyleID.Spear;
    }

    public static bool IsBasicMelee(Item item)
    {
        if (item.IsAir || item.damage <= 0 || !item.CountsAsClass(DamageClass.Melee))
            return false;

        if (IsStandardShortsword(item))
            return true;

        // Spears have their own projectile-driven remote handling. Other visible, non-channeling
        // direct-melee swings are allowed even when they also emit a projectile (Ice Blade,
        // Enchanted Sword-style weapons, etc.). Yoyos/boomerangs/flails remain excluded by their
        // noUseGraphic/noMelee/channel behavior rather than being treated as ordinary swings.
        if (IsSpearLike(item) || item.noUseGraphic || item.noMelee || item.channel)
            return false;

        return item.useStyle == ItemUseStyleID.Swing ||
               item.useStyle == ItemUseStyleID.Thrust;
    }

    // Terragrim/Arkhalis are held-projectile weapons: the item itself is invisible and all damage
    // comes from one persistent projectile that vanilla keeps anchored to the player. Their shared
    // aiStyle (HeldProjectile) is also used by guns and several unrelated weapons, so this family is
    // matched by projectile type rather than by aiStyle.
    public static bool IsHeldProjectileSword(Item item)
    {
        if (item.IsAir || item.damage <= 0 || !item.CountsAsClass(DamageClass.Melee))
            return false;

        return item.shoot == ProjectileID.Terragrim || item.shoot == ProjectileID.Arkhalis;
    }

    // Bullet ammo is what separates guns from bows and launchers. A telekinetic grip is a stable
    // firing platform for a gun, while drawing a bow remotely is deliberately out of scope.
    public static bool IsBulletGun(Item item)
    {
        return !item.IsAir &&
               item.damage > 0 &&
               item.CountsAsClass(DamageClass.Ranged) &&
               item.useAmmo == AmmoID.Bullet &&
               item.shoot > ProjectileID.None;
    }

    public static bool IsTool(Item item) => item.pick > 0 || item.axe > 0 || item.hammer > 0;

    public static bool IsPlaceable(Item item) => item.createTile >= 0 || item.createWall >= 0;

    public static bool IsRemoteEligible(Item item) =>
        IsBasicMelee(item) || IsSpearLike(item) || IsHeldProjectileSword(item) ||
        IsBulletGun(item) || IsTool(item) || IsPlaceable(item);

    public static bool HasToolTileTarget(Item item)
    {
        if (!IsTool(item))
            return false;

        int x = Player.tileTargetX;
        int y = Player.tileTargetY;
        if (!WorldGen.InWorld(x, y, 1))
            return false;

        Tile tile = Framing.GetTileSafely(x, y);

        if (tile.HasTile) {
            bool axeTarget = item.axe > 0 &&
                             tile.TileType < Main.tileAxe.Length &&
                             Main.tileAxe[tile.TileType];

            bool pickTarget = item.pick > 0 &&
                              (tile.TileType >= Main.tileAxe.Length || !Main.tileAxe[tile.TileType]);

            bool hammerTarget = item.hammer > 0;

            if (axeTarget || pickTarget || hammerTarget)
                return true;
        }

        return item.hammer > 0 && tile.WallType > WallID.None;
    }

    public static bool UsesTileTargetForCurrentUse(Item item)
    {
        return IsPlaceable(item) || HasToolTileTarget(item);
    }

    public static Vector2 GetActionTargetWorld(Item item)
    {
        // Weapons and tools always use the cursor as their visible/melee pivot. Tool effects are
        // authorized separately against Player.tileTargetX/Y and do not reposition the melee hitbox.
        if (IsBasicMelee(item) || IsSpearLike(item) || IsHeldProjectileSword(item) ||
            IsBulletGun(item) || IsTool(item))
            return Main.MouseWorld;

        if (IsPlaceable(item)) {
            return new Vector2(
                (Player.tileTargetX + 0.5f) * 16f,
                (Player.tileTargetY + 0.5f) * 16f
            );
        }

        return Main.MouseWorld;
    }
}
