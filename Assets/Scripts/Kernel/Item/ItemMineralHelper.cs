using System.Collections.Generic;

namespace Kernel.Item
{
    /// <summary>
    /// 物品矿物属性读取工具。
    /// </summary>
    public static class ItemMineralHelper
    {
        /// <summary>
        /// 获取物品的矿物成分信息。
        /// </summary>
        /// <param name="itemId">物品ID。</param>
        /// <param name="composition">成分字典。</param>
        /// <returns>是否成功获取。</returns>
        public static bool TryGetMineralComposition(string itemId, out Dictionary<string, FloatRange> composition)
        {
            composition = null;
            if (!ItemDatabase.TryGet(itemId, out var def))
            {
                return false;
            }

            composition = def.MineralComposition;
            return composition != null;
        }

        /// <summary>
        /// 获取物品的加工属性信息。
        /// </summary>
        /// <param name="itemId">物品ID。</param>
        /// <param name="processingInfo">加工属性。</param>
        /// <returns>是否成功获取。</returns>
        public static bool TryGetProcessingInfo(string itemId, out MineralProcessingInfo processingInfo)
        {
            processingInfo = null;
            if (!ItemDatabase.TryGet(itemId, out var def))
            {
                return false;
            }

            processingInfo = def.ProcessingInfo;
            return processingInfo != null;
        }
    }
}
