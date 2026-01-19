using System.Collections.Generic;
using Colo.Def.Building;
using Kernel.Map;
using Kernel.World;
using Lonize.Logging;
using UnityEngine;

namespace Kernel.Building
{
    /// <summary>
    /// summary: 矿机行为，周期性读取所在区块矿物信息并输出产物日志。
    /// </summary>
    public class MinerBehaviour : IBuildingBehaviour
    {
        private string _outputItemId;
        private int _outputCount;
        private int _tickInterval;
        private int _tickAccumulator;
        private BuildingRuntime _runtime;
        private ChunkMineralInfo _cachedMineralInfo;

        /// <summary>
        /// summary: 构建矿机行为。
        /// param: outputItemId 输出物品ID
        /// param: outputCount 每次输出数量
        /// param: tickInterval 输出间隔（Tick）
        /// return: 无
        /// </summary>
        public MinerBehaviour(string outputItemId, int outputCount, int tickInterval)
        {
            _outputItemId = string.IsNullOrWhiteSpace(outputItemId) ? "raw_ore" : outputItemId;
            _outputCount = Mathf.Max(1, outputCount);
            _tickInterval = Mathf.Max(1, tickInterval);
        }

        /// <summary>
        /// summary: 绑定运行时并初始化矿物缓存。
        /// param: runtime 建筑运行时
        /// return: 无
        /// </summary>
        public void OnBind(BuildingRuntime runtime)
        {
            _runtime = runtime;
            _tickAccumulator = 0;
            ApplyDefOverrides(runtime);
            TryResolveMineralInfo(out _cachedMineralInfo);
        }

        /// <summary>
        /// summary: 解绑运行时并清理缓存。
        /// param: runtime 建筑运行时
        /// return: 无
        /// </summary>
        public void OnUnbind(BuildingRuntime runtime)
        {
            _runtime = null;
            _cachedMineralInfo = default;
        }

        /// <summary>
        /// summary: Tick 推进并在间隔到达时输出产物日志。
        /// param: ticks Tick 数量
        /// return: 无
        /// </summary>
        public void Tick(int ticks)
        {
            if (_runtime == null)
            {
                return;
            }

            _tickAccumulator += Mathf.Max(0, ticks);
            if (_tickAccumulator < _tickInterval)
            {
                return;
            }

            _tickAccumulator -= _tickInterval;
            if (!TryResolveMineralInfo(out var mineralInfo))
            {
                GameDebug.LogWarning($"[Miner] 未能获取矿物信息，建筑ID={_runtime.BuildingID}。");
                return;
            }

            _cachedMineralInfo = mineralInfo;
            GameDebug.Log($"[Miner] 输出矿物：{_outputItemId} x{_outputCount}，Cell={_runtime.CellPosition}，Chunk={mineralInfo.ChunkCoord}。");
            Log.Info($"[Miner] Output item={_outputItemId}, count={_outputCount}, cell={_runtime.CellPosition}, chunk={mineralInfo.ChunkCoord}.");

            if (mineralInfo.MineralComposition != null && mineralInfo.MineralComposition.Count > 0)
            {
                foreach (var kvp in mineralInfo.MineralComposition)
                {
                    GameDebug.Log($"[Miner] 矿物成分：{kvp.Key}={kvp.Value:F3}");
                }
            }
            else
            {
                GameDebug.Log("[Miner] 矿物成分为空，默认无法分解。");
            }
        }

        /// <summary>
        /// summary: 根据运行时所在格子获取区块矿物信息。
        /// param: mineralInfo 输出矿物信息
        /// return: 是否获取成功
        /// </summary>
        private bool TryResolveMineralInfo(out ChunkMineralInfo mineralInfo)
        {
            mineralInfo = default;

            var worldGrid = WorldGrid.Instance;
            if (worldGrid == null || _runtime == null)
            {
                return false;
            }

            Vector2Int chunkCoord = ResolveChunkCoord(worldGrid, _runtime.CellPosition);
            var generator = Object.FindFirstObjectByType<WorldChunkMeshGenerator>();
            if (generator == null)
            {
                return false;
            }

            return generator.TryGetChunkMineralInfo(chunkCoord, out mineralInfo);
        }

        /// <summary>
        /// summary: 使用 MinerDef 中的配置覆盖行为参数。
        /// param: runtime 建筑运行时
        /// return: 无
        /// </summary>
        private void ApplyDefOverrides(BuildingRuntime runtime)
        {
            if (runtime?.Def is not MinerDef def)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(def.outputItemId))
            {
                _outputItemId = def.outputItemId;
            }

            if (def.outputCount > 0)
            {
                _outputCount = def.outputCount;
            }

            if (def.tickInterval > 0)
            {
                _tickInterval = def.tickInterval;
            }
        }

        /// <summary>
        /// summary: 计算格子坐标对应的区块坐标。
        /// param: worldGrid 世界网格
        /// param: cellPos 建筑所在格子
        /// return: 区块坐标
        /// </summary>
        private static Vector2Int ResolveChunkCoord(WorldGrid worldGrid, Vector2Int cellPos)
        {
            int chunkWidth = Mathf.Max(1, worldGrid.chunkWidthCells);
            int chunkHeight = Mathf.Max(1, worldGrid.chunkHeightCells);
            int chunkX = Mathf.FloorToInt((float)cellPos.x / chunkWidth);
            int chunkY = Mathf.FloorToInt((float)cellPos.y / chunkHeight);
            return new Vector2Int(chunkX, chunkY);
        }
    }
}
