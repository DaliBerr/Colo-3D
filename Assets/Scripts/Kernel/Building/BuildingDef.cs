using System;
using System.Collections.Generic;
using Newtonsoft.Json;
    
namespace Kernel.Building
{
    [Serializable]
    public class BuildingDef
    {
        // 基本
        [JsonProperty("id", Required = Required.Always)]
        public string Id;

        [JsonProperty("version")]
        public string Version = "1.0.0";

        [JsonProperty("name")]
        public string Name;

        [JsonProperty("description")]
        public string Description;

        // 资源（Addressables 地址或 Label）
        [JsonProperty("icon")]
        public string IconAddress;

        [JsonProperty("prefab", Required = Required.DisallowNull)]
        public string PrefabAddress;

        // 占格（格子宽高，旋转是否允许）
        [JsonProperty("width")]
        public int Width = 1;

        [JsonProperty("height")]
        public int Height = 1;

        [JsonProperty("allowRotate")]
        public bool AllowRotate = true; // 以 0/90/180/270 度离散旋转

        // 基础数值
        [JsonProperty("maxHP")]
        public int MaxHP = 100;

        [JsonProperty("buildTime")]
        public float BuildTime = 1.0f; // 秒

        // 造价（与物品系统打通：itemId -> 数量）
        [JsonProperty("cost")]
        public Dictionary<string, int> Cost = new();

        // 通用属性&标签
        [JsonProperty("stats")]
        public Dictionary<string, float> Stats = new();

        [JsonProperty("tags")]
        public List<string> Tags = new();

        // 放置规则（可由你的地图系统解释）
        [JsonProperty("placementRequiredTags")]
        public List<string> PlacementRequiredTags = new(); // 地块必须具备的Tag

        [JsonProperty("placementForbiddenTags")]
        public List<string> PlacementForbiddenTags = new(); // 地块禁止的Tag

        // 组件化行为（见下）
        [JsonProperty("components")]
        public List<BuildingComponentData> Components = new();
    }
}