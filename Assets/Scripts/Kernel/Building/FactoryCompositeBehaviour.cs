using System.Collections.Generic;
using Kernel.Factory.Connections;
using Lonize.Logging;
using Lonize.Tick;

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
        private FactoryInteriorConnectionsRuntime _connections;
        private readonly List<FactoryNodeRuntime> _nodes = new();
        private readonly Dictionary<long, FactoryNodeRuntime> _nodeByLocalId = new();
        private List<InteriorDataPacket> _incomingBuffer = new();
        private List<InteriorDataPacket> _outgoingBuffer = new();

        public IReadOnlyList<FactoryNodeRuntime> Nodes => _nodes;

        /// <summary>
        /// summary: 绑定工厂运行时并初始化节点数据。
        /// param: runtime 建筑运行时
        /// return: 无
        /// </summary>
        public void OnBind(BuildingRuntime runtime)
        {
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
            _connections = null;
            _nodes.Clear();
            _nodeByLocalId.Clear();
            _incomingBuffer.Clear();
            _outgoingBuffer.Clear();
        }

        /// <summary>
        /// summary: 按连接图顺序推进节点 Tick。
        /// param: ticks Tick 数量
        /// return: 无
        /// </summary>
        public void Tick(int ticks)
        {
            if (_nodes.Count == 0) return;

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
                        if (packet != null)
                        {
                            _outgoingBuffer.Add(packet);
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
    }
}
