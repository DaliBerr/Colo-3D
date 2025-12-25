using Kernel.Building;
using UnityEngine;
using Lonize.Flow;
using Colo.Def.Building;

namespace Colo.Building
{
    /// <summary>
    /// 发电机运行时脚本，实现 IFlowEndpointAdapter，用于与 FlowSystem 交互。
    /// 不负责创建 FlowEndpoint，由 FlowEndpointComponent 管理端口注册。
    /// </summary>
    public class PowerGeneratorRuntime : MonoBehaviour, IFlowEndpointAdapter
    {
        [Header("引用（可选）")]
        [Tooltip("如果使用 BuildingDef 配置发电机参数，可以在这里绑定对应的 BuildingRuntimeHost。")]
        [SerializeField]
        private BuildingRuntimeHost _buildingRuntimeHost;

        [Header("发电机基础参数")]
        [Tooltip("最大发电功率上限，单位为游戏自定义的电力单位每秒。")]
        public float maxOutput = 100f;

        [Tooltip("目标输出功率，通常等于 maxOutput，可用于后期做功率调节。")]
        public float desiredOutput = 100f;

        [Tooltip("每单位电力输出所消耗的燃料量。")]
        public float fuelPerPowerUnit = 0.01f;

        [Tooltip("供电优先级，资源不足时数值越大越优先输出。")]
        public int supplyPriority = 0;

        [Header("初始状态")]
        [Tooltip("初始燃料量。")]
        public float initialFuelAmount = 100f;

        [Tooltip("是否在没燃料时自动关机。")]
        public bool autoShutdownOnNoFuel = true;

        [Header("运行时状态（只读观察）")]
        [Tooltip("当前剩余燃料量。")]
        public float fuelAmount;

        [Tooltip("当前是否处于开启状态。")]
        public bool isOn = true;

        [Tooltip("最近一次结算的实际输出功率。")]
        public float lastActualOutput;

        /// <summary>
        /// Unity 生命周期，在对象启用时初始化状态与可选的 Def 参数。
        /// </summary>
        /// <returns>无返回值。</returns>
        private void Start()
        {
            // 初始化燃料
            fuelAmount = initialFuelAmount;

            // 如果有 BuildingRuntimeHost 且 Def 是 PowerGeneratorDef，则用配置覆盖参数
            if (_buildingRuntimeHost != null)
            {
                var def = _buildingRuntimeHost.Runtime?.Def as PowerGeneratorDef;
                if (def != null)
                {
                    maxOutput = def.maxOutput;
                    desiredOutput = def.defaultDesiredOutput;
                    fuelPerPowerUnit = def.fuelPerPowerUnit;
                    supplyPriority = def.defaultSupplyPriority;
                }
            }
        }

        /// <summary>
        /// 手动设置发电机开关状态，可供 UI 或其他逻辑调用。
        /// </summary>
        /// <param name="on">是否开启。</param>
        /// <returns>无返回值。</returns>
        public void SetOn(bool on)
        {
            isOn = on;
        }

        /// <summary>
        /// 向发电机添加燃料。
        /// </summary>
        /// <param name="amount">添加的燃料量。</param>
        /// <returns>无返回值。</returns>
        public void AddFuel(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            fuelAmount += amount;
        }

        /// <summary>
        /// 获取当前燃料占初始值的比例，用于显示 UI。
        /// </summary>
        /// <returns>燃料比例，0 到 1 之间。</returns>
        public float GetFuelRatio()
        {
            if (initialFuelAmount <= 0f)
            {
                return 0f;
            }

            float ratio = fuelAmount / initialFuelAmount;
            if (ratio < 0f)
            {
                ratio = 0f;
            }

            if (ratio > 1f)
            {
                ratio = 1f;
            }

            return ratio;
        }

        // -------------- IFlowEndpointAdapter 接口实现 --------------

        /// <summary>
        /// 获取当前希望输出的电力功率。
        /// </summary>
        /// <param name="resourceType">资源类型。</param>
        /// <param name="kind">端口类型。</param>
        /// <returns>希望的输出功率，若关机或无燃料则为 0。</returns>
        public float GetDesiredRate(FlowResourceType resourceType, FlowEndpointKind kind)
        {
            // 只对电力生产端口生效
            if (resourceType != FlowResourceType.Power ||
                kind != FlowEndpointKind.Producer)
            {
                return 0f;
            }

            if (!isOn)
            {
                return 0f;
            }

            if (fuelAmount <= 0f)
            {
                return 0f;
            }

            // 可以在这里根据其他状态（损坏、过热等）做动态调整
            return desiredOutput;
        }

        /// <summary>
        /// 获取发电机的最大发电功率上限。
        /// </summary>
        /// <param name="resourceType">资源类型。</param>
        /// <param name="kind">端口类型。</param>
        /// <returns>最大输出功率，若类型不匹配则为 0。</returns>
        public float GetMaxRate(FlowResourceType resourceType, FlowEndpointKind kind)
        {
            if (resourceType != FlowResourceType.Power ||
                kind != FlowEndpointKind.Producer)
            {
                return 0f;
            }

            return maxOutput;
        }

        /// <summary>
        /// 获取发电机在供电网络中的优先级。
        /// </summary>
        /// <param name="resourceType">资源类型。</param>
        /// <param name="kind">端口类型。</param>
        /// <returns>优先级数值，若类型不匹配则为 0。</returns>
        public int GetPriority(FlowResourceType resourceType, FlowEndpointKind kind)
        {
            if (resourceType != FlowResourceType.Power ||
                kind != FlowEndpointKind.Producer)
            {
                return 0;
            }

            return supplyPriority;
        }

        /// <summary>
        /// 应用 FlowSystem 结算后的实际输出功率，并更新燃料与运行状态。
        /// </summary>
        /// <param name="resourceType">资源类型。</param>
        /// <param name="kind">端口类型。</param>
        /// <param name="actualRate">本 tick 实际输出的功率。</param>
        /// <param name="deltaTime">时间步长（秒）。</param>
        /// <returns>无返回值。</returns>
        public void ApplyFlow(FlowResourceType resourceType, FlowEndpointKind kind, float actualRate, float deltaTime)
        {
            if (resourceType != FlowResourceType.Power ||
                kind != FlowEndpointKind.Producer)
            {
                return;
            }

            lastActualOutput = actualRate;

            // 根据实际输出计算燃料消耗
            float fuelConsumed = actualRate * fuelPerPowerUnit * deltaTime;
            if (fuelConsumed > 0f)
            {
                fuelAmount -= fuelConsumed;
            }

            if (fuelAmount <= 0f)
            {
                fuelAmount = 0f;

                if (autoShutdownOnNoFuel)
                {
                    isOn = false;
                    // TODO：这里可以触发事件或通知 UI，“发电机燃料耗尽”
                }
            }

            // TODO：根据 actualRate / maxOutput 调节动画、声音等效果
        }
    }
}
