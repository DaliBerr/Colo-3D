using System.Collections.Generic;
using Kernel.Building;
using Kernel.Item;

namespace Colo.Def.Building
{
    public class FactoryDef : BuildingDef
    {
        /// <summary>
        /// 工厂等级。
        /// </summary>
        public byte FactoryLevel;
        /// <summary>
        /// 默认供电优先级。
        /// </summary>
        public int defaultSupplyPriority;
    }
}