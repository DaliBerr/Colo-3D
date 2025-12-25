using System;
using System.Collections.Generic;

namespace Lonize.Flow
{
    /// <summary>
    /// 连续资源类型，例如电力、算力等。
    /// </summary>
    public enum FlowResourceType
    {
        Power,
        Compute
        // 以后可以在这里继续扩展其他资源类型
    }

    /// <summary>
    /// 端口类型，表示是供给方还是消耗方。
    /// </summary>
    public enum FlowEndpointKind
    {
        Producer,
        Consumer
    }

    /// <summary>
    /// Flow 端口适配器接口，由具体建筑实现，负责把抽象流量和具体游戏逻辑打通。
    /// </summary>
    public interface IFlowEndpointAdapter
    {
        /// <summary>
        /// 获取当前希望的流量（正值），比如发电机能提供多少，机器希望消耗多少。
        /// </summary>
        /// <param name="resourceType">资源类型。</param>
        /// <param name="kind">端口类型（供给或消耗）。</param>
        /// <returns>希望的流量大小，单位由游戏自定义（例如每秒多少单位电力）。</returns>
        float GetDesiredRate(FlowResourceType resourceType, FlowEndpointKind kind);

        /// <summary>
        /// 获取该端口允许的最大流量上限。
        /// </summary>
        /// <param name="resourceType">资源类型。</param>
        /// <param name="kind">端口类型。</param>
        /// <returns>最大流量上限。</returns>
        float GetMaxRate(FlowResourceType resourceType, FlowEndpointKind kind);

        /// <summary>
        /// 获取该端口的优先级，用于在资源不足时决定谁先被满足。
        /// </summary>
        /// <param name="resourceType">资源类型。</param>
        /// <param name="kind">端口类型。</param>
        /// <returns>整数优先级，数值越大优先级越高。</returns>
        int GetPriority(FlowResourceType resourceType, FlowEndpointKind kind);

        /// <summary>
        /// 应用本 tick 的流量结算结果。
        /// </summary>
        /// <param name="resourceType">资源类型。</param>
        /// <param name="kind">端口类型。</param>
        /// <param name="actualRate">实际分配到的流量。</param>
        /// <param name="deltaTime">本次 tick 的时间长度（秒）。</param>
        /// <returns>无返回值。</returns>
        void ApplyFlow(FlowResourceType resourceType, FlowEndpointKind kind, float actualRate, float deltaTime);
    }

    /// <summary>
    /// Flow 端口对象，代表一个建筑在某种资源上的一个供给或消耗接口。
    /// </summary>
    public sealed class FlowEndpoint
    {
        /// <summary>
        /// 资源类型。
        /// </summary>
        public FlowResourceType ResourceType { get; }

        /// <summary>
        /// 端口类型（供给或消耗）。
        /// </summary>
        public FlowEndpointKind Kind { get; }

        /// <summary>
        /// 端口背后的适配器，由具体建筑提供。
        /// </summary>
        public IFlowEndpointAdapter Adapter { get; }

        /// <summary>
        /// 构造一个 Flow 端口。
        /// </summary>
        /// <param name="resourceType">资源类型。</param>
        /// <param name="kind">端口类型。</param>
        /// <param name="adapter">适配器实现，用于访问具体建筑逻辑。</param>
        /// <returns>无返回值。</returns>
        public FlowEndpoint(FlowResourceType resourceType, FlowEndpointKind kind, IFlowEndpointAdapter adapter)
        {
            ResourceType = resourceType;
            Kind = kind;
            Adapter = adapter;
        }

        /// <summary>
        /// 获取当前希望的流量（已经由适配器决定）。
        /// </summary>
        /// <returns>希望的流量大小。</returns>
        public float GetDesiredRate()
        {
            return Adapter.GetDesiredRate(ResourceType, Kind);
        }

