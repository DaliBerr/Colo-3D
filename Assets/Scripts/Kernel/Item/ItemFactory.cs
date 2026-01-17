
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Kernel.Item
{
        public class ItemRuntimeBehaviourHost : MonoBehaviour
    {
        public ItemInstance Instance;
        public List<IItemBehaviour> Behaviours = new();
    }

    public static class ItemFactory
    {
        /// <summary>
        /// 创建物品数据实例。
        /// </summary>
        /// <param name="id">物品 ID。</param>
        /// <returns>物品实例。</returns>
        public static ItemInstance CreateData(string id)
            => ItemDatabase.CreateInstance(id);

        // 实例化到场景（如掉落物/装备展示）
        public static async Task<GameObject> SpawnToWorldAsync(string id, Vector3 pos, Quaternion rot)
        {
            if (!ItemDatabase.TryGet(id, out var def)) return null;

            var prefab = await ItemDatabase.LoadPrefabAsync(def);
            GameObject go = prefab ? Object.Instantiate(prefab, pos, rot)
                                   : new GameObject($"Item_{id}");

            var host = go.GetComponent<ItemRuntimeBehaviourHost>();
            if (!host) host = go.AddComponent<ItemRuntimeBehaviourHost>();
            host.Instance = ItemDatabase.CreateInstance(id);

            // 解析并构造行为
            host.Behaviours.Clear();
            foreach (var c in def.Components)
            {
                var bh = ItemBehaviourFactory.Create(c);
                if (bh != null)
                {
                    bh.OnBind(host.Instance);
                    host.Behaviours.Add(bh);
                }
            }
            return go;
        }
    }
}
