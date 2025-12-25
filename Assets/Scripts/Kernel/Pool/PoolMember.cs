using UnityEngine;

namespace Kernel.Pool
{
    /// <summary>
    /// 建筑物池成员组件，用于记录其所属的对象池 Key。
    /// </summary>
    public class BuildingPoolMember : MonoBehaviour
    {
        /// <summary>
        /// 该对象所属的池子 Key（通常是 Addressables Key 或 DefName）。
        /// </summary>
        public string Address { get; private set; }

        /// <summary>
        /// 初始化池成员的标识。
        /// </summary>
        /// <param name="address">池子使用的 Key（与 PoolManager 中一致）。</param>
        public void Init(string address)
        {
            Address = address;
        }
    }
}
