using System.Collections.Generic;
using Kernel.Item;

namespace Kernel.Inventory
{
    /// <summary>
    /// summary: 基于 ItemDef 的物品标签目录实现。
    /// </summary>
    public sealed class ItemDefCatalog : IItemCatalog
    {
        /// <summary>
        /// summary: 尝试获取物品的标签列表。
        /// param: itemId 物品定义ID
        /// param: tags 输出标签（只读）
        /// return: 是否成功
        /// </summary>
        public bool TryGetTags(string itemId, out IReadOnlyList<string> tags)
        {
            tags = null;
            if (!ItemDatabase.TryGet(itemId, out var def) || def == null || def.Tags == null || def.Tags.Count == 0)
            {
                return false;
            }

            tags = def.Tags;
            return true;
        }
    }
}
