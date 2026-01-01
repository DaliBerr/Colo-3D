using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kernel.Storage
{
    public static class BuildingRuntimeStatsCodeC{
        /// <summary>
        /// summary: 将库存内容编码/解码到 BuildingRuntime.RuntimeStats（string->float）的工具。
        /// </summary>
        public static class StorageRuntimeStatsCodec
        {
        /// <summary>
        /// summary: 库存 Key 前缀（后面直接拼 itemId）。
        /// </summary>
        public const string ItemKeyPrefix = "__inv__:";

            /// <summary>
            /// summary: 尝试从 RuntimeStats 中解码库存快照。
            /// param: stats 运行时统计字典
            /// param: itemIds 输出物品ID数组
            /// param: counts 输出数量数组
            /// return: 是否解码到至少一条库存
            /// </summary>
            public static bool TryDecodeInventory(Dictionary<string, float> stats, out string[] itemIds, out int[] counts)
            {
                itemIds = Array.Empty<string>();
                counts = Array.Empty<int>();

                if (stats == null || stats.Count == 0)
                    return false;

                var ids = new List<string>(8);
                var cs = new List<int>(8);

                foreach (var kv in stats)
                {
                    var key = kv.Key;
                    if (string.IsNullOrEmpty(key))
                        continue;

                    if (!key.StartsWith(ItemKeyPrefix, StringComparison.Ordinal))
                        continue;

                    var id = key.Substring(ItemKeyPrefix.Length);
                    if (string.IsNullOrEmpty(id))
                        continue;

                    int c = Mathf.RoundToInt(kv.Value);
                    if (c <= 0)
                        continue;

                    ids.Add(id);
                    cs.Add(c);
                }

                if (ids.Count == 0)
                    return false;

                itemIds = ids.ToArray();
                counts = cs.ToArray();
                return true;
            }
        }
    }
}
