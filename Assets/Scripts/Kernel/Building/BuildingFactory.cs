using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Kernel.Item;
using Lonize.Logging;
using Lonize.Math; // 若要和物品系统打通（可移除）
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

            var prefab = await AddressableRef.LoadAsync<GameObject>(def.PrefabAddress);
            var go = prefab ? Object.Instantiate(prefab, pos, rot)
                            : new GameObject($"Building_{id}");

            var host = go.GetComponent<BuildingRuntimeHost>();
            if (!host) host = go.AddComponent<BuildingRuntimeHost>();

            host.Runtime = new BuildingRuntime { Def = def, HP = def.MaxHP , BuildingID =  BuildingIDManager.GenerateBuildingID()};
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

        public static async Task<Sprite> LoadIconAsync(BuildingDef def) =>
            await AddressableRef.LoadAsync<Sprite>(def.IconAddress);
    }
}