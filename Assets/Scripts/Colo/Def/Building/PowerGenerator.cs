using Kernel.Building;

namespace Colo.Def.Building
{
    public class PowerGeneratorDef : BuildingDef
    {
        /// <summary>
        /// 最大发电功率。
        /// </summary>
        public float maxOutput;

        /// <summary>
        /// 默认目标输出功率。
        /// </summary>
        public float defaultDesiredOutput;

        /// <summary>
        /// 燃料消耗系数。
        /// </summary>
        public float fuelPerPowerUnit;

        /// <summary>
        /// 默认供电优先级。
        /// </summary>
        public int defaultSupplyPriority;
    }
}