using System.Collections.Generic;
using UnityEngine;
using Kernel.World;
using Lonize.Logging;

namespace Kernel.Building
{
    /// <summary>
    /// summary: 建筑实例宿主，持有 Runtime 与行为；并提供基于 WorldGrid 的存档数据生成/应用。
    /// </summary>
    public class BuildingRuntimeHost : MonoBehaviour
    {
        public BuildingRuntime Runtime;
        public List<IBuildingBehaviour> Behaviours = new();

        [Header("Placement (Grid)")]
        [SerializeField] private int _cellX;
        [SerializeField] private int _cellZ;
        [SerializeField] private byte _rotSteps;
        [SerializeField] private bool _hasPlacement;

        /// <summary>
        /// summary: 写入放置数据（由放置系统在落地时调用）。
        /// param: anchorCell 锚点格（x=cellX, y=cellZ）
        /// param: rotSteps 旋转步数（0..3，绕Y轴）
        /// return: 无
        /// </summary>
        public void SetPlacement(Vector3Int anchorCell, byte rotSteps)
        {
            _cellX = anchorCell.x;
            _cellZ = anchorCell.y;
            _rotSteps = (byte)(rotSteps & 3);
            _hasPlacement = true;
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
            GameDebug.Log($"[BuildingRuntimeHost] TryGetPlacement: anchorCell=({anchorCell.x}, {anchorCell.y}), rotSteps={_rotSteps}");
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
                cellPos = worldGrid != null ? worldGrid.WorldToCellXZ(transform.position)
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
                HP = Runtime.HP
            };

            if (Runtime.RuntimeStats != null && Runtime.RuntimeStats.Count > 0)
            {
                int count = Runtime.RuntimeStats.Count;
                data.StatKeys = new string[count];
                data.StatValues = new float[count];

                int i = 0;
                foreach (var kv in Runtime.RuntimeStats)
                {
                    data.StatKeys[i] = kv.Key;
                    data.StatValues[i] = kv.Value;
                    i++;
                }
            }
            else
            {
                data.StatKeys = System.Array.Empty<string>();
                data.StatValues = System.Array.Empty<float>();
            }

            return data;
        }

        /// <summary>
        /// summary: 将存档数据应用到当前建筑实例（仅应用 Runtime 状态；放置信息由外部决定是否 SetPlacement）。
        /// param: data 存档数据
        /// return: 无
        /// </summary>
        public void ApplySaveData(SaveBuildingInstance data)
        {
            if (data == null || Runtime == null)
                return;

            Runtime.BuildingID = data.RuntimeId;
            Runtime.Def.Id = data.DefId;
            Runtime.HP = data.HP;

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

            // 同时写入 placement（让拆除/再存档一致）
            SetPlacement(new Vector3Int(data.CellX, data.CellY, 0), data.RotSteps);
        }
    }
}
