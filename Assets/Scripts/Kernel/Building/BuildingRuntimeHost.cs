using System;
using System.Collections.Generic;
using UnityEngine;
using Kernel.World;
using Lonize.Logging;
using static Kernel.Storage.BuildingRuntimeStatsCodeC;
using Kernel.Storage;
using Kernel.Factory.Connections;
using Lonize;
using Lonize.Tick;

namespace Kernel.Building
{
    /// <summary>
    /// summary: 建筑实例宿主，持有 Runtime 与行为列表，并负责生成/应用存档数据。
    /// </summary>
    public class BuildingRuntimeHost : MonoBehaviour,ITickable
    {
        [SerializeField]public BuildingRuntime Runtime;
        public List<IBuildingBehaviour> Behaviours = new();
        private readonly HashSet<IInteriorIOFilterProvider> _ioFilterProviders = new();
        private readonly FactoryFilterResolver _factoryFilterResolver = new();

        /// <summary>
        /// summary: 判断一个运行时 StatKey 是否为库存编码键（以 __inv__ 前缀存储）。
        /// param: key StatKey
        /// return: true=库存编码键；false=普通键
        /// </summary>
        private static bool IsInventoryStatKey(string key)
        {
            return !string.IsNullOrEmpty(key) && key.StartsWith(StorageRuntimeStatsCodec.ItemKeyPrefix);
        }

        public void Tick(int ticks)
        {
            if (Runtime != null && Runtime.Def != null && Runtime.Def.Category == BuildingCategory.Factory)
            {
                FactoryCompositeBehaviour composite = Runtime.CompositeBehaviour;
                if (composite == null)
                {
                    foreach (var behaviour in Behaviours)
                    {
                        if (behaviour is FactoryCompositeBehaviour factoryComposite)
                        {
                            composite = factoryComposite;
                            break;
                        }
                    }
                }

                if (composite != null)
                {
                    composite.Tick(ticks);
                    return;
                }
            }

            // 1. 驱动自身的组件 (比如工厂本身的耗电)
            foreach (var behaviour in Behaviours)
            {
                if (behaviour is ITickable tickable)
                {
                    tickable.Tick(ticks);
                }
            }

        }

        private DevControls _devControls;
        /// <summary>
        /// summary: 在建筑被移除/回收前清理运行时数据与行为，避免残留状态被复用。
        /// param: 无
        /// return: 无
        /// </summary>
        public void CleanupForRemoval()
        {
            if (Runtime == null)
            {
                Behaviours?.Clear();
                return;
            }

            UnsubscribeFactoryInterfaceFilters();
            bool compositeInList = ContainsBehaviour(Behaviours, Runtime.CompositeBehaviour);
            UnbindBehaviours(Behaviours, Runtime);
            Behaviours?.Clear();

            if (Runtime.CompositeBehaviour != null && !compositeInList)
            {
                SafeUnbindBehaviour(Runtime.CompositeBehaviour, Runtime);
            }

            Runtime.CompositeBehaviour = null;

            if (Runtime.Def != null && Runtime.Def.Category == BuildingCategory.Factory)
            {
                ClearFactoryInteriorRuntime(Runtime);
                BuildingIDManager.ReleaseLocalIdContext(Runtime.BuildingID);
            }

            Runtime.RuntimeStats?.Clear();
        }

        /// <summary>
        /// summary: 清理工厂内部建筑运行时数据与行为（包含连接与统计数据）。
        /// param: runtime 工厂建筑运行时
        /// return: 无
        /// </summary>
        private static void ClearFactoryInteriorRuntime(BuildingRuntime runtime)
        {
            if (runtime?.FactoryInterior == null)
                return;

            var interior = runtime.FactoryInterior;
            if (interior.Children != null)
            {
                foreach (var child in interior.Children)
                {
                    if (child == null) continue;
                    UnbindBehaviours(child.Behaviours, child.ProxyRuntime);
                    child.Behaviours?.Clear();
                    child.RuntimeStats?.Clear();
                    child.ProxyRuntime = null;
                }

                interior.Children.Clear();
            }

            interior.InteriorLinks?.Clear();
            interior.Connections?.Graph?.Clear();
        }

