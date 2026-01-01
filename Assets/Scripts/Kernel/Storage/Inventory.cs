using System.Collections.Generic;

namespace Kernel.Inventory
{
    /// <summary>
    /// summary: 物品信息查询接口（用于获取标签等元数据，解耦具体 ItemDef 系统）。
    /// </summary>
    public interface IItemCatalog
    {
        /// <summary>
        /// summary: 尝试获取物品的标签列表。
        /// param: itemId 物品定义ID
        /// param: tags 输出标签（只读）
        /// return: 是否成功
        /// </summary>
        bool TryGetTags(string itemId, out IReadOnlyList<string> tags);
    }
}
