using System;
using System.Collections.Generic;
using UnityEngine;
using Kernel.Inventory;

namespace Kernel.Storage
{
    /// <summary>
    /// summary: 全局储物系统（注册/注销容器，查找最佳容器，存取操作，存档回填）。
    /// </summary>
    public sealed class StorageSystem
    {
        private static readonly StorageSystem _instance = new StorageSystem();
        public static StorageSystem Instance => _instance;

        public IItemCatalog ItemCatalog { get; set; }

        /// <summary>
        /// summary: 当任意容器内容变化时触发（可用于 UI/搬运系统转发到 EventBus）。
        /// </summary>
        public event Action<long> OnContainerChanged;

        private readonly Dictionary<long, StorageContainer> _containers = new();
        private readonly Dictionary<long, (string[] itemIds, int[] counts)> _pendingImports = new();

        private StorageSystem() { }

        /// <summary>
        /// summary: 清空所有容器与挂起回填（新游戏/切档时调用）。
        /// return: 无
        /// </summary>
        public void ClearAll()
        {
            _containers.Clear();
            _pendingImports.Clear();
        }

        /// <summary>
        /// summary: 注册一个容器；若存在挂起回填则自动导入。
        /// param: runtimeId 建筑运行时ID
        /// param: cell 容器所在格
        /// param: capacity 容量
        /// param: allowTags 允许标签
        /// param: allowItemIds 允许物品ID
        /// param: filterMode 过滤模式
        /// param: priority 优先级（越大越优先）
        /// return: 注册后的容器实例
        /// </summary>
        public StorageContainer Register(
            long runtimeId,
            Vector2Int cell,
            int capacity,
            List<string> allowTags,
            List<string> allowItemIds,
            StorageFilterMode filterMode,
            int priority)
        {
            if (runtimeId <= 0)
                throw new ArgumentException("runtimeId must be > 0");

            var c = new StorageContainer(runtimeId, cell, capacity, allowTags, allowItemIds, filterMode, priority);
            _containers[runtimeId] = c;

            if (_pendingImports.TryGetValue(runtimeId, out var pending))
            {
                var occupations = BuildOccupations(pending.itemIds);
                c.Import(pending.itemIds, pending.counts, occupations);
                _pendingImports.Remove(runtimeId);
                OnContainerChanged?.Invoke(runtimeId);
            }

            return c;
        }

        /// <summary>
        /// summary: 注销容器（建筑销毁/拆除时调用）。
        /// param: runtimeId 建筑运行时ID
        /// return: 是否成功注销
        /// </summary>
        public bool Unregister(long runtimeId)
        {
            return _containers.Remove(runtimeId);
        }

        /// <summary>
        /// summary: 尝试获取容器。
        /// param: runtimeId 建筑运行时ID
        /// param: container 输出容器
        /// return: 是否存在
        /// </summary>
        public bool TryGet(long runtimeId, out StorageContainer container)
        {
            return _containers.TryGetValue(runtimeId, out container);
        }

        /// <summary>
        /// summary: 更新指定容器的标签过滤并触发变化事件。
        /// param: runtimeId 建筑运行时ID
        /// param: tags 允许标签列表（空=全收）
        /// return: 是否成功更新
        /// </summary>
        public bool UpdateContainerFilter(long runtimeId, List<string> tags)
        {
            if (!TryGet(runtimeId, out var c)) return false;

            c.UpdateAllowTags(tags);
            OnContainerChanged?.Invoke(runtimeId);
            return true;
        }

        /// <summary>
        /// summary: 更新指定容器的过滤参数并触发变化事件。
        /// param: runtimeId 建筑运行时ID
        /// param: tags 允许标签列表（空=全收）
        /// param: itemIds 允许物品ID列表（空=全收）
        /// param: filterMode 过滤模式
        /// return: 是否成功更新
        /// </summary>
        public bool UpdateContainerFilter(long runtimeId, List<string> tags, List<string> itemIds, StorageFilterMode filterMode)
        {
            if (!TryGet(runtimeId, out var c)) return false;

            c.UpdateAllowTags(tags);
            c.UpdateAllowItemIds(itemIds);
            c.UpdateFilterMode(filterMode);
            OnContainerChanged?.Invoke(runtimeId);
            return true;
        }

        /// <summary>
        /// summary: 设置容器拒绝全部标记并触发变化事件。
        /// param: runtimeId 建筑运行时ID
        /// param: rejectAll 是否拒绝全部
        /// return: 是否成功更新
        /// </summary>
        public bool SetContainerRejectAll(long runtimeId, bool rejectAll)
        {
            if (!TryGet(runtimeId, out var c)) return false;

            c.SetRejectAll(rejectAll);
            OnContainerChanged?.Invoke(runtimeId);
            return true;
        }

