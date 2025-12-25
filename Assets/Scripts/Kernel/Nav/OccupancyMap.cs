using System.Collections.Generic;
using Kernel.UI;
using Lonize.Logging;
using UnityEngine;
using Kernel.World;

namespace Kernel.Nav
{
    /// <summary>
    /// summary: 占用/阻挡表（3D 网格版），用于建筑放置与寻路阻挡。
    /// </summary>
    public class OccupancyMap : MonoBehaviour
    {
        /// <summary>
        /// summary: 单例引用。
        /// </summary>
        public static OccupancyMap Instance { get; private set; }

        [Header("Refs")]
        public WorldGrid worldGrid;

        /// <summary>
        /// summary: 已阻挡格子集合（稀疏存储）。
        /// </summary>
        private readonly HashSet<Vector3Int> _blocked = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (worldGrid == null)
                worldGrid = WorldGrid.Instance;
        }

        /// <summary>
        /// summary: 判断某格是否阻挡（无效格默认阻挡）。
        /// param: cell 格子坐标（x=cellX, y=cellZ）
        /// return: 是否阻挡
        /// </summary>
        public bool IsCellBlocked(Vector3Int cell)
        {
            if (worldGrid != null && !worldGrid.IsCellValid(cell))
                return true;

            return _blocked.Contains(cell);
        }

        /// <summary>
        /// summary: 更新单个格子的阻挡状态。
        /// param: cell 格子坐标
        /// param: blocked 是否阻挡
        /// return: 无
        /// </summary>
        public void UpdateCellBlocked(Vector3Int cell, bool blocked)
        {
            if (worldGrid != null && !worldGrid.IsCellValid(cell))
                return;

            if (blocked) _blocked.Add(cell);
            else _blocked.Remove(cell);
        }

        /// <summary>
        /// summary: 清空所有阻挡（用于重开地图/调试）。
        /// param: 无
        /// return: 无
        /// </summary>
        public void ClearAll()
        {
            _blocked.Clear();
        }

        /// <summary>
        /// summary: 更新矩形占用区（与现有 NavGrid 接口一致）。
        /// param: anchorCell 锚点格（默认以中心为锚）
        /// param: width 建筑宽（格）
        /// param: height 建筑高（格）
        /// param: rotationSteps 旋转步数（0/1/2/3）
        /// param: blocked 是否阻挡
        /// return: 无
        /// </summary>
        public void UpdateAreaBlocked(Vector3Int anchorCell, int width, int height, int rotationSteps, bool blocked)
        {
            var cells = GetFootprintCells(anchorCell, width, height, rotationSteps);
            for (int i = 0; i < cells.Count; i++)
                UpdateCellBlocked(cells[i], blocked);
        }

        /// <summary>
        /// summary: 获取建筑在当前旋转下覆盖的格子列表（不依赖 Tilemap）。
        /// param: anchorCell 锚点格（中心锚）
        /// param: width 建筑宽（格）
        /// param: height 建筑高（格）
        /// param: rotationSteps 旋转步数（0/1/2/3）
        /// return: 覆盖格子列表
        /// </summary>
        public List<Vector3Int> GetFootprintCells(Vector3Int anchorCell, int width, int height, int rotationSteps)
        {
            var cells = new List<Vector3Int>();
            FillFootprintCells(cells, anchorCell, width, height, rotationSteps, filterInvalid: true);
            return cells;
        }

        /// <summary>
        /// summary: 将覆盖格子写入 buffer（可复用 List，减少 GC）。
        /// param: buffer 输出 buffer
        /// param: anchorCell 锚点格
        /// param: width 建筑宽（格）
        /// param: height 建筑高（格）
        /// param: rotationSteps 旋转步数
        /// param: filterInvalid 是否过滤无效格
        /// return: 实际写入数量
        /// </summary>
        public int FillFootprintCells(List<Vector3Int> buffer, Vector3Int anchorCell, int width, int height, int rotationSteps, bool filterInvalid)
        {
            buffer.Clear();

            int rot = ((rotationSteps % 4) + 4) % 4;
            int realWidth = (rot % 2 == 1) ? height : width;
            int realHeight = (rot % 2 == 1) ? width : height;

            // 延续你现有 NavGrid 的“中心锚”逻辑 :contentReference[oaicite:3]{index=3}
            Vector3Int start = anchorCell - new Vector3Int(realWidth / 2, realHeight / 2, 0);

            for (int x = 0; x < realWidth; x++)
            {
                for (int z = 0; z < realHeight; z++)
                {
                    Vector3Int cell = start + new Vector3Int(x, z, 0);
                    if (filterInvalid && worldGrid != null && !worldGrid.IsCellValid(cell))
                        continue;

                    buffer.Add(cell);
                }
            }

            return buffer.Count;
        }

        /// <summary>
        /// summary: 检查某 footprint 是否可放置（可建 + 不阻挡）。
        /// param: anchorCell 锚点格
        /// param: width 宽（格）
        /// param: height 高（格）
        /// param: rotationSteps 旋转步数
        /// return: 是否可放置
        /// </summary>
        public bool CanPlace(Vector3Int anchorCell, int width, int height, int rotationSteps)
        {
            var cells = GetFootprintCells(anchorCell, width, height, rotationSteps);
            if (cells.Count == 0) return false;

            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                if (IsCellBlocked(c)) return false;
                if (worldGrid != null && !worldGrid.IsCellBuildable(c)) return false;
            }
            return true;
        }
    }
}
