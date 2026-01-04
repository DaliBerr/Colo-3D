using System.Collections.Generic;
using UnityEngine;
using Kernel.World;
using Lonize.Logging;
using static Kernel.Storage.BuildingRuntimeStatsCodeC;
using Kernel.Storage;

namespace Kernel.Building
{
    /// <summary>
    /// summary: 建筑实例宿主，持有 Runtime 与行为列表，并负责生成/应用存档数据。
    /// </summary>
    public class BuildingRuntimeHost : MonoBehaviour
    {
        public BuildingRuntime Runtime;
        public List<IBuildingBehaviour> Behaviours = new();

        /// <summary>
        /// summary: 判断一个运行时 StatKey 是否为库存编码键（以 __inv__ 前缀存储）。
        /// param: key StatKey
        /// return: true=库存编码键；false=普通键
        /// </summary>
        private static bool IsInventoryStatKey(string key)
        {
            return !string.IsNullOrEmpty(key) && key.StartsWith(StorageRuntimeStatsCodec.ItemKeyPrefix);
        }

        /// <summary>
        /// summary: 移除 RuntimeStats 中的库存编码键，避免读档后 RuntimeStats 污染导致二次保存重复写入。
        /// param: stats 运行时 Stat 字典
        /// return: 无
        /// </summary>
        private static void StripInventoryStatKeys(Dictionary<string, float> stats)
        {
            if (stats == null || stats.Count == 0) return;

            // 先收集再删除，避免遍历期间修改字典
            List<string> toRemove = null;
            foreach (var kv in stats)
            {
                if (!IsInventoryStatKey(kv.Key)) continue;
                toRemove ??= new List<string>();
                toRemove.Add(kv.Key);
            }

            if (toRemove == null) return;
            for (int i = 0; i < toRemove.Count; i++)
                stats.Remove(toRemove[i]);
        }

        [Header("Placement (Grid)")]
        [SerializeField] private int _cellX;
        [SerializeField] private int _cellZ;
        [SerializeField] private byte _rotSteps;
        [SerializeField] private bool _hasPlacement;

        /// <summary>
        /// summary: 写入放置数据，并同步到 Runtime（路径B：Spawn 时先 SetPlacement 再 Bind 行为）。
        /// param: anchorCell 锚点格（x=CellX, y=CellZ）
        /// param: rotSteps 旋转步数（0..3）
        /// return: 无
        /// </summary>
        public void SetPlacement(Vector3Int anchorCell, byte rotSteps)
        {
            _cellX = anchorCell.x;
            _cellZ = anchorCell.y;
            _rotSteps = (byte)(rotSteps & 3);
            _hasPlacement = true;

            if (Runtime != null)
            {
                Runtime.CellPosition = new Vector2Int(_cellX, _cellZ);
                Runtime.RotationSteps = (byte)(_rotSteps & 3);
            }
        }

        /// <summary>
        /// summary: 尝试获取放置数据。
        /// param: anchorCell 输出锚点格
        /// param: rotSteps 输出旋转步数
        /// return: 是否存在放置信息
        /// </summary>
        public bool TryGetPlacement(out Vector3Int anchorCell, out byte rotSteps)
        {
            anchorCell = new Vector3Int(_cellX, _cellZ, 0);
            rotSteps = (byte)(_rotSteps & 3);
            return _hasPlacement;
        }

