using System;
using System.Collections.Generic;
using Kernel.Building;
using Kernel.Factory.Connections; // 这里引用你 Graph 所在的 namespace
using Lonize.Logging;

namespace Kernel.Factory.Connections
{
    /// <summary>
    /// summary: 端口声明（由建筑行为/组件提供，用来绑定到 Graph）。
    /// </summary>
    public readonly struct PortDescriptor
    {
        public readonly string PortId;
        public readonly PortDirection Direction;
        public readonly ConnectionChannel Channel;
        public readonly int MaxLinks;

        /// <summary>
        /// summary: 创建端口声明。
        /// param: portId 端口ID（建议稳定字符串）
        /// param: direction 端口方向
        /// param: channel 通道类型
        /// param: maxLinks 最大连接数（<=0 不限制）
        /// return: 端口声明
        /// </summary>
        public PortDescriptor(string portId, PortDirection direction, ConnectionChannel channel, int maxLinks = 1)
        {
            PortId = portId ?? string.Empty;
            Direction = direction;
            Channel = channel;
            MaxLinks = maxLinks;
        }
    }

    /// <summary>
    /// summary: 内部建筑端口提供者（建议由 Behaviour 实现）。
    /// </summary>
    public interface IInteriorPortProvider
    {
        /// <summary>
        /// summary: 获取该建筑提供的端口列表。
        /// param: 无
        /// return: 端口声明枚举
        /// </summary>
        IEnumerable<PortDescriptor> GetPorts();
    }

    /// <summary>
    /// summary: 每个工厂内部的连接运行时（持有 Graph，负责 bind/unbind 端口与维护连线）。
    /// </summary>
    [Serializable]
    public sealed class FactoryInteriorConnectionsRuntime
    {
        public FactoryConnectionGraph Graph { get; private set; }

        /// <summary>
        /// summary: 创建工厂内部连接运行时。
        /// param: allowParallelLinks 是否允许同一对端口重复连线
        /// return: 连接运行时实例
        /// </summary>
        public FactoryInteriorConnectionsRuntime(bool allowParallelLinks = false)
        {
            Graph = new FactoryConnectionGraph(allowParallelLinks);
        }

