using System.Collections.Generic;
using Kernel.Factory.Connections;
using Kernel.Storage;

namespace Kernel.Building
{
    /// <summary>
    /// summary: 工厂内部接口箱行为（库存代理到工厂容器）。
    /// </summary>
    public sealed class InteriorInterfaceBoxBehaviour : InteriorStorageBehaviour
    {
        private const string InputPortId = "interface_in";
        private const string OutputPortId = "interface_out";
        private readonly List<string> _allowTags;
        private readonly List<string> _allowItemIds;
        private readonly StorageFilterMode _filterMode;

        /// <summary>
        /// summary: 创建内部接口箱行为实例。
        /// param: allowTags 允许标签列表
        /// param: allowItemIds 允许物品ID列表
        /// param: filterMode 过滤模式
        /// return: 无
        /// </summary>
        public InteriorInterfaceBoxBehaviour(List<string> allowTags, List<string> allowItemIds, StorageFilterMode filterMode)
        {
            _allowTags = allowTags != null ? new List<string>(allowTags) : new List<string>();
            _allowItemIds = allowItemIds != null ? new List<string>(allowItemIds) : new List<string>();
            _filterMode = filterMode;
        }

        /// <summary>
        /// summary: 绑定运行时并同步接口过滤标签。
        /// param: runtime 建筑运行时
        /// return: 无
        /// </summary>
        public override void OnBind(BuildingRuntime runtime)
        {
            base.OnBind(runtime);
            SetIOAllowTags(_allowTags);
            SetIOAllowItemIds(_allowItemIds);
            SetIOFilterMode(_filterMode);
        }

        /// <summary>
        /// summary: 输入端口父ID。
        /// </summary>
        protected override string InputPortIdParent => InputPortId;

        /// <summary>
        /// summary: 输出端口父ID。
        /// </summary>
        protected override string OutputPortIdParent => OutputPortId;

        /// <summary>
        /// summary: 构建接口箱端口声明。
        /// param: 无
        /// return: 端口声明列表
        /// </summary>
        protected override IEnumerable<PortDescriptor> BuildPorts()
        {
            return new[]
            {
                new PortDescriptor(InputPortId, PortDirection.Input, ConnectionChannel.Item, 1),
                new PortDescriptor(OutputPortId, PortDirection.Output, ConnectionChannel.Item, 1)
            };
        }

        /// <summary>
        /// summary: 尝试向工厂容器存入物品。
        /// param: itemId 物品ID
        /// param: count 请求数量
        /// param: added 实际存入数量
        /// return: 是否存入成功
        /// </summary>
        public bool TryAdd(string itemId, int count, out int added)
        {
            added = 0;
            var tags = ResolveItemTags(itemId);
            if (TryGetFactoryDispatcher(out var dispatcher))
            {
                return dispatcher.TryRequestAdd(BuildingLocalId, itemId, count, tags, out added);
            }

            if (!TryGetFactoryContainer(out var container))
            {
                return false;
            }

            if (!container.TryAdd(itemId, count, tags, out added))
            {
                return false;
            }

            return added > 0;
        }

        /// <summary>
        /// summary: 尝试从工厂容器取出物品。
        /// param: itemId 物品ID
        /// param: count 请求数量
        /// param: removed 实际取出数量
        /// return: 是否取出成功
        /// </summary>
        public bool TryRemove(string itemId, int count, out int removed)
        {
            removed = 0;
            if (TryGetFactoryDispatcher(out var dispatcher))
            {
                return dispatcher.TryRequestRemove(BuildingLocalId, itemId, count, out removed);
            }

            if (!TryGetFactoryContainer(out var container))
            {
                return false;
            }

            if (!container.TryRemove(itemId, count, out removed))
            {
                return false;
            }

            return removed > 0;
        }

        /// <summary>
        /// summary: 获取工厂容器中的物品数量。
        /// param: itemId 物品ID
        /// return: 数量
        /// </summary>
        public int GetCount(string itemId)
        {
            if (!TryGetFactoryContainer(out var container))
            {
                return 0;
            }

            return container.GetCount(itemId);
        }

        /// <summary>
        /// summary: 获取工厂容器已使用容量。
        /// param: 无
        /// return: 已使用数量
        /// </summary>
        public int GetUsed()
        {
            if (!TryGetFactoryContainer(out var container))
            {
                return 0;
            }

            return container.GetUsed();
        }

        /// <summary>
        /// summary: 获取工厂容器剩余容量。
        /// param: 无
        /// return: 剩余数量
        /// </summary>
        public int GetFree()
        {
            if (!TryGetFactoryContainer(out var container))
            {
                return 0;
            }

            return container.GetFree();
        }

        /// <summary>
        /// summary: 获取工厂容器容量。
        /// param: 无
        /// return: 总容量
        /// </summary>
        public int GetCapacity()
        {
            if (!TryGetFactoryContainer(out var container))
            {
                return 0;
            }

            return container.Capacity;
        }

        /// <summary>
        /// summary: 尝试获取工厂容器引用。
        /// param: container 输出工厂容器
        /// return: 是否成功获取
        /// </summary>
        private bool TryGetFactoryContainer(out StorageContainer container)
        {
            container = null;
            if (FactoryId <= 0)
            {
                return false;
            }

            return StorageSystem.Instance != null && StorageSystem.Instance.TryGet(FactoryId, out container);
        }

        /// <summary>
        /// summary: 尝试获取工厂分发器（用于访问配额）。
        /// param: dispatcher 输出分发器
        /// return: 是否成功获取
        /// </summary>
        private bool TryGetFactoryDispatcher(out FactoryCompositeBehaviour dispatcher)
        {
            dispatcher = null;
            if (FactoryId <= 0 || BuildingManager.Instance == null)
            {
                return false;
            }

            BuildingManager.Instance.getBuildingById(FactoryId, out var runtime);
            if (runtime?.CompositeBehaviour == null)
            {
                return false;
            }

            dispatcher = runtime.CompositeBehaviour;
            return true;
        }

        /// <summary>
        /// summary: 解析物品标签。
        /// param: itemId 物品ID
        /// return: 标签列表或 null
        /// </summary>
        private IReadOnlyList<string> ResolveItemTags(string itemId)
        {
            var catalog = StorageSystem.Instance?.ItemCatalog;
            if (catalog == null)
            {
                return null;
            }

            return catalog.TryGetTags(itemId, out var tags) ? tags : null;
        }
    }
}
