using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.GameInput;
using Terraria.ModLoader;

namespace TelekinesisPrototype.Common;

public sealed class TelekinesisPlayer : ModPlayer
{
    public enum GripMode
    {
        Idle,
        Deploying,
        Active,
        Returning
    }

    private const float GripSpeed = 48f;
    private const float ArrivalDistance = 10f;
    private const int ReturnDelayTicks = 36;
    private const int ArtificialTileRange = 200;

    public Vector2 GripPosition { get; private set; }
    public Vector2 GripTarget { get; private set; }
    public GripMode Mode { get; private set; }
    public int RemoteItemType { get; private set; }
    public bool RemoteControlEnabled { get; private set; }

    public bool IsRemoteItemBeingUsed =>
        RemoteControlEnabled &&
        Mode == GripMode.Active &&
        RemoteItemType == Player.HeldItem.type &&
        TelekinesisItemRules.IsRemoteEligible(Player.HeldItem);

    private bool _gripInitialized;
    private bool _lastRawUse;
    private bool _pendingBufferedUse;
    private Point _pendingBufferedTargetKey;
    private bool _hasPendingBufferedTargetKey;
    private int _idleTicks;

    private bool _restoreControlUse;
    private bool _savedControlUse;

    private bool _gripReachable = true;
    private readonly TelekineticReachability _reachability = new();
    private readonly List<Vector2> _gripMovementPath = new();
    private int _gripPathWaypointIndex;
    private Point _gripPathTargetKey;
    private bool _hasGripPathTargetKey;
    private int _gripReachabilityVersion = -1;
    private bool _gripPathAllowsFinalBlockedSegment;

    // Tool tile operation is deliberately separate from the visible/melee pivot. The tool always
    // swings at the cursor; this point only answers whether vanilla mining/axe/hammer range can
    // legally reach the selected tile from connected open space.
    private Vector2 _tileOperationGrip;
    private bool _hasTileOperationGrip;

    private bool _restoreTileRange;
    private int _savedTileRangeX;
    private int _savedTileRangeY;

    private bool _restoreChestTileRange;
    private int _savedChestTileRangeX;
    private int _savedChestTileRangeY;

    // Vanilla's direct-melee pipeline performs one final tile-visibility check from the physical
    // player even after a custom melee collision hook accepts the target. Remote broadswords
    // temporarily suppress that one check only after our grip-based collision/LOS has succeeded.
    private readonly Dictionary<int, bool> _remoteBroadswordTileCollisionOverrides = new();

    public override void Initialize()
    {
        // Player.Initialize/ModPlayer.Initialize can run while Terraria is constructing temporary
        // Player instances whose mount-related state is not ready yet. Do not access MountedCenter
        // here; initialize the grip lazily on the first real item-check tick instead.
        GripPosition = Vector2.Zero;
        GripTarget = Vector2.Zero;
        Mode = GripMode.Idle;
        RemoteItemType = ItemID.None;
        RemoteControlEnabled = true;
        _gripInitialized = false;
        InvalidateGripPathCache();
    }

    public override void ProcessTriggers(TriggersSet triggersSet)
    {
        if (Player.whoAmI != Main.myPlayer || Main.netMode != NetmodeID.SinglePlayer)
            return;

        if (TelekinesisPrototypeMod.ToggleRemoteControlKeybind?.JustPressed != true)
            return;

        RemoteControlEnabled = !RemoteControlEnabled;

        if (!RemoteControlEnabled && _gripInitialized)
            ResetGripImmediately();

        Main.NewText(RemoteControlEnabled ? "Telekinetic remote control: ON" : "Telekinetic remote control: OFF");
    }

