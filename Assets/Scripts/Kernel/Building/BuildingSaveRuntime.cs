using System.Collections;
using System.Collections.Generic;
using Lonize.Logging;
using UnityEngine;
using Kernel.Nav;
using Kernel.Pool;
using Kernel.World;

namespace Kernel.Building
{
    /// <summary>
    /// summary: 负责在存档和场景建筑之间做转换（3D 版：WorldGrid + OccupancyMap）。
    /// </summary>
    public static class BuildingSaveRuntime
    {
        /// <summary>
        /// summary: 收集场景中所有建筑并写入存档列表。
        /// param: list 用于输出存档数据的列表引用
        /// return: 无
        /// </summary>
        public static void CollectBuildingsForSave(ref List<SaveBuildingInstance> list)
        {
            var controller = Object.FindFirstObjectByType<BuildingPlacementController>();
            if (controller == null)
            {
                GameDebug.LogError("[SaveAllBuildings] CollectBuildingsForSave 找不到 BuildingPlacementController。");
                list = null;
                return;
            }

            if (controller.occupancyMap == null || controller.occupancyMap.worldGrid == null)
            {
                GameDebug.LogError("[SaveAllBuildings] CollectBuildingsForSave 中 occupancyMap/worldGrid 为空。");
                list = null;
                return;
            }

            BuildingRuntimeHost[] hosts;
            if (controller.buildingRoot != null)
                hosts = controller.buildingRoot.GetComponentsInChildren<BuildingRuntimeHost>(true);
            else
                hosts = Object.FindObjectsByType<BuildingRuntimeHost>(UnityEngine.FindObjectsSortMode.None);

            if (hosts == null || hosts.Length == 0)
            {
                list = new List<SaveBuildingInstance>();
                GameDebug.Log("[SaveAllBuildings] CollectBuildingsForSave：当前没有建筑需要保存。");
                return;
            }

            if (list == null) list = new List<SaveBuildingInstance>(hosts.Length);
            else
            {
                list.Clear();
                if (list.Capacity < hosts.Length) list.Capacity = hosts.Length;
            }

            var grid = controller.occupancyMap.worldGrid;

            foreach (var host in hosts)
            {
                if (host == null) continue;

                var data = host.CreateSaveData(grid);
                if (data != null)
                    list.Add(data);
            }

            Log.Info($"[SaveAllBuildings] CollectBuildingsForSave：已收集 {list.Count} 个建筑。");
            GameDebug.Log($"[SaveAllBuildings] CollectBuildingsForSave：已收集 {list.Count} 个建筑。");
        }

        /// <summary>
        /// summary: 清理当前地图上的所有建筑，并清空 OccupancyMap。
        /// param: controller 放置控制器
        /// return: 无
        /// </summary>
        private static void ClearExistingBuildings(BuildingPlacementController controller)
        {
            if (controller == null) return;

            BuildingRuntimeHost[] hosts;
            if (controller.buildingRoot != null)
                hosts = controller.buildingRoot.GetComponentsInChildren<BuildingRuntimeHost>(true);
            else
                hosts = Object.FindObjectsByType<BuildingRuntimeHost>(UnityEngine.FindObjectsSortMode.None);

            if (hosts != null)
            {
                foreach (var host in hosts)
                {
                    if (host == null) continue;
                    var go = host.gameObject;

                    if (PoolManager.Instance != null)
                        PoolManager.Instance.ReturnToPool(go);
                    else
                        Object.Destroy(go);
                }

                Log.Info($"[SaveAllBuildings] ClearExistingBuildings：清理 {hosts.Length} 个建筑。");
                GameDebug.Log($"[SaveAllBuildings] ClearExistingBuildings：清理 {hosts.Length} 个建筑。");
            }

            // 3D 版：直接清空占用，读档时重新写入
            (controller.occupancyMap != null ? controller.occupancyMap : OccupancyMap.Instance)?.ClearAll();
        }

        /// <summary>
        /// summary: 根据存档数据在场景中重新生成建筑。
        /// param: list 从存档读取的建筑实例数据列表
        /// return: 无
        /// </summary>
        public static void RestoreBuildingsFromSave(List<SaveBuildingInstance> list)
        {
            GameDebug.Log("RestoreBuildingsFromSave called. List count: " + (list != null ? list.Count.ToString() : "null"));

            if (list == null || list.Count == 0)
            {
                GameDebug.Log("[SaveAllBuildings] RestoreBuildingsFromSave：存档中没有建筑。");
                return;
            }

            var controller = Object.FindFirstObjectByType<BuildingPlacementController>();
            if (controller == null)
            {
                GameDebug.LogError("[SaveAllBuildings] RestoreBuildingsFromSave 找不到 BuildingPlacementController。");
                return;
            }

            ClearExistingBuildings(controller);
            controller.StartCoroutine(RestoreBuildingsCoroutine(controller, list));
        }

