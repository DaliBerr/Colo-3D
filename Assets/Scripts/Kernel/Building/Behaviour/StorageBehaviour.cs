using System.Collections.Generic;
using Kernel.Storage;
using UnityEngine;

namespace Kernel.Building
{

    public class StorageBehaviour : IBuildingBehaviour
    {
        public int Capacity;
        public int Priority;
        public List<string> AllowTags = new();
        public List<string> AllowItemIds = new();
        public StorageFilterMode FilterMode = StorageFilterMode.TagOnly;

        public long RuntimeId { get; private set; }
        public StorageContainer Container { get; private set; }

        /// <summary>
        /// summary: 创建储物行为（仅标签过滤模式）。
        /// param: capacity 容量
        /// param: allowTags 允许标签列表
        /// param: priority 优先级
        /// return: 无
        /// </summary>
        public StorageBehaviour(int capacity, List<string> allowTags, int priority = 0)
            : this(capacity, allowTags, null, StorageFilterMode.TagOnly, priority)
        {
        }

        /// <summary>
        /// summary: 创建储物行为并配置过滤规则。
        /// param: capacity 容量
        /// param: allowTags 允许标签列表
        /// param: allowItemIds 允许物品ID列表
        /// param: filterMode 过滤模式
        /// param: priority 优先级
        /// return: 无
        /// </summary>
        public StorageBehaviour(
            int capacity,
            List<string> allowTags,
            List<string> allowItemIds,
            StorageFilterMode filterMode,
            int priority = 0)
        {
            Capacity = Mathf.Max(0, capacity);
            Priority = priority;
            if (allowTags != null) AllowTags = allowTags;
            if (allowItemIds != null) AllowItemIds = allowItemIds;
            FilterMode = filterMode;
        }

        /// <summary>
        /// summary: 绑定时创建并注册容器（仅工厂类建筑默认拒收全部物品）。
        /// param: r 建筑运行时
        /// return: 无
        /// </summary>
        public void OnBind(BuildingRuntime r)
        {
            if (r == null) return;

            RuntimeId = r.BuildingID;
            if (!CanRegisterContainer(r))
            {
                Container = null;
                return;
            }

            Container = StorageSystem.Instance.Register(
                RuntimeId,
                r.CellPosition,
                Capacity,
                AllowTags,
                AllowItemIds,
                FilterMode,
                Priority);
            if (r.Category == BuildingCategory.Factory)
            {
                Container?.SetRejectAll(true);
            }
        }

        /// <summary>
        /// summary: 解绑时注销容器。
        /// param: r 建筑运行时
        /// return: 无
        /// </summary>
        public void OnUnbind(BuildingRuntime r)
        {
            if (RuntimeId > 0 && Container != null)
                StorageSystem.Instance.Unregister(RuntimeId);

            Container = null;
            RuntimeId = 0;
        }
        public void Tick(int ticks)
        {
            // throw new NotImplementedException();
        }

        /// <summary>
        /// summary: 判断是否允许注册到全局库存系统（内部接口箱禁用容器注册）。
        /// param: runtime 建筑运行时
        /// return: 是否允许注册
        /// </summary>
        private static bool CanRegisterContainer(BuildingRuntime runtime)
        {
            if (runtime == null)
            {
                return false;
            }

            return runtime.Category != BuildingCategory.Internal;
        }
    }

}