    public override bool PreItemCheck()
    {
        // Remote held-projectile aiming temporarily moves the cursor the vanilla projectile AI
        // reads. Player item use must never observe that spoofed cursor.
        TelekinesisGlobalProjectile.RestoreSpoofedAimCursor();

        if (Player.whoAmI != Main.myPlayer || Main.netMode != NetmodeID.SinglePlayer)
            return true;

        EnsureGripInitialized();

        Item heldItem = Player.HeldItem;
        bool rawUse = Player.controlUseItem;

        if (!RemoteControlEnabled) {
            if (Mode != GripMode.Idle)
                ResetGripImmediately();

            // Keep edge detection synchronized so enabling remote mode while the button is already
            // held does not manufacture a new click.
            _lastRawUse = rawUse;
            return true;
        }

        bool justPressed = rawUse && !_lastRawUse;
        _lastRawUse = rawUse;

        if (Mode != GripMode.Idle && RemoteItemType != ItemID.None && heldItem.type != RemoteItemType) {
            ResetGripImmediately();
        }

        bool eligible = TelekinesisItemRules.IsRemoteEligible(heldItem);

        if (!eligible) {
            UpdateReturnMotion();
            return true;
        }

        if (justPressed) {
            if (Mode == GripMode.Idle || Mode == GripMode.Returning) {
                BeginDeployment(heldItem);
                _pendingBufferedUse = true;
                _pendingBufferedTargetKey = GetInteractionTargetKey(heldItem);
                _hasPendingBufferedTargetKey = true;
            }
        }

        if (Mode == GripMode.Deploying || Mode == GripMode.Active) {
            UpdateRemoteGripMotion(heldItem);

            if (Mode == GripMode.Deploying && _gripReachable &&
                Vector2.DistanceSquared(GripPosition, GripTarget) <= ArrivalDistance * ArrivalDistance)
                Mode = GripMode.Active;
        }
        else if (Mode == GripMode.Returning) {
            UpdateReturnMotion();
        }

        if (_pendingBufferedUse && _hasPendingBufferedTargetKey &&
            GetInteractionTargetKey(heldItem) != _pendingBufferedTargetKey) {
            ClearPendingBufferedUse();
        }

        if (_pendingBufferedUse && !rawUse && !_gripReachable)
            ClearPendingBufferedUse();

        bool interactionValid = Mode == GripMode.Active && IsCurrentInteractionValid(heldItem);

        _savedControlUse = rawUse;
        _restoreControlUse = true;

        if (!interactionValid) {
            Player.controlUseItem = false;

            if (Mode == GripMode.Active && !rawUse)
                ClearPendingBufferedUse();
        }
        else if (_pendingBufferedUse) {
            Player.controlUseItem = true;
            ClearPendingBufferedUse();
        }
        else {
            Player.controlUseItem = rawUse;
        }

        if (interactionValid && TelekinesisItemRules.UsesTileTargetForCurrentUse(heldItem)) {
            _savedTileRangeX = Player.tileRangeX;
            _savedTileRangeY = Player.tileRangeY;
            _restoreTileRange = true;

            Player.tileRangeX = ArtificialTileRange;
            Player.tileRangeY = ArtificialTileRange;
        }

        if (Mode == GripMode.Active) {
            if (rawUse || Player.itemAnimation > 0 || _pendingBufferedUse) {
                _idleTicks = 0;
            }
            else {
                _idleTicks++;
                if (_idleTicks >= ReturnDelayTicks)
                    Mode = GripMode.Returning;
            }
        }

        return true;
    }

    public override void ModifyHitNPCWithItem(Item item, NPC target, ref NPC.HitModifiers modifiers)
    {
        RestoreRemoteBroadswordTileCollisionOverride(target);

        if (Player.whoAmI != Main.myPlayer || !ShouldUseRemoteMeleeHitbox(item))
            return;

        // Vanilla melee knockback is normally derived from the physical player. A remote weapon
        // should instead knock the target away from the telekinetic grip.
        modifiers.HitDirectionOverride = target.Center.X >= GripPosition.X ? 1 : -1;
    }

    public override void PostItemCheck()
    {
        RestoreAllRemoteBroadswordTileCollisionOverrides();

        if (_restoreTileRange) {
            Player.tileRangeX = _savedTileRangeX;
            Player.tileRangeY = _savedTileRangeY;
            _restoreTileRange = false;
        }

        if (_restoreControlUse) {
            Player.controlUseItem = _savedControlUse;
            _restoreControlUse = false;
        }
    }

