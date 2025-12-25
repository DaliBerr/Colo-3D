
using System;

namespace Kernel.UI
{
    /// <summary>
    /// 全局加载进度管理器，用于给加载界面提供0-1的进度值。
    /// </summary>
    public static class GlobalLoadingProgress
    {
        /// <summary>
        /// 建筑定义加载进度（0-1）。
        /// </summary>
        public static float BuildingDefProgress { get; private set; }

        /// <summary>
        /// 物品定义加载进度（0-1）。
        /// </summary>
        public static float ItemDefProgress { get; private set; }

        private static float allDefProgress()
        {
            return (BuildingDefProgress + ItemDefProgress) * 0.5f;
        }


        /// <summary>
        /// 游戏整体加载进度（0-1），简单按建筑和物品各占一半。
        /// </summary>
        public static float GameLoadingProgress
        {
            get
            {
                var g = allDefProgress();
                if (g < 0f) g = 0f;
                else if (g > 1f) g = 1f;
                return g;
            }
        }

        /// <summary>
        /// 重置全部加载进度为0。
        /// </summary>
        /// <returns>无返回值。</returns>
        public static void Reset()
        {
            BuildingDefProgress = 0f;
            ItemDefProgress = 0f;
        }

        /// <summary>
        /// 上报建筑定义加载进度。
        /// </summary>
        /// <param name="loaded">已处理数量。</param>
        /// <param name="total">总数量。</param>
        /// <returns>无返回值。</returns>
        public static void ReportBuilding(int loaded, int total)
        {
            BuildingDefProgress = Calc01(loaded, total);
        }

        /// <summary>
        /// 上报物品定义加载进度。
        /// </summary>
        /// <param name="loaded">已处理数量。</param>
        /// <param name="total">总数量。</param>
        /// <returns>无返回值。</returns>
        public static void ReportItem(int loaded, int total)
        {
            ItemDefProgress = Calc01(loaded, total);
        }

        /// <summary>
        /// 工具函数：根据数量计算0-1进度。
        /// </summary>
        /// <param name="loaded">已处理数量。</param>
        /// <param name="total">总数量。</param>
        /// <returns>进度值（0-1）。</returns>
        private static float Calc01(int loaded, int total)
        {
            if (total <= 0) return 1f;
            var v = (float)loaded / total;
            if (v < 0f) v = 0f;
            else if (v > 1f) v = 1f;
            return v;
        }
    }
}
