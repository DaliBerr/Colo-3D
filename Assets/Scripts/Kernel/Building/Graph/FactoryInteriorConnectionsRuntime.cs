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
        public readonly bool Required;

        /// <summary>
        /// summary: 创建端口声明。
        /// param: portId 端口ID（建议稳定字符串）
        /// param: direction 端口方向
        /// param: channel 通道类型
        /// param: maxLinks 最大连接数（<=0 不限制）
        /// param: required 是否为必需端口
        /// return: 端口声明
        /// </summary>
        public PortDescriptor(string portId, PortDirection direction, ConnectionChannel channel, int maxLinks = 1, bool required = false)
        {
            PortId = portId ?? string.Empty;
            Direction = direction;
            Channel = channel;
            MaxLinks = maxLinks;
            Required = required;
        }
    }

    /// <summary>
    /// summary: 内部建筑端口提供者（建议由 Behaviour 实现）。
    /// </summary>
    public interface IInteriorPortProvider
    {
        /// <summary>
        /// summary: 获取该建筑提供的端口列表（PortId 必须与 Behaviour 使用的 ID 完全一致）。
        /// param: 无
        /// return: 端口声明枚举
        /// </summary>
        IEnumerable<PortDescriptor> GetPorts();
    }

    /// <summary>
    /// summary: 工厂内部接口过滤提供者（用于收集允许标签并联动储物过滤）。
    /// </summary>
    public interface IInteriorIOFilterProvider
    {
        /// <summary>
        /// summary: 接口过滤参数变化事件（过滤条件变化时触发）。
        /// </summary>
        event Action<IInteriorIOFilterProvider> OnIOFilterChanged;

        /// <summary>
        /// summary: 是否作为外部接口参与过滤汇总。
        /// param: 无
        /// return: 是否启用外部接口
        /// </summary>
        bool IsExternalInterface { get; }

        /// <summary>
        /// summary: 获取当前接口允许的标签列表。
        /// param: 无
        /// return: 允许标签列表（空=全收）
        /// </summary>
        IReadOnlyList<string> GetIOAllowTags();
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

            foreach (var port in ports)
            {
                var desc = port.Descriptor;
                GameDebug.Log($"[Connections] Bind PortId={desc.PortId} Provider={port.ProviderName} Direction={desc.Direction} Channel={desc.Channel} MaxLinks={desc.MaxLinks}");
                if (string.IsNullOrEmpty(desc.PortId)) continue;

                var key = new PortKey(child.BuildingParentID, child.BuildingLocalID, desc.PortId);

                if (Graph.TryGetPort(key, out _))
                {
                    continue; 
                }
                
                var info = new PortInfo(key, desc.Direction, desc.Channel, desc.MaxLinks, desc.Required);

                if (Graph.TryBindPort(info, out var error))
                {
                    count++;
                }
                else
                {
                    GameDebug.LogWarning($"[Connections] BindPort 失败: {error}");
                }
            }

            var systemPorts = CollectSystemPorts(child);
            if (systemPorts == null) return count;

            foreach (var port in systemPorts)
            {
                var desc = port.Descriptor;
                GameDebug.Log($"[Connections] Bind System PortId={desc.PortId} Provider={port.ProviderName} Direction={desc.Direction} Channel={desc.Channel} MaxLinks={desc.MaxLinks}");
                if (string.IsNullOrEmpty(desc.PortId)) continue;

                var key = new PortKey(child.BuildingParentID, child.BuildingLocalID, desc.PortId);

                if (Graph.TryGetPort(key, out _))
                {
                    continue;
                }

                var info = new PortInfo(key, desc.Direction, desc.Channel, desc.MaxLinks, desc.Required);

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
        /// summary: 校验当前连接图（必需端口、方向/通道、孤立节点等）。
        /// param: errors 返回错误列表
        /// return: 是否通过校验
        /// </summary>
        public bool ValidateGraph(out List<string> errors)
        {
            errors = new List<string>();

            if (Graph == null)
            {
                errors.Add("连接图未初始化。");
                return false;
            }

            var ports = Graph.GetAllPorts();
            var links = Graph.GetAllLinks();

            var portLinkCounts = new Dictionary<PortKey, int>();
            var buildingLinkMap = new Dictionary<(long FactoryId, long LocalId), bool>();

            for (int i = 0; i < ports.Count; i++)
            {
                var port = ports[i];
                var linkCount = Graph.GetLinkIdsOfPort(port.Key).Count;
                portLinkCounts[port.Key] = linkCount;

                var buildingKey = (port.Key.FactoryId, port.Key.LocalBuildingId);
                if (!buildingLinkMap.ContainsKey(buildingKey))
                    buildingLinkMap.Add(buildingKey, false);

                if (linkCount > 0)
                    buildingLinkMap[buildingKey] = true;

                if (port.Required && linkCount == 0)
                    errors.Add($"必需端口未连通：{port.Key}");
            }

            for (int i = 0; i < links.Count; i++)
            {
                var link = links[i];
                if (link == null) continue;

                if (!Graph.TryGetPort(link.A, out var portA))
                {
                    errors.Add($"连接引用不存在端口：{link.A}");
                    continue;
                }

                if (!Graph.TryGetPort(link.B, out var portB))
                {
                    errors.Add($"连接引用不存在端口：{link.B}");
                    continue;
                }

                if (portA.Channel != portB.Channel)
                    errors.Add($"连接通道不匹配：{portA.Channel} vs {portB.Channel} ({link.A} <-> {link.B})");

                if (link.Channel != portA.Channel || link.Channel != portB.Channel)
                    errors.Add($"连接通道与端口不一致：{link.Channel} ({link.A} <-> {link.B})");

                if (!ValidateDirections(portA.Direction, portB.Direction))
                    errors.Add($"连接方向不匹配：{portA.Direction} -> {portB.Direction} ({link.A} <-> {link.B})");
            }

            foreach (var kv in buildingLinkMap)
            {
                if (!kv.Value)
                    errors.Add($"孤立节点：FactoryId={kv.Key.FactoryId}, LocalId={kv.Key.LocalId}");
            }

            return errors.Count == 0;
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

            foreach (var port in ports)
            {
                var desc = port.Descriptor;
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
        private static List<PortDescriptorContext> CollectPorts(FactoryChildRuntime child)
        {
            var list = new List<PortDescriptorContext>();
            if (child.Behaviours == null) return list;

            for (int i = 0; i < child.Behaviours.Count; i++)
            {
                var bh = child.Behaviours[i];
                if (bh is IInteriorPortProvider provider)
                {
                    var ports = provider.GetPorts();
                    if (ports == null) continue;
                    var providerName = bh.GetType().Name;
                    foreach (var p in ports) list.Add(new PortDescriptorContext(p, providerName));
                }
            }
            return list;
        }

        /// <summary>
        /// summary: 收集某内部建筑的系统端口声明（电力/算力）。
        /// param: child 内部建筑运行时
        /// return: 系统端口声明列表（可能为空）
        /// </summary>
        private static List<PortDescriptorContext> CollectSystemPorts(FactoryChildRuntime child)
        {
            var list = new List<PortDescriptorContext>();
            if (child.Behaviours == null) return list;

            for (int i = 0; i < child.Behaviours.Count; i++)
            {
                var bh = child.Behaviours[i];
                if (bh is BaseInteriorBehaviour interiorBehaviour)
                {
                    var ports = interiorBehaviour.GetSystemPorts();
                    if (ports == null) continue;
                    var providerName = bh.GetType().Name;
                    foreach (var p in ports) list.Add(new PortDescriptorContext(p, providerName));
                }
            }

            return list;
        }

        /// <summary>
        /// summary: 校验端口方向是否允许连接。
        /// param: a A方向
        /// param: b B方向
        /// return: 是否允许
        /// </summary>
        private static bool ValidateDirections(PortDirection a, PortDirection b)
        {
            if (a == PortDirection.Bidirectional || b == PortDirection.Bidirectional)
                return true;

            return a == PortDirection.Output && b == PortDirection.Input;
        }


        /// <summary>
        /// summary: 同步所有端口（只绑定未绑定的，不清除现有连接）。
        /// 适用于 WYSIWYG 模式，确保 Graph 中包含所有子建筑的端口。
        /// </summary>
        public void SyncPorts(IReadOnlyList<FactoryChildRuntime> children)
        {
            if (Graph == null) Graph = new FactoryConnectionGraph(false);
            // 注意：这里不调用 Graph.Clear()！

            if (children == null) return;

            for (int i = 0; i < children.Count; i++)
            {
                var child = children[i];
                if (child == null) continue;
                
                // BindChildPorts 内部调用 TryBindPort，如果端口已存在会返回 false 并忽略，
                // 所以这里重复调用是安全的，不会覆盖已有数据。
                BindChildPorts(child);
            }
        }

        /// <summary>
        /// summary: 端口声明上下文（用于记录来源 Behaviour）。
        /// param: descriptor 端口声明
        /// param: providerName 端口提供者名称
        /// return: 上下文实例
        /// </summary>
        private readonly struct PortDescriptorContext
        {
            public readonly PortDescriptor Descriptor;
            public readonly string ProviderName;

            public PortDescriptorContext(PortDescriptor descriptor, string providerName)
            {
                Descriptor = descriptor;
                ProviderName = providerName ?? string.Empty;
            }
        }
    }
}
