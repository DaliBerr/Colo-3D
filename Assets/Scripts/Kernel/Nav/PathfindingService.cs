using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lonize.Logging;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Kernel.Nav
{
    /// <summary>
    /// 网格路径寻路服务，支持同步与异步查询。
    /// </summary>
    public sealed class PathfindingService
    {
        private readonly Tilemap _tilemap;
        private readonly NavGrid _navGrid;
        private readonly IMovementCostStrategy _movementCostStrategy;
        private readonly INeighborStrategy _neighborStrategy;
        private readonly bool _allowDiagonal;

        public PathfindingService(
            Tilemap tilemap,
            NavGrid navGrid,
            IMovementCostStrategy movementCostStrategy = null,
            INeighborStrategy neighborStrategy = null,
            bool allowDiagonal = false)
        {
            _tilemap = tilemap ? tilemap : throw new ArgumentNullException(nameof(tilemap));
            _navGrid = navGrid ?? throw new ArgumentNullException(nameof(navGrid));
            _movementCostStrategy = movementCostStrategy ?? new DefaultMovementCostStrategy();
            _neighborStrategy = neighborStrategy ?? new GridNeighborStrategy();
            _allowDiagonal = allowDiagonal;

            // 简单一致性检查：如果 NavGrid 已经绑定了 mainTilemap，但与传入的 tilemap 不同，则给出警告。
            if (_navGrid.occupancyMap != null && _navGrid.occupancyMap != _tilemap)
            {
                GameDebug.LogWarning("[PathfindingService] NavGrid.mainTilemap 与 PathfindingService 使用的 Tilemap 不一致，寻路结果可能异常。");
            }
        }

        /// <summary>
        /// 使用世界坐标进行同步寻路。
        /// </summary>
        public IReadOnlyList<Vector3Int> FindPathFromWorld(
            Vector3 worldStart,
            Vector3 worldGoal,
            int maxSteps = 4096,
            bool? allowDiagonalOverride = null,
            CancellationToken cancellationToken = default)
        {
            var startCell = WorldToCell(worldStart);
            var goalCell = WorldToCell(worldGoal);
            return FindPath(startCell, goalCell, maxSteps, allowDiagonalOverride, cancellationToken);
        }

        /// <summary>
        /// 使用世界坐标进行异步寻路，带超时保护。
        /// </summary>
        public async Task<IReadOnlyList<Vector3Int>> FindPathFromWorldAsync(
            Vector3 worldStart,
            Vector3 worldGoal,
            int maxSteps = 4096,
            TimeSpan? timeout = null,
            bool? allowDiagonalOverride = null,
            CancellationToken cancellationToken = default)
        {
            // 先在主线程把世界坐标转换为 Cell，避免在后台线程访问 Unity 对象。
            var startCell = WorldToCell(worldStart);
            var goalCell = WorldToCell(worldGoal);

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (timeout.HasValue)
            {
                linkedCts.CancelAfter(timeout.Value);
            }

            return await Task.Run(
                () => FindPath(startCell, goalCell, maxSteps, allowDiagonalOverride, linkedCts.Token),
                linkedCts.Token);
        }

        /// <summary>
        /// 使用 cell 坐标进行同步寻路。
        /// </summary>
        public IReadOnlyList<Vector3Int> FindPath(
            Vector3Int start,
            Vector3Int goal,
            int maxSteps = 4096,
            bool? allowDiagonalOverride = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsWalkable(start) || !IsWalkable(goal))
            {
                return Array.Empty<Vector3Int>();
            }

            var openSet = new List<Vector3Int> { start };
            var cameFrom = new Dictionary<Vector3Int, Vector3Int>();
            var gScore = new Dictionary<Vector3Int, float> { [start] = 0f };
            var fScore = new Dictionary<Vector3Int, float> { [start] = Heuristic(start, goal, allowDiagonalOverride) };
            var allowDiagonal = allowDiagonalOverride ?? _allowDiagonal;

            var steps = 0;
            while (openSet.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (steps++ > maxSteps)
                {
                    return Array.Empty<Vector3Int>();
                }

                var current = PopLowest(openSet, fScore);
                if (current == goal)
                {
                    return ReconstructPath(cameFrom, current);
                }

                foreach (var neighbor in _neighborStrategy.GetNeighbors(current, allowDiagonal))
                {
                    if (!IsWalkable(neighbor))
                    {
                        continue;
                    }

                    var tentativeG = gScore[current] + _movementCostStrategy.GetCost(current, neighbor, _navGrid);
                    if (!gScore.TryGetValue(neighbor, out var neighborScore) || tentativeG < neighborScore)
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        fScore[neighbor] = tentativeG + Heuristic(neighbor, goal, allowDiagonal);
                        if (!openSet.Contains(neighbor))
                        {
                            openSet.Add(neighbor);
                        }
                    }
                }
            }

            return Array.Empty<Vector3Int>();
        }

        /// <summary>
        /// 使用 cell 坐标进行异步寻路，带超时保护。
        /// </summary>
        public async Task<IReadOnlyList<Vector3Int>> FindPathAsync(
            Vector3Int start,
            Vector3Int goal,
            int maxSteps = 4096,
            TimeSpan? timeout = null,
            bool? allowDiagonalOverride = null,
            CancellationToken cancellationToken = default)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (timeout.HasValue)
            {
                linkedCts.CancelAfter(timeout.Value);
            }

            return await Task.Run(
                () => FindPath(start, goal, maxSteps, allowDiagonalOverride, linkedCts.Token),
                linkedCts.Token);
        }

        private Vector3Int WorldToCell(Vector3 worldPosition)
        {
            return _tilemap.WorldToCell(worldPosition);
        }

        /// <summary>
        /// 检查某个 cell 是否可行走（基于 NavGrid 的阻挡信息）。
        /// </summary>
        /// <param name="cell">要检查的网格坐标。</param>
        /// <returns>如果未被阻挡则返回 true，否则返回 false。</returns>
        private bool IsWalkable(Vector3Int cell)
        {
            // NavGrid 当前只提供 IsCellBlocked，所以这里做一次反转。
            return !_navGrid.IsCellBlocked(cell);
        }

        private static Vector3Int PopLowest(List<Vector3Int> openSet, Dictionary<Vector3Int, float> fScore)
        {
            var bestIndex = 0;
            var bestValue = float.PositiveInfinity;
            for (var i = 0; i < openSet.Count; i++)
            {
                var node = openSet[i];
                var score = fScore.TryGetValue(node, out var value) ? value : float.PositiveInfinity;
                if (score < bestValue)
                {
                    bestValue = score;
                    bestIndex = i;
                }
            }

            var result = openSet[bestIndex];
            openSet.RemoveAt(bestIndex);
            return result;
        }

        private static IReadOnlyList<Vector3Int> ReconstructPath(Dictionary<Vector3Int, Vector3Int> cameFrom, Vector3Int current)
        {
            var path = new List<Vector3Int> { current };
            while (cameFrom.TryGetValue(current, out var previous))
            {
                current = previous;
                path.Add(current);
            }

            path.Reverse();
            return path;
        }

        private static float Heuristic(Vector3Int from, Vector3Int to, bool? allowDiagonalOverride)
        {
            var dx = Mathf.Abs(from.x - to.x);
            var dy = Mathf.Abs(from.y - to.y);
            var allowDiagonal = allowDiagonalOverride ?? false;
            // 对角启用时使用棋盘距离，否则使用曼哈顿距离
            return allowDiagonal ? Mathf.Max(dx, dy) : dx + dy;
        }
    }

    /// <summary>
    /// 移动代价策略接口，便于支持不同地形。
    /// </summary>
    public interface IMovementCostStrategy
    {
        float GetCost(Vector3Int from, Vector3Int to, NavGrid navGrid);
    }

    /// <summary>
    /// 邻居采样策略接口，便于切换对角移动规则。
    /// </summary>
    public interface INeighborStrategy
    {
        IEnumerable<Vector3Int> GetNeighbors(Vector3Int cell, bool allowDiagonal);
    }

    /// <summary>
    /// 默认移动代价策略，当前只根据是否对角移动来调整代价。
    /// 如需支持地形成本，可在 NavGrid 中扩展相应数据，并在此读取。
    /// </summary>
    public sealed class DefaultMovementCostStrategy : IMovementCostStrategy
    {
        public float GetCost(Vector3Int from, Vector3Int to, NavGrid navGrid)
        {
            var isDiagonal = Math.Abs(from.x - to.x) == 1 && Math.Abs(from.y - to.y) == 1;
            // 基础代价固定为 1，对角移动稍微增大代价。
            return isDiagonal ? 1.4142f : 1f;
        }
    }

    /// <summary>
    /// 默认邻居策略，支持 4/8 方向。
    /// </summary>
    public sealed class GridNeighborStrategy : INeighborStrategy
    {
        private static readonly Vector3Int[] CardinalDirections =
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0),
            new Vector3Int(0, -1, 0)
        };

        private static readonly Vector3Int[] DiagonalDirections =
        {
            new Vector3Int(1, 1, 0),
            new Vector3Int(1, -1, 0),
            new Vector3Int(-1, 1, 0),
            new Vector3Int(-1, -1, 0)
        };

        public IEnumerable<Vector3Int> GetNeighbors(Vector3Int cell, bool allowDiagonal)
        {
            foreach (var dir in CardinalDirections)
            {
                yield return cell + dir;
            }

            if (!allowDiagonal)
            {
                yield break;
            }

            foreach (var dir in DiagonalDirections)
            {
                yield return cell + dir;
            }
        }
    }
}