        /// <summary>
        /// summary: 安全解绑行为列表，避免异常阻断清理流程。
        /// param: behaviours 行为列表
        /// param: runtime 解绑所需运行时
        /// return: 无
        /// </summary>
        private static void UnbindBehaviours(List<IBuildingBehaviour> behaviours, BuildingRuntime runtime)
        {
            if (behaviours == null || behaviours.Count == 0)
                return;

            for (int i = 0; i < behaviours.Count; i++)
            {
                var behaviour = behaviours[i];
                SafeUnbindBehaviour(behaviour, runtime);
            }
        }

        /// <summary>
        /// summary: 解绑单个行为并记录异常。
        /// param: behaviour 需要解绑的行为
        /// param: runtime 对应运行时
        /// return: 无
        /// </summary>
        private static void SafeUnbindBehaviour(IBuildingBehaviour behaviour, BuildingRuntime runtime)
        {
            if (behaviour == null)
                return;

            try
            {
                behaviour.OnUnbind(runtime);
            }
            catch (System.Exception ex)
            {
                GameDebug.LogWarning($"[BuildingRuntimeHost] 行为解绑异常：{behaviour.GetType().Name}, error={ex}");
            }
        }

        /// <summary>
        /// summary: 判断行为列表中是否包含指定行为引用。
        /// param: behaviours 行为列表
        /// param: target 目标行为
        /// return: true=包含；false=不包含
        /// </summary>
        private static bool ContainsBehaviour(List<IBuildingBehaviour> behaviours, IBuildingBehaviour target)
        {
            if (behaviours == null || target == null)
                return false;

            for (int i = 0; i < behaviours.Count; i++)
            {
                if (ReferenceEquals(behaviours[i], target))
                    return true;
            }

            return false;
        }
        private void OnEnable()
        {
            // 尝试注册。如果 TickDriver 还没准备好（比如场景刚开始加载），
            // 可能需要放到 Start 里，但一般 Awake 会先于 OnEnable/Start 执行。
            if (TickDriver.Instance != null && TickDriver.Instance.tickManager != null)
            {
                TickDriver.Instance.tickManager.Register(this);
            }
        }
        private void Start()
        {
            _devControls = InputActionManager.Instance.Dev;    
        }

