using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace TelekinesisPrototype.Common;

public sealed class TelekinesisLevitationPlayer : ModPlayer
{
    // Duration is the only progression variable. Finite duration uses one smooth
    // exponential HP curve in GetMaxDurationTicks().
    private const int RechargeTicksPerTick = 1; // 1 second OFF restores 1 second of levitation.
    private const float VerticalAcceleration = 0.80f;
    private const float MaxVerticalSpeed = 6f;

    // Levitation owns horizontal movement while active instead of inheriting vanilla
    // walking/sprint acceleration. 48 tiles/s keeps levitation moderately slower than
    // endgame Soaring Insignia + Celestial Starboard movement while preserving precision utility.
    private const float HorizontalAcceleration = 0.20f; // pixels/tick^2
    private const float MaxHorizontalSpeed = 48f * 16f / 60f; // 48 tiles/s

    public bool LevitationEnabled { get; private set; }
    public bool LevitationInfinite => GetMaxDurationTicks() < 0;
    public float LevitationRemainingSeconds => LevitationInfinite ? float.PositiveInfinity : _remainingTicks / 60f;
    public float LevitationMaxSeconds
    {
        get
        {
            int maxTicks = GetMaxDurationTicks();
            return maxTicks < 0 ? float.PositiveInfinity : maxTicks / 60f;
        }
    }

    public bool SharedChargeAvailable
    {
        get
        {
            EnsureDurationInitialized();
            RefreshDurationForMaxHealthChanges();
            return LevitationInfinite || _remainingTicks > 0;
        }
    }

    private bool _durationInitialized;
    private int _remainingTicks;
    private int _previousMaxTicks;
    private bool _brakingInheritedHorizontalMomentum;
    private bool _brakingInheritedVerticalMomentum;
    private bool _finiteHoldLockedUntilRelease;

    public override void Initialize()
    {
        LevitationEnabled = false;
        _durationInitialized = false;
        _remainingTicks = 0;
        _previousMaxTicks = 0;
        _brakingInheritedHorizontalMomentum = false;
        _brakingInheritedVerticalMomentum = false;
        _finiteHoldLockedUntilRelease = false;
    }

    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        if (Player.whoAmI != Main.myPlayer || Main.netMode != NetmodeID.SinglePlayer)
            return;

        EnsureDurationInitialized();
        RefreshDurationForMaxHealthChanges();

        bool keyHeld = TelekinesisPrototypeMod.ToggleLevitationKeybind?.Current == true;

        if (LevitationInfinite) {
            // Infinite levitation is a toggle: each new keypress flips the state. If the player
            // crosses the 490-HP threshold while already levitating, the current ON state is
            // preserved and releasing Shift does not turn it off.
            _finiteHoldLockedUntilRelease = false;

            if (TelekinesisPrototypeMod.ToggleLevitationKeybind?.JustPressed != true)
                return;

            if (LevitationEnabled) {
                DisableLevitation();
                return;
            }

            EnableLevitation();
            return;
        }

        // Finite levitation is hold-to-use. Releasing the key immediately ends levitation and
        // clears an exhaustion lockout so the shared charge can recharge normally.
        if (!keyHeld) {
            _finiteHoldLockedUntilRelease = false;
            if (LevitationEnabled)
                DisableLevitation();
            return;
        }

        if (_finiteHoldLockedUntilRelease)
            return;

        if (_remainingTicks <= 0) {
            DisableLevitation();
            _finiteHoldLockedUntilRelease = true;
            Main.NewText("Telekinetic charge exhausted. Release sustained telekinesis to recharge.");
            return;
        }