    // Chest range. Vanilla gates both opening a chest and keeping it open on Player.tileRangeX/Y,
    // so telekinetic chest reach is expressed the same way tool reach already is: the chest counts
    // as in range when some reachable grip position exists within normal tile range of it.
    //
    // Player.Update runs PostUpdateRunSpeeds, then LookForTileInteractions (which opens a chest and
    // re-checks the open one), then PreUpdateMovement, and only after that item use. Opening the
    // inflated range here and closing it in PreUpdateMovement therefore brackets exactly the
    // vanilla tile interaction pass and leaves item use reading the real range.
    public override void PostUpdateRunSpeeds()
    {
        UpdateTelekineticChestRange();
    }

    public override void PreUpdateMovement()
    {
        if (_restoreChestTileRange) {
            Player.tileRangeX = _savedChestTileRangeX;
            Player.tileRangeY = _savedChestTileRangeY;
            _restoreChestTileRange = false;
        }
    }

    private void UpdateTelekineticChestRange()
    {
        if (Player.whoAmI != Main.myPlayer || Main.netMode != NetmodeID.SinglePlayer)
            return;

        if (!RemoteControlEnabled || !IsTelekineticChestTargetAuthorized())
            return;

        _savedChestTileRangeX = Player.tileRangeX;
        _savedChestTileRangeY = Player.tileRangeY;
        _restoreChestTileRange = true;

        Player.tileRangeX = ArtificialTileRange;
        Player.tileRangeY = ArtificialTileRange;
    }

    private bool IsTelekineticChestTargetAuthorized()
    {
        _reachability.EnsureCurrent(Player);

        // An already-open world chest has to stay authorized from its own tile. Vanilla re-checks
        // range every tick and closes the chest the moment it fails, so authorizing only the
        // hovered tile would close the chest as soon as the cursor moved off it. Bank containers
        // (Player.chest < -1) are item-based and are left entirely to vanilla.
        if (Player.chest >= 0 && IsTileTelekineticallyInRange(Player.chestX, Player.chestY))
            return true;

        return IsChestTile(Player.tileTargetX, Player.tileTargetY) &&
               IsTileTelekineticallyInRange(Player.tileTargetX, Player.tileTargetY);
    }

    private bool IsTileTelekineticallyInRange(int tileX, int tileY)
    {
        return _reachability.TryFindGripForTileInteraction(
            tileX,
            tileY,
            Player.tileRangeX,
            Player.tileRangeY,
            Main.MouseWorld,
            out _
        );
    }

    private static bool IsChestTile(int tileX, int tileY)
    {
        if (!WorldGen.InWorld(tileX, tileY, 1))
            return false;

        Tile tile = Framing.GetTileSafely(tileX, tileY);
        if (!tile.HasTile)
            return false;

        return TileID.Sets.BasicChest[tile.TileType] || TileID.Sets.BasicDresser[tile.TileType];
    }

    public bool ShouldRelocateHeldItem(Item item)
    {
        return RemoteControlEnabled &&
               Mode == GripMode.Active &&
               item.type == RemoteItemType &&
               TelekinesisItemRules.IsRemoteEligible(item) &&
               Player.itemAnimation > 0;
    }

    public bool ShouldUseRemoteMeleeHitbox(Item item)
    {
        return ShouldRelocateHeldItem(item) &&
               item.damage > 0 &&
               !item.noMelee &&
               !TelekinesisItemRules.IsStandardShortsword(item) &&
               (TelekinesisItemRules.IsBasicMelee(item) || TelekinesisItemRules.IsTool(item));
    }

    // The remote hitbox and the visible swing stay exactly at the cursor, even when the cursor is
    // inside the tile being mined. Wall blocking cannot be probed from inside solid terrain, so it
    // is anchored at the open point the tile operation was already authorized from.
    public Vector2 MeleeObstructionOrigin =>
        _hasTileOperationGrip && Collision.IsWorldPointSolid(GripPosition, true)
            ? _tileOperationGrip
            : GripPosition;

