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
        [JsonProperty("maxStack", DefaultValueHandling = DefaultValueHandling.Populate)]
        public int MaxStack = 1;

        [JsonProperty("rarity", DefaultValueHandling = DefaultValueHandling.Populate)]
        public string Rarity = "common";

        // 自由扩展的数值属性（如重量、价格、耐久等）
        [JsonProperty("stats")]
        public Dictionary<string, float> Stats = new();

        // 可分离（是否可以变成掉落物）
        [JsonProperty("detachable")]
        public bool Detachable = false;
        
        // 标签/过滤
        [JsonProperty("tags")]
        public List<string> Tags = new();

        // 组件化行为（见下）
        [JsonProperty("components")]
        public List<ItemComponentData> Components = new();
    }

}