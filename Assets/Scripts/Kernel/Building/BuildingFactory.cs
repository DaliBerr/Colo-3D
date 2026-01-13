using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Kernel.Item;
using Lonize.Logging;
using Lonize.Math;
using System.Linq;
namespace Kernel.Building
{


    public static class BuildingFactory
    {
        /// <summary>
        /// 在世界中生成一个建筑实例。
        /// </summary>
        /// <param name="id">建筑的唯一标识符。</param>
        /// <param name="pos">建筑生成的位置。</param>
        /// <param name="rot">建筑生成的旋转角度。</param>
        /// <returns>生成的建筑游戏对象。</returns>
        public static async Task<GameObject> SpawnToWorldAsync(string id, Vector3 pos, Quaternion rot)
        {
            if (!BuildingDatabase.TryGet(id, out var def))
            {
                Log.Error($"[Building] 未找到ID：{id}");
                GameDebug.LogError($"[Building] 未找到ID：{id}");
                return null;
            }

            if (def.Category == BuildingCategory.Internal)
            {
                Log.Error($"[Building] 内部建筑不允许生成模型：{id}");
                GameDebug.LogError($"[Building] 内部建筑不允许生成模型：{id}");
                return null;
            }

            if (string.IsNullOrWhiteSpace(def.PrefabAddress))
            {
                Log.Error($"[Building] 缺少 PrefabAddress：{id}");
                GameDebug.LogError($"[Building] 缺少 PrefabAddress：{id}");
                return null;
            }

            var prefab = await AddressableRef.LoadAsync<GameObject>(def.PrefabAddress);
            var go = prefab ? Object.Instantiate(prefab, pos, rot)
                            : new GameObject($"Building_{id}");

            var host = go.GetComponent<BuildingRuntimeHost>();
            if (!host) host = go.AddComponent<BuildingRuntimeHost>();

            host.Runtime = new BuildingRuntime
            {
                Def = def,
                BuildingID = BuildingIDManager.GenerateBuildingID(),
                Category = def.Category
            };

            if (def.Category == BuildingCategory.Factory)
                host.Runtime.EnsureFactoryInterior();

            host.Behaviours.Clear();

            foreach (var c in def.Components)
            {
                var bh = BuildingBehaviourFactory.Create(c);
                if (bh != null)
                {
                    bh.OnBind(host.Runtime);
                    host.Behaviours.Add(bh);
                }
            }

            if (def.Category == BuildingCategory.Factory)
            {
                host.InitializeFactoryInterfaceFilters();
            }

            var colliderInit = go.GetComponent<BuildingColliderInit>();
            if (colliderInit != null)
            {
                colliderInit.Initialize();
            }
            return go;
        }
    /// <summary>
    /// summary: 创建工厂内部运行时实例（不挂载到场景，仅数据层面）。
    /// </summary>
    /// <param name="parentBuildingId">父建筑的全局唯一ID</param>
    /// <param name="defID">内部建筑的定义ID</param>
    /// <param name="cell">内部建筑在工厂内部的格子位置</param>
    /// <returns>创建的工厂内部运行时实例</returns>
    public static FactoryChildRuntime CreateInternalRuntime(long parentBuildingId, string defID, Vector2Int cell)
    {
        if (!BuildingDatabase.TryGet(defID, out var def))
        {
            Log.Error($"[Building] 未找到ID：{defID}");
            GameDebug.LogError($"[Building] 未找到ID：{defID}");
            return null;
        }

        if (def.Category != BuildingCategory.Internal)
        {
            GameDebug.LogWarning($"[Building] Def 不是内部建筑类型：{defID}");
        }

        // 1) 如果该父节点的 local 发号器尚未建立上下文：扫一遍已有子物品，计算 max+1 初始化 nextLocal
        if (!BuildingIDManager.TryGetNextLocalBuildingID(parentBuildingId, out _))
        {
            int nextLocal = 1;

            if (TryGetParentChildren(parentBuildingId, out var children) && children != null)
            {
                // 如果你也想防止“同一格重复放置”，可以在这里额外检查 cell 是否已被占用
                // if (children.Any(c => c != null && c.CellPosition == cell)) { ... return null; }

                nextLocal = BuildingIDManager.ComputeNextLocalIdFromUsed(
                    children.Where(c => c != null).Select(c => c.BuildingLocalID),
                    startValue: 1
                );
            }

            BuildingIDManager.SetNextLocalBuildingID(parentBuildingId, nextLocal);
        }

        // 2) 正式分配 localID（只保证在该 parentBuildingId 下唯一）
        int localId = BuildingIDManager.GenerateLocalBuildingID(parentBuildingId);

        var ChildRuntime = new FactoryChildRuntime
        {
            Def = def,
            BuildingParentID = parentBuildingId,
            BuildingLocalID = localId,
            CellPosition = cell,
            Category = def.Category
        };
        InitializeInternalBehaviours(ChildRuntime);
        return ChildRuntime;
    }