    public void TemporarilyBypassPhysicalBroadswordTileCheck(NPC target)
    {
        if (!_remoteBroadswordTileCollisionOverrides.ContainsKey(target.whoAmI))
            _remoteBroadswordTileCollisionOverrides[target.whoAmI] = target.noTileCollide;

        target.noTileCollide = true;
    }

    private void RestoreRemoteBroadswordTileCollisionOverride(NPC target)
    {
        if (!_remoteBroadswordTileCollisionOverrides.Remove(target.whoAmI, out bool originalNoTileCollide))
            return;

        target.noTileCollide = originalNoTileCollide;
    }

    private void RestoreAllRemoteBroadswordTileCollisionOverrides()
    {
        foreach (KeyValuePair<int, bool> entry in _remoteBroadswordTileCollisionOverrides) {
            if (entry.Key >= 0 && entry.Key < Main.maxNPCs)
                Main.npc[entry.Key].noTileCollide = entry.Value;
        }

        _remoteBroadswordTileCollisionOverrides.Clear();
    }

    public bool ShouldDrawTravellingItem(out int itemType)
    {
        itemType = RemoteItemType;

        if (!RemoteControlEnabled || RemoteItemType == ItemID.None || Mode == GripMode.Idle)
            return false;

        if (Mode == GripMode.Active && Player.itemAnimation > 0 && Player.HeldItem.type == RemoteItemType)
            return TelekinesisItemRules.IsStandardShortsword(Player.HeldItem) ||
                   ShouldOverrideRotatedMountHeldItemDraw(Player.HeldItem);

        return true;
    }

    public bool ShouldOverrideRotatedMountHeldItemDraw(Item item)
    {
        return IsRemoteItemBeingUsed &&
               Player.itemAnimation > 0 &&
               Player.mount.Active &&
               Math.Abs(Player.fullRotation) > 0.001f &&
               !TelekinesisItemRules.IsStandardShortsword(item) &&
               (TelekinesisItemRules.IsBasicMelee(item) || TelekinesisItemRules.IsTool(item));
    }

    public override void HideDrawLayers(PlayerDrawSet drawInfo)
    {
        if (drawInfo.drawPlayer.whoAmI != Main.myPlayer)
            return;

        if (ShouldOverrideRotatedMountHeldItemDraw(Player.HeldItem))
            PlayerDrawLayers.HeldItem.Hide();
    }

    private void EnsureGripInitialized()
    {
        if (_gripInitialized)
            return;

        GripPosition = Player.MountedCenter;
        GripTarget = GripPosition;
        _gripInitialized = true;
    }

    private void BeginDeployment(Item item)
    {
        RemoteItemType = item.type;
        GripPosition = Player.MountedCenter;
        GripTarget = CalculateDesiredGrip(item);
        Mode = GripMode.Deploying;
        _idleTicks = 0;
        InvalidateGripPathCache();
    }

    private void ResetGripImmediately()
    {
        GripPosition = Player.MountedCenter;
        GripTarget = Player.MountedCenter;
        Mode = GripMode.Idle;
        RemoteItemType = ItemID.None;
        ClearPendingBufferedUse();
        _idleTicks = 0;
        InvalidateGripPathCache();
    }

    private void UpdateReturnMotion()
    {
        if (Mode != GripMode.Returning)
            return;

        GripTarget = Player.MountedCenter;
        GripPosition = MoveTowards(GripPosition, GripTarget, GripSpeed);

        if (Vector2.DistanceSquared(GripPosition, GripTarget) <= ArrivalDistance * ArrivalDistance)
            ResetGripImmediately();
    }

