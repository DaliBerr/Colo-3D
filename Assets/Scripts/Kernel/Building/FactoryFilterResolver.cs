using System;
using System.Collections.Generic;
using Kernel.Factory.Connections;

namespace Kernel.Building
{
    /// <summary>
    /// summary: 工厂接口过滤合并策略解析器，将多个接口 allowTags 合成为容器 allowTags。
    /// </summary>
    public sealed class FactoryFilterResolver
    {
        /// <summary>
        /// summary: 解析内部接口过滤标签，按并集策略输出容器允许标签；空集合表示全收。
        /// param: providers 内部接口过滤提供者集合
        /// return: 合并后的 allowTags 列表
        /// </summary>
        public List<string> ResolveAllowTags(IEnumerable<IInteriorIOFilterProvider> providers)
        {
            var mergedTags = new HashSet<string>(StringComparer.Ordinal);
            if (providers == null)
            {
                return new List<string>();
            }

            foreach (var provider in providers)
            {
                var tags = provider?.GetIOAllowTags();
                if (tags == null || tags.Count == 0)
                {
                    continue;
                }

                foreach (var tag in tags)
                {
                    if (string.IsNullOrWhiteSpace(tag))
                    {
                        continue;
                    }

                    mergedTags.Add(tag);
                }
            }

            return new List<string>(mergedTags);
        }
    }
}
