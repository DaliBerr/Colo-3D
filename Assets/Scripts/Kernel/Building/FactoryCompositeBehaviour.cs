using System;
using System.Collections.Generic;
using Kernel.Factory.Connections;
using Kernel.Storage;
using Lonize.Logging;
using Lonize.Tick;
using UnityEngine;

namespace Kernel.Building
{
    /// <summary>
    /// summary: 工厂节点运行状态。
    /// </summary>
    public enum FactoryNodeState
    {
        Idle = 0,
        Running = 1,
        Completed = 2
    }

    /// <summary>
    /// summary: 工厂内部节点运行时（用于 UI 展示步骤与状态）。
    /// </summary>
    public sealed class FactoryNodeRuntime
    {
        public FactoryChildRuntime Child { get; }
        public FactoryNodeState State { get; private set; }
        public int StepIndex { get; private set; }

        private FactoryNodeRuntime(FactoryChildRuntime child)
        {
            Child = child;
            ResetState();
        }

        /// <summary>
        /// summary: 创建节点运行时。
        /// param: child 内部建筑运行时
        /// return: 节点运行时实例
        /// </summary>
        public static FactoryNodeRuntime Create(FactoryChildRuntime child)
        {
            return new FactoryNodeRuntime(child);
        }

        /// <summary>
        /// summary: 重置节点状态与步骤。
        /// param: 无
        /// return: 无
        /// </summary>
        public void ResetState()
        {
            State = FactoryNodeState.Idle;
            StepIndex = 0;
        }

        /// <summary>
        /// summary: 开始节点步骤。
        /// param: stepIndex 步骤序号
        /// return: 无
        /// </summary>
        public void BeginStep(int stepIndex)
        {
            StepIndex = stepIndex;
            State = FactoryNodeState.Running;
        }

        /// <summary>
        /// summary: 完成节点步骤。
        /// param: 无
        /// return: 无
        /// </summary>
        public void CompleteStep()
        {
            State = FactoryNodeState.Completed;
        }

        /// <summary>
        /// summary: Tick 节点内部行为。
        /// param: ticks Tick 数量
        /// return: 无
        /// </summary>
        public void TickBehaviours(int ticks)
        {
            if (Child?.Behaviours == null) return;

            foreach (var behaviour in Child.Behaviours)
            {
                if (behaviour is ITickable tickable)
                {
                    tickable.Tick(ticks);
                }
            }
        }
    }

    /// <summary>
    /// summary: 工厂合成行为（按连接图推进节点 Tick）。
    /// </summary>
    public sealed class FactoryCompositeBehaviour : IBuildingBehaviour, ITickable
    {
        private sealed class InterfaceBoxQuota
        {
            public int RemainingAdd;
            public int RemainingRemove;

            public InterfaceBoxQuota(int remainingAdd, int remainingRemove)
            {
                RemainingAdd = remainingAdd;
                RemainingRemove = remainingRemove;
            }
        }

        private BuildingRuntime _runtime;
        private FactoryInteriorConnectionsRuntime _connections;
        private readonly List<FactoryNodeRuntime> _nodes = new();
        private readonly Dictionary<long, FactoryNodeRuntime> _nodeByLocalId = new();
        private List<InteriorDataPacket> _incomingBuffer = new();
        private List<InteriorDataPacket> _outgoingBuffer = new();
        private readonly List<long> _interfaceBoxLocalIds = new();
        private readonly Dictionary<long, InterfaceBoxQuota> _interfaceBoxQuotas = new();

        public IReadOnlyList<FactoryNodeRuntime> Nodes => _nodes;

        /// <summary>
        /// summary: 绑定工厂运行时并初始化节点数据。
        /// param: runtime 建筑运行时
        /// return: 无
        /// </summary>
        public void OnBind(BuildingRuntime runtime)
        {
            _runtime = runtime;
            var interior = runtime?.FactoryInterior;
            _connections = interior?.Connections;
            RebuildNodes(interior?.Children);
        }

        /// <summary>
        /// summary: 解绑工厂运行时并清理节点数据。
        /// param: runtime 建筑运行时
        /// return: 无
        /// </summary>
        public void OnUnbind(BuildingRuntime runtime)
        {
            _runtime = null;
            _connections = null;
            _nodes.Clear();
            _nodeByLocalId.Clear();
            _incomingBuffer.Clear();
            _outgoingBuffer.Clear();
            _interfaceBoxLocalIds.Clear();
            _interfaceBoxQuotas.Clear();
        }

        /// <summary>
        /// summary: 按连接图顺序推进节点 Tick。
        /// param: ticks Tick 数量
        /// return: 无
        /// </summary>
        public void Tick(int ticks)
        {
            if (_nodes.Count == 0) return;

            RefreshInterfaceBoxQuotas();

            var orderedNodes = BuildTraversalOrder();
            if (orderedNodes.Count == 0) return;

            RouteIncomingBuffer();

            for (int i = 0; i < orderedNodes.Count; i++)
            {
                var node = orderedNodes[i];
                node.BeginStep(i);
                node.TickBehaviours(ticks);
                node.CompleteStep();
            }

            CollectOutgoingBuffer();
            SwapBuffers();
        }