        /// <summary>
        /// summary: 生成存档数据（3D 版：基于 WorldGrid；CellY 字段继续存 cellZ 以兼容旧结构）。
        /// param: worldGrid WorldGrid 服务
        /// return: 存档数据，失败返回 null
        /// </summary>
        public SaveBuildingInstance CreateSaveData(WorldGrid worldGrid)
        {
            if (Runtime == null || Runtime.Def == null)
                return null;

            Vector3Int cellPos;
            byte rotSteps;

            if (_hasPlacement)
            {
                cellPos = new Vector3Int(_cellX, _cellZ, 0);
                rotSteps = (byte)(_rotSteps & 3);
            }
            else
            {
                // 兜底：从 transform 反推（不推荐，但防止老对象没写 placement）
                cellPos = worldGrid != null
                    ? worldGrid.WorldToCellXZ(transform.position)
                    : new Vector3Int(Mathf.RoundToInt(transform.position.x), Mathf.RoundToInt(transform.position.z), 0);
                rotSteps = (byte)(Mathf.RoundToInt(transform.eulerAngles.y / 90f) & 3);
            }

            var data = new SaveBuildingInstance
            {
                DefId = Runtime.Def.Id,
                RuntimeId = Runtime.BuildingID,

                CellX = cellPos.x,
                CellY = cellPos.y, // 注意：这里存的是 cellZ

                RotSteps = rotSteps,
            };

            // 1) 基础 stats + 2) 库存 stats 追加
            // 注意：RuntimeStats 里可能残留库存编码键（__inv__:*）。
            // 若不跳过，会出现「基础 stats 已包含库存键 + 追加库存键」导致存档重复/膨胀。
            int baseCount = 0;
            if (Runtime.RuntimeStats != null)
            {
                foreach (var kv in Runtime.RuntimeStats)
                {
                    if (IsInventoryStatKey(kv.Key)) continue;
                    baseCount++;
                }
            }
            string[] invIds = System.Array.Empty<string>();
            int[] invCounts = System.Array.Empty<int>();
            int invCount = 0;

            if (StorageSystem.Instance != null &&
                StorageSystem.Instance.TryGet(Runtime.BuildingID, out var container) &&
                container != null)
            {
                container.Export(out invIds, out invCounts);
                invCount = invIds != null ? invIds.Length : 0;
            }

            int total = baseCount + invCount;

            if (total <= 0)
            {
                data.StatKeys = System.Array.Empty<string>();
                data.StatValues = System.Array.Empty<float>();
                return data;
            }

            data.StatKeys = new string[total];
            data.StatValues = new float[total];

            int i = 0;

            // 写入基础 stats（跳过 __inv__:*）
            if (Runtime.RuntimeStats != null)
            {
                foreach (var kv in Runtime.RuntimeStats)
                {
                    if (IsInventoryStatKey(kv.Key)) continue;
                    data.StatKeys[i] = kv.Key;
                    data.StatValues[i] = kv.Value;
                    i++;
                }
            }

            // append 库存 stats（Key=__inv__:{itemId}, Value=count）
            for (int j = 0; j < invCount; j++)
            {
                var id = invIds[j];
                int c = (invCounts != null && j < invCounts.Length) ? invCounts[j] : 0;
                if (string.IsNullOrEmpty(id) || c <= 0) continue;

                data.StatKeys[i] = StorageRuntimeStatsCodec.ItemKeyPrefix + id;
                data.StatValues[i] = c;
                i++;
            }

            // 如果中途跳过了非法项，收缩数组
            if (i != total)
            {
                System.Array.Resize(ref data.StatKeys, i);
                System.Array.Resize(ref data.StatValues, i);
            }

            if (Runtime.FactoryInterior != null)
                data.InteriorBuildings = Runtime.FactoryInterior.CreateSaveData();

            return data;
        }

        /// <summary>
        /// summary: 将存档数据应用到当前建筑实例（路径B：行为绑定由外部 Spawn 流程负责）。
        /// param: data 存档数据
        /// return: 无
        /// </summary>
        public void ApplySaveData(SaveBuildingInstance data)
        {
            if (data == null) return;

            // 绑定 Def 引用（不改 Def.Id）
            Runtime ??= new BuildingRuntime();
            if (Runtime.Def == null || Runtime.Def.Id != data.DefId)
            {
                if (BuildingDatabase.TryGet(data.DefId, out var def))
                    Runtime.Def = def;
            }

            Runtime.BuildingID = data.RuntimeId;

            Runtime.RuntimeStats ??= new Dictionary<string, float>();
            Runtime.RuntimeStats.Clear();

            if (data.StatKeys != null && data.StatValues != null)
            {
                int len = Mathf.Min(data.StatKeys.Length, data.StatValues.Length);
                for (int i = 0; i < len; i++)
                {
                    var key = data.StatKeys[i];
                    var val = data.StatValues[i];
                    if (!string.IsNullOrEmpty(key))
                        Runtime.RuntimeStats[key] = val;
                }
            }

            // 库存：从 RuntimeStats 解码后交给 StorageSystem
            if (StorageRuntimeStatsCodec.TryDecodeInventory(Runtime.RuntimeStats, out var itemIds, out var counts))
            {
                if (StorageSystem.Instance != null)
                    StorageSystem.Instance.ApplyOrDeferImport(Runtime.BuildingID, itemIds, counts);
            }

            // 清理：把 __inv__:* 从 RuntimeStats 移除，避免后续保存重复写入
            StripInventoryStatKeys(Runtime.RuntimeStats);

            // 工厂：确保 interior，然后应用内部存档
            if (Runtime.Def != null && Runtime.Def.Category == BuildingCategory.Factory)
                Runtime.EnsureFactoryInterior();

            if (data.InteriorBuildings != null && data.InteriorBuildings.Count > 0 && Runtime.FactoryInterior != null)
                Runtime.FactoryInterior.ApplySaveData(data.InteriorBuildings);

            // 写入 placement（保证读档后再次保存一致）
            SetPlacement(new Vector3Int(data.CellX, data.CellY, 0), data.RotSteps);
        }
    }
}
