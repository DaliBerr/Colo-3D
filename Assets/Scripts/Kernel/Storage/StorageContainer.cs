using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kernel.Storage
{
    /// <summary>
    /// summary: 储物容器（按总数量容量，支持标签筛选与基础存取）。
    /// </summary>
    public sealed class StorageContainer
    {
        public long RuntimeId { get; private set; }
        public Vector2Int Cell { get; private set; }
        public int Capacity { get; private set; }
        public int Priority { get; private set; }
        public IReadOnlyList<string> AllowTags => _allowTags;

        private readonly List<string> _allowTags;
        private readonly Dictionary<string, int> _items = new();
        private int _used;

        /// <summary>
        /// summary: 构造一个储物容器。
        /// param: runtimeId 建筑运行时ID
        /// param: cell 容器所在格子
        /// param: capacity 容量（总数量）
        /// param: allowTags 允许标签（空=全收）
        /// param: priority 优先级（越大越优先）
        /// return: 无
        /// </summary>
        public StorageContainer(long runtimeId, Vector2Int cell, int capacity, List<string> allowTags, int priority)
        {
            RuntimeId = runtimeId;
            Cell = cell;
            Capacity = Mathf.Max(0, capacity);
            Priority = priority;

            _allowTags = allowTags != null ? new List<string>(allowTags) : new List<string>();
            _used = 0;
        }

        /// <summary>
        /// summary: 当前已使用容量（总数量）。
        /// return: used
        /// </summary>
        public int GetUsed() => _used;

        /// <summary>
        /// summary: 当前剩余容量（总数量）。
        /// return: free
        /// </summary>
        public int GetFree() => Mathf.Max(0, Capacity - _used);

        /// <summary>
        /// summary: 判断容器是否能接收该物品（按标签过滤）。
        /// param: itemTags 物品标签（可为空）
        /// return: 是否允许
        /// </summary>
        public bool CanAccept(IReadOnlyList<string> itemTags)
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
        /// param: itemTags 物品标签
        /// param: added 实际存入数量
        /// return: 是否存入成功（added>0）
        /// </summary>
        public bool TryAdd(string itemId, int count, IReadOnlyList<string> itemTags, out int added)
        {
            added = 0;
            if (string.IsNullOrEmpty(itemId) || count <= 0) return false;
            if (!CanAccept(itemTags)) return false;

            int free = GetFree();
            if (free <= 0) return false;

            added = Mathf.Min(count, free);

            _items.TryGetValue(itemId, out var old);
            _items[itemId] = old + added;
            _used += added;

            return added > 0;
        }

        /// <summary>
        /// summary: 尝试取出物品。
        /// param: itemId 物品ID
        /// param: count 请求数量
        /// param: removed 实际取出数量
        /// return: 是否取出成功（removed>0）
        /// </summary>
        public bool TryRemove(string itemId, int count, out int removed)
        {
            removed = 0;
            if (string.IsNullOrEmpty(itemId) || count <= 0) return false;

            if (!_items.TryGetValue(itemId, out var have) || have <= 0)
                return false;

            removed = Mathf.Min(count, have);

            int left = have - removed;
            if (left <= 0) _items.Remove(itemId);
            else _items[itemId] = left;

            _used = Mathf.Max(0, _used - removed);
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
        /// return: 无
        /// </summary>
        public void Import(string[] itemIds, int[] counts)
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
                _used += c;
            }

            // 防御性：如果存档数据超过容量，保持 used 不溢出但不强行丢弃（由上层决定是否裁剪）
            _used = Mathf.Max(0, _used);
        }
    }
}
