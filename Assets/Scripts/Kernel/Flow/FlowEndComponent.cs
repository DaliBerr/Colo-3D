using System;
using Lonize.Flow;
using UnityEngine;

namespace Kernel.Flow
{
    /// <summary>
    /// 通用 Flow 端口组件，用于将实现 IFlowEndpointAdapter 的脚本接入 FlowSystem。
    /// 一个组件代表一个端口（某资源类型 + 供给或消耗）。
    /// </summary>
    [DisallowMultipleComponent]
    public class FlowEndpointComponent : MonoBehaviour
    {
        [Header("Flow 基本配置")]
        [Tooltip("该端口使用的资源类型，例如电力或算力。")]
        public FlowResourceType resourceType = FlowResourceType.Power;

        [Tooltip("该端口是供给方还是消耗方。")]
        public FlowEndpointKind endpointKind = FlowEndpointKind.Consumer;

        [Header("适配器配置")]
        [Tooltip("实现了 IFlowEndpointAdapter 的组件，例如发电机、用电机器等。")]
        public MonoBehaviour adapterBehaviour;

        /// <summary>
        /// 对外公开的 Flow 端口对象，可用于其他系统进行连接操作。
        /// </summary>
        public FlowEndpoint Endpoint => _endpoint;

        /// <summary>
        /// 缓存的适配器引用。
        /// </summary>
        private IFlowEndpointAdapter _adapter;

        /// <summary>
        /// 缓存的 Flow 端口实例。
        /// </summary>
        private FlowEndpoint _endpoint;

        /// <summary>
        /// Unity 生命周期回调，在对象创建时初始化端口并注册到 FlowSystem。
        /// </summary>
        /// <returns>无返回值。</returns>
        private void Awake()
        {
            TryInitAdapter();

            if (_adapter == null)
            {
                Debug.LogError($"[FlowEndpointComponent] {name} 初始化失败：未找到有效的 IFlowEndpointAdapter。");
                return;
            }

            _endpoint = new FlowEndpoint(resourceType, endpointKind, _adapter);
            FlowSystem.Instance.RegisterEndpoint(_endpoint);
        }

        /// <summary>
        /// Unity 生命周期回调，在对象销毁时从 FlowSystem 中注销端口。
        /// </summary>
        /// <returns>无返回值。</returns>
        private void OnDestroy()
        {
            if (_endpoint != null)
            {
                FlowSystem.Instance.UnregisterEndpoint(_endpoint);
                _endpoint = null;
            }
        }

        /// <summary>
        /// Unity 编辑器回调，用于在 Inspector 修改时自动尝试绑定适配器。
        /// </summary>
        /// <returns>无返回值。</returns>
        private void OnValidate()
        {
            // 只在编辑器下执行，运行时 Awake 会再做一次校验
#if UNITY_EDITOR
            if (adapterBehaviour == null)
            {
                // 尝试在当前 GameObject 上自动寻找一个实现 IFlowEndpointAdapter 的组件
                var components = GetComponents<MonoBehaviour>();
                for (int i = 0; i < components.Length; i++)
                {
                    if (components[i] is IFlowEndpointAdapter)
                    {
                        adapterBehaviour = components[i];
                        break;
                    }
                }
            }
#endif
        }

        /// <summary>
        /// 尝试初始化适配器引用，将 MonoBehaviour 转为 IFlowEndpointAdapter。
        /// </summary>
        /// <returns>无返回值。</returns>
        private void TryInitAdapter()
        {
            if (_adapter != null)
            {
                return;
            }

            if (adapterBehaviour == null)
            {
                // Awake 时依然为空就直接返回，交给上层报错
                return;
            }

            _adapter = adapterBehaviour as IFlowEndpointAdapter;
            if (_adapter == null)
            {
                Debug.LogError(
                    $"[FlowEndpointComponent] {name} 绑定的组件 {adapterBehaviour.GetType().Name} 未实现 IFlowEndpointAdapter 接口。");
            }
        }
    }
}