        private void Update()
        {

            if( _devControls.Building.PrintInfo.WasPressedThisFrame())
            {
                if(Runtime != null)
                {
                    GameDebug.Log("--------------------------------------------------");
                    GameDebug.Log($"🏠 Building ID: {Runtime.BuildingID}, Def ID: {Runtime.Def?.Id}, Category: {Runtime.Category}, CellPosition: {Runtime.CellPosition}, RotationSteps: {Runtime.RotationSteps}");
                    GameDebug.Log("   Runtime Stats:");
                    if(Runtime.RuntimeStats != null)
                    {
                        foreach(var kv in Runtime.RuntimeStats)
                        {
                            GameDebug.Log($"      {kv.Key} : {kv.Value}");
                        }
                    }
                    else
                    {
                        GameDebug.Log("      (none)");
                    }
                    GameDebug.Log("   Behaviours:");
                    foreach(var behaviour in Behaviours)
                    {
                        GameDebug.Log($"      {behaviour.GetType().Name}");
                    }
                    GameDebug.Log($"Category Specific Info: {Runtime.Category}");
                    GameDebug.Log($"   Factory Interior Children Count: {Runtime.FactoryInterior?.Children?.Count}");
                    if(Runtime.FactoryInterior?.Children != null)
                    {
                        foreach(var child in Runtime.FactoryInterior.Children)
                        {
                            GameDebug.Log($"      Child Def ID: {child.Def?.Id}, ParentID: {child.BuildingParentID}, LocalID: {child.BuildingLocalID}, CellPosition: {child.CellPosition}");
                        
                            GameDebug.Log($"      Child LocalID: {child.BuildingLocalID}");

                            GameDebug.Log("      Child Runtime Stats:");
                            if(child.RuntimeStats != null)
                            {
                                foreach(var kv in child.RuntimeStats)
                                {
                                    GameDebug.Log($"         {kv.Key} : {kv.Value}");
                                }
                            }
                            else
                            {
                                GameDebug.Log("         (none)");
                            }

                            // GameDebug.Log("      Child Behaviours:");
                            // foreach(var behaviour in child.Behaviours)
                            // {
                            //     GameDebug.Log($"         {behaviour.GetType().Name}");
                            // }
                            GameDebug.Log("      ----------------------------");
                        }
                    }

                    GameDebug.Log("--------------------------------------------------");
                }
            }
        }
        /// <summary>
        /// summary: 移除 RuntimeStats 中的库存编码键，避免读档后 RuntimeStats 污染导致二次保存重复写入。
        /// param: stats 运行时 Stat 字典
        /// return: 无
        /// </summary>
        private static void StripInventoryStatKeys(Dictionary<string, float> stats)
        {
            if (stats == null || stats.Count == 0) return;

            // 先收集再删除，避免遍历期间修改字典
            List<string> toRemove = null;
            foreach (var kv in stats)
            {
                if (!IsInventoryStatKey(kv.Key)) continue;
                toRemove ??= new List<string>();
                toRemove.Add(kv.Key);
            }

            if (toRemove == null) return;
            for (int i = 0; i < toRemove.Count; i++)
                stats.Remove(toRemove[i]);
        }

        [Header("Placement (Grid)")]
        [SerializeField] private int _cellX;
        [SerializeField] private int _cellZ;
        [SerializeField] private byte _rotSteps;
        [SerializeField] private bool _hasPlacement;

        /// <summary>
        /// summary: 写入放置数据，并同步到 Runtime（路径B：Spawn 时先 SetPlacement 再 Bind 行为）。
        /// param: anchorCell 锚点格（x=CellX, y=CellZ）
        /// param: rotSteps 旋转步数（0..3）
        /// return: 无
        /// </summary>
        public void SetPlacement(Vector3Int anchorCell, byte rotSteps)
        {
            _cellX = anchorCell.x;
            _cellZ = anchorCell.y;
            _rotSteps = (byte)(rotSteps & 3);
            _hasPlacement = true;

            if (Runtime != null)
            {
                Runtime.CellPosition = new Vector2Int(_cellX, _cellZ);
                Runtime.RotationSteps = (byte)(_rotSteps & 3);
            }
        }

        /// <summary>
        /// summary: 尝试获取放置数据。
        /// param: anchorCell 输出锚点格
        /// param: rotSteps 输出旋转步数
        /// return: 是否存在放置信息
        /// </summary>
        public bool TryGetPlacement(out Vector3Int anchorCell, out byte rotSteps)
        {
            anchorCell = new Vector3Int(_cellX, _cellZ, 0);
            rotSteps = (byte)(_rotSteps & 3);
            return _hasPlacement;
        }

