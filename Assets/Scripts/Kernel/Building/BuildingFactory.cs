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

            var colliderInit = go.GetComponent<BuildingColliderInit>();
            if (colliderInit != null)
            {
                colliderInit.Initialize();
            }
            return go;
        }

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

        return new FactoryChildRuntime
        {
            Def = def,
            BuildingParentID = parentBuildingId,
            BuildingLocalID = localId,
            CellPosition = cell,
            Category = def.Category
        };
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
        children = BuildingFactoryController.Instance.GetCurrentFactoryRuntime().FactoryInterior.Children;
        return children != null;
        

    }
        public static async Task<Sprite> LoadIconAsync(BuildingDef def) =>
            await AddressableRef.LoadAsync<Sprite>(def.IconAddress);
    }
}
