using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;

namespace TelekinesisPrototype.Common;

internal sealed class TelekineticReachability
{
    private const int PeriodicRefreshTicks = 12;

    private static readonly Point[] Neighbors =
    {
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1)
    };

    private const byte PassabilityUntested = 0;
    private const byte PassabilityPassable = 1;
    private const byte PassabilityBlocked = 2;

    private Rectangle _bounds;
    private bool[] _reachable = Array.Empty<bool>();
    private int[] _parent = Array.Empty<int>();

    // Scratch state reused across rebuilds so a rebuild allocates nothing. _passability caches
    // the solidity probe for each tile within a single rebuild.
    private byte[] _passability = Array.Empty<byte>();
    private readonly Queue<Point> _floodQueue = new();
    private Point _rootTile;
    private bool _initialized;
    private ulong _lastRefreshTick;

    public int Version { get; private set; }

    public void EnsureCurrent(Player player)
    {
        Rectangle desiredBounds = CalculateBounds();
        ulong tick = Main.GameUpdateCount;

        bool boundsChanged = !_initialized || desiredBounds != _bounds;
        bool refreshDue = !_initialized || tick - _lastRefreshTick >= PeriodicRefreshTicks;

        if (boundsChanged || refreshDue)
            Rebuild(player, desiredBounds, tick);
    }

    public bool IsReachableOpenPoint(Vector2 world)
    {
        Point tile = WorldToTile(world);
        if (!Contains(tile) || !IsReachable(tile.X, tile.Y))
            return false;

        return !Collision.IsWorldPointSolid(world, true);
    }

    public bool TryFindGripForTileInteraction(
        int targetTileX,
        int targetTileY,
        int rangeX,
        int rangeY,
        Vector2 preferredWorld,
        out Vector2 gripWorld)
    {
        gripWorld = Vector2.Zero;

        Point preferredTile = WorldToTile(preferredWorld);
        if (Contains(preferredTile) &&
            IsReachable(preferredTile.X, preferredTile.Y) &&
            Math.Abs(preferredTile.X - targetTileX) <= rangeX &&
            Math.Abs(preferredTile.Y - targetTileY) <= rangeY &&
            !Collision.IsWorldPointSolid(preferredWorld, true)) {
            gripWorld = preferredWorld;
            return true;
        }

        int minX = Math.Max(_bounds.Left, targetTileX - rangeX);
        int maxX = Math.Min(_bounds.Right - 1, targetTileX + rangeX);
        int minY = Math.Max(_bounds.Top, targetTileY - rangeY);
        int maxY = Math.Min(_bounds.Bottom - 1, targetTileY + rangeY);

        float bestDistanceSq = float.MaxValue;
        bool found = false;

        for (int y = minY; y <= maxY; y++) {
            for (int x = minX; x <= maxX; x++) {
                if (!IsReachable(x, y))
                    continue;

                Vector2 candidate = TileCenter(x, y);
                float distanceSq = Vector2.DistanceSquared(candidate, preferredWorld);
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                gripWorld = candidate;
                found = true;
            }
        }

        return found;
    }

    public bool TryBuildPath(Vector2 fromWorld, Vector2 toWorld, out List<Vector2> path)
    {
        path = new List<Vector2>();

        Point fromTile = WorldToTile(fromWorld);
        Point toTile = WorldToTile(toWorld);

        if (!Contains(fromTile) || !Contains(toTile) ||
            !IsReachable(fromTile.X, fromTile.Y) || !IsReachable(toTile.X, toTile.Y))
            return false;

        if (fromTile == toTile) {
            path.Add(fromWorld);
            path.Add(toWorld);
            return true;
        }

        List<Point> rootToFrom = BuildRootPath(fromTile);
        List<Point> rootToTo = BuildRootPath(toTile);
        if (rootToFrom.Count == 0 || rootToTo.Count == 0)
            return false;

        int commonCount = 0;
        int maxCommon = Math.Min(rootToFrom.Count, rootToTo.Count);
        while (commonCount < maxCommon && rootToFrom[commonCount] == rootToTo[commonCount])
            commonCount++;

        int commonIndex = commonCount - 1;
        if (commonIndex < 0)
            return false;

        List<Vector2> raw = new() { fromWorld };

        for (int i = rootToFrom.Count - 2; i >= commonIndex; i--)
            raw.Add(TileCenter(rootToFrom[i].X, rootToFrom[i].Y));

        for (int i = commonIndex + 1; i < rootToTo.Count; i++)
            raw.Add(TileCenter(rootToTo[i].X, rootToTo[i].Y));

        if (raw.Count == 1)
            raw.Add(toWorld);
        else
            raw[^1] = toWorld;

        path = SmoothPath(raw);
        return path.Count > 0;
    }

    private void Rebuild(Player player, Rectangle bounds, ulong tick)
    {
        _bounds = bounds;
        _rootTile = WorldToTile(player.MountedCenter);
        _lastRefreshTick = tick;
        _initialized = true;
        Version++;

        int count = _bounds.Width * _bounds.Height;
        if (_reachable.Length != count) {
            _reachable = new bool[count];
            _parent = new int[count];
            _passability = new byte[count];
        }
        else {
            Array.Clear(_reachable, 0, _reachable.Length);
            Array.Clear(_passability, 0, _passability.Length);
        }

        Array.Fill(_parent, -1);

        if (!Contains(_rootTile))
            return;

        _floodQueue.Clear();
        int rootIndex = LocalIndex(_rootTile.X, _rootTile.Y);
        _reachable[rootIndex] = true;
        _floodQueue.Enqueue(_rootTile);

        while (_floodQueue.Count > 0) {
            Point current = _floodQueue.Dequeue();
            int currentIndex = LocalIndex(current.X, current.Y);

            foreach (Point step in Neighbors) {
                int nx = current.X + step.X;
                int ny = current.Y + step.Y;

                if (!_bounds.Contains(nx, ny))
                    continue;

                int nextIndex = LocalIndex(nx, ny);
                if (_reachable[nextIndex])
                    continue;

                if (!IsPassableCached(nextIndex, nx, ny))
                    continue;

                _reachable[nextIndex] = true;
                _parent[nextIndex] = currentIndex;
                _floodQueue.Enqueue(new Point(nx, ny));
            }
        }
    }

    private static Rectangle CalculateBounds()
    {
        // The gameplay boundary is explicit: telekinetic connectivity is evaluated only through
        // the currently visible screen area. There is no hidden off-screen connectivity margin.
        int left = (int)MathF.Floor(Main.screenPosition.X / 16f);
        int top = (int)MathF.Floor(Main.screenPosition.Y / 16f);
        int right = (int)MathF.Ceiling((Main.screenPosition.X + Main.screenWidth) / 16f);
        int bottom = (int)MathF.Ceiling((Main.screenPosition.Y + Main.screenHeight) / 16f);

        left = Math.Clamp(left, 1, Main.maxTilesX - 2);
        top = Math.Clamp(top, 1, Main.maxTilesY - 2);
        right = Math.Clamp(right, left + 1, Main.maxTilesX - 1);
        bottom = Math.Clamp(bottom, top + 1, Main.maxTilesY - 1);

        return new Rectangle(left, top, right - left, bottom - top);
    }

    private List<Point> BuildRootPath(Point target)
    {
        List<Point> reversed = new();
        int cursor = LocalIndex(target.X, target.Y);
        int rootIndex = LocalIndex(_rootTile.X, _rootTile.Y);

        while (cursor >= 0) {
            Point point = FromLocalIndex(cursor);
            reversed.Add(point);

            if (cursor == rootIndex)
                break;

            cursor = _parent[cursor];
        }

        if (reversed.Count == 0 || reversed[^1] != _rootTile)
            return new List<Point>();

        reversed.Reverse();
        return reversed;
    }

    private static List<Vector2> SmoothPath(List<Vector2> raw)
    {
        if (raw.Count <= 2)
            return raw;

        List<Vector2> smooth = new() { raw[0] };
        int index = 0;

        while (index < raw.Count - 1) {
            int next = raw.Count - 1;
            while (next > index + 1 && !Collision.CanHitLine(raw[index], 1, 1, raw[next], 1, 1))
                next--;

            smooth.Add(raw[next]);
            index = next;
        }

        return smooth;
    }

    private bool IsPassableCached(int index, int tileX, int tileY)
    {
        byte state = _passability[index];

        if (state == PassabilityUntested) {
            state = IsPassable(tileX, tileY) ? PassabilityPassable : PassabilityBlocked;
            _passability[index] = state;
        }

        return state == PassabilityPassable;
    }

    private bool IsReachable(int tileX, int tileY)
    {
        if (!Contains(new Point(tileX, tileY)))
            return false;

        return _reachable[LocalIndex(tileX, tileY)];
    }

    private bool Contains(Point point) => _bounds.Contains(point.X, point.Y);

    private int LocalIndex(int tileX, int tileY)
    {
        return (tileX - _bounds.Left) + (tileY - _bounds.Top) * _bounds.Width;
    }

    private Point FromLocalIndex(int index)
    {
        int y = index / _bounds.Width;
        int x = index - y * _bounds.Width;
        return new Point(x + _bounds.Left, y + _bounds.Top);
    }

    private static bool IsPassable(int tileX, int tileY)
    {
        if (!WorldGen.InWorld(tileX, tileY, 1))
            return false;

        return !Collision.IsWorldPointSolid(TileCenter(tileX, tileY), true);
    }

    private static Point WorldToTile(Vector2 world) => new((int)(world.X / 16f), (int)(world.Y / 16f));

    private static Vector2 TileCenter(int x, int y) => new((x + 0.5f) * 16f, (y + 0.5f) * 16f);
}
