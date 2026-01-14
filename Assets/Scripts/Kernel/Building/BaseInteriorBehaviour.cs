using System;
using System.Collections.Generic;
using Kernel.Factory.Connections;
using Kernel.UI;
using UnityEngine;

namespace Kernel.Building
{
    /// <summary>
    /// summary: 输出端口选择策略模式。
    /// </summary>
    public enum OutputSelectionMode
    {
        /// <summary>
        /// summary: 默认模式（不开启策略时使用）。
        /// </summary>
        Default,
        /// <summary>
        /// summary: 轮询选择输出端口。
        /// </summary>
        RoundRobin,
        /// <summary>
        /// summary: 使用指定索引的输出端口。
        /// </summary>
        PreferredPort
    }

    /// <summary>
    /// summary: 工厂内部建筑行为基类，统一端口与数据流逻辑。
    /// </summary>
    public abstract class BaseInteriorBehaviour : IBuildingBehaviour, IInteriorPortProvider, IInteriorDataOutput, IInteriorDataInput
    {
        private const string DefaultPowerPortId = "power_in";
        private const string DefaultComputePortId = "compute_in";

        private readonly Queue<object> _pendingOutputs = new();
        private readonly List<string> _defaultInputPortIds = new();
        private readonly HashSet<string> _inactivePorts = new(StringComparer.Ordinal);
        private bool _portsActive = true;

        /// <summary>
        /// summary: UI 端口与系统端口是两套列表，System 端口默认不参与 UI 连接。
        /// </summary>
        public List<PortDescriptor> SystemPowerPorts { get; } = new();

        /// <summary>
        /// summary: UI 端口与系统端口是两套列表，System 端口默认不参与 UI 连接。
        /// </summary>
        public List<PortDescriptor> SystemComputePorts { get; } = new();

        /// <summary>
        /// summary: 是否启用输出端口选择策略，默认不开启以兼容现有行为。
        /// return: 当前是否启用策略
        /// </summary>
        public bool EnableOutputSelection { get; set; } = false;

        /// <summary>
        /// summary: 输出端口选择模式，默认 Default。
        /// return: 当前输出端口选择模式
        /// </summary>
        public OutputSelectionMode SelectionMode { get; set; } = OutputSelectionMode.Default;

        /// <summary>
        /// summary: 首选输出端口索引，默认 0。
        /// return: 首选输出端口索引
        /// </summary>
        public int PreferredOutputIndex { get; set; } = 0;

        /// <summary>
        /// summary: 设置输出端口选择模式与首选索引。
        /// param: mode 选择模式
        /// param: preferredIndex 首选端口索引
        /// return: 无
        /// </summary>
        public void SetOutputSelectionMode(OutputSelectionMode mode, int preferredIndex = 0)
        {
            SelectionMode = mode;
            PreferredOutputIndex = preferredIndex;
            EnableOutputSelection = mode != OutputSelectionMode.Default;
        }

        /// <summary>
        /// summary: 轮询输出端口的游标，默认 0。
        /// return: 当前轮询游标
        /// </summary>
        private int _roundRobinCursor = 0;

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
            OnPreTick(ticks);
            OnTick(ticks);
            OnPostTick(ticks);

            
        }

        /// <summary>
        /// summary: 获取 UI 可连线的端口声明列表（不包含系统端口）。
        /// param: 无
        /// return: 端口声明集合
        /// </summary>
        public IEnumerable<PortDescriptor> GetPorts()
        {
            UpdatePortContext();
            if (!_portsActive)
            {
                return Array.Empty<PortDescriptor>();
            }

            var customPorts = BuildPorts();
            if (customPorts != null)
            {
                var ports = new List<PortDescriptor>();
                foreach (var port in customPorts)
                {
                    if (IsPortActive(port.PortId))
                    {
                        ports.Add(port);
                    }
                }

                return ports;
            }

            return Array.Empty<PortDescriptor>();
        }