        /// <summary>
        /// summary: 生成存档数据（3D 版：基于 WorldGrid；CellY 字段继续存 cellZ 以兼容旧结构）。
        /// param: worldGrid WorldGrid 服务
        /// return: 存档数据，失败返回 null
        /// </summary>
        public SaveBuildingInstance CreateSaveData(WorldGrid worldGrid)
        {
            if (Runtime == null || Runtime.Def == null)
                return null;

            Vector3Int cellPos;
            byte rotSteps;

            if (_hasPlacement)
            {
                cellPos = new Vector3Int(_cellX, _cellZ, 0);
                rotSteps = (byte)(_rotSteps & 3);
            }
            else
            {
                // 兜底：从 transform 反推（不推荐，但防止老对象没写 placement）
                cellPos = worldGrid != null
                    ? worldGrid.WorldToCellXZ(transform.position)
                    : new Vector3Int(Mathf.RoundToInt(transform.position.x), Mathf.RoundToInt(transform.position.z), 0);
                rotSteps = (byte)(Mathf.RoundToInt(transform.eulerAngles.y / 90f) & 3);
            }

            var data = new SaveBuildingInstance
            {
                DefId = Runtime.Def.Id,
                RuntimeId = Runtime.BuildingID,

                CellX = cellPos.x,
                CellY = cellPos.y, // 注意：这里存的是 cellZ

                RotSteps = rotSteps,
            };

            // 1) 基础 stats + 2) 库存 stats 追加
            // 注意：RuntimeStats 里可能残留库存编码键（__inv__:*）。
            // 若不跳过，会出现「基础 stats 已包含库存键 + 追加库存键」导致存档重复/膨胀。
            int baseCount = 0;
            if (Runtime.RuntimeStats != null)
            {
                foreach (var kv in Runtime.RuntimeStats)
                {
                    if (IsInventoryStatKey(kv.Key)) continue;
                    baseCount++;
                }
            }
            string[] invIds = System.Array.Empty<string>();
            int[] invCounts = System.Array.Empty<int>();
            int invCount = 0;

            if (StorageSystem.Instance != null &&
                StorageSystem.Instance.TryGet(Runtime.BuildingID, out var container) &&
                container != null)
            {
                container.Export(out invIds, out invCounts);
                invCount = invIds != null ? invIds.Length : 0;
            }

            int total = baseCount + invCount;

            if (total <= 0)
            {
                data.StatKeys = System.Array.Empty<string>();
                data.StatValues = System.Array.Empty<float>();
                // return data;
            }

            data.StatKeys = new string[total];
            data.StatValues = new float[total];

            int i = 0;

            // 写入基础 stats（跳过 __inv__:*）
            if (Runtime.RuntimeStats != null)
            {
                foreach (var kv in Runtime.RuntimeStats)
                {
                    if (IsInventoryStatKey(kv.Key)) continue;
                    data.StatKeys[i] = kv.Key;
                    data.StatValues[i] = kv.Value;
                    i++;
                }
            }

            // append 库存 stats（Key=__inv__:{itemId}, Value=count）
            for (int j = 0; j < invCount; j++)
            {
                var id = invIds[j];
                int c = (invCounts != null && j < invCounts.Length) ? invCounts[j] : 0;
                if (string.IsNullOrEmpty(id) || c <= 0) continue;

                data.StatKeys[i] = StorageRuntimeStatsCodec.ItemKeyPrefix + id;
                data.StatValues[i] = c;
                i++;
            }

            // 如果中途跳过了非法项，收缩数组
            if (i != total)
            {
                System.Array.Resize(ref data.StatKeys, i);
                System.Array.Resize(ref data.StatValues, i);
            }

            if (Runtime.FactoryInterior != null)
            {
                data.InteriorBuildings = Runtime.FactoryInterior.CreateSaveData();
                data.InteriorLinks = Runtime.FactoryInterior.InteriorLinks;

                GameDebug.Log($"[SaveBuildingInstance] Factory Interior Save Data: {data.InteriorBuildings?.Count} buildings, {data.InteriorLinks?.Count} links.");
            }
            
            return data;
        }