    private void UpdateRemoteGripMotion(Item item)
    {
        _reachability.EnsureCurrent(Player);

        Vector2 rawDesired = CalculateDesiredGrip(item);
        bool usesTileTarget = TelekinesisItemRules.UsesTileTargetForCurrentUse(item);
        Point targetKey = usesTileTarget
            ? new Point(Player.tileTargetX, Player.tileTargetY)
            : WorldToTile(rawDesired);

        bool targetReachable = TryResolveReachableGrip(
            item,
            rawDesired,
            out Vector2 resolvedGrip,
            out Vector2 pathAnchor,
            out bool allowFinalBlockedSegment
        );

        bool regionChanged = _gripReachabilityVersion != _reachability.Version;
        bool targetChanged = !_hasGripPathTargetKey || targetKey != _gripPathTargetKey;
        bool pathBlocked = _gripReachable && IsNextGripPathSegmentBlocked();
        bool alreadyAtResolvedTarget = Vector2.DistanceSquared(GripPosition, resolvedGrip) <= ArrivalDistance * ArrivalDistance;

        if (!targetReachable) {
            _gripReachable = false;
            _gripMovementPath.Clear();
            _gripPathWaypointIndex = 0;
            GripTarget = Player.MountedCenter;
            GripPosition = MoveTowards(GripPosition, GripTarget, GripSpeed);
            return;
        }

        // A periodic reachability rebuild does not need to restart a weapon/tool that is already at
        // the same valid target. The new connectivity result above is enough to validate it.
        bool regionNeedsPathRebuild = regionChanged && !alreadyAtResolvedTarget;

        if (targetChanged || regionNeedsPathRebuild || pathBlocked || !_gripReachable || _gripMovementPath.Count == 0) {
            _gripPathTargetKey = targetKey;
            _hasGripPathTargetKey = true;
            RebuildGripMovementPath(resolvedGrip, pathAnchor, allowFinalBlockedSegment);
        }
        else {
            _gripReachabilityVersion = _reachability.Version;

            // Weapons and tools use the exact cursor as their pivot. Tool tile authorization remains
            // separate, so sub-tile cursor movement updates only the final visible/melee endpoint.
            if (!usesTileTarget || TelekinesisItemRules.IsTool(item))
                TryUpdateGripPathEndpoint(resolvedGrip, targetKey, allowFinalBlockedSegment);
        }

        if (!_gripReachable) {
            GripTarget = Player.MountedCenter;
            GripPosition = MoveTowards(GripPosition, GripTarget, GripSpeed);
            return;
        }

        AdvanceGripAlongCachedPath(GripSpeed);
    }

    private bool TryResolveReachableGrip(
        Item item,
        Vector2 rawDesired,
        out Vector2 resolvedGrip,
        out Vector2 pathAnchor,
        out bool allowFinalBlockedSegment)
    {
        _hasTileOperationGrip = false;
        allowFinalBlockedSegment = false;

        if (TelekinesisItemRules.UsesTileTargetForCurrentUse(item)) {
            GetTileInteractionRange(item, out int rangeX, out int rangeY);

            if (!_reachability.TryFindGripForTileInteraction(
                    Player.tileTargetX,
                    Player.tileTargetY,
                    rangeX,
                    rangeY,
                    rawDesired,
                    out Vector2 operationGrip)) {
                resolvedGrip = Vector2.Zero;
                pathAnchor = Vector2.Zero;
                return false;
            }

            _tileOperationGrip = operationGrip;
            _hasTileOperationGrip = true;

            if (TelekinesisItemRules.IsTool(item)) {
                // Mining/chopping/hammering is authorized from the reachable operationGrip, but the
                // actual tool is still held and swung exactly at the cursor. The short final segment
                // may therefore enter the solid tile the cursor is pointing at.
                resolvedGrip = rawDesired;
                pathAnchor = operationGrip;
                allowFinalBlockedSegment = true;
                return true;
            }

            // Placement has no melee collision to preserve, so use the reachable operation point as
            // the actual remote grip as before.
            resolvedGrip = operationGrip;
            pathAnchor = operationGrip;
            return true;
        }

        if (_reachability.IsReachableOpenPoint(rawDesired)) {
            resolvedGrip = rawDesired;
            pathAnchor = rawDesired;
            return true;
        }

        resolvedGrip = Vector2.Zero;
        pathAnchor = Vector2.Zero;
        return false;
    }

