using System;
using System.Collections.Generic;
using Kernel.Factory.Connections;
using Kernel.UI;
using UnityEngine;

namespace Kernel.Building
{
    /// <summary>
    /// summary: 工厂内部建筑行为基类，统一端口与数据流逻辑。
    /// </summary>
    public abstract class BaseInteriorBehaviour : IBuildingBehaviour, IInteriorPortProvider, IInteriorDataOutput, IInteriorDataInput
    {
        private readonly Queue<object> _pendingOutputs = new();

        /// <summary>
        /// summary: 当前所属工厂ID。
        /// </summary>
        public long FactoryId { get; protected set; }

        /// <summary>
        /// summary: 当前建筑本地ID。
        /// </summary>
        public long BuildingLocalId { get; protected set; }

        /// <summary>
        /// summary: 输入端口数量。
        /// </summary>
        public int InputPortCount { get; protected set; } = -1;

        /// <summary>
        /// summary: 输出端口数量。
        /// </summary>
        public int OutputPortCount { get; protected set; } = -1;

        /// <summary>
        /// summary: 输入端口父ID。
        /// </summary>
        protected abstract string InputPortIdParent { get; }

        /// <summary>
        /// summary: 输出端口父ID。
        /// </summary>
        protected abstract string OutputPortIdParent { get; }

        /// <summary>
        /// summary: 输出通道类型。
        /// </summary>
        protected virtual ConnectionChannel OutputChannel => ConnectionChannel.Item;

        /// <summary>
        /// summary: 绑定建筑运行时并初始化上下文。
        /// param: runtime 建筑运行时
        /// return: 无
        /// </summary>
        public virtual void OnBind(BuildingRuntime runtime)
        {
            BuildingLocalId = runtime?.BuildingID ?? 0;
            FactoryId = 0;
            InputPortCount = -1;
            OutputPortCount = -1;
            _pendingOutputs.Clear();
        }

        /// <summary>
        /// summary: 解绑建筑运行时。
        /// param: runtime 建筑运行时
        /// return: 无
        /// </summary>
        public virtual void OnUnbind(BuildingRuntime runtime)
        {
        }

        /// <summary>
        /// summary: Tick 驱动入口。
        /// param: ticks Tick 数量
        /// return: 无
        /// </summary>
        public virtual void Tick(int ticks)
        {
            if (ticks <= 0) return;

            UpdatePortContext();
            OnTick(ticks);
        }

        /// <summary>
        /// summary: 获取端口声明列表。
        /// param: 无
        /// return: 端口声明集合
        /// </summary>
        public IEnumerable<PortDescriptor> GetPorts()
        {
            UpdatePortContext();
            return BuildPorts() ?? Array.Empty<PortDescriptor>();
        }

        /// <summary>
        /// summary: 构建端口声明配置。
        /// param: 无
        /// return: 端口声明集合
        /// </summary>
        protected virtual IEnumerable<PortDescriptor> BuildPorts()
        {
            return Array.Empty<PortDescriptor>();
        }

        /// <summary>
        /// summary: 收集输出数据包。
        /// param: 无
        /// return: 数据包集合
        /// </summary>
        public IEnumerable<InteriorDataPacket> CollectOutputs()
        {
            if (_pendingOutputs.Count == 0) return Array.Empty<InteriorDataPacket>();

            UpdatePortContext();
            var outputPortId = ResolveOutputPortId();
            if (FactoryId <= 0 || BuildingLocalId <= 0 || string.IsNullOrEmpty(outputPortId))
            {
                return Array.Empty<InteriorDataPacket>();
            }

            var key = new PortKey(FactoryId, BuildingLocalId, outputPortId);
            var packets = new List<InteriorDataPacket>(_pendingOutputs.Count);
            while (_pendingOutputs.Count > 0)
            {
                var payload = _pendingOutputs.Dequeue();
                packets.Add(new InteriorDataPacket(key, OutputChannel, payload));
            }

            return packets;
        }

        /// <summary>
        /// summary: 接收输入数据包。
        /// param: packet 数据包
        /// return: 无
        /// </summary>
        public void ReceiveInput(InteriorDataPacket packet)
        {
            if (packet == null) return;

            UpdatePortContext();
            if (!IsMatchingInputPort(packet.PortId)) return;

            OnReceiveInput(packet);
        }

        /// <summary>
        /// summary: 子类处理输入数据包。
        /// param: packet 数据包
        /// return: 无
        /// </summary>
        protected virtual void OnReceiveInput(InteriorDataPacket packet)
        {
        }

        /// <summary>
        /// summary: 子类处理 Tick 逻辑。
        /// param: ticks Tick 数量
        /// return: 无
        /// </summary>
        protected virtual void OnTick(int ticks)
        {
        }

        /// <summary>
        /// summary: 将输出负载加入缓冲队列。
        /// param: payload 输出负载
        /// return: 无
        /// </summary>
        protected void EnqueueOutput(object payload)
        {
            _pendingOutputs.Enqueue(payload);
        }

        /// <summary>
        /// summary: 更新端口上下文（工厂ID、端口数量）。
        /// param: 无
        /// return: 无
        /// </summary>
        protected void UpdatePortContext()
        {
            if (FactoryId > 0 && InputPortCount >= 0 && OutputPortCount >= 0) return;

            var ui = FindInteriorUI();
            if (ui == null) return;

            if (FactoryId <= 0)
            {
                FactoryId = ui.BuildingParentId;
            }

            if (InputPortCount < 0)
            {
                InputPortCount = ui.InputButtons?.Count ?? 0;
            }

            if (OutputPortCount < 0)
            {
                OutputPortCount = ui.OutputButtons?.Count ?? 0;
            }
        }

        /// <summary>
        /// summary: 解析主输出端口ID。
        /// param: 无
        /// return: 输出端口ID
        /// </summary>
        protected string ResolveOutputPortId()
        {
            if (string.IsNullOrEmpty(OutputPortIdParent)) return string.Empty;

            if (OutputPortCount > 1)
            {
                return $"{OutputPortIdParent}_0";
            }

            return OutputPortIdParent;
        }

        /// <summary>
        /// summary: 判断输入端口是否匹配。
        /// param: portId 端口ID
        /// return: 是否匹配
        /// </summary>
        protected bool IsMatchingInputPort(string portId)
        {
            if (string.IsNullOrEmpty(portId) || string.IsNullOrEmpty(InputPortIdParent)) return false;

            if (InputPortCount > 1)
            {
                return portId.StartsWith($"{InputPortIdParent}_", StringComparison.Ordinal)
                       || string.Equals(portId, InputPortIdParent, StringComparison.Ordinal);
            }

            return string.Equals(portId, InputPortIdParent, StringComparison.Ordinal);
        }

        /// <summary>
        /// summary: 查找对应的内部建筑 UI。
        /// param: 无
        /// return: UI 实例
        /// </summary>
        protected virtual IInteriorBuildingUI FindInteriorUI()
        {
            if (BuildingLocalId <= 0) return null;

            var uis = UnityEngine.Object.FindObjectsByType<IInteriorBuildingUI>(UnityEngine.FindObjectsSortMode.None);
            if (uis == null || uis.Length == 0) return null;

            for (int i = 0; i < uis.Length; i++)
            {
                var ui = uis[i];
                if (ui == null) continue;
                if (ui.BuildingLocalId == BuildingLocalId)
                {
                    return ui;
                }
            }

            return null;
        }
    }
}
