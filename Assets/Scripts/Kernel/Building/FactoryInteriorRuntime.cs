using System;
using System.Collections.Generic;
using Kernel.Factory.Connections;
using Lonize.Logging;
using UnityEngine;

namespace Kernel.Building
{
    [Serializable]
    public class FactoryChildRuntime
    {
        // public BuildingRuntime Runtime;
        public BuildingDef Def;
        public long BuildingParentID;
        public int BuildingLocalID;
        public Vector2Int CellPosition;
        public Dictionary<string, float> RuntimeStats = new();
        public BuildingCategory Category = BuildingCategory.Internal;
        public bool IsExternalInterface = true;

        public List<IBuildingBehaviour> Behaviours = new();
        public BuildingRuntime ProxyRuntime;
    }

    [Serializable]
    public class FactoryInteriorRuntime
    {
        public List<FactoryChildRuntime> Children = new();
        public FactoryInteriorConnectionsRuntime Connections = new();
        public List<SaveFactoryConnectionLink> InteriorLinks = new();
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
        // public bool AddChild(BuildingRuntime runtime, Vector2Int cell, byte rotSteps)
        // {
        //     if (runtime == null || runtime.Def == null)
        //         return false;

        //     // 将运行时信息写回 runtime（保持 runtime 自身的状态），
        //     // 但 FactoryChildRuntime 不再存储 RotationSteps，因此不记录旋转。
        //     runtime.CellPosition = cell;
        //     runtime.RotationSteps = (byte)(rotSteps & 3);

        //     var child = new FactoryChildRuntime
        //     {
        //         Def = runtime.Def,
        //         BuildingParentID = runtime.BuildingID,
        //         // BuildingLocalID = runtime.BuildingLocalID,
        //         CellPosition = cell,
        //         Category = runtime.Category,
        //         RuntimeStats = runtime.RuntimeStats != null
        //             ? new Dictionary<string, float>(runtime.RuntimeStats)
        //             : new Dictionary<string, float>()
        //     };

        //     Children.Add(child);
        //     return true;
        // }

        /// <summary>
        /// summary: 生成工厂内部子建筑的存档数据。
        /// param: 无
        /// return: 子建筑存档列表
        /// </summary>
        public List<SaveFactoryBuildingInstance> CreateSaveData()
        {
            Connections ??= new FactoryInteriorConnectionsRuntime();
            if (Children == null || Children.Count == 0)
            {
                InteriorLinks = Connections != null
                    ? Connections.ExportLinksForSave()
                    : new List<SaveFactoryConnectionLink>();
                return new List<SaveFactoryBuildingInstance>();
            }

            var list = new List<SaveFactoryBuildingInstance>(Children.Count);
            foreach (var child in Children)
            {
                if (child == null || child.Def == null)
                    continue;

                var data = new SaveFactoryBuildingInstance
                {
                    DefId = child.Def.Id,
                    ParentId = child.BuildingParentID,
                    localId = child.BuildingLocalID,
                    CellX = child.CellPosition.x,
                    CellY = child.CellPosition.y,
                    IsExternalInterface = ResolveExternalInterfaceState(child)
                };

                ExportRuntimeStats(child.RuntimeStats, out data.StatKeys, out data.StatValues);
                list.Add(data);
            }

            InteriorLinks = Connections != null
                ? Connections.ExportLinksForSave()
                : new List<SaveFactoryConnectionLink>();

            return list;
        }

        /// <summary>
        /// summary: 根据存档数据还原工厂内部子建筑列表与连接缓存。
        /// param: list 子建筑存档列表
        /// param: links 连接存档列表
        /// return: 无
        /// </summary>
        public void ApplySaveData(List<SaveFactoryBuildingInstance> list, List<SaveFactoryConnectionLink> links = null)
        {
            Clear();
            Connections ??= new FactoryInteriorConnectionsRuntime();
            InteriorLinks = links != null
                ? new List<SaveFactoryConnectionLink>(links)
                : new List<SaveFactoryConnectionLink>();

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

                var child = new FactoryChildRuntime
                {
                    Def = def,
                    BuildingParentID = data.ParentId,
                    BuildingLocalID = data.localId,
                    Category = def.Category,
                    CellPosition = new Vector2Int(data.CellX, data.CellY),
                    IsExternalInterface = data.IsExternalInterface
                };

                ImportRuntimeStats(child.RuntimeStats, data.StatKeys, data.StatValues);

                Children.Add(child);
                // initializeInternalBehaviours(child);
            }
        }

        /// <summary>
        /// summary: 构建内部小型建筑运行时数据（不实例化模型）。
        /// param: defId 建筑定义ID
        /// param: cell 内部网格坐标
        /// param: rotSteps 旋转步数
        /// return: 生成的运行时实例
        /// </summary>
        // public BuildingRuntime CreateInternalRuntime(string defId, Vector2Int cell, byte rotSteps)
        // {
        //     if (!BuildingDatabase.TryGet(defId, out var def))
        //     {
        //         GameDebug.LogWarning($"[FactoryInterior] 未找到子建筑 Def: {defId}");
        //         return null;
        //     }

        //     if (def.Category != BuildingCategory.Internal)
        //         GameDebug.LogWarning($"[FactoryInterior] 子建筑 Def 类型不是 Internal: {defId}");

        //     var runtime = new BuildingRuntime
        //     {
        //         Def = def,
        //         BuildingID = BuildingIDManager.GenerateBuildingID(),
        //         Category = def.Category,
        //         CellPosition = cell,
        //         RotationSteps = (byte)(rotSteps & 3)
        //     };

        //     AddChild(runtime, cell, rotSteps);
        //     return runtime;
        // }

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
        /// summary: 解析子建筑当前外部接口启用状态。
        /// param: child 子建筑运行时数据
        /// return: 是否启用外部接口
        /// </summary>
        private static bool ResolveExternalInterfaceState(FactoryChildRuntime child)
        {
            if (child?.Behaviours != null)
            {
                foreach (var behaviour in child.Behaviours)
                {
                    if (behaviour is IInteriorIOFilterProvider provider)
                    {
                        return provider.IsExternalInterface;
                    }
                }
            }

            return child?.IsExternalInterface ?? true;
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