    private void RebuildGripMovementPath(Vector2 resolvedGrip, Vector2 pathAnchor, bool allowFinalBlockedSegment)
    {
        Vector2 playerOrigin = Player.MountedCenter;
        GripTarget = resolvedGrip;

        List<Vector2> movementPath;
        if (Collision.CanHitLine(GripPosition, 1, 1, pathAnchor, 1, 1)) {
            movementPath = new List<Vector2> { GripPosition, pathAnchor };
        }
        else if (!_reachability.TryBuildPath(GripPosition, pathAnchor, out movementPath) || movementPath.Count == 0) {
            // A world edit can leave the old grip outside the newly rebuilt connected region. The
            // target is still reachable from the player, so redeploy from the player's region.
            GripPosition = playerOrigin;
            Mode = GripMode.Deploying;

            if (!_reachability.TryBuildPath(playerOrigin, pathAnchor, out movementPath) || movementPath.Count == 0) {
                _gripReachable = false;
                _gripMovementPath.Clear();
                _gripPathWaypointIndex = 0;
                return;
            }
        }

        if (Vector2.DistanceSquared(movementPath[^1], resolvedGrip) > 1f)
            movementPath.Add(resolvedGrip);
        else
            movementPath[^1] = resolvedGrip;

        _gripMovementPath.Clear();
        _gripMovementPath.AddRange(movementPath);
        _gripPathWaypointIndex = _gripMovementPath.Count > 1 ? 1 : 0;
        _gripReachable = true;
        _gripReachabilityVersion = _reachability.Version;
        _gripPathAllowsFinalBlockedSegment = allowFinalBlockedSegment;
    }

    private bool IsNextGripPathSegmentBlocked()
    {
        if (_gripMovementPath.Count == 0 || _gripPathWaypointIndex <= 0 ||
            _gripPathWaypointIndex >= _gripMovementPath.Count)
            return false;

        Vector2 next = _gripMovementPath[_gripPathWaypointIndex];
        bool isFinalSegment = _gripPathWaypointIndex == _gripMovementPath.Count - 1;
        if (isFinalSegment && _gripPathAllowsFinalBlockedSegment)
            return false;

        if (Collision.IsWorldPointSolid(next, true))
            return true;

        return !Collision.CanHitLine(GripPosition, 1, 1, next, 1, 1);
    }

    private void TryUpdateGripPathEndpoint(Vector2 rawDesired, Point targetKey, bool allowFinalBlockedSegment)
    {
        if (_gripMovementPath.Count == 0)
            return;

        if (!allowFinalBlockedSegment &&
            (WorldToTile(rawDesired) != targetKey || Collision.IsWorldPointSolid(rawDesired, true)))
            return;

        Vector2 segmentStart = _gripMovementPath.Count >= 2
            ? _gripMovementPath[^2]
            : GripPosition;

        if (!allowFinalBlockedSegment && !Collision.CanHitLine(segmentStart, 1, 1, rawDesired, 1, 1))
            return;

        _gripMovementPath[^1] = rawDesired;
        GripTarget = rawDesired;
        _gripPathAllowsFinalBlockedSegment = allowFinalBlockedSegment;
    }

    private void AdvanceGripAlongCachedPath(float maxDistance)
    {
        if (_gripMovementPath.Count == 0)
            return;

        float remaining = maxDistance;

        while (_gripPathWaypointIndex > 0 &&
               _gripPathWaypointIndex < _gripMovementPath.Count &&
               remaining > 0f) {
            Vector2 waypoint = _gripMovementPath[_gripPathWaypointIndex];
            Vector2 delta = waypoint - GripPosition;
            float distance = delta.Length();

            if (distance <= 0.001f) {
                GripPosition = waypoint;
                _gripPathWaypointIndex++;
                continue;
            }

            if (distance <= remaining) {
                GripPosition = waypoint;
                remaining -= distance;
                _gripPathWaypointIndex++;
            }
            else {
                GripPosition += delta / distance * remaining;
                remaining = 0f;
            }
        }

        if (_gripPathWaypointIndex >= _gripMovementPath.Count)
            GripPosition = GripTarget;
    }

