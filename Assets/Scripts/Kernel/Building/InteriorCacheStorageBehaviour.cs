using System.Collections.Generic;
using Kernel.Factory.Connections;
using Kernel.Storage;
using UnityEngine;

namespace Kernel.Building
{
    /// <summary>
    /// summary: 工厂内部缓存箱标识接口（用于排除外部接口逻辑）。
    /// </summary>
    public interface IInteriorCacheStorage
    {
    }

    /// <summary>
    /// summary: 工厂内部缓存箱行为（仅在内部系统使用，不影响外部接口）。
    /// </summary>
    public sealed class InteriorCacheStorageBehaviour : InteriorStorageBehaviour, IInteriorCacheStorage
    {
        private const string InputPortId = "interface_in";
        private const string OutputPortId = "interface_out";
        private readonly int _capacity;
        private readonly List<string> _allowTags;
        private StorageContainer _container;

        /// <summary>
        /// summary: 创建内部缓存箱行为实例。
        /// param: capacity 缓存箱容量
        /// param: allowTags 允许标签列表
        /// return: 无
        /// </summary>
        public InteriorCacheStorageBehaviour(int capacity, List<string> allowTags)
        {
            _capacity = Mathf.Max(0, capacity);
            _allowTags = allowTags != null ? new List<string>(allowTags) : new List<string>();
        }

        /// <summary>
        /// summary: 绑定运行时并初始化内部缓存容器。
        /// param: runtime 建筑运行时
        /// return: 无
        /// </summary>
        public override void OnBind(BuildingRuntime runtime)
        {
            base.OnBind(runtime);
            SetExternalInterfaceEnabled(false);
            _container = runtime != null
                ? new StorageContainer(runtime.BuildingID, runtime.CellPosition, _capacity, _allowTags, 0)
                : null;
        }

        /// <summary>
        /// summary: 解绑运行时并清理缓存容器。
        /// param: runtime 建筑运行时
        /// return: 无
        /// </summary>
        public override void OnUnbind(BuildingRuntime runtime)
        {
            base.OnUnbind(runtime);
            _container = null;
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
        /// summary: 构建缓存箱端口声明。
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
        /// summary: 尝试向缓存箱存入物品。
        /// param: itemId 物品ID
        /// param: count 请求数量
        /// param: itemTags 物品标签
        /// param: added 实际存入数量
        /// return: 是否存入成功
        /// </summary>
        public bool TryAdd(string itemId, int count, IReadOnlyList<string> itemTags, out int added)
        {
            added = 0;
            if (_container == null)
            {
                return false;
            }

            return _container.TryAdd(itemId, count, itemTags, out added);
        }

        /// <summary>
        /// summary: 尝试从缓存箱取出物品。
        /// param: itemId 物品ID
        /// param: count 请求数量
        /// param: removed 实际取出数量
        /// return: 是否取出成功
        /// </summary>
        public bool TryRemove(string itemId, int count, out int removed)
        {
            removed = 0;
            if (_container == null)
            {
                return false;
            }

            return _container.TryRemove(itemId, count, out removed);
        }

        /// <summary>
        /// summary: 获取缓存箱中某物品数量。
        /// param: itemId 物品ID
        /// return: 物品数量
        /// </summary>
        public int GetCount(string itemId)
        {
            return _container?.GetCount(itemId) ?? 0;
        }

        /// <summary>
        /// summary: 获取缓存箱已使用容量。
        /// param: 无
        /// return: 已使用数量
        /// </summary>
        public int GetUsed()
        {
            return _container?.GetUsed() ?? 0;
        }

        /// <summary>
        /// summary: 获取缓存箱剩余容量。
        /// param: 无
        /// return: 剩余数量
        /// </summary>
        public int GetFree()
        {
            return _container?.GetFree() ?? 0;
        }

        /// <summary>
        /// summary: 获取缓存箱容量。
        /// param: 无
        /// return: 总容量
        /// </summary>
        public int GetCapacity()
        {
            return _container?.Capacity ?? 0;
        }
    }
}
