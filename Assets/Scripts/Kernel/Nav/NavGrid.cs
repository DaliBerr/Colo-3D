using System.Collections.Generic;
using Kernel.UI;
using Lonize.Logging;
using UnityEngine;
using Kernel.World;

namespace Kernel.Nav
{
    /// <summary>
    /// summary: 导航/阻挡服务（3D 版），内部委托给 OccupancyMap。
    /// </summary>
    public class NavGrid : MonoBehaviour
    {
        public static NavGrid Instance { get; private set; }

        [Header("Refs")]
        public WorldGrid worldGrid;
        public OccupancyMap occupancyMap;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (worldGrid == null) worldGrid = WorldGrid.Instance;
            if (occupancyMap == null) occupancyMap = OccupancyMap.Instance;
        }

        /// <summary>
        /// summary: 检查某格是否阻挡。
        /// param: cell 格子坐标
        /// return: 是否阻挡
        /// </summary>
        public bool IsCellBlocked(Vector3Int cell)
        {
            if (occupancyMap != null) return occupancyMap.IsCellBlocked(cell);

            // 极端兜底：没有 occupancy 时，至少把无效格视为阻挡
            if (worldGrid != null && !worldGrid.IsCellValid(cell))
                return true;

            return false;
        }

        /// <summary>
        /// summary: 更新单格阻挡。
        /// param: cell 格子坐标
        /// param: blocked 是否阻挡
        /// return: 无
        /// </summary>
        public void UpdateCellBlocked(Vector3Int cell, bool blocked)
        {
            if (occupancyMap != null)
            {
                occupancyMap.UpdateCellBlocked(cell, blocked);
                return;
            }
        }

        /// <summary>
        /// summary: 更新矩形区域阻挡（支持旋转）。
        /// param: anchorCell 锚点格
        /// param: width 宽（格）
        /// param: height 高（格）
        /// param: rotationSteps 旋转步数（0..3）
        /// param: blocked 是否阻挡
        /// return: 无
        /// </summary>
        public void UpdateAreaBlocked(Vector3Int anchorCell, int width, int height, int rotationSteps, bool blocked)
        {
            if (occupancyMap != null)
            {
                occupancyMap.UpdateAreaBlocked(anchorCell, width, height, rotationSteps, blocked);
                return;
            }
        }

        /// <summary>
        /// summary: 获取覆盖格子列表。
        /// param: anchorCell 锚点格
        /// param: width 宽（格）
        /// param: height 高（格）
        /// param: rotationSteps 旋转步数
        /// return: 覆盖格子列表
        /// </summary>
        public List<Vector3Int> GetFootprintCells(Vector3Int anchorCell, int width, int height, int rotationSteps)
        {
            if (occupancyMap != null) return occupancyMap.GetFootprintCells(anchorCell, width, height, rotationSteps);

            return new List<Vector3Int> { anchorCell };
        }

        /// <summary>
        /// summary: 兼容旧接口（Tilemap 时代的初始化），3D 版不需要扫描 Tilemap。
        /// param: _ 旧参数
        /// param: __ 旧参数
        /// param: ___ 旧参数
        /// return: 无
        /// </summary>
        public void InitializeFromTilemap(object _, LayerMask __, LayerMask ___)
        {
            GameDebug.LogWarning("[NavGrid] InitializeFromTilemap 已废弃：3D 版请使用 OccupancyMap / WorldGrid。");
            Log.Warn("[NavGrid] InitializeFromTilemap 已废弃：3D 版请使用 OccupancyMap / WorldGrid。");
        }
    }
}