        /// <summary>
        /// 获取流量上限。
        /// </summary>
        /// <returns>最大流量。</returns>
        public float GetMaxRate()
        {
            return Adapter.GetMaxRate(ResourceType, Kind);
        }

        /// <summary>
        /// 获取优先级。
        /// </summary>
        /// <returns>优先级数值。</returns>
        public int GetPriority()
        {
            return Adapter.GetPriority(ResourceType, Kind);
        }

        /// <summary>
        /// 把结算后的实际流量应用到适配器。
        /// </summary>
        /// <param name="actualRate">实际流量。</param>
        /// <param name="deltaTime">时间步长。</param>
        /// <returns>无返回值。</returns>
        public void ApplyFlow(float actualRate, float deltaTime)
        {
            Adapter.ApplyFlow(ResourceType, Kind, actualRate, deltaTime);
        }
    }

    /// <summary>
    /// 单个连通网络，包含同一种资源类型的一组端口。
    /// </summary>
    internal sealed class FlowNetwork
    {
        /// <summary>
        /// 该网络的资源类型。
        /// </summary>
        public FlowResourceType ResourceType { get; }

        /// <summary>
        /// 网络中的所有端口。
        /// </summary>
        public List<FlowEndpoint> Endpoints { get; }

        /// <summary>
        /// 构造一个新的流网络。
        /// </summary>
        /// <param name="resourceType">资源类型。</param>
        /// <returns>无返回值。</returns>
        public FlowNetwork(FlowResourceType resourceType)
        {
            ResourceType = resourceType;
            Endpoints = new List<FlowEndpoint>();
        }
    }

    /// <summary>
    /// Flow 系统入口，负责维护端口连接关系并在每个 tick 中进行资源结算。
    /// </summary>
    public sealed class FlowSystem
    {
        /// <summary>
        /// 单例实例。
        /// </summary>
        public static FlowSystem Instance => _instance ?? (_instance = new FlowSystem());

        private static FlowSystem _instance;

        /// <summary>
        /// 图结构：每个端口及其相邻端口列表。
        /// </summary>
        private readonly Dictionary<FlowEndpoint, List<FlowEndpoint>> _graph =
            new Dictionary<FlowEndpoint, List<FlowEndpoint>>();

        /// <summary>
        /// 当前拆分好的所有网络。
        /// </summary>
        private readonly List<FlowNetwork> _networks = new List<FlowNetwork>();

        /// <summary>
        /// 拓扑是否已过期，需要重建网络。
        /// </summary>
        private bool _networkDirty = true;

        /// <summary>
        /// 构造函数设为私有，使用单例访问。
        /// </summary>
        /// <returns>无返回值。</returns>
        private FlowSystem()
        {
        }

        /// <summary>
        /// 注册一个新的端口到 Flow 系统中。
        /// </summary>
        /// <param name="endpoint">要注册的端口。</param>
        /// <returns>无返回值。</returns>
        public void RegisterEndpoint(FlowEndpoint endpoint)
        {
            if (endpoint == null)
            {
                return;
            }

            if (_graph.ContainsKey(endpoint))
            {
                return;
            }

            _graph[endpoint] = new List<FlowEndpoint>();
            _networkDirty = true;
        }

        /// <summary>
        /// 将一个端口从 Flow 系统中移除。
        /// </summary>
        /// <param name="endpoint">要移除的端口。</param>
        /// <returns>无返回值。</returns>
        public void UnregisterEndpoint(FlowEndpoint endpoint)
        {
            if (endpoint == null)
            {
                return;
            }

            if (!_graph.ContainsKey(endpoint))
            {
                return;
            }

            foreach (var kvp in _graph)
            {
                kvp.Value.Remove(endpoint);
            }

            _graph.Remove(endpoint);
            _networkDirty = true;
        }

