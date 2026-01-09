
using Lonize.Logging;

namespace Kernel.UI
{
    public class SampleInteriorBuildingUI : IInteriorBuildingUI
    {
        protected override void OnInputButtonClicked(int index)
        {
            base.OnInputButtonClicked(index);
            // 示例实现：打印输入按钮点击信息
            GameDebug.Log($"Input Button {index} clicked in SampleInteriorBuildingUI.");
        }

        protected override void OnOutputButtonClicked(int index)
        {
            base.OnOutputButtonClicked(index);
            // 示例实现：打印输出按钮点击信息
            GameDebug.Log($"Output Button {index} clicked in SampleInteriorBuildingUI.");
        }

        protected override void handleStart()
        {
            // 示例初始化逻辑
            GameDebug.Log("SampleInteriorBuildingUI started.");
        }

        protected override void handleAwake()
        {
            // 示例初始化逻辑
            GameDebug.Log("SampleInteriorBuildingUI awoke.");
        }
    }    




}