    private void InvalidateGripPathCache()
    {
        _gripMovementPath.Clear();
        _gripPathWaypointIndex = 0;
        _hasGripPathTargetKey = false;
        _gripReachabilityVersion = -1;
        _gripPathAllowsFinalBlockedSegment = false;
        _hasTileOperationGrip = false;
        _gripReachable = true;
    }

    private Point GetInteractionTargetKey(Item item)
    {
        if (TelekinesisItemRules.UsesTileTargetForCurrentUse(item))
            return new Point(Player.tileTargetX, Player.tileTargetY);

        return WorldToTile(Main.MouseWorld);
    }

    private void ClearPendingBufferedUse()
    {
        _pendingBufferedUse = false;
        _hasPendingBufferedTargetKey = false;
    }

    private static Point WorldToTile(Vector2 world)
    {
        return new Point((int)(world.X / 16f), (int)(world.Y / 16f));
    }

    private Vector2 CalculateDesiredGrip(Item item)
    {
        Vector2 playerOrigin = Player.MountedCenter;
        Vector2 actionTarget = TelekinesisItemRules.GetActionTargetWorld(item);

        // Remote melee now mirrors vanilla spatial behavior: the cursor chooses the hand/pivot,
        // not an enemy/contact point. The weapon or tool only hits an NPC if its normal swing/thrust
        // hitbox actually reaches that NPC from this remote pivot. Tools still use the separately
        // tracked vanilla tile target for mining/chopping/hammering.
        if (TelekinesisItemRules.IsBasicMelee(item) || TelekinesisItemRules.IsTool(item))
            return actionTarget;

        Vector2 towardTarget = NormalizeOr(actionTarget - playerOrigin, new Vector2(Player.direction, 0f));

        if (TelekinesisItemRules.IsPlaceable(item)) {
            // Placement keeps its existing behavior: leave the grip just outside the tile being
            // placed so a newly placed block does not trap the grip inside itself.
            return actionTarget - towardTarget * 22f;
        }

        return actionTarget;
    }

    private bool IsCurrentInteractionValid(Item item)
    {
        if (!_gripReachable)
            return false;

        if (!TelekinesisItemRules.UsesTileTargetForCurrentUse(item))
            return true;

        if (!_hasTileOperationGrip)
            return false;

        int targetX = Player.tileTargetX;
        int targetY = Player.tileTargetY;
        int operationGripTileX = (int)(_tileOperationGrip.X / 16f);
        int operationGripTileY = (int)(_tileOperationGrip.Y / 16f);

        GetTileInteractionRange(item, out int rangeX, out int rangeY);

        return Math.Abs(targetX - operationGripTileX) <= rangeX &&
               Math.Abs(targetY - operationGripTileY) <= rangeY;
    }

    private void GetTileInteractionRange(Item item, out int rangeX, out int rangeY)
    {
        int extraPlacementRange = TelekinesisItemRules.IsPlaceable(item) ? Player.blockRange : 0;
        rangeX = Player.tileRangeX + item.tileBoost + extraPlacementRange;
        rangeY = Player.tileRangeY + item.tileBoost + extraPlacementRange;
    }

    private static Vector2 MoveTowards(Vector2 current, Vector2 target, float maxDistance)
    {
        Vector2 delta = target - current;
        float distance = delta.Length();

        if (distance <= maxDistance || distance <= 0.001f)
            return target;

        return current + delta / distance * maxDistance;
    }

    private static Vector2 NormalizeOr(Vector2 value, Vector2 fallback)
    {
        float length = value.Length();
        if (length <= 0.001f)
            return fallback;

        return value / length;
    }

}