    public static void InitializeInternalBehaviours(FactoryChildRuntime child)
    {
        GameDebug.Log($"[FactoryInterior] 初始化内部建筑行为组件：ParentID={child.BuildingParentID}, LocalID={child.BuildingLocalID}, DefID={child.Def.Id}");
        if (child.Def == null) return;

        // 1. 构造一个代理 Runtime，让 Behaviour 以为自己在操作一个标准建筑
        // 关键：共享 RuntimeStats 引用，这样 Behaviour 修改属性时，数据会存下来
        child.ProxyRuntime = new BuildingRuntime
        {
            Def = child.Def,
            BuildingID = child.BuildingLocalID, // 或者组合ID，看你需求
            // 注意：这里为了日志清晰，你可能想存一个特殊的 ID，或者就把 localID 给它
            // 如果 TestCounter 需要全局唯一ID，可能需要特殊处理
            Category = child.Category,
            CellPosition = child.CellPosition,
            RuntimeStats = child.RuntimeStats 
        };

        child.Behaviours = new List<IBuildingBehaviour>();
        GameDebug.Log($"[FactoryInterior] 为内部建筑 {child.Def.Id} 创建行为组件，共有 {child.Def.Components.Count} 个组件配置。");
        // 2. 遍历 Def 中的组件配置，实例化 Behaviour
        foreach (var compData in child.Def.Components)
        {
            var behaviour = BuildingBehaviourFactory.Create(compData);
            if (behaviour != null)
            {
                // 绑定到代理 Runtime 上
                behaviour.OnBind(child.ProxyRuntime);
                child.Behaviours.Add(behaviour);
            }
            else
            {
                GameDebug.LogWarning($"无法为内部建筑 {child.Def.Id} 创建行为组件，类型：{compData.GetType().Name}");
            }
        }
    }
    
    /// <summary>
    /// summary: 获取父节点当前已有的内部子物品列表（用于加载后/首次创建时扫描 localID）。
    /// param: parentBuildingId 父节点全局ID
    /// param: children 输出子物品列表
    /// return: 是否成功获取
    /// </summary>
    private static bool TryGetParentChildren(long parentBuildingId, out IReadOnlyList<FactoryChildRuntime> children)
    {
        children = null;
        BuildingManager.Instance.getBuildingById(parentBuildingId, out var building);
        children = building?.FactoryInterior.Children;
        return children != null;
        

    }
    public static async Task<Sprite> LoadIconAsync(BuildingDef def) =>
        await AddressableRef.LoadAsync<Sprite>(def.IconAddress);

    /// <summary>
    /// summary: 构建工厂合成行为并绑定到运行时。
    /// param: runtime 工厂建筑运行时
    /// return: 构建的合成行为，失败返回 null
    /// </summary>
    public static FactoryCompositeBehaviour BuildFactoryCompositeBehaviour(BuildingRuntime runtime)
    {
        if (runtime == null)
        {
            return null;
        }

        if (runtime.CompositeBehaviour != null)
        {
            runtime.CompositeBehaviour.OnUnbind(runtime);
        }

        var composite = new FactoryCompositeBehaviour();
        composite.OnBind(runtime);
        runtime.CompositeBehaviour = composite;
        return composite;
    }
    }
}
