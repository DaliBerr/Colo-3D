using System;
using System.Collections.Generic;
using Lonize.Logging;
using UnityEngine;

namespace Kernel.Building
{
    [Serializable]
    public class FactoryChildRuntime
    {
        public BuildingRuntime Runtime;
        public Vector2Int CellPosition;
        public byte RotationSteps;
    }

    [Serializable]
    public class FactoryInteriorRuntime
    {
        public List<FactoryChildRuntime> Children = new();

        /// <summary>
        /// summary: 清空工厂内部的所有子建筑数据。
        /// param: 无
        /// return: 无
        /// </summary>
        public void Clear()
        {
            Children.Clear();
        }

        /// <summary>
        /// summary: 向工厂内部添加一个子建筑运行时数据。
        /// param: runtime 子建筑运行时
        /// param: cell 内部网格坐标
        /// param: rotSteps 旋转步数
        /// return: 是否添加成功
        /// </summary>
        public bool AddChild(BuildingRuntime runtime, Vector2Int cell, byte rotSteps)
        {
            if (runtime == null || runtime.Def == null)
                return false;

            runtime.CellPosition = cell;
            runtime.RotationSteps = (byte)(rotSteps & 3);

            Children.Add(new FactoryChildRuntime
            {
                Runtime = runtime,
                CellPosition = cell,
                RotationSteps = runtime.RotationSteps
            });

            return true;
        }

        /// <summary>
        /// summary: 生成工厂内部子建筑的存档数据。
        /// param: 无
        /// return: 子建筑存档列表
        /// </summary>
        public List<SaveFactoryBuildingInstance> CreateSaveData()
        {
            if (Children == null || Children.Count == 0)
                return new List<SaveFactoryBuildingInstance>();

            var list = new List<SaveFactoryBuildingInstance>(Children.Count);
            foreach (var child in Children)
            {
                if (child?.Runtime?.Def == null)
                    continue;

                var data = new SaveFactoryBuildingInstance
                {
                    DefId = child.Runtime.Def.Id,
                    RuntimeId = child.Runtime.BuildingID,
                    CellX = child.CellPosition.x,
                    CellY = child.CellPosition.y,
                    RotSteps = (byte)(child.RotationSteps & 3),
                    HP = child.Runtime.HP
                };

                ExportRuntimeStats(child.Runtime.RuntimeStats, out data.StatKeys, out data.StatValues);
                list.Add(data);
            }

            return list;
        }

        /// <summary>
        /// summary: 根据存档数据还原工厂内部子建筑列表。
        /// param: list 子建筑存档列表
        /// return: 无
        /// </summary>
        public void ApplySaveData(List<SaveFactoryBuildingInstance> list)
        {
            Clear();

            if (list == null || list.Count == 0)
                return;

            foreach (var data in list)
            {
                if (data == null)
                    continue;

                if (!BuildingDatabase.TryGet(data.DefId, out var def))
                {
                    GameDebug.LogWarning($"[FactoryInterior] 未找到子建筑 Def: {data.DefId}");
                    continue;
                }

                var runtime = new BuildingRuntime
                {
                    Def = def,
                    BuildingID = data.RuntimeId,
                    HP = data.HP,
                    Category = def.Category,
                    CellPosition = new Vector2Int(data.CellX, data.CellY),
                    RotationSteps = (byte)(data.RotSteps & 3)
                };

                ImportRuntimeStats(runtime.RuntimeStats, data.StatKeys, data.StatValues);

                Children.Add(new FactoryChildRuntime
                {
                    Runtime = runtime,
                    CellPosition = runtime.CellPosition,
                    RotationSteps = runtime.RotationSteps
                });
            }
        }

        /// <summary>
        /// summary: 构建内部小型建筑运行时数据（不实例化模型）。
        /// param: defId 建筑定义ID
        /// param: cell 内部网格坐标
        /// param: rotSteps 旋转步数
        /// return: 生成的运行时实例
        /// </summary>
        public BuildingRuntime CreateInternalRuntime(string defId, Vector2Int cell, byte rotSteps)
        {
            if (!BuildingDatabase.TryGet(defId, out var def))
            {
                GameDebug.LogWarning($"[FactoryInterior] 未找到子建筑 Def: {defId}");
                return null;
            }

            if (def.Category != BuildingCategory.Internal)
                GameDebug.LogWarning($"[FactoryInterior] 子建筑 Def 类型不是 Internal: {defId}");

            var runtime = new BuildingRuntime
            {
                Def = def,
                BuildingID = BuildingIDManager.GenerateBuildingID(),
                HP = def.MaxHP,
                Category = def.Category,
                CellPosition = cell,
                RotationSteps = (byte)(rotSteps & 3)
            };

            AddChild(runtime, cell, rotSteps);
            return runtime;
        }

        /// <summary>
        /// summary: 将运行时统计字典导出为数组。
        /// param: stats 运行时统计字典
        /// param: keys 输出键数组
        /// param: values 输出值数组
        /// return: 无
        /// </summary>
        private static void ExportRuntimeStats(Dictionary<string, float> stats, out string[] keys, out float[] values)
        {
            if (stats == null || stats.Count == 0)
            {
                keys = Array.Empty<string>();
                values = Array.Empty<float>();
                return;
            }

            keys = new string[stats.Count];
            values = new float[stats.Count];

            int i = 0;
            foreach (var kv in stats)
            {
                keys[i] = kv.Key;
                values[i] = kv.Value;
                i++;
            }
        }

        /// <summary>
        /// summary: 将统计数组写回运行时字典。
        /// param: stats 运行时统计字典
        /// param: keys 键数组
        /// param: values 值数组
        /// return: 无
        /// </summary>
        private static void ImportRuntimeStats(Dictionary<string, float> stats, string[] keys, float[] values)
        {
            stats ??= new Dictionary<string, float>();
            stats.Clear();

            if (keys == null || values == null)
                return;

            int len = Mathf.Min(keys.Length, values.Length);
            for (int i = 0; i < len; i++)
            {
                var key = keys[i];
                if (string.IsNullOrEmpty(key))
                    continue;

                stats[key] = values[i];
            }
        }
    }
}