        /// <summary>
        /// 在两个端口之间建立连接（无方向）。
        /// </summary>
        /// <param name="a">端口 A。</param>
        /// <param name="b">端口 B。</param>
        /// <returns>无返回值。</returns>
        public void Connect(FlowEndpoint a, FlowEndpoint b)
        {
            if (a == null || b == null || a == b)
            {
                return;
            }

            if (!_graph.ContainsKey(a) || !_graph.ContainsKey(b))
            {
                return;
            }

            var neighborsA = _graph[a];
            if (!neighborsA.Contains(b))
            {
                neighborsA.Add(b);
            }

            var neighborsB = _graph[b];
            if (!neighborsB.Contains(a))
            {
                neighborsB.Add(a);
            }

            _networkDirty = true;
        }

        /// <summary>
        /// 断开两个端口之间的连接。
        /// </summary>
        /// <param name="a">端口 A。</param>
        /// <param name="b">端口 B。</param>
        /// <returns>无返回值。</returns>
        public void Disconnect(FlowEndpoint a, FlowEndpoint b)
        {
            if (a == null || b == null || a == b)
            {
                return;
            }

            if (!_graph.ContainsKey(a) || !_graph.ContainsKey(b))
            {
                return;
            }

            _graph[a].Remove(b);
            _graph[b].Remove(a);
            _networkDirty = true;
        }

        /// <summary>
        /// 每个 tick 调用一次，用于驱动所有网络进行一次资源结算。
        /// </summary>
        /// <param name="deltaTime">时间步长（秒）。</param>
        /// <returns>无返回值。</returns>
        public void Tick(float deltaTime)
        {
            if (_graph.Count == 0)
            {
                return;
            }

            if (_networkDirty)
            {
                RebuildNetworks();
                _networkDirty = false;
            }

            ResolveNetworks(deltaTime);
        }

        /// <summary>
        /// 当拓扑变化后，重新按连通性和资源类型拆分网络。
        /// </summary>
        /// <returns>无返回值。</returns>
        private void RebuildNetworks()
        {
            _networks.Clear();
            var visited = new HashSet<FlowEndpoint>();

            foreach (var endpoint in _graph.Keys)
            {
                if (visited.Contains(endpoint))
                {
                    continue;
                }

                var resourceType = endpoint.ResourceType;
                var network = new FlowNetwork(resourceType);
                var queue = new Queue<FlowEndpoint>();

                visited.Add(endpoint);
                queue.Enqueue(endpoint);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (current.ResourceType != resourceType)
                    {
                        continue;
                    }

                    network.Endpoints.Add(current);

                    var neighbors = _graph[current];
                    for (int i = 0; i < neighbors.Count; i++)
                    {
                        var neighbor = neighbors[i];
                        if (neighbor == null ||
                            neighbor.ResourceType != resourceType ||
                            visited.Contains(neighbor))
                        {
                            continue;
                        }

                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }

                if (network.Endpoints.Count > 0)
                {
                    _networks.Add(network);
                }
            }
        }

        /// <summary>
        /// 对所有网络进行一轮结算。
        /// </summary>
        /// <param name="deltaTime">时间步长（秒）。</param>
        /// <returns>无返回值。</returns>
        private void ResolveNetworks(float deltaTime)
        {
            for (int i = 0; i < _networks.Count; i++)
            {
                ResolveSingleNetwork(_networks[i], deltaTime);
            }
        }

        /// <summary>
        /// 内部使用的消费者记录结构，用于在一次结算中存放中间数据。
        /// </summary>
        private sealed class ConsumerRecord
        {
            /// <summary>
            /// 对应的端口。
            /// </summary>
            public FlowEndpoint Endpoint;

            /// <summary>
            /// 希望的需求量。
            /// </summary>
            public float Demand;

            /// <summary>
            /// 实际分配到的流量。
            /// </summary>
            public float Allocated;

            /// <summary>
            /// 优先级。
            /// </summary>
            public int Priority;
        }

