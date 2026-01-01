using System.Collections.Generic;
using Kernel.Building;
using Kernel.Item;

namespace Colo.Def.Building
{
    public class StorageDef : BuildingDef
    {
        /// <summary>
        /// 存储容量。
        /// </summary>
        public float capacity;

        /// <summary>
        /// 允许存储的物品类型列表。
        /// </summary>
        public List<string> allowedItemTypes;

        /// <summary>
        /// 每种物品类型的容量限制。
        /// </summary>
        public Dictionary<string, float> itemTypeCapacities;

        /// <summary>
        /// 默认供电优先级。
        /// </summary>
        public int defaultSupplyPriority;
    }
}