        /// <summary>
        /// summary: 选择最佳存入容器（优先级高优先，其次距离近）。
        /// param: itemId 物品ID
        /// param: fromCell 起点格子
        /// param: best 输出最佳容器
        /// return: 是否找到
        /// </summary>
        public bool TryFindBestForStore(string itemId, Vector2Int fromCell, out StorageContainer best)
        {
            best = null;
            var tags = ResolveTags(itemId);
            int occupation = ResolveOccupation(itemId);

            int bestPriority = int.MinValue;
            int bestDist2 = int.MaxValue;

            foreach (var kv in _containers)
            {
                var c = kv.Value;
                if (c.GetFree() < occupation) continue;
                if (!c.CanAccept(itemId, tags)) continue;

                int pr = c.Priority;
                int dist2 = (c.Cell.x - fromCell.x) * (c.Cell.x - fromCell.x) + (c.Cell.y - fromCell.y) * (c.Cell.y - fromCell.y);

                if (pr > bestPriority || (pr == bestPriority && dist2 < bestDist2))
                {
                    bestPriority = pr;
                    bestDist2 = dist2;
                    best = c;
                }
            }

            return best != null;
        }

        /// <summary>
        /// summary: 从最佳容器尝试存入物品。
        /// param: itemId 物品ID
        /// param: count 请求数量
        /// param: fromCell 起点格子
        /// param: stored 实际存入
        /// param: containerId 实际存入的容器ID
        /// return: 是否成功（stored>0）
        /// </summary>
        public bool TryStoreToBest(string itemId, int count, Vector2Int fromCell, out int stored, out long containerId)
        {
            stored = 0;
            containerId = 0;

            if (!TryFindBestForStore(itemId, fromCell, out var c))
                return false;

            var tags = ResolveTags(itemId);
            int occupation = ResolveOccupation(itemId);
            if (!c.TryAdd(itemId, count, occupation, tags, out stored))
                return false;

            containerId = c.RuntimeId;
            OnContainerChanged?.Invoke(containerId);
            return stored > 0;
        }

        /// <summary>
        /// summary: 从某个容器尝试取出物品。
        /// param: containerId 容器ID
        /// param: itemId 物品ID
        /// param: count 请求数量
        /// param: removed 实际取出
        /// return: 是否成功（removed>0）
        /// </summary>
        public bool TryTake(long containerId, string itemId, int count, out int removed)
        {
            removed = 0;
            if (!TryGet(containerId, out var c)) return false;

            int occupation = ResolveOccupation(itemId);
            if (!c.TryRemove(itemId, count, occupation, out removed))
                return false;

            OnContainerChanged?.Invoke(containerId);
            return removed > 0;
        }

        /// <summary>
        /// summary: 将存档内容应用到容器；若容器未注册则先挂起等待。
        /// param: runtimeId 容器对应建筑运行时ID
        /// param: itemIds 物品ID数组
        /// param: counts 数量数组
        /// return: 无
        /// </summary>
        public void ApplyOrDeferImport(long runtimeId, string[] itemIds, int[] counts)
        {
            if (runtimeId <= 0) return;

            if (_containers.TryGetValue(runtimeId, out var c))
            {
                var occupations = BuildOccupations(itemIds);
                c.Import(itemIds, counts, occupations);
                OnContainerChanged?.Invoke(runtimeId);
                return;
            }

            _pendingImports[runtimeId] = (itemIds, counts);
        }

        /// <summary>
        /// summary: 构建所有容器的存档快照（由存档系统遍历写入）。
        /// return: 容器快照列表
        /// </summary>
        public List<(long runtimeId, string[] itemIds, int[] counts)> BuildSaveSnapshots()
        {
            var list = new List<(long, string[], int[])>(_containers.Count);
            foreach (var kv in _containers)
            {
                kv.Value.Export(out var ids, out var cs);
                list.Add((kv.Key, ids, cs));
            }
            return list;
        }

        /// <summary>
        /// summary: 解析物品标签（无 Catalog 时返回 null）。
        /// param: itemId 物品ID
        /// return: 标签列表或 null
        /// </summary>
        private IReadOnlyList<string> ResolveTags(string itemId)
        {
            if (ItemCatalog == null) return null;
            return ItemCatalog.TryGetTags(itemId, out var tags) ? tags : null;
        }

        /// <summary>
        /// summary: 解析物品占用值（无 Catalog 时默认 1）。
        /// param: itemId 物品ID
        /// return: 占用值
        /// </summary>
        private int ResolveOccupation(string itemId)
        {
            if (ItemCatalog == null) return 1;
            return ItemCatalog.TryGetStorageOccupation(itemId, out var occupation) && occupation > 0 ? occupation : 1;
        }

        /// <summary>
        /// summary: 构建占用值数组（与 itemIds 对齐）。
        /// param: itemIds 物品ID数组
        /// return: 占用值数组
        /// </summary>
        private int[] BuildOccupations(string[] itemIds)
        {
            if (itemIds == null) return null;

            var occupations = new int[itemIds.Length];
            for (int i = 0; i < itemIds.Length; i++)
            {
                occupations[i] = ResolveOccupation(itemIds[i]);
            }
            return occupations;
        }
    }
}