        /// <summary>
        /// summary: 获取系统端口声明列表（电力/算力）。
        /// param: 无
        /// return: 系统端口声明集合
        /// </summary>
        public IEnumerable<PortDescriptor> GetSystemPorts()
        {
            UpdatePortContext();
            if (!_portsActive)
            {
                return Array.Empty<PortDescriptor>();
            }

            var ports = new List<PortDescriptor>(SystemPowerPorts.Count + SystemComputePorts.Count);
            AppendActivePorts(SystemPowerPorts, ports);
            AppendActivePorts(SystemComputePorts, ports);
            return ports;
        }

        /// <summary>
        /// summary: 获取系统电力端口声明列表。
        /// param: 无
        /// return: 系统电力端口声明集合
        /// </summary>
        public IEnumerable<PortDescriptor> GetSystemPowerPorts()
        {
            UpdatePortContext();
            if (!_portsActive)
            {
                return Array.Empty<PortDescriptor>();
            }

            return FilterActivePorts(SystemPowerPorts);
        }

        /// <summary>
        /// summary: 获取系统算力端口声明列表。
        /// param: 无
        /// return: 系统算力端口声明集合
        /// </summary>
        public IEnumerable<PortDescriptor> GetSystemComputePorts()
        {
            UpdatePortContext();
            if (!_portsActive)
            {
                return Array.Empty<PortDescriptor>();
            }

            return FilterActivePorts(SystemComputePorts);
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
        /// summary: 设置所有端口的启用状态。
        /// param: isActive 是否启用端口
        /// return: 无
        /// </summary>
        public void SetPortsActive(bool isActive)
        {
            _portsActive = isActive;
        }

        /// <summary>
        /// summary: 设置单个端口的启用状态。
        /// param: portId 端口ID
        /// param: isActive 是否启用
        /// return: 无
        /// </summary>
        public void SetPortActive(string portId, bool isActive)
        {
            if (string.IsNullOrEmpty(portId)) return;

            if (isActive)
            {
                _inactivePorts.Remove(portId);
            }
            else
            {
                _inactivePorts.Add(portId);
            }
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
            if (OutputPortCount == 0)
            {
                return Array.Empty<InteriorDataPacket>();
            }
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
        /// summary: 判断端口是否启用。
        /// param: portId 端口ID
        /// return: 是否启用
        /// </summary>
        private bool IsPortActive(string portId)
        {
            if (string.IsNullOrEmpty(portId)) return false;
            return !_inactivePorts.Contains(portId);
        }

        /// <summary>
        /// summary: 刷新默认输入端口ID。
        /// param: 无
        /// return: 无
        /// </summary>
        private void RefreshDefaultPortIds()
        {
            _defaultInputPortIds.Clear();

            if (InputPortCount > 1)
            {
                _defaultInputPortIds.Add($"{DefaultPowerPortId}_0");
                _defaultInputPortIds.Add($"{DefaultComputePortId}_1");
                RefreshSystemPortDescriptors();
                return;
            }

            _defaultInputPortIds.Add(DefaultPowerPortId);
            _defaultInputPortIds.Add(DefaultComputePortId);

            RefreshSystemPortDescriptors();
        }

        /// <summary>
        /// summary: 刷新系统端口描述列表（电力/算力）。
        /// param: 无
        /// return: 无
        /// </summary>
        private void RefreshSystemPortDescriptors()
        {
            SystemPowerPorts.Clear();
            SystemComputePorts.Clear();

            for (int i = 0; i < _defaultInputPortIds.Count; i++)
            {
                var portId = _defaultInputPortIds[i];
                if (portId.StartsWith(DefaultPowerPortId, StringComparison.Ordinal))
                {
                    SystemPowerPorts.Add(new PortDescriptor(portId, PortDirection.Input, ConnectionChannel.Power, 1));
                }
                else if (portId.StartsWith(DefaultComputePortId, StringComparison.Ordinal))
                {
                    SystemComputePorts.Add(new PortDescriptor(portId, PortDirection.Input, ConnectionChannel.Compute, 1));
                }
            }
        }

        /// <summary>
        /// summary: 追加启用状态的端口到列表。
        /// param: source 源端口列表
        /// param: target 目标端口列表
        /// return: 无
        /// </summary>
        private void AppendActivePorts(List<PortDescriptor> source, List<PortDescriptor> target)
        {
            if (source == null || target == null) return;

            for (int i = 0; i < source.Count; i++)
            {
                var port = source[i];
                if (!IsPortActive(port.PortId)) continue;
                target.Add(port);
            }
        }

        /// <summary>
        /// summary: 获取启用状态的端口列表。
        /// param: source 源端口列表
        /// return: 过滤后的端口列表
        /// </summary>
        private List<PortDescriptor> FilterActivePorts(List<PortDescriptor> source)
        {
            if (source == null || source.Count == 0) return new List<PortDescriptor>();

            var result = new List<PortDescriptor>(source.Count);
            AppendActivePorts(source, result);
            return result;
        }
        /// summary: 子类处理预 Tick 逻辑。
        /// param: ticks Tick 数量
        /// return: 无
        /// </summary>
        protected virtual void OnPreTick(int ticks)
        {
        }

        /// <summary>
        /// summary: 子类处理后 Tick 逻辑。
        /// param: ticks Tick 数量
        /// return: 无
        /// </summary>
        protected virtual void OnPostTick(int ticks)
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

            RefreshDefaultPortIds();
        }

        /// <summary>
        /// summary: 解析主输出端口ID。
        /// param: 无
        /// return: 输出端口ID
        /// </summary>
        protected string ResolveOutputPortId()
        {
            if (string.IsNullOrEmpty(OutputPortIdParent)) return string.Empty;

            if (!EnableOutputSelection || OutputPortCount <= 1)
            {
                return ResolveOutputPortId(0);
            }

            var index = ResolveOutputIndex();
            return ResolveOutputPortId(index);
        }

        /// <summary>
        /// summary: 根据策略解析输出端口索引。
        /// param: 无
        /// return: 输出端口索引
        /// </summary>
        private int ResolveOutputIndex()
        {
            if (OutputPortCount <= 0) return 0;

            switch (SelectionMode)
            {
                case OutputSelectionMode.RoundRobin:
                    return ConsumeRoundRobinIndex();
                case OutputSelectionMode.PreferredPort:
                    if (PreferredOutputIndex >= 0 && PreferredOutputIndex < OutputPortCount)
                    {
                        return PreferredOutputIndex;
                    }

                    if (OutputPortCount <= 1)
                    {
                        LogOutputSelectionFallback("PreferredOutputIndex 超出范围，回退到 0 号端口。");
                        return 0;
                    }

                    LogOutputSelectionFallback("PreferredOutputIndex 超出范围，回退到轮询策略。");
                    return ConsumeRoundRobinIndex();
                default:
                    return 0;
            }
        }

        /// <summary>
        /// summary: 记录输出端口选择回退日志。
        /// param: message 日志内容
        /// return: 无
        /// </summary>
        private void LogOutputSelectionFallback(string message)
        {
            Debug.LogWarning($"[OutputSelection] Building {BuildingLocalId}: {message}");
        }

        /// <summary>
        /// summary: 消费并推进轮询输出端口索引。
        /// param: 无
        /// return: 轮询选择的输出端口索引
        /// </summary>
        private int ConsumeRoundRobinIndex()
        {
            if (OutputPortCount <= 0) return 0;

            if (_roundRobinCursor < 0)
            {
                _roundRobinCursor = 0;
            }

            var index = _roundRobinCursor % OutputPortCount;
            if (index < 0)
            {
                index += OutputPortCount;
            }

            _roundRobinCursor = (index + 1) % OutputPortCount;
            return index;
        }

        /// <summary>
        /// summary: 根据索引解析输出端口ID。
        /// param: index 输出端口索引
        /// return: 输出端口ID
        /// </summary>
        protected string ResolveOutputPortId(int index)
        {
            if (string.IsNullOrEmpty(OutputPortIdParent)) return string.Empty;

            if (OutputPortCount <= 1)
            {
                return OutputPortIdParent;
            }

            if (index < 0 || index >= OutputPortCount)
            {
                index = 0;
            }

            return $"{OutputPortIdParent}_{index}";
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
