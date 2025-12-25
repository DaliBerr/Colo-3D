

using System;
using System.Collections.Generic;
using Lonize.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Kernel.Building
{
    [Serializable]
    public class BuildingComponentData
    {
        [JsonProperty("type", Required = Required.Always)]
        public string Type;

        [JsonProperty("params")]
        public JObject Params;
    }

    // 运行时宿主
    public class BuildingRuntime
    {
        public BuildingDef Def;
        public long BuildingID;
        public Vector2Int CellPosition; // 基于网格的坐标
        public byte RotationSteps; // 0-3，表示0/90/180/
        public int HP;
        public Dictionary<string, float> RuntimeStats = new();
    }

    public interface IBuildingBehaviour
    {
        void OnBind(BuildingRuntime runtime);
        // 需要时可扩展 Tick/OnPowerChanged/OnInventoryChanged 等接口
    }

    // ——示例行为——

    // 发电机：持续输出功率
    public class PowerProducerBehaviour : IBuildingBehaviour
    {
        public float Power; // 正值表示发电

        public PowerProducerBehaviour(float power) { Power = power; }
        public void OnBind(BuildingRuntime r) { }
    }

    // 耗电设备：持续消耗功率
    public class PowerConsumerBehaviour : IBuildingBehaviour
    {
        public float Power; // 正值表示消耗

        public PowerConsumerBehaviour(float power) { Power = power; }
        public void OnBind(BuildingRuntime r) { }
    }

    // 储物：容量 & 可选标签过滤
    public class StorageBehaviour : IBuildingBehaviour
    {
        public int Capacity;
        public List<string> AllowTags = new();
        public void OnBind(BuildingRuntime r) { }

        public StorageBehaviour(int capacity, List<string> allowTags)
        {
            Capacity = capacity;
            if (allowTags != null) AllowTags = allowTags;
        }
    }

    // 简易生产机：配方（输入若干物品，耗时，产出若干物品），此处只示例数据形态
    public class ProducerBehaviour : IBuildingBehaviour
    {
        public float CraftTime;
        public Dictionary<string, int> Inputs = new();
        public Dictionary<string, int> Outputs = new();
        public void OnBind(BuildingRuntime r) { }

        public ProducerBehaviour(float t, Dictionary<string, int> i, Dictionary<string, int> o)
        {
            CraftTime = t;
            Inputs = i ?? new();
            Outputs = o ?? new();
        }
    }

    // 工厂：把 JSON 组件转为运行时行为
    public static class BuildingBehaviourFactory
    {
        public static IBuildingBehaviour Create(BuildingComponentData data)
        {
            switch (data.Type)
            {
                case "power_producer":
                {
                    float p = data.Params?["power"]?.Value<float>() ?? 0f;
                    return new PowerProducerBehaviour(p);
                }
                case "power_consumer":
                {
                    float p = data.Params?["power"]?.Value<float>() ?? 0f;
                    return new PowerConsumerBehaviour(p);
                }
                case "storage":
                {
                    int cap = data.Params?["capacity"]?.Value<int>() ?? 0;
                    var tags = data.Params?["allowTags"]?.ToObject<List<string>>() ?? new List<string>();
                    return new StorageBehaviour(cap, tags);
                }
                case "producer":
                {
                    float t = data.Params?["craftTime"]?.Value<float>() ?? 1f;
                    var ins = data.Params?["inputs"]?.ToObject<Dictionary<string,int>>() ?? new();
                    var outs = data.Params?["outputs"]?.ToObject<Dictionary<string,int>>() ?? new();
                    return new ProducerBehaviour(t, ins, outs);
                }
                default:
                    GameDebug.LogWarning($"[Building] 未知组件类型: {data.Type}");
                    return null;
            }
        }
    }
}