        /// <summary>
        /// summary: 重建节点数据（用于内部建筑变化后刷新）。
        /// param: children 内部建筑列表
        /// return: 无
        /// </summary>
        public void RebuildNodes(IReadOnlyList<FactoryChildRuntime> children)
        {
            _nodes.Clear();
            _nodeByLocalId.Clear();

            if (children == null) return;

            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child == null) continue;

                var node = FactoryNodeRuntime.Create(child);
                _nodes.Add(node);
                _nodeByLocalId[child.BuildingLocalID] = node;
            }

            RebuildInterfaceBoxCache(children);
        }

        /// <summary>
        /// summary: 重新扫描对外接口箱并建立本地ID缓存。
        /// param: children 内部建筑列表
        /// return: 无
        /// </summary>
        private void RebuildInterfaceBoxCache(IReadOnlyList<FactoryChildRuntime> children)
        {
            _interfaceBoxLocalIds.Clear();
            _interfaceBoxQuotas.Clear();

            if (children == null) return;

            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child?.Behaviours == null) continue;

                foreach (var behaviour in child.Behaviours)
                {
                    if (behaviour is InteriorInterfaceBoxBehaviour)
                    {
                        _interfaceBoxLocalIds.Add(child.BuildingLocalID);
                        break;
                    }
                }
            }

            _interfaceBoxLocalIds.Sort();
        }

        /// <summary>
        /// summary: 刷新对外接口箱配额（每 Tick 更新）。
        /// param: 无
        /// return: 无
        /// </summary>
        private void RefreshInterfaceBoxQuotas()
        {
            _interfaceBoxQuotas.Clear();

            if (_interfaceBoxLocalIds.Count == 0)
            {
                return;
            }

            if (!TryGetFactoryContainer(out var container))
            {
                return;
            }

            int totalFree = Mathf.Max(0, container.GetFree());
            int totalUsed = Mathf.Max(0, container.GetUsed());
            int count = _interfaceBoxLocalIds.Count;

            int baseAdd = totalFree / count;
            int extraAdd = totalFree % count;
            int baseRemove = totalUsed / count;
            int extraRemove = totalUsed % count;

            for (int i = 0; i < count; i++)
            {
                long localId = _interfaceBoxLocalIds[i];
                int addQuota = baseAdd + (i < extraAdd ? 1 : 0);
                int removeQuota = baseRemove + (i < extraRemove ? 1 : 0);
                _interfaceBoxQuotas[localId] = new InterfaceBoxQuota(addQuota, removeQuota);
            }
        }

        /// <summary>
        /// summary: 对外接口箱请求向工厂容器存入物品。
        /// param: localId 接口箱本地ID
        /// param: itemId 物品ID
        /// param: count 请求数量
        /// param: itemTags 物品标签
        /// param: added 实际存入数量
        /// return: 是否存入成功
        /// </summary>
        public bool TryRequestAdd(long localId, string itemId, int count, IReadOnlyList<string> itemTags, out int added)
        {
            added = 0;
            if (count <= 0 || string.IsNullOrEmpty(itemId)) return false;

            if (!_interfaceBoxQuotas.TryGetValue(localId, out var quota))
            {
                RefreshInterfaceBoxQuotas();
                if (!_interfaceBoxQuotas.TryGetValue(localId, out quota))
                {
                    return false;
                }
            }

            int allowed = Mathf.Min(count, quota.RemainingAdd);
            if (allowed <= 0) return false;

            if (!TryGetFactoryContainer(out var container))
            {
                return false;
            }

            if (!container.TryAdd(itemId, allowed, itemTags, out added))
            {
                return false;
            }

            quota.RemainingAdd = Mathf.Max(0, quota.RemainingAdd - added);
            return added > 0;
        }

        /// <summary>
        /// summary: 对外接口箱请求从工厂容器取出物品。
        /// param: localId 接口箱本地ID
        /// param: itemId 物品ID
        /// param: count 请求数量
        /// param: removed 实际取出数量
        /// return: 是否取出成功
        /// </summary>
        public bool TryRequestRemove(long localId, string itemId, int count, out int removed)
        {
            removed = 0;
            if (count <= 0 || string.IsNullOrEmpty(itemId)) return false;

            if (!_interfaceBoxQuotas.TryGetValue(localId, out var quota))
            {
                RefreshInterfaceBoxQuotas();
                if (!_interfaceBoxQuotas.TryGetValue(localId, out quota))
                {
                    return false;
                }
            }

            int allowed = Mathf.Min(count, quota.RemainingRemove);
            if (allowed <= 0) return false;

            if (!TryGetFactoryContainer(out var container))
            {
                return false;
            }

            if (!container.TryRemove(itemId, allowed, out removed))
            {
                return false;
            }

            quota.RemainingRemove = Mathf.Max(0, quota.RemainingRemove - removed);
            return removed > 0;
        }

        /// <summary>
        /// summary: 尝试获取工厂容器引用。
        /// param: container 输出工厂容器
        /// return: 是否成功获取
        /// </summary>
        private bool TryGetFactoryContainer(out StorageContainer container)
        {
            container = null;
            if (_runtime == null || _runtime.BuildingID <= 0)
            {
                return false;
            }

            return StorageSystem.Instance != null && StorageSystem.Instance.TryGet(_runtime.BuildingID, out container);
        }

        private List<FactoryNodeRuntime> BuildTraversalOrder()
        {
            var ordered = new List<FactoryNodeRuntime>(_nodes.Count);
            var added = new HashSet<FactoryNodeRuntime>();

            var links = _connections?.Graph?.GetAllLinks();
            if (links != null && links.Count > 0)
            {
                foreach (var link in links)
                {
                    TryAddNode(link.A.LocalBuildingId, ordered, added);
                    TryAddNode(link.B.LocalBuildingId, ordered, added);
                }
            }

            for (int i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                if (added.Add(node))
                    ordered.Add(node);
            }

            return ordered;
        }

        private void TryAddNode(long localId, List<FactoryNodeRuntime> ordered, HashSet<FactoryNodeRuntime> added)
        {
            if (_nodeByLocalId.TryGetValue(localId, out var node) && added.Add(node))
            {
                ordered.Add(node);
            }
        }

        /// <summary>
        /// summary: 尝试通过端口键解析目标节点。
        /// param: key 端口键
        /// param: node 解析到的节点
        /// return: 是否解析成功
        /// </summary>
        public bool TryResolveNode(PortKey key, out FactoryNodeRuntime node)
        {
            if (_nodeByLocalId.TryGetValue(key.LocalBuildingId, out node)) return true;

            GameDebug.LogWarning($"[FactoryComposite] 未找到节点，端口键: {key}");
            return false;
        }

        /// <summary>
        /// summary: 将输入缓冲内的数据包按连接路由到下游节点。
        /// param: 无
        /// return: 无
        /// </summary>
        private void RouteIncomingBuffer()
        {
            if (_incomingBuffer == null || _incomingBuffer.Count == 0) return;

            var graph = _connections?.Graph;
            if (graph == null) return;

            for (int i = 0; i < _incomingBuffer.Count; i++)
            {
                var packet = _incomingBuffer[i];
                if (packet == null) continue;

                if (!graph.TryGetPort(packet.PortKey, out var sourcePort)) continue;
                if (sourcePort.Direction == PortDirection.Input) continue;

                var links = graph.GetLinksOfPort(packet.PortKey);
                if (links == null || links.Count == 0) continue;

                for (int j = 0; j < links.Count; j++)
                {
                    var link = links[j];
                    if (link == null || link.Channel != packet.Channel) continue;

                    var targetKey = link.GetOther(packet.PortKey);
                    if (!graph.TryGetPort(targetKey, out var targetPort)) continue;
                    if (targetPort.Direction == PortDirection.Output) continue;

                    if (!TryResolveNode(targetKey, out var targetNode)) continue;
                    var behaviours = targetNode.Child?.Behaviours;
                    if (behaviours == null) continue;

                    var routedPacket = new InteriorDataPacket(targetKey, packet.Channel, packet.Payload);
                    foreach (var behaviour in behaviours)
                    {
                        if (behaviour is IInteriorDataInput input)
                        {
                            input.ReceiveInput(routedPacket);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// summary: 收集节点输出数据并写入输出缓冲。
        /// param: 无
        /// return: 无
        /// </summary>
        private void CollectOutgoingBuffer()
        {
            if (_outgoingBuffer == null) return;

            for (int i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                var behaviours = node.Child?.Behaviours;
                if (behaviours == null) continue;

                foreach (var behaviour in behaviours)
                {
                    if (behaviour is not IInteriorDataOutput output) continue;

                    var packets = output.CollectOutputs();
                    if (packets == null) continue;

                    foreach (var packet in packets)
                    {
                        var normalized = NormalizeOutputPacket(packet);
                        if (normalized != null)
                        {
                            _outgoingBuffer.Add(normalized);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// summary: 交换输入输出缓冲。
        /// param: 无
        /// return: 无
        /// </summary>
        private void SwapBuffers()
        {
            _incomingBuffer = _outgoingBuffer;
            _outgoingBuffer = new List<InteriorDataPacket>();
        }

        /// <summary>
        /// summary: 标准化输出数据包，确保 PortId 与端口一致。
        /// param: packet 原始数据包
        /// return: 标准化后的数据包
        /// </summary>
        private static InteriorDataPacket NormalizeOutputPacket(InteriorDataPacket packet)
        {
            if (packet == null) return null;

            var portId = packet.PortId ?? string.Empty;
            var keyPortId = packet.PortKey.PortId ?? string.Empty;

            if (string.Equals(portId, keyPortId, StringComparison.Ordinal))
            {
                return packet;
            }

            GameDebug.LogWarning($"[FactoryComposite] PortId 与 PortKey 不一致，已以 PortKey 为准。PacketPortId={portId}, KeyPortId={keyPortId}");
            return new InteriorDataPacket(packet.PortKey, packet.Channel, packet.Payload);
        }
    }
}
