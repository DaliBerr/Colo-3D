using System;
using System.Collections.Generic;
using Newtonsoft.Json;
namespace Kernel.Item
{
    [Serializable]
    public class ItemDef
    {
        // 必填
        [JsonProperty("id", Required = Required.Always)]
        public string Id;

        [JsonProperty("version", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Version = "1.0.0";

        // 展示
        [JsonProperty("name")]
        public string Name;

        [JsonProperty("description")]
        public string Description;

        // 资源（Addressables 的地址或label：你自己在Addressables里配置）
        [JsonProperty("icon")]
        public string IconAddress;

        [JsonProperty("prefab")]
        public string PrefabAddress;

        // 基本属性
        [JsonProperty("storageOccupation", DefaultValueHandling = DefaultValueHandling.Populate)]
        public int StorageOccupation = 1;

        /// <summary>
        /// 矿物成分信息。
        /// </summary>
        [JsonProperty("mineralComposition")]
        public Dictionary<string, FloatRange> MineralComposition = new();

        /// <summary>
        /// 矿物加工属性。
        /// </summary>
        [JsonProperty("processingInfo")]
        public MineralProcessingInfo ProcessingInfo;

        // 自由扩展的数值属性（如重量、价格、耐久等）
        [JsonProperty("stats")]
        public Dictionary<string, float> Stats = new();

        // 标签/过滤
        [JsonProperty("tags")]
        public List<string> Tags = new();

        // 组件化行为（见下）
        [JsonProperty("components")]
        public List<ItemComponentData> Components = new();
    }

    /// <summary>
    /// 矿物加工属性信息。
    /// </summary>
    [Serializable]
    public class MineralProcessingInfo
    {
        /// <summary>
        /// 磁性强度（0~1）。
        /// </summary>
        [JsonProperty("magnetism")]
        public FloatRange Magnetism;

        /// <summary>
        /// 矿物类型（sulfide/oxide/carbonate/silicate）。
        /// </summary>
        [JsonProperty("mineralType")]
        public string MineralType;

        /// <summary>
        /// 粒度（0~1）。
        /// </summary>
        [JsonProperty("particleSize")]
        public FloatRange ParticleSize;

        /// <summary>
        /// 可浮选性（0~1）。
        /// </summary>
        [JsonProperty("floatability")]
        public FloatRange Floatability;

        /// <summary>
        /// 可浸出性（0~1）。
        /// </summary>
        [JsonProperty("leachability")]
        public FloatRange Leachability;

        /// <summary>
        /// 伴生矿物物品ID。
        /// </summary>
        [JsonProperty("associatedMineralId")]
        public string AssociatedMineralId;
    }

    /// <summary>
    /// 浮点范围数据。
    /// </summary>
    [Serializable]
    public struct FloatRange
    {
        /// <summary>
        /// 最小值。
        /// </summary>
        [JsonProperty("min")]
        public float Min;

        /// <summary>
        /// 最大值。
        /// </summary>
        [JsonProperty("max")]
        public float Max;
    }

    /// <summary>
    /// 矿物加工属性运行时数据。
    /// </summary>
    [Serializable]
    public class MineralProcessingData
    {
        /// <summary>
        /// 磁性强度（0~1）。
        /// </summary>
        public float Magnetism;

        /// <summary>
        /// 矿物类型。
        /// </summary>
        public string MineralType;

        /// <summary>
        /// 粒度（0~1）。
        /// </summary>
        public float ParticleSize;

        /// <summary>
        /// 可浮选性（0~1）。
        /// </summary>
        public float Floatability;

        /// <summary>
        /// 可浸出性（0~1）。
        /// </summary>
        public float Leachability;

        /// <summary>
        /// 伴生矿物物品ID。
        /// </summary>
        public string AssociatedMineralId;
    }

}