        /// <summary>
        /// summary: 将存档数据应用到当前建筑实例（路径B：行为绑定由外部 Spawn 流程负责）。
        /// param: data 存档数据
        /// return: 无
        /// </summary>
        public void ApplySaveData(SaveBuildingInstance data)
        {
            if (data == null) return;

            // 绑定 Def 引用（不改 Def.Id）
            Runtime ??= new BuildingRuntime();
            if (Runtime.Def == null || Runtime.Def.Id != data.DefId)
            {
                if (BuildingDatabase.TryGet(data.DefId, out var def))
                    Runtime.Def = def;
            }

            Runtime.BuildingID = data.RuntimeId;

            Runtime.RuntimeStats ??= new Dictionary<string, float>();
            Runtime.RuntimeStats.Clear();

            if (data.StatKeys != null && data.StatValues != null)
            {
                int len = Mathf.Min(data.StatKeys.Length, data.StatValues.Length);
                for (int i = 0; i < len; i++)
                {
                    var key = data.StatKeys[i];
                    var val = data.StatValues[i];
                    if (!string.IsNullOrEmpty(key))
                        Runtime.RuntimeStats[key] = val;
                }
            }

            // 库存：从 RuntimeStats 解码后交给 StorageSystem
            if (StorageRuntimeStatsCodec.TryDecodeInventory(Runtime.RuntimeStats, out var itemIds, out var counts))
            {
                if (StorageSystem.Instance != null)
                    StorageSystem.Instance.ApplyOrDeferImport(Runtime.BuildingID, itemIds, counts);
            }

            // 清理：把 __inv__:* 从 RuntimeStats 移除，避免后续保存重复写入
            StripInventoryStatKeys(Runtime.RuntimeStats);

            // 工厂：确保 interior，然后应用内部存档
            if (Runtime.Def != null && Runtime.Def.Category == BuildingCategory.Factory)
                Runtime.EnsureFactoryInterior();

            if (data.InteriorBuildings != null && data.InteriorBuildings.Count > 0 && Runtime.FactoryInterior != null)
            {
                Runtime.FactoryInterior.ApplySaveData(data.InteriorBuildings, data.InteriorLinks);
                foreach (var child in Runtime.FactoryInterior.Children)
                {
                    if (child == null) continue;
                    BuildingFactory.InitializeInternalBehaviours(child); 

                }

                if (Runtime.FactoryInterior.Connections != null)
                {
                    Runtime.FactoryInterior.Connections.RebindAllPorts(Runtime.FactoryInterior.Children);
                    Runtime.FactoryInterior.Connections.RebuildGraphFromLinks(Runtime.FactoryInterior.InteriorLinks);
                }
            }

            InitializeFactoryInterfaceFilters();
            RestoreFactoryInteriorInterfaceState();

            // 写入 placement（保证读档后再次保存一致）
            SetPlacement(new Vector3Int(data.CellX, data.CellY, 0), data.RotSteps);
        }

        /// <summary>
        /// summary: 初始化工厂内部接口过滤订阅并刷新容器过滤。
        /// param: 无
        /// return: 无
        /// </summary>
        public void InitializeFactoryInterfaceFilters()
        {
            if (Runtime?.Def == null || Runtime.Def.Category != BuildingCategory.Factory)
            {
                return;
            }

            UnsubscribeFactoryInterfaceFilters();
            SubscribeFactoryInterfaceFilters();
            UpdateFactoryContainerFilter();
        }

        /// <summary>
        /// summary: 订阅所有内部接口过滤变更事件。
        /// param: 无
        /// return: 无
        /// </summary>
        private void SubscribeFactoryInterfaceFilters()
        {
            if (Runtime?.FactoryInterior?.Children == null)
            {
                return;
            }

            foreach (var child in Runtime.FactoryInterior.Children)
            {
                if (child?.Behaviours == null)
                {
                    continue;
                }

                foreach (var behaviour in child.Behaviours)
                {
                    if (behaviour is not IInteriorIOFilterProvider provider)
                    {
                        continue;
                    }

                    if (provider is IInteriorCacheStorage)
                    {
                        continue;
                    }

                    if (_ioFilterProviders.Add(provider))
                    {
                        provider.OnIOFilterChanged += HandleFactoryIOFilterChanged;
                    }
                }
            }
        }

