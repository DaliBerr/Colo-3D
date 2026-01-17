using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kernel.Storage
{
    /// <summary>
    /// summary: 储物容器过滤模式（按标签/ID/组合规则）。
    /// </summary>
    public enum StorageFilterMode
    {
        TagOnly,
        IdOnly,
        TagAndId,
        TagOrId
    }

    /// <summary>
    /// summary: 储物容器（按总占用容量，支持标签筛选与基础存取）。
    /// </summary>
    public sealed class StorageContainer
    {
        public long RuntimeId { get; private set; }
        public Vector2Int Cell { get; private set; }
        public int Capacity { get; private set; }
        public int Priority { get; private set; }
        public IReadOnlyList<string> AllowTags => _allowTags;
        public IReadOnlyList<string> AllowItemIds => _allowItemIds;
        public StorageFilterMode FilterMode { get; private set; }
        public bool RejectAll { get; private set; }

        private readonly List<string> _allowTags;
        private readonly List<string> _allowItemIds;
        private readonly Dictionary<string, int> _items = new();
        private int _used;

        /// <summary>
        /// summary: 构造一个储物容器。
        /// param: runtimeId 建筑运行时ID
        /// param: cell 容器所在格子
        /// param: capacity 容量（总占用）
        /// param: allowTags 允许标签（空=全收）
        /// param: allowItemIds 允许物品ID（空=全收）
        /// param: filterMode 过滤模式
        /// param: priority 优先级（越大越优先）
        /// return: 无
        /// </summary>
        public StorageContainer(
            long runtimeId,
            Vector2Int cell,
            int capacity,
            List<string> allowTags,
            List<string> allowItemIds,
            StorageFilterMode filterMode,
            int priority)
        {
            RuntimeId = runtimeId;
            Cell = cell;
            Capacity = Mathf.Max(0, capacity);
            Priority = priority;
            FilterMode = filterMode;

            _allowTags = allowTags != null ? new List<string>(allowTags) : new List<string>();
            _allowItemIds = allowItemIds != null ? new List<string>(allowItemIds) : new List<string>();
            _used = 0;
            RejectAll = false;
        }

        /// <summary>
        /// summary: 更新容器允许的标签过滤（空=全收）。
        /// param: tags 允许标签列表
        /// return: 无
        /// </summary>
        public void UpdateAllowTags(List<string> tags)
        {
            RejectAll = false;
            _allowTags.Clear();
            if (tags == null || tags.Count == 0)
                return;

            _allowTags.AddRange(tags);
        }

        /// <summary>
        /// summary: 更新容器允许的物品ID过滤（空=全收）。
        /// param: itemIds 允许物品ID列表
        /// return: 无
        /// </summary>
        public void UpdateAllowItemIds(List<string> itemIds)
        {
            RejectAll = false;
            _allowItemIds.Clear();
            if (itemIds == null || itemIds.Count == 0)
                return;

            _allowItemIds.AddRange(itemIds);
        }

        /// <summary>
        /// summary: 更新容器过滤模式。
        /// param: filterMode 过滤模式
        /// return: 无
        /// </summary>
        public void UpdateFilterMode(StorageFilterMode filterMode)
        {
            FilterMode = filterMode;
        }

        /// <summary>
        /// summary: 设置容器是否拒绝全部物品。
        /// param: rejectAll 是否拒绝全部
        /// return: 无
        /// </summary>
        public void SetRejectAll(bool rejectAll)
        {
            RejectAll = rejectAll;
        }

        /// <summary>
        /// summary: 当前已使用容量（占用总量）。
        /// return: used
        /// </summary>
        public int GetUsed() => _used;

        /// <summary>
        /// summary: 当前剩余容量（占用总量）。
        /// return: free
        /// </summary>
        public int GetFree() => Mathf.Max(0, Capacity - _used);

        /// <summary>
        /// summary: 判断容器是否能接收该物品（按过滤模式）。
        /// param: itemId 物品ID
        /// param: itemTags 物品标签（可为空）
        /// return: 是否允许
        /// </summary>
        public bool CanAccept(string itemId, IReadOnlyList<string> itemTags)
        {
            if (RejectAll)
                return false;

            bool tagAllowed = IsTagAllowed(itemTags);
            bool idAllowed = IsIdAllowed(itemId);

            switch (FilterMode)
            {
                case StorageFilterMode.TagOnly:
                    return tagAllowed;
                case StorageFilterMode.IdOnly:
                    return idAllowed;
                case StorageFilterMode.TagAndId:
                    return tagAllowed && idAllowed;
                case StorageFilterMode.TagOrId:
                    return tagAllowed || idAllowed;
                default:
                    return tagAllowed;
            }
        }

        /// <summary>
        /// summary: 检查标签是否允许。
        /// param: itemTags 物品标签
        /// return: 是否允许
        /// </summary>
        private bool IsTagAllowed(IReadOnlyList<string> itemTags)
        {
            if (_allowTags == null || _allowTags.Count == 0)
                return true;

            if (itemTags == null || itemTags.Count == 0)
                return false;

            for (int i = 0; i < itemTags.Count; i++)
            {
                if (_allowTags.Contains(itemTags[i]))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// summary: 检查物品ID是否允许。
        /// param: itemId 物品ID
        /// return: 是否允许
        /// </summary>
        private bool IsIdAllowed(string itemId)
        {
            if (_allowItemIds == null || _allowItemIds.Count == 0)
                return true;

            return !string.IsNullOrEmpty(itemId) && _allowItemIds.Contains(itemId);
        }

        /// <summary>
        /// summary: 获取某物品当前数量。
        /// param: itemId 物品ID
        /// return: 数量（不存在为0）
        /// </summary>
        public int GetCount(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            return _items.TryGetValue(itemId, out var c) ? c : 0;
        }

        /// <summary>
        /// summary: 尝试存入物品（受容量与标签限制）。
        /// param: itemId 物品ID
        /// param: count 请求数量
        /// param: occupation 单件占用值
        /// param: itemTags 物品标签
        /// param: added 实际存入数量
        /// return: 是否存入成功（added>0）
        /// </summary>
        public bool TryAdd(string itemId, int count, int occupation, IReadOnlyList<string> itemTags, out int added)
        {
            added = 0;
            if (string.IsNullOrEmpty(itemId) || count <= 0) return false;
            if (!CanAccept(itemId, itemTags)) return false;

            int free = GetFree();
            if (free <= 0) return false;

            int per = Mathf.Max(1, occupation);
            int maxByFree = free / per;
            if (maxByFree <= 0) return false;

            added = Mathf.Min(count, maxByFree);

            _items.TryGetValue(itemId, out var old);
            _items[itemId] = old + added;
            _used += added * per;

            return added > 0;
        }

        /// <summary>
        /// summary: 尝试取出物品。
        /// param: itemId 物品ID
        /// param: count 请求数量
        /// param: occupation 单件占用值
        /// param: removed 实际取出数量
        /// return: 是否取出成功（removed>0）
        /// </summary>
        public bool TryRemove(string itemId, int count, int occupation, out int removed)
        {
            removed = 0;
            if (string.IsNullOrEmpty(itemId) || count <= 0) return false;

            if (!_items.TryGetValue(itemId, out var have) || have <= 0)
                return false;

            removed = Mathf.Min(count, have);

            int left = have - removed;
            if (left <= 0) _items.Remove(itemId);
            else _items[itemId] = left;

            int per = Mathf.Max(1, occupation);
            _used = Mathf.Max(0, _used - removed * per);
            return removed > 0;
        }

        /// <summary>
        /// summary: 导出容器内容为两个数组（用于存档）。
        /// param: itemIds 输出物品ID数组
        /// param: counts 输出数量数组
        /// return: 无
        /// </summary>
        public void Export(out string[] itemIds, out int[] counts)
        {
            int n = _items.Count;
            itemIds = new string[n];
            counts = new int[n];

            int i = 0;
            foreach (var kv in _items)
            {
                itemIds[i] = kv.Key;
                counts[i] = kv.Value;
                i++;
            }
        }

        /// <summary>
        /// summary: 用数组覆盖容器内容（用于读档回填）。
        /// param: itemIds 物品ID数组
        /// param: counts 数量数组
        /// param: occupations 占用值数组
        /// return: 无
        /// </summary>
        public void Import(string[] itemIds, int[] counts, int[] occupations)
        {
            _items.Clear();
            _used = 0;

            if (itemIds == null || counts == null) return;

            int len = Mathf.Min(itemIds.Length, counts.Length);
            for (int i = 0; i < len; i++)
            {
                var id = itemIds[i];
                var c = counts[i];
                if (string.IsNullOrEmpty(id) || c <= 0) continue;

                _items[id] = c;
                int per = 1;
                if (occupations != null && i < occupations.Length)
                {
                    per = Mathf.Max(1, occupations[i]);
                }
                _used += c * per;
            }

            // 防御性：如果存档数据超过容量，保持 used 不溢出但不强行丢弃（由上层决定是否裁剪）
            _used = Mathf.Max(0, _used);
        }
    }
}