        /// <summary>
        /// summary: 协程逐个生成建筑并应用存档数据（会等待 WorldGrid 对应格子有效，避免地形未生成完成）。
        /// param: controller 放置控制器
        /// param: list 存档中的建筑实例列表
        /// return: 协程枚举器
        /// </summary>
        private static IEnumerator RestoreBuildingsCoroutine(BuildingPlacementController controller, List<SaveBuildingInstance> list)
        {
            var occupancyMap = controller.occupancyMap != null ? controller.occupancyMap : OccupancyMap.Instance;
            if (occupancyMap == null || occupancyMap.worldGrid == null)
            {
                GameDebug.LogError("[SaveAllBuildings] RestoreBuildingsCoroutine 中 occupancyMap/worldGrid 为空。");
                yield break;
            }

            var grid = occupancyMap.worldGrid;
            int restoredCount = 0;

            foreach (var data in list)
            {
                if (data == null) continue;

                if (!BuildingDatabase.TryGet(data.DefId, out var def))
                {
                    Log.Warn($"[SaveAllBuildings] 还原时未找到 BuildingDef: {data.DefId}");
                    GameDebug.LogWarning($"[SaveAllBuildings] 还原时未找到 BuildingDef: {data.DefId}");
                    continue;
                }

                // CellY 字段存的是 cellZ
                var anchorCell = new Vector3Int(data.CellX, data.CellY, 0);
                byte rotSteps = (byte)(data.RotSteps & 3);

                // 等待地形准备（最多等 300 帧，避免卡死）
                int wait = 0;
                while (!grid.IsCellValid(anchorCell) && wait < 300)
                {
                    wait++;
                    yield return null;
                }

                // 计算世界位置（XZ 对齐格子中心，Y 取 footprint 平均高度）
                Vector3 worldPos = grid.CellToWorldCenterXZ(anchorCell);
                worldPos.y = TryComputeFootprintAvgHeight(grid, occupancyMap, anchorCell, def, rotSteps, out float avgH) ? avgH : 0f;

                Quaternion rot = Quaternion.Euler(0f, rotSteps * 90f, 0f);

                if (PoolManager.Instance == null)
                {
                    Log.Error("[SaveAllBuildings] RestoreBuildingsCoroutine 中 PoolManager.Instance 为空。");
                    GameDebug.LogError("[SaveAllBuildings] RestoreBuildingsCoroutine 中 PoolManager.Instance 为空。");
                    yield break;
                }

                var task = PoolManager.Instance.GetAsync(def.Id, worldPos, rot);
                while (!task.IsCompleted) yield return null;

                var go = task.Result;
                if (go == null)
                {
                    Log.Error($"[SaveAllBuildings] PoolManager.GetAsync 返回 null, DefId={def.Id}");
                    continue;
                }

                if (controller.buildingRoot != null)
                    go.transform.SetParent(controller.buildingRoot, true);

                go.transform.SetPositionAndRotation(worldPos, rot);

                // 写阻挡
                occupancyMap.UpdateAreaBlocked(anchorCell, def.Width, def.Height, rotSteps, true);

                // 应用运行时状态 + 写入 placement
                var host = go.GetComponent<BuildingRuntimeHost>();
                if (host != null)
                {
                    host.ApplySaveData(data);
                    host.SetPlacement(anchorCell, rotSteps);
                }

                restoredCount++;
                if (restoredCount % 16 == 0)
                    yield return null;
            }

            GameDebug.Log($"[SaveAllBuildings] RestoreBuildingsFromSave 完成，还原 {restoredCount} 个建筑。");
        }

        /// <summary>
        /// summary: 计算 footprint 平均地面高度（用于多格建筑贴地）。
        /// param: grid WorldGrid
        /// param: occ OccupancyMap
        /// param: anchorCell 锚点格
        /// param: def 建筑定义
        /// param: rotSteps 旋转步数
        /// param: avgHeight 输出平均高度
        /// return: 是否成功
        /// </summary>
        private static bool TryComputeFootprintAvgHeight(WorldGrid grid, OccupancyMap occ, Vector3Int anchorCell, BuildingDef def, int rotSteps, out float avgHeight)
        {
            avgHeight = 0f;
            if (grid == null || occ == null || def == null) return false;

            var cells = occ.GetFootprintCells(anchorCell, def.Width, def.Height, rotSteps);
            if (cells == null || cells.Count == 0) return false;

            float sum = 0f;
            int count = 0;

            for (int i = 0; i < cells.Count; i++)
            {
                if (grid.TryGetGroundAtCell(cells[i], out float h, out _, out _))
                {
                    sum += h;
                    count++;
                }
            }

            if (count <= 0) return false;
            avgHeight = sum / count;
            return true;
        }
    }
}
