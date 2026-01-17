using System;
using System.Collections.Generic;
using Kernel.Factory.Connections;
using Kernel.Storage;

namespace Kernel.Building
{
    /// <summary>
    /// summary: 工厂接口过滤合并策略解析器，将多个接口 allowTags 合成为容器 allowTags。
    /// </summary>
    public sealed class FactoryFilterResolver
    {
        /// <summary>
        /// summary: 工厂过滤合并结果（标签、ID 与过滤模式）。
        /// </summary>
        public readonly struct FactoryFilterResult
        {
            public readonly List<string> AllowTags;
            public readonly List<string> AllowItemIds;
            public readonly StorageFilterMode FilterMode;

            /// <summary>
            /// summary: 创建过滤合并结果。
            /// param: allowTags 允许标签列表
            /// param: allowItemIds 允许物品ID列表
            /// param: filterMode 过滤模式
            /// return: 无
            /// </summary>
            public FactoryFilterResult(List<string> allowTags, List<string> allowItemIds, StorageFilterMode filterMode)
            {
                AllowTags = allowTags;
                AllowItemIds = allowItemIds;
                FilterMode = filterMode;
            }
        }

        /// <summary>
        /// summary: 解析内部接口过滤条件，按并集策略输出容器过滤参数。
        /// param: providers 内部接口过滤提供者集合
        /// return: 合并后的过滤结果
        /// </summary>
        public FactoryFilterResult ResolveFilters(IEnumerable<IInteriorIOFilterProvider> providers)
        {
            var mergedTags = new HashSet<string>(StringComparer.Ordinal);
            var mergedItemIds = new HashSet<string>(StringComparer.Ordinal);
            var mergedMode = StorageFilterMode.TagOnly;
            if (providers == null)
            {
                return new FactoryFilterResult(new List<string>(), new List<string>(), mergedMode);
            }

            foreach (var provider in providers)
            {
                if (provider == null || provider is IInteriorCacheStorage || !provider.IsExternalInterface)
                {
                    continue;
                }

                var tags = provider?.GetIOAllowTags();
                if (tags != null && tags.Count > 0)
                {
                    foreach (var tag in tags)
                    {
                        if (string.IsNullOrWhiteSpace(tag))
                        {
                            continue;
                        }

                        mergedTags.Add(tag);
                    }
                }

                var itemIds = provider.GetIOAllowItemIds();
                if (itemIds != null && itemIds.Count > 0)
                {
                    foreach (var itemId in itemIds)
                    {
                        if (string.IsNullOrWhiteSpace(itemId))
                        {
                            continue;
                        }

                        mergedItemIds.Add(itemId);
                    }
                }

                mergedMode = MergeFilterMode(mergedMode, provider.GetIOFilterMode());
            }

            return new FactoryFilterResult(new List<string>(mergedTags), new List<string>(mergedItemIds), mergedMode);
        }

        /// <summary>
        /// summary: 合并多个接口过滤模式（优先更宽松的组合模式）。
        /// param: current 当前合并模式
        /// param: incoming 新的过滤模式
        /// return: 合并后的过滤模式
        /// </summary>
        private static StorageFilterMode MergeFilterMode(StorageFilterMode current, StorageFilterMode incoming)
        {
            if (current == StorageFilterMode.TagOrId || incoming == StorageFilterMode.TagOrId)
            {
                return StorageFilterMode.TagOrId;
            }

            if (current == StorageFilterMode.TagAndId || incoming == StorageFilterMode.TagAndId)
            {
                return StorageFilterMode.TagAndId;
            }

            if (current == StorageFilterMode.IdOnly || incoming == StorageFilterMode.IdOnly)
            {
                return StorageFilterMode.IdOnly;
            }

            return StorageFilterMode.TagOnly;
        }
    }
}