        /// <summary>
        /// summary: 取消订阅内部接口过滤变更事件。
        /// param: 无
        /// return: 无
        /// </summary>
        private void UnsubscribeFactoryInterfaceFilters()
        {
            if (_ioFilterProviders.Count == 0)
            {
                return;
            }

            foreach (var provider in _ioFilterProviders)
            {
                if (provider == null)
                {
                    continue;
                }

                provider.OnIOFilterChanged -= HandleFactoryIOFilterChanged;
            }

            _ioFilterProviders.Clear();
        }

        /// <summary>
        /// summary: 处理接口过滤变化并更新容器过滤。
        /// param: provider 触发事件的接口提供者
        /// return: 无
        /// </summary>
        private void HandleFactoryIOFilterChanged(IInteriorIOFilterProvider provider)
        {
            UpdateFactoryContainerFilter();
        }

        /// <summary>
        /// summary: 汇总外部接口过滤并刷新工厂容器过滤（无外部接口或无标签则拒绝全部）。
        /// param: 无
        /// return: 无
        /// </summary>
        private void UpdateFactoryContainerFilter()
        {
            if (Runtime == null || Runtime.BuildingID <= 0)
            {
                return;
            }

            if (!StorageSystem.Instance.TryGet(Runtime.BuildingID, out _))
            {
                return;
            }

            bool hasExternalInterface = false;
            foreach (var provider in _ioFilterProviders)
            {
                if (provider == null || provider is IInteriorCacheStorage || !provider.IsExternalInterface)
                {
                    continue;
                }

                hasExternalInterface = true;
                break;
            }

            var resolvedFilter = _factoryFilterResolver.ResolveFilters(_ioFilterProviders);
            bool hasValidFilters = HasValidFactoryFilters(resolvedFilter.FilterMode, resolvedFilter.AllowTags, resolvedFilter.AllowItemIds);

            if (!hasExternalInterface || !hasValidFilters)
            {
                StorageSystem.Instance.SetContainerRejectAll(Runtime.BuildingID, true);
                return;
            }

            StorageSystem.Instance.UpdateContainerFilter(Runtime.BuildingID, resolvedFilter.AllowTags, resolvedFilter.AllowItemIds, resolvedFilter.FilterMode);
        }

        /// <summary>
        /// summary: 判断工厂过滤结果是否包含有效条件。
        /// param: filterMode 过滤模式
        /// param: allowTags 允许标签列表
        /// param: allowItemIds 允许物品ID列表
        /// return: 是否包含有效过滤条件
        /// </summary>
        private static bool HasValidFactoryFilters(StorageFilterMode filterMode, IReadOnlyList<string> allowTags, IReadOnlyList<string> allowItemIds)
        {
            int tagCount = allowTags?.Count ?? 0;
            int idCount = allowItemIds?.Count ?? 0;
            switch (filterMode)
            {
                case StorageFilterMode.IdOnly:
                    return idCount > 0;
                case StorageFilterMode.TagAndId:
                    return tagCount > 0 && idCount > 0;
                case StorageFilterMode.TagOrId:
                    return tagCount > 0 || idCount > 0;
                case StorageFilterMode.TagOnly:
                default:
                    return tagCount > 0;
            }
        }

        /// <summary>
        /// summary: 恢复内部建筑外部接口状态并触发过滤刷新。
        /// param: 无
        /// return: 无
        /// </summary>
        private void RestoreFactoryInteriorInterfaceState()
        {
            if (Runtime?.FactoryInterior?.Children == null || _ioFilterProviders.Count == 0)
            {
                return;
            }

            foreach (var child in Runtime.FactoryInterior.Children)
            {
                if (child?.Behaviours == null)
                {
                    continue;
                }

                foreach (var behaviour in child.Behaviours)
                {
                    if (behaviour is InteriorStorageBehaviour storageBehaviour)
                    {
                        if (storageBehaviour is IInteriorCacheStorage)
                        {
                            continue;
                        }

                        storageBehaviour.RestoreExternalInterfaceState(child.IsExternalInterface, true);
                    }
                }
            }
        }
    }
}
