using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Lonize.Logging;
using Kernel.Building;

namespace Kernel.Pool
{
    /// <summary>
    /// 建筑对象池管理器，按 BuildingDef.Id 复用建筑 GameObject。
    /// </summary>
    public class PoolManager : MonoBehaviour
    {
        /// <summary>
        /// 全局唯一实例。
        /// </summary>
        public static PoolManager Instance { get; private set; }

        /// <summary>
        /// 对象池字典：Key 为 BuildingDef.Id（或你定义的建筑类型Key），Value 为闲置对象队列。
        /// </summary>
        private readonly Dictionary<string, Queue<GameObject>> _pool = new();

        /// <summary>
        /// 初始化单例。
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        /// <summary>
        /// 从池中获取建筑对象；若池中没有可用实例，则通过 BuildingFactory 创建新的。
        /// </summary>
        /// <param name="buildingId">
        /// BuildingDef 的 Id，例如 "powergenerator"。
        /// 约定：此 Id 既作为 BuildingFactory 的参数，也作为对象池的 Key。
        /// </param>
        /// <param name="pos">放置的世界坐标。</param>
        /// <param name="rot">放置的世界旋转。</param>
        /// <returns>返回激活且已定位的建筑 GameObject；失败时返回 null。</returns>
        public async Task<GameObject> GetAsync(string buildingId, Vector3 pos, Quaternion rot)
        {
            // 1. 先尝试从池中取出闲置对象
            if (_pool.TryGetValue(buildingId, out var queue) && queue.Count > 0)
            {
                while (queue.Count > 0)
                {
                    var obj = queue.Dequeue();
                    if (obj == null)
                    {
                        // 被外部 Destroy 了，继续取下一个
                        continue;
                    }

                    obj.transform.SetPositionAndRotation(pos, rot);
                    obj.SetActive(true);
                    return obj;
                }
            }

            // 2. 池子里没有可用对象，走 BuildingFactory 的正常创建流程
            var go = await BuildingFactory.SpawnToWorldAsync(buildingId, pos, rot);
            if (go == null)
            {
                GameDebug.LogError($"[Pool] BuildingFactory.SpawnToWorldAsync 失败，buildingId = {buildingId}");
                return null;
            }

            // 3. 确保挂上 Pool 标记组件，写入当前 buildingId
            var member = go.GetComponent<BuildingPoolMember>();
            if (member == null)
            {
                member = go.AddComponent<BuildingPoolMember>();
            }
            // member.Address = buildingId;
            member.Init(buildingId);
            return go;
        }

        /// <summary>
        /// 将建筑对象回收到对象池中。
        /// </summary>
        /// <param name="obj">需要回收的建筑 GameObject。</param>
        public void ReturnToPool(GameObject obj)
        {
            if (obj == null) return;

            var member = obj.GetComponent<BuildingPoolMember>();
            if (member == null || string.IsNullOrEmpty(member.Address))
            {
                GameDebug.LogWarning("[Pool] 回收对象时未找到 BuildingPoolMember 或 Address 为空，将直接销毁。");
                Destroy(obj);
                return;
            }

            obj.SetActive(false);
            obj.transform.SetParent(transform, false); // 收纳到 PoolManager 下面

            if (!_pool.TryGetValue(member.Address, out var queue))
            {
                queue = new Queue<GameObject>();
                _pool[member.Address] = queue;
            }

            queue.Enqueue(obj);
        }
    }
}
