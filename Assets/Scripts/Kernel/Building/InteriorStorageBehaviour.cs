using System;
using System.Collections.Generic;
using Kernel.Factory.Connections;

namespace Kernel.Building
{
    /// <summary>
    /// summary: 用于存储交互的内部建筑行为基类（提供接口过滤变更事件）。
    /// </summary>
    public abstract class InteriorStorageBehaviour : BaseInteriorBehaviour, IInteriorIOFilterProvider
    {
        private readonly List<string> _ioAllowTags = new();
        private bool _isExternalInterface = true;

        /// <summary>
        /// summary: 接口过滤参数变化事件（过滤条件变化时触发）。
        /// </summary>
        public event Action<IInteriorIOFilterProvider> OnIOFilterChanged;

        /// <summary>
        /// summary: 获取当前是否作为外部接口参与过滤汇总。
        /// param: 无
        /// return: 是否启用外部接口
        /// </summary>
        public bool IsExternalInterface => _isExternalInterface;

        /// <summary>
        /// summary: 获取当前接口允许的标签列表。
        /// param: 无
        /// return: 允许标签列表（空=全收）
        /// </summary>
        public IReadOnlyList<string> GetIOAllowTags()
        {
            if (!_isExternalInterface)
            {
                return Array.Empty<string>();
            }

            return _ioAllowTags;
        }

        /// <summary>
        /// summary: 设置接口允许标签并触发变更事件。
        /// param: tags 允许标签集合
        /// return: 无
        /// </summary>
        protected void SetIOAllowTags(IEnumerable<string> tags)
        {
            var normalized = NormalizeAllowTags(tags);
            if (AreAllowTagsEqual(_ioAllowTags, normalized))
            {
                return;
            }

            _ioAllowTags.Clear();
            _ioAllowTags.AddRange(normalized);
            if (!_isExternalInterface)
            {
                return;
            }

            OnIOFilterChanged?.Invoke(this);
        }

        /// <summary>
        /// summary: 设置外部接口启用状态并通知过滤变更。
        /// param: enabled 是否启用外部接口
        /// return: 无
        /// </summary>
        public void SetExternalInterfaceEnabled(bool enabled)
        {
            if (_isExternalInterface == enabled)
            {
                return;
            }

            _isExternalInterface = enabled;
            OnIOFilterChanged?.Invoke(this);
        }

        /// <summary>
        /// summary: 规范化标签列表（去空、去重并保持稳定顺序）。
        /// param: tags 原始标签集合
        /// return: 规范化后的标签列表
        /// </summary>
        private static List<string> NormalizeAllowTags(IEnumerable<string> tags)
        {
            if (tags == null)
            {
                return new List<string>();
            }

            var result = new List<string>();
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                {
                    continue;
                }

                if (set.Add(tag))
                {
                    result.Add(tag);
                }
            }

            return result;
        }

        /// <summary>
        /// summary: 比较两组标签是否一致。
        /// param: current 当前标签列表
        /// param: incoming 新标签列表
        /// return: 是否一致
        /// </summary>
        private static bool AreAllowTagsEqual(IReadOnlyList<string> current, IReadOnlyList<string> incoming)
        {
            if (ReferenceEquals(current, incoming))
            {
                return true;
            }

            if (current == null || incoming == null)
            {
                return false;
            }

            if (current.Count != incoming.Count)
            {
                return false;
            }

            for (int i = 0; i < current.Count; i++)
            {
                if (!string.Equals(current[i], incoming[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
