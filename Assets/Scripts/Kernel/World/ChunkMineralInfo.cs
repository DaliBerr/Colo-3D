using System.Collections.Generic;
using Kernel.Item;
using UnityEngine;

namespace Kernel.World
{
    /// <summary>
    /// 区块矿物信息。
    /// </summary>
    public struct ChunkMineralInfo
    {
        /// <summary>
        /// 区块坐标。
        /// </summary>
        public Vector2Int ChunkCoord;

        /// <summary>
        /// 矿物成分数据。
        /// </summary>
        public Dictionary<string, float> MineralComposition;

        /// <summary>
        /// 加工属性数据。
        /// </summary>
        public MineralProcessingData ProcessingInfo;
    }
}
