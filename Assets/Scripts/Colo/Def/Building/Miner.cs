using Kernel.Building;

namespace Colo.Def.Building
{
    /// <summary>
    /// summary: 矿机建筑定义。
    /// </summary>
    public class MinerDef : BuildingDef
    {
        /// <summary>
        /// 输出物品ID。
        /// </summary>
        public string outputItemId;

        /// <summary>
        /// 每次输出数量。
        /// </summary>
        public int outputCount;

        /// <summary>
        /// 输出间隔（Tick）。
        /// </summary>
        public int tickInterval;
    }
}
