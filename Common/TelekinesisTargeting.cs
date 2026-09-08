using Microsoft.Xna.Framework;
using Terraria;

namespace TelekinesisPrototype.Common;

internal static class TelekinesisTargeting
{
    // A held-projectile blade only reaches a few tiles, so its auto-aim is restricted to enemies
    // actually near the grip instead of anything visible on screen.
    public const float HeldProjectileSwordRange = 25 * 16f;

    // Shared remote auto-aim target selection. Candidates are limited to enemies currently visible
    // on screen so the grip can never silently engage something the player cannot see, and the
    // nearest such enemy to the grip wins.
    public static NPC FindNearestEnemy(Vector2 origin, float maxRange = -1f, bool requireLineOfSight = true)
    {
        NPC bestTarget = null;
        float bestDistanceSquared = maxRange > 0f ? maxRange * maxRange : float.MaxValue;

        Rectangle visibleWorld = new Rectangle(
            (int)Main.screenPosition.X,
            (int)Main.screenPosition.Y,
            Main.screenWidth,
            Main.screenHeight
        );

        Vector2 lineOfSightOrigin = origin - new Vector2(2f, 2f);
        for (int i = 0; i < Main.maxNPCs; i++) {
            NPC npc = Main.npc[i];
            if (!npc.active || npc.friendly || npc.life <= 0 || npc.dontTakeDamage ||
                !visibleWorld.Intersects(npc.Hitbox))
                continue;

            float distanceSquared = Vector2.DistanceSquared(origin, npc.Center);
            if (distanceSquared >= bestDistanceSquared)
                continue;

            // Line of sight is optional because some remote weapons legitimately reach enemies the
            // grip cannot see in a straight line (falling stars pass through terrain).
            if (requireLineOfSight &&
                !Collision.CanHit(lineOfSightOrigin, 4, 4, npc.position, npc.width, npc.height))
                continue;

            bestDistanceSquared = distanceSquared;
            bestTarget = npc;
        }

        return bestTarget;
    }
}