        if (!LevitationEnabled)
            EnableLevitation();
    }

    public override void SetControls()
    {
        if (Player.whoAmI != Main.myPlayer || Main.netMode != NetmodeID.SinglePlayer)
            return;

        // Left Shift is also vanilla Smart Select. While it is assigned to levitation,
        // levitation takes priority so holding/pressing it does not also switch items.
        if (TelekinesisPrototypeMod.ToggleLevitationKeybind?.Current == true)
            Player.controlSmart = false;
    }

    public override void PreUpdateMovement()
    {
        if (Player.whoAmI != Main.myPlayer || Main.netMode != NetmodeID.SinglePlayer)
            return;

        EnsureDurationInitialized();
        RefreshDurationForMaxHealthChanges();

        TelekinesisEnemyManipulationPlayer manipulation = Player.GetModPlayer<TelekinesisEnemyManipulationPlayer>();
        manipulation.SetChargeAllowedThisTick(false);

        int maxTicks = GetMaxDurationTicks();

        if (Player.dead) {
            LevitationEnabled = false;
            manipulation.OnSharedChargeExhausted();
            return;
        }

        bool manipulationRequested = manipulation.WantsManipulationThisTick;
        bool levitationConsumesCharge = LevitationEnabled && !Player.mount.Active;
        bool sharedChargeInUse = levitationConsumesCharge || manipulationRequested;

        // The same finite pool powers both self-levitation and enemy manipulation. If neither
        // sustained effect is using it, recharge at the existing 1:1 rate. A levitation toggle
        // left ON while mounted still neither drains nor recharges, preserving prior behavior.
        if (!sharedChargeInUse) {
            if (!LevitationEnabled && maxTicks >= 0)
                _remainingTicks = Math.Min(maxTicks, _remainingTicks + RechargeTicksPerTick);
            return;
        }

        if (maxTicks >= 0 && _remainingTicks <= 0) {
            DisableLevitation();
            _finiteHoldLockedUntilRelease = true;
            manipulation.OnSharedChargeExhausted();
            return;
        }

        if (levitationConsumesCharge)
            ApplyLevitationMovement();

        // Simultaneous self-levitation + enemy manipulation still drains one tick per tick:
        // both are powered by one shared sustained-use timer rather than independent costs.
        manipulation.SetChargeAllowedThisTick(manipulationRequested);

        if (maxTicks >= 0) {
            _remainingTicks--;
            if (_remainingTicks <= 0) {
                _remainingTicks = 0;
                DisableLevitation();
                _finiteHoldLockedUntilRelease = true;
                manipulation.OnSharedChargeExhausted();
            }
        }
    }
    public override void UpdateDead()
    {
        LevitationEnabled = false;
        _durationInitialized = false;
        _remainingTicks = 0;
        _previousMaxTicks = 0;
        _brakingInheritedHorizontalMomentum = false;
        _brakingInheritedVerticalMomentum = false;
        _finiteHoldLockedUntilRelease = false;
    }

    private void EnableLevitation()
    {
        LevitationEnabled = true;
        _brakingInheritedHorizontalMomentum = Math.Abs(Player.velocity.X) > 0.001f;
        _brakingInheritedVerticalMomentum = Math.Abs(Player.velocity.Y) > 0.001f;
    }

    private void DisableLevitation()
    {
        LevitationEnabled = false;
        _brakingInheritedHorizontalMomentum = false;
        _brakingInheritedVerticalMomentum = false;
    }

    public override void FrameEffects()
    {
        if (!LevitationEnabled || Player.mount.Active)
            return;

        // Levitation owns horizontal movement, so do not let zero vertical velocity make
        // Terraria play its normal running cycle while the player is moving through the air.
        // Keep the legs in the same neutral pose used while suspended in place.
        Player.legFrameCounter = 0d;
        Player.legFrame.Y = 0;
    }

    private void ApplyLevitationMovement()
    {
        ApplyHorizontalLevitationControl();

        bool ascend = Player.controlUp || Player.controlJump;
        bool descend = Player.controlDown && !ascend;

        // When levitation is first enabled, transition from the player's existing jump/fall
        // velocity toward the requested levitation velocity using the same vertical acceleration
        // as ordinary levitation. There is no separate braking/reversal mode.
        if (_brakingInheritedVerticalMomentum) {
            float targetVelocity = ascend ? -MaxVerticalSpeed : descend ? MaxVerticalSpeed : 0f;
            Player.velocity.Y = MoveTowards(Player.velocity.Y, targetVelocity, VerticalAcceleration);
            if (Player.velocity.Y == targetVelocity)
                _brakingInheritedVerticalMomentum = false;
            return;
        }

        if (ascend) {
            if (Player.velocity.Y > -MaxVerticalSpeed)
                Player.velocity.Y = Math.Max(Player.velocity.Y - VerticalAcceleration, -MaxVerticalSpeed);
            return;
        }

        if (descend) {
            if (Player.velocity.Y < MaxVerticalSpeed)
                Player.velocity.Y = Math.Min(Player.velocity.Y + VerticalAcceleration, MaxVerticalSpeed);
            return;
        }

        Player.velocity.Y = 0f;
    }

    private void ApplyHorizontalLevitationControl()
    {
        bool left = Player.controlLeft && !Player.controlRight;
        bool right = Player.controlRight && !Player.controlLeft;

        // Apply the same activation transition horizontally. Once inherited momentum has been
        // absorbed, levitation-created movement keeps the existing precise instant-stop behavior.
        if (_brakingInheritedHorizontalMomentum) {
            float targetVelocity = left ? -MaxHorizontalSpeed : right ? MaxHorizontalSpeed : 0f;
            Player.velocity.X = MoveTowards(Player.velocity.X, targetVelocity, HorizontalAcceleration);
            if (Player.velocity.X == targetVelocity)
                _brakingInheritedHorizontalMomentum = false;
            return;
        }

        if (left) {
            Player.velocity.X = Math.Max(Player.velocity.X - HorizontalAcceleration, -MaxHorizontalSpeed);
            return;
        }

        if (right) {
            Player.velocity.X = Math.Min(Player.velocity.X + HorizontalAcceleration, MaxHorizontalSpeed);
            return;
        }

        Player.velocity.X = 0f;
    }

    private void EnsureDurationInitialized()
    {
        if (_durationInitialized)
            return;

        int maxTicks = GetMaxDurationTicks();
        _previousMaxTicks = maxTicks;
        _remainingTicks = maxTicks < 0 ? 0 : maxTicks;
        _durationInitialized = true;
    }

    private void RefreshDurationForMaxHealthChanges()
    {
        int maxTicks = GetMaxDurationTicks();
        if (maxTicks == _previousMaxTicks)
            return;

        if (maxTicks < 0) {
            // 490 permanent max HP unlocks infinite sustained telekinesis immediately.
            _previousMaxTicks = maxTicks;
            return;
        }

        if (_previousMaxTicks < 0) {
            _remainingTicks = maxTicks;
        }
        else if (maxTicks > _previousMaxTicks) {
            // Eating a Life Crystal/Fruit adds the newly unlocked duration immediately without
            // erasing the amount already spent in the current flight.
            _remainingTicks = Math.Min(maxTicks, _remainingTicks + (maxTicks - _previousMaxTicks));
        }
        else {
            _remainingTicks = Math.Min(_remainingTicks, maxTicks);
        }

        _previousMaxTicks = maxTicks;
    }

    private int GetMaxDurationTicks()
    {
        // statLifeMax is the permanent vanilla maximum (Life Crystals/Fruit) before temporary
        // statLifeMax2 modifiers, which keeps accessories/buffs from changing innate levitation.
        int permanentMaxHealth = Math.Clamp(Player.statLifeMax, 100, 500);

        if (permanentMaxHealth >= 490)
            return -1;

        // One smooth convex formula for the entire finite progression:
        // seconds = 30 ^ ((HP - 100) / 385).
        // This is exactly 1s at 100 HP, rises continuously faster as HP increases, and reaches
        // exactly 30s at 485 HP. 485-489 remains capped at 30s, then 490 HP unlocks infinity.
        int finiteHealth = Math.Min(permanentMaxHealth, 485);
        float seconds = MathF.Pow(30f, (finiteHealth - 100f) / 385f);

        return Math.Max(1, (int)MathF.Round(seconds * 60f));
    }

    private static float MoveTowards(float current, float target, float maxDelta)
    {
        if (Math.Abs(target - current) <= maxDelta)
            return target;

        return current + Math.Sign(target - current) * maxDelta;
    }
}
