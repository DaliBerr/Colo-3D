namespace Kernel.Item
{
    public static class Minerals
    {
        /// <summary>
        /// 获取矿物类型的物品ID。
        /// </summary>
        /// <param name="mineralType">矿物类型枚举。</param>
        /// <returns>对应的物品ID。</returns>
        public static int GetMineralItemId(MineralType mineralType)
        {
            return mineralType switch
            {
                MineralType.raw_ore => 1, // 假设 raw_ore 对应的物品ID为 1
                _ => 0, // 未知矿物类型返回 0
            };
        }
        public static string GetMineralDefName(MineralType mineralType)
        {
            return mineralType switch
            {
                MineralType.raw_ore => "raw_ore", // 假设 raw_ore 对应的 DefName 为 "raw_ore"
                _ => string.Empty, // 未知矿物类型返回空字符串
            };
        }
    }
    public enum MineralType
    {
        raw_ore = 1,
    }
}