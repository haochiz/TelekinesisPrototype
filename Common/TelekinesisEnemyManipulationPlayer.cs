using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace TelekinesisPrototype.Common;

public sealed class TelekinesisEnemyManipulationPlayer : ModPlayer
{
    private const float BaseAcceleration = 0.50f; // px/tick^2 at 100% knockback resistance.
    private const float MaxManipulationSpeed = 10f;
    private const float ArrivalRadius = 12f;

    private int _targetNpcIndex = -1;
    private bool _chargeAllowedThisTick;
    private bool _lockedOutUntilRelease;

    public bool IsManipulating =>
        TelekinesisPrototypeMod.ManipulateEnemyKeybind?.Current == true &&
        !_lockedOutUntilRelease &&
        TryGetTarget(out _);

    public override void Initialize()
    {
        _targetNpcIndex = -1;
        _chargeAllowedThisTick = false;
        _lockedOutUntilRelease = false;
    }

    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        if (Player.whoAmI != Main.myPlayer || Main.netMode != NetmodeID.SinglePlayer)
            return;

        bool held = TelekinesisPrototypeMod.ManipulateEnemyKeybind?.Current == true;
        if (!held) {
            _lockedOutUntilRelease = false;
            ClearTarget();
            return;
        }

        if (_lockedOutUntilRelease)
            return;

        TelekinesisLevitationPlayer sharedCharge = Player.GetModPlayer<TelekinesisLevitationPlayer>();
        if (!sharedCharge.SharedChargeAvailable) {
            ClearTarget();
            _lockedOutUntilRelease = true;
            return;
        }

        if (!TryGetTarget(out _))
            AcquireTargetUnderCursor();
    }

    public override void UpdateDead()
    {
        _targetNpcIndex = -1;
        _chargeAllowedThisTick = false;
        _lockedOutUntilRelease = false;
    }

    internal bool WantsManipulationThisTick => IsManipulating;

    internal void SetChargeAllowedThisTick(bool allowed)
    {
        _chargeAllowedThisTick = allowed;
    }

    internal void OnSharedChargeExhausted()
    {
        _lockedOutUntilRelease = true;
    }

    internal void ApplyToNpc(NPC npc)
    {
        if (!_chargeAllowedThisTick || npc.whoAmI != _targetNpcIndex || !IsEligibleEnemy(npc))
            return;

        float acceleration = BaseAcceleration * Math.Max(0f, npc.knockBackResist);
        if (acceleration <= 0f)
            return;

        Vector2 toCursor = Main.MouseWorld - npc.Center;
        float distance = toCursor.Length();

        Vector2 desiredVelocity = Vector2.Zero;
        if (distance > ArrivalRadius) {
            float desiredSpeed = Math.Min(MaxManipulationSpeed, distance * 0.20f);
            desiredVelocity = toCursor / distance * desiredSpeed;
        }

        npc.velocity = MoveTowards(npc.velocity, desiredVelocity, acceleration);
    }

    private void AcquireTargetUnderCursor()
    {
        Point cursor = Main.MouseWorld.ToPoint();
        float bestDistanceSquared = float.MaxValue;
        int bestIndex = -1;

        for (int i = 0; i < Main.maxNPCs; i++) {
            NPC npc = Main.npc[i];
            if (!IsEligibleEnemy(npc) || !npc.Hitbox.Contains(cursor))
                continue;

            float distanceSquared = Vector2.DistanceSquared(npc.Center, Main.MouseWorld);
            if (distanceSquared >= bestDistanceSquared)
                continue;

            bestDistanceSquared = distanceSquared;
            bestIndex = i;
        }

        _targetNpcIndex = bestIndex;
    }

    private bool TryGetTarget(out NPC target)
    {
        if (_targetNpcIndex >= 0 && _targetNpcIndex < Main.maxNPCs) {
            NPC npc = Main.npc[_targetNpcIndex];
            if (IsEligibleEnemy(npc)) {
                target = npc;
                return true;
            }
        }

        _targetNpcIndex = -1;
        target = null;
        return false;
    }

    private static bool IsEligibleEnemy(NPC npc)
    {
        return npc.active &&
               !npc.friendly &&
               npc.life > 0 &&
               npc.knockBackResist > 0f;
    }

    private void ClearTarget()
    {
        _targetNpcIndex = -1;
        _chargeAllowedThisTick = false;
    }

    private static Vector2 MoveTowards(Vector2 current, Vector2 target, float maxDelta)
    {
        Vector2 delta = target - current;
        float distance = delta.Length();

        if (distance <= maxDelta || distance <= 0.0001f)
            return target;

        return current + delta / distance * maxDelta;
    }
}
