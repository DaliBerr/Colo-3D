using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using Kernel.Nav;

namespace Kernel
{
    /// <summary>
    /// 全局寻路管理器，负责持有 PathfindingService 实例。
    /// </summary>
    public class PathfindingManager : MonoBehaviour
    {
        public Tilemap groundTilemap;

        public static PathfindingManager Instance { get; private set; }

        private PathfindingService _service;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            var navGrid = NavGrid.Instance;
            _service = new PathfindingService(
                groundTilemap,
                navGrid,
                movementCostStrategy: null,
                neighborStrategy: null,
                allowDiagonal: true);
        }

        /// <summary>
        /// 从世界坐标获取路径（同步版）。
        /// </summary>
        /// <param name="startWorld">起点世界坐标。</param>
        /// <param name="targetWorld">终点世界坐标。</param>
        /// <returns>按顺序排列的网格坐标路径；找不到返回空列表。</returns>
        public IReadOnlyList<Vector3Int> GetPath(Vector3 startWorld, Vector3 targetWorld)
        {
            return _service.FindPathFromWorld(startWorld, targetWorld);
        }
    }
}