        /// <summary>
        /// 对单个网络执行一次供需结算。
        /// </summary>
        /// <param name="network">要处理的网络。</param>
        /// <param name="deltaTime">时间步长（秒）。</param>
        /// <returns>无返回值。</returns>
        private void ResolveSingleNetwork(FlowNetwork network, float deltaTime)
        {
            var producers = new List<FlowEndpoint>();
            var consumers = new List<ConsumerRecord>();

            float totalSupply = 0f;
            float totalDemand = 0f;

            // 先扫描一遍端口，统计供给与需求
            for (int i = 0; i < network.Endpoints.Count; i++)
            {
                var endpoint = network.Endpoints[i];
                var desired = endpoint.GetDesiredRate();
                var max = endpoint.GetMaxRate();
                if (desired <= 0f || max <= 0f)
                {
                    continue;
                }

                var clamped = desired > max ? max : desired;

                if (endpoint.Kind == FlowEndpointKind.Producer)
                {
                    producers.Add(endpoint);
                    totalSupply += clamped;
                }
                else if (endpoint.Kind == FlowEndpointKind.Consumer)
                {
                    var record = new ConsumerRecord
                    {
                        Endpoint = endpoint,
                        Demand = clamped,
                        Allocated = 0f,
                        Priority = endpoint.GetPriority()
                    };
                    consumers.Add(record);
                    totalDemand += clamped;
                }
            }

            // 没有供给：所有消费者拿不到资源，所有生产者也不输出
            if (producers.Count == 0)
            {
                for (int i = 0; i < consumers.Count; i++)
                {
                    consumers[i].Endpoint.ApplyFlow(0f, deltaTime);
                }

                for (int i = 0; i < producers.Count; i++)
                {
                    producers[i].ApplyFlow(0f, deltaTime);
                }

                return;
            }

            // 没有人要：所有流量为 0
            if (totalDemand <= 0f)
            {
                for (int i = 0; i < consumers.Count; i++)
                {
                    consumers[i].Endpoint.ApplyFlow(0f, deltaTime);
                }

                for (int i = 0; i < producers.Count; i++)
                {
                    producers[i].ApplyFlow(0f, deltaTime);
                }

                return;
            }

            float totalSatisfiedDemand = 0f;

            // 供给充足：所有需求全额满足
            if (totalSupply >= totalDemand)
            {
                for (int i = 0; i < consumers.Count; i++)
                {
                    consumers[i].Allocated = consumers[i].Demand;
                    totalSatisfiedDemand += consumers[i].Allocated;
                }
            }
            else
            {
                // 供给不足：按优先级从高到低分配
                consumers.Sort((a, b) => b.Priority.CompareTo(a.Priority));

                float remaining = totalSupply;

                for (int i = 0; i < consumers.Count; i++)
                {
                    if (remaining <= 0f)
                    {
                        consumers[i].Allocated = 0f;
                        continue;
                    }

                    float take = consumers[i].Demand;
                    if (take > remaining)
                    {
                        take = remaining;
                    }

                    consumers[i].Allocated = take;
                    remaining -= take;
                    totalSatisfiedDemand += take;
                }
            }

            // 计算生产者缩放比例，让所有生产者按比例出力
            float producerScale = 0f;
            if (totalSupply > 0f && totalSatisfiedDemand > 0f)
            {
                producerScale = totalSatisfiedDemand / totalSupply;
            }

            // 先把结果应用到消费者
            for (int i = 0; i < consumers.Count; i++)
            {
                consumers[i].Endpoint.ApplyFlow(consumers[i].Allocated, deltaTime);
            }

            // 再应用到生产者（可能不是满负荷）
            for (int i = 0; i < producers.Count; i++)
            {
                float desired = producers[i].GetDesiredRate();
                float max = producers[i].GetMaxRate();
                float clamped = desired > max ? max : desired;
                float actual = clamped * producerScale;
                producers[i].ApplyFlow(actual, deltaTime);
            }
        }
    }
}