        /// <summary>
        /// summary: 重建端口绑定（通常在读档完成并初始化 behaviours 后调用）。
        /// param: children 工厂内部子建筑列表
        /// return: 无
        /// </summary>
        public void RebindAllPorts(IReadOnlyList<FactoryChildRuntime> children)
        {
            if (Graph == null) Graph = new FactoryConnectionGraph(false);
            Graph.Clear();

            if (children == null) return;

            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child == null) continue;
                BindChildPorts(child);
            }
        }

        /// <summary>
        /// summary: 绑定某个内部建筑的所有端口到 Graph（从 behaviours 扫描 IInteriorPortProvider）。
        /// param: child 内部建筑运行时
        /// return: 绑定到 Graph 的端口数量
        /// </summary>
        public int BindChildPorts(FactoryChildRuntime child)
        {
            if (child == null) return 0;

            int count = 0;
            var ports = CollectPorts(child);
            if (ports == null) return 0;

            foreach (var desc in ports)
            {
                if (string.IsNullOrEmpty(desc.PortId)) continue;

                var key = new PortKey(child.BuildingParentID, child.BuildingLocalID, desc.PortId);
                var info = new PortInfo(key, desc.Direction, desc.Channel, desc.MaxLinks);

                if (Graph.TryBindPort(info, out var error))
                {
                    count++;
                }
                else
                {
                    GameDebug.LogWarning($"[Connections] BindPort 失败: {error}");
                }
            }

            return count;
        }

        /// <summary>
        /// summary: 解绑某个内部建筑的所有端口（并自动清理相关 Link）。
        /// param: child 内部建筑运行时
        /// return: 实际解绑的端口数量
        /// </summary>
        public int UnbindChildPorts(FactoryChildRuntime child)
        {
            if (child == null) return 0;

            int count = 0;
            var ports = CollectPorts(child);
            if (ports == null) return 0;

            foreach (var desc in ports)
            {
                if (string.IsNullOrEmpty(desc.PortId)) continue;

                var key = new PortKey(child.BuildingParentID, child.BuildingLocalID, desc.PortId);
                if (Graph.UnbindPort(key)) count++;
            }

            return count;
        }

        /// <summary>
        /// summary: 创建连接（对 Graph.TryAddLink 的轻量封装）。
        /// param: a 端口A
        /// param: b 端口B
        /// param: linkId 返回新连接ID
        /// param: error 失败原因
        /// return: 是否成功
        /// </summary>
        public bool TryCreateLink(PortKey a, PortKey b, out long linkId, out string error)
        {
            linkId = -1;
            error = null;

            if (Graph == null)
            {
                error = "Graph 未初始化。";
                return false;
            }

            return Graph.TryAddLink(a, b, out linkId, out error);
        }

        /// <summary>
        /// summary: 删除连接（对 Graph.RemoveLink 的轻量封装）。
        /// param: linkId 连接ID
        /// return: 是否成功删除
        /// </summary>
        public bool RemoveLink(long linkId)
        {
            if (Graph == null) return false;
            return Graph.RemoveLink(linkId);
        }

        /// <summary>
        /// summary: 从 Graph 导出连接存档列表。
        /// param: 无
        /// return: 连接存档列表
        /// </summary>
        public List<SaveFactoryConnectionLink> ExportLinksForSave()
        {
            if (Graph == null) return new List<SaveFactoryConnectionLink>();

            var links = Graph.GetAllLinks();
            var result = new List<SaveFactoryConnectionLink>(links.Count);

            foreach (var link in links)
            {
                if (link == null) continue;
                result.Add(new SaveFactoryConnectionLink
                {
                    LinkId = link.LinkId,
                    AFactoryId = link.A.FactoryId,
                    ALocalId = link.A.LocalBuildingId,
                    APortId = link.A.PortId,
                    BFactoryId = link.B.FactoryId,
                    BLocalId = link.B.LocalBuildingId,
                    BPortId = link.B.PortId,
                    Channel = link.Channel
                });
            }

            return result;
        }

        /// <summary>
        /// summary: 从连接存档重建 Graph（需在端口绑定完成后调用）。
        /// param: links 连接存档列表
        /// return: 无
        /// </summary>
        public void RebuildGraphFromLinks(IReadOnlyList<SaveFactoryConnectionLink> links)
        {
            if (Graph == null) Graph = new FactoryConnectionGraph(false);
            if (links == null || links.Count == 0)
            {
                Graph.RebuildNextLinkId();
                return;
            }

            for (int i = 0; i < links.Count; i++)
            {
                var link = links[i];
                if (link == null) continue;

                var a = new PortKey(link.AFactoryId, link.ALocalId, link.APortId);
                var b = new PortKey(link.BFactoryId, link.BLocalId, link.BPortId);

                if (Graph.TryAddLinkWithId(link.LinkId, a, b, out var error))
                {
                    continue;
                }

                GameDebug.LogWarning($"[Connections] 连接还原失败: {error}");
            }

            Graph.RebuildNextLinkId();
        }

        /// <summary>
        /// summary: 收集某内部建筑的端口声明（扫描 behaviours 中的 IInteriorPortProvider）。
        /// param: child 内部建筑运行时
        /// return: 端口声明列表（可能为空）
        /// </summary>
        private static List<PortDescriptor> CollectPorts(FactoryChildRuntime child)
        {
            var list = new List<PortDescriptor>();
            if (child.Behaviours == null) return list;

            for (int i = 0; i < child.Behaviours.Count; i++)
            {
                var bh = child.Behaviours[i];
                if (bh is IInteriorPortProvider provider)
                {
                    var ports = provider.GetPorts();
                    if (ports == null) continue;
                    foreach (var p in ports) list.Add(p);
                }
            }
            return list;
        }
    }
}
