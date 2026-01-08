using System;
using System.Collections.Generic;

namespace Kernel.Factory.Connections
{
    /// <summary>
    /// summary: 端口方向定义。
    /// </summary>
    public enum PortDirection
    {
        Input = 0,
        Output = 1,
        Bidirectional = 2
    }

    /// <summary>
    /// summary: 连接通道/资源类型（可按你的项目扩展）。
    /// </summary>
    public enum ConnectionChannel
    {
        Item = 0,
        Fluid = 1,
        Power = 2,
        Compute = 3
    }

    /// <summary>
    /// summary: 端口唯一键（稳定存档友好）。
    /// </summary>
    public readonly struct PortKey : IEquatable<PortKey>
    {
        public readonly long FactoryId;
        public readonly long LocalBuildingId;
        public readonly string PortId;

        /// <summary>
        /// summary: 创建端口唯一键。
        /// param: factoryId 工厂ID
        /// param: localBuildingId 工厂内部建筑LocalID
        /// param: portId 端口ID（建议用字符串而不是数组下标）
        /// return: 端口键
        /// </summary>
        public PortKey(long factoryId, long localBuildingId, string portId)
        {
            FactoryId = factoryId;
            LocalBuildingId = localBuildingId;
            PortId = portId ?? string.Empty;
        }

        /// <summary>
        /// summary: 判等。
        /// param: other 另一端口键
        /// return: 是否相等
        /// </summary>
        public bool Equals(PortKey other)
        {
            return FactoryId == other.FactoryId
                   && LocalBuildingId == other.LocalBuildingId
                   && string.Equals(PortId, other.PortId, StringComparison.Ordinal);
        }

        /// <summary>
        /// summary: 判等。
        /// param: obj 对象
        /// return: 是否相等
        /// </summary>
        public override bool Equals(object obj) => obj is PortKey other && Equals(other);

        /// <summary>
        /// summary: 哈希。
        /// return: hash code
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = FactoryId.GetHashCode();
                hash = (hash * 397) ^ LocalBuildingId.GetHashCode();
                hash = (hash * 397) ^ (PortId != null ? StringComparer.Ordinal.GetHashCode(PortId) : 0);
                return hash;
            }
        }

        /// <summary>
        /// summary: 便于调试输出。
        /// return: 字符串
        /// </summary>
        public override string ToString() => $"Factory:{FactoryId} Local:{LocalBuildingId} Port:{PortId}";
    }

    /// <summary>
    /// summary: 端口信息（绑定到Graph后参与连接）。
    /// </summary>
    public sealed class PortInfo
    {
        public PortKey Key { get; }
        public PortDirection Direction { get; }
        public ConnectionChannel Channel { get; }
        public int MaxLinks { get; } // <=0 表示不限制
        public bool Required { get; }

        /// <summary>
        /// summary: 创建端口信息。
        /// param: key 端口键
        /// param: direction 输入/输出/双向
        /// param: channel 通道类型
        /// param: maxLinks 最大连接数（<=0 不限制）
        /// param: required 是否为必需端口
        /// return: 端口信息
        /// </summary>
        public PortInfo(PortKey key, PortDirection direction, ConnectionChannel channel, int maxLinks = 1, bool required = false)
        {
            Key = key;
            Direction = direction;
            Channel = channel;
            MaxLinks = maxLinks;
            Required = required;
        }
    }

    /// <summary>
    /// summary: 连接信息（Link）。
    /// </summary>
    public sealed class LinkInfo
    {
        public long LinkId { get; }
        public PortKey A { get; }
        public PortKey B { get; }
        public ConnectionChannel Channel { get; }

        /// <summary>
        /// summary: 创建连接信息。
        /// param: linkId 连接ID
        /// param: a 端点A
        /// param: b 端点B
        /// param: channel 通道类型
        /// return: 连接信息
        /// </summary>
        public LinkInfo(long linkId, PortKey a, PortKey b, ConnectionChannel channel)
        {
            LinkId = linkId;
            A = a;
            B = b;
            Channel = channel;
        }

        /// <summary>
        /// summary: 判断是否包含某端口。
        /// param: key 端口键
        /// return: 是否包含
        /// </summary>
        public bool Contains(PortKey key) => A.Equals(key) || B.Equals(key);

        /// <summary>
        /// summary: 获取另一端口。
        /// param: key 已知端口
        /// return: 另一端口（若不在此Link中则返回默认值）
        /// </summary>
        public PortKey GetOther(PortKey key)
        {
            if (A.Equals(key)) return B;
            if (B.Equals(key)) return A;
            return default;
        }

        /// <summary>
        /// summary: 便于调试输出。
        /// return: 字符串
        /// </summary>
        public override string ToString() => $"Link({LinkId}) {A} <-> {B} [{Channel}]";
    }

    /// <summary>
    /// summary: 工厂内部连接图（Port + Link）。
    /// </summary>
    public sealed class FactoryConnectionGraph
    {
        private readonly Dictionary<PortKey, PortInfo> _ports = new Dictionary<PortKey, PortInfo>();
        private readonly Dictionary<long, LinkInfo> _links = new Dictionary<long, LinkInfo>();
        private readonly Dictionary<PortKey, HashSet<long>> _portToLinks = new Dictionary<PortKey, HashSet<long>>();

        private long _nextLinkId = 1;
        private readonly bool _allowParallelLinks;

        /// <summary>
        /// summary: Link新增事件（可用于UI刷新）。
        /// </summary>
        public event Action<LinkInfo> OnLinkAdded;

        /// <summary>
        /// summary: Link移除事件（可用于UI刷新）。
        /// </summary>
        public event Action<LinkInfo> OnLinkRemoved;

        /// <summary>
        /// summary: Port解绑事件（可用于UI刷新）。
        /// </summary>
        public event Action<PortKey> OnPortUnbound;

        /// <summary>
        /// summary: 创建连接图。
        /// param: allowParallelLinks 是否允许同一对端口重复连多条线
        /// return: Graph实例
        /// </summary>
        public FactoryConnectionGraph(bool allowParallelLinks = false)
        {
            _allowParallelLinks = allowParallelLinks;
        }

        /// <summary>
        /// summary: 绑定一个端口到Graph。
        /// param: port 端口信息
        /// param: error 失败原因
        /// return: 是否成功
        /// </summary>
        public bool TryBindPort(PortInfo port, out string error)
        {
            error = null;
            if (port == null)
            {
                error = "PortInfo 为空。";
                return false;
            }

            if (string.IsNullOrEmpty(port.Key.PortId))
            {
                error = "PortId 为空。";
                return false;
            }

            if (_ports.ContainsKey(port.Key))
            {
                error = $"端口已存在：{port.Key}";
                return false;
            }

            _ports.Add(port.Key, port);
            if (!_portToLinks.ContainsKey(port.Key))
                _portToLinks.Add(port.Key, new HashSet<long>());

            return true;
        }

        /// <summary>
        /// summary: 解绑端口，并清理相关连接。
        /// param: key 端口键
        /// return: 是否成功解绑（不存在返回false）
        /// </summary>
        public bool UnbindPort(PortKey key)
        {
            if (!_ports.ContainsKey(key))
                return false;

            // 先删除所有相关Link
            if (_portToLinks.TryGetValue(key, out var linkIds))
            {
                // 复制一份，避免边遍历边修改
                var toRemove = new List<long>(linkIds);
                for (int i = 0; i < toRemove.Count; i++)
                    RemoveLink(toRemove[i]);
            }

            _ports.Remove(key);
            _portToLinks.Remove(key);

            OnPortUnbound?.Invoke(key);
            return true;
        }

        /// <summary>
        /// summary: 尝试添加连接（会做方向/类型/容量/重复校验）。
        /// param: a 端口A
        /// param: b 端口B
        /// param: linkId 返回新连接ID
        /// param: error 失败原因
        /// return: 是否成功
        /// </summary>
        public bool TryAddLink(PortKey a, PortKey b, out long linkId, out string error)
        {
            linkId = -1;
            error = null;

            if (a.Equals(b))
            {
                error = "不能连接到自身端口。";
                return false;
            }

            if (!_ports.TryGetValue(a, out var portA))
            {
                error = $"端口不存在：{a}";
                return false;
            }

            if (!_ports.TryGetValue(b, out var portB))
            {
                error = $"端口不存在：{b}";
                return false;
            }

            if (portA.Key.FactoryId != portB.Key.FactoryId)
            {
                error = "不允许跨工厂连接。";
                return false;
            }

            if (portA.Channel != portB.Channel)
            {
                error = $"通道类型不匹配：{portA.Channel} vs {portB.Channel}";
                return false;
            }

            if (!ValidateDirections(portA.Direction, portB.Direction))
            {
                error = $"方向不匹配：{portA.Direction} -> {portB.Direction}";
                return false;
            }

            if (!ValidateCapacity(a, portA, out error)) return false;
            if (!ValidateCapacity(b, portB, out error)) return false;

            if (!_allowParallelLinks)
            {
                if (TryFindExistingLink(a, b, portA.Channel, out var existedId))
                {
                    error = $"已存在相同连接（LinkId={existedId}）。";
                    return false;
                }
            }

            var id = AllocateLinkId();
            var link = new LinkInfo(id, a, b, portA.Channel);

            _links.Add(id, link);
            _portToLinks[a].Add(id);
            _portToLinks[b].Add(id);

            linkId = id;
            OnLinkAdded?.Invoke(link);
            return true;
        }

        /// <summary>
        /// summary: 移除连接。
        /// param: linkId 连接ID
        /// return: 是否成功移除（不存在返回false）
        /// </summary>
        public bool RemoveLink(long linkId)
        {
            if (!_links.TryGetValue(linkId, out var link))
                return false;

            _links.Remove(linkId);

            if (_portToLinks.TryGetValue(link.A, out var aSet))
                aSet.Remove(linkId);

            if (_portToLinks.TryGetValue(link.B, out var bSet))
                bSet.Remove(linkId);

            OnLinkRemoved?.Invoke(link);
            return true;
        }

        /// <summary>
        /// summary: 按指定ID添加连接（用于读档恢复）。
        /// param: linkId 连接ID
        /// param: a 端口A
        /// param: b 端口B
        /// param: error 失败原因
        /// return: 是否成功
        /// </summary>
        public bool TryAddLinkWithId(long linkId, PortKey a, PortKey b, out string error)
        {
            error = null;

            if (linkId <= 0)
            {
                error = "LinkId 非法。";
                return false;
            }

            if (_links.ContainsKey(linkId))
            {
                error = $"LinkId 已存在：{linkId}";
                return false;
            }

            if (a.Equals(b))
            {
                error = "不能连接到自身端口。";
                return false;
            }

            if (!_ports.TryGetValue(a, out var portA))
            {
                error = $"端口不存在：{a}";
                return false;
            }

            if (!_ports.TryGetValue(b, out var portB))
            {
                error = $"端口不存在：{b}";
                return false;
            }

            if (portA.Key.FactoryId != portB.Key.FactoryId)
            {
                error = "不允许跨工厂连接。";
                return false;
            }

            if (portA.Channel != portB.Channel)
            {
                error = $"通道类型不匹配：{portA.Channel} vs {portB.Channel}";
                return false;
            }

            if (!ValidateDirections(portA.Direction, portB.Direction))
            {
                error = $"方向不匹配：{portA.Direction} -> {portB.Direction}";
                return false;
            }

            if (!ValidateCapacity(a, portA, out error)) return false;
            if (!ValidateCapacity(b, portB, out error)) return false;

            if (!_allowParallelLinks)
            {
                if (TryFindExistingLink(a, b, portA.Channel, out var existedId))
                {
                    error = $"已存在相同连接（LinkId={existedId}）。";
                    return false;
                }
            }

            var link = new LinkInfo(linkId, a, b, portA.Channel);
            _links.Add(linkId, link);
            _portToLinks[a].Add(linkId);
            _portToLinks[b].Add(linkId);

            if (linkId >= _nextLinkId)
                _nextLinkId = linkId + 1;

            OnLinkAdded?.Invoke(link);
            return true;
        }

        /// <summary>
        /// summary: 尝试获取端口。
        /// param: key 端口键
        /// param: port 返回端口信息
        /// return: 是否存在
        /// </summary>
        public bool TryGetPort(PortKey key, out PortInfo port) => _ports.TryGetValue(key, out port);

        /// <summary>
        /// summary: 尝试获取连接。
        /// param: linkId 连接ID
        /// param: link 返回连接信息
        /// return: 是否存在
        /// </summary>
        public bool TryGetLink(long linkId, out LinkInfo link) => _links.TryGetValue(linkId, out link);

        /// <summary>
        /// summary: 获取全部连接信息（只读拷贝）。
        /// param: 无
        /// return: 连接信息列表
        /// </summary>
        public List<LinkInfo> GetAllLinks()
        {
            var result = new List<LinkInfo>(_links.Count);
            foreach (var link in _links.Values)
                result.Add(link);
            return result;
        }

        /// <summary>
        /// summary: 获取全部端口信息（只读拷贝）。
        /// param: 无
        /// return: 端口信息列表
        /// </summary>
        public List<PortInfo> GetAllPorts()
        {
            var result = new List<PortInfo>(_ports.Count);
            foreach (var port in _ports.Values)
                result.Add(port);
            return result;
        }

        /// <summary>
        /// summary: 获取某端口的所有连接ID（只读拷贝）。
        /// param: key 端口键
        /// return: 连接ID列表
        /// </summary>
        public List<long> GetLinkIdsOfPort(PortKey key)
        {
            if (!_portToLinks.TryGetValue(key, out var set))
                return new List<long>(0);

            return new List<long>(set);
        }

        /// <summary>
        /// summary: 获取某端口的所有连接信息（只读拷贝）。
        /// param: key 端口键
        /// return: 连接信息列表
        /// </summary>
        public List<LinkInfo> GetLinksOfPort(PortKey key)
        {
            var result = new List<LinkInfo>();
            if (!_portToLinks.TryGetValue(key, out var set))
                return result;

            foreach (var id in set)
            {
                if (_links.TryGetValue(id, out var link))
                    result.Add(link);
            }
            return result;
        }

        /// <summary>
        /// summary: 判断两端口之间是否有连接。
        /// param: a 端口A
        /// param: b 端口B
        /// param: linkId 返回找到的连接ID
        /// return: 是否存在连接
        /// </summary>
        public bool HasLinkBetween(PortKey a, PortKey b, out long linkId)
        {
            return TryFindExistingLink(a, b, null, out linkId);
        }

        /// <summary>
        /// summary: 获取与起点端口连通的所有端口（BFS）。
        /// param: start 起点端口
        /// return: 连通端口列表（包含start）
        /// </summary>
        public List<PortKey> GetConnectedPorts(PortKey start)
        {
            var result = new List<PortKey>();
            if (!_ports.ContainsKey(start))
                return result;

            var visited = new HashSet<PortKey>();
            var queue = new Queue<PortKey>();

            visited.Add(start);
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                result.Add(cur);

                if (!_portToLinks.TryGetValue(cur, out var linkIds))
                    continue;

                foreach (var lid in linkIds)
                {
                    if (!_links.TryGetValue(lid, out var link))
                        continue;

                    var other = link.GetOther(cur);
                    if (other.Equals(default)) continue;
                    if (!_ports.ContainsKey(other)) continue;

                    if (visited.Add(other))
                        queue.Enqueue(other);
                }
            }

            return result;
        }

        /// <summary>
        /// summary: 清空Graph（端口与连接全部移除）。
        /// return: 无
        /// </summary>
        public void Clear()
        {
            _ports.Clear();
            _links.Clear();
            _portToLinks.Clear();
            _nextLinkId = 1;
        }

        /// <summary>
        /// summary: 读取后重建LinkId分配器（避免ID冲突）。
        /// return: 无
        /// </summary>
        public void RebuildNextLinkId()
        {
            long max = 0;
            foreach (var kv in _links)
            {
                if (kv.Key > max) max = kv.Key;
            }
            _nextLinkId = max + 1;
            if (_nextLinkId < 1) _nextLinkId = 1;
        }

        // ------------------------ private helpers ------------------------

        /// <summary>
        /// summary: 分配新的LinkId。
        /// return: 新ID
        /// </summary>
        private long AllocateLinkId()
        {
            var id = _nextLinkId++;
            if (id < 1) id = 1;
            return id;
        }

        /// <summary>
        /// summary: 校验端口容量限制。
        /// param: key 端口键
        /// param: port 端口信息
        /// param: error 失败原因
        /// return: 是否可连接
        /// </summary>
        private bool ValidateCapacity(PortKey key, PortInfo port, out string error)
        {
            error = null;

            if (port.MaxLinks <= 0)
                return true;

            if (!_portToLinks.TryGetValue(key, out var set))
            {
                error = $"端口未绑定链接表：{key}";
                return false;
            }

            if (set.Count >= port.MaxLinks)
            {
                error = $"端口连接数已达上限：{key} ({set.Count}/{port.MaxLinks})";
                return false;
            }

            return true;
        }

        /// <summary>
        /// summary: 校验方向是否允许连接。
        /// param: a A方向
        /// param: b B方向
        /// return: 是否允许
        /// </summary>
        private bool ValidateDirections(PortDirection a, PortDirection b)
        {
            // 双向与任何方向都可连（由你玩法决定）
            if (a == PortDirection.Bidirectional || b == PortDirection.Bidirectional)
                return true;

            // 常规：Output -> Input
            return a == PortDirection.Output && b == PortDirection.Input;
        }

        /// <summary>
        /// summary: 查找已存在的连接（可按通道过滤）。
        /// param: a 端口A
        /// param: b 端口B
        /// param: channel 可选过滤通道（为null则忽略）
        /// param: linkId 返回连接ID
        /// return: 是否找到
        /// </summary>
        private bool TryFindExistingLink(PortKey a, PortKey b, ConnectionChannel? channel, out long linkId)
        {
            linkId = -1;

            if (!_portToLinks.TryGetValue(a, out var aLinks))
                return false;

            foreach (var id in aLinks)
            {
                if (!_links.TryGetValue(id, out var link))
                    continue;

                if (channel.HasValue && link.Channel != channel.Value)
                    continue;

                bool same =
                    (link.A.Equals(a) && link.B.Equals(b)) ||
                    (link.A.Equals(b) && link.B.Equals(a));

                if (same)
                {
                    linkId = id;
                    return true;
                }
            }

            return false;
        }
    }
}
