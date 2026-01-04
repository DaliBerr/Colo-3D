

using System;
using System.Collections.Generic;
using Kernel.Storage;
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
        public Dictionary<string, float> RuntimeStats = new();
        public BuildingCategory Category = BuildingCategory.Single;
        public FactoryInteriorRuntime FactoryInterior = new();

        /// <summary>
        /// summary: 确保工厂内部数据已初始化。
        /// param: 无
        /// return: 工厂内部数据实例
        /// </summary>
        public FactoryInteriorRuntime EnsureFactoryInterior()
        {
            if (FactoryInterior == null)
            {
                FactoryInterior = new FactoryInteriorRuntime();
            }
            return FactoryInterior;
        }
    }

    public interface IBuildingBehaviour
    {
        void OnBind(BuildingRuntime runtime);
        // 需要时可扩展 Tick/OnPowerChanged/OnInventoryChanged 等接口
    }
    /// <summary>
    /// summary: 行为生命周期扩展（用于资源注销等）。
    /// </summary>
    public interface IBuildingBehaviourLifecycle
    {
        /// <summary>
        /// summary: 解绑回调（宿主销毁/拆除时调用）。
        /// param: runtime 建筑运行时
        /// return: 无
        /// </summary>
        void OnUnbind(BuildingRuntime runtime);
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

    public class StorageBehaviour : IBuildingBehaviour, IBuildingBehaviourLifecycle
    {
        public int Capacity;
        public int Priority;
        public List<string> AllowTags = new();

        public long RuntimeId { get; private set; }
        public StorageContainer Container { get; private set; }

        public StorageBehaviour(int capacity, List<string> allowTags, int priority = 0)
        {
            Capacity = Mathf.Max(0, capacity);
            Priority = priority;
            if (allowTags != null) AllowTags = allowTags;
        }

        /// <summary>
        /// summary: 绑定时创建并注册容器。
        /// param: r 建筑运行时
        /// return: 无
        /// </summary>
        public void OnBind(BuildingRuntime r)
        {
            if (r == null) return;

            RuntimeId = r.BuildingID;
            Container = StorageSystem.Instance.Register(RuntimeId, r.CellPosition, Capacity, AllowTags, Priority);
        }

        /// <summary>
        /// summary: 解绑时注销容器。
        /// param: r 建筑运行时
        /// return: 无
        /// </summary>
        public void OnUnbind(BuildingRuntime r)
        {
            if (RuntimeId > 0)
                StorageSystem.Instance.Unregister(RuntimeId);

            Container = null;
            RuntimeId = 0;
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
                    int pr = data.Params?["priority"]?.Value<int>() ?? 0;
                    var tags = data.Params?["allowTags"]?.ToObject<List<string>>() ?? new List<string>();
                    return new StorageBehaviour(cap, tags, pr);
                }
                case "producer":
                {
                    float t = data.Params?["craftTime"]?.Value<float>() ?? 1f;
                    var ins = data.Params?["inputs"]?.ToObject<Dictionary<string,int>>() ?? new();
                    var outs = data.Params?["outputs"]?.ToObject<Dictionary<string,int>>() ?? new();
                    return new ProducerBehaviour(t, ins, outs);
                }
                case "factory":
                {
                    // TODO:
                    return null;
                }
                case "factory_interior":
                {
                    // TODO:
                    return null;
                }
                default:
                    GameDebug.LogWarning($"[Building] 未知组件类型: {data.Type}");
                    return null;
            }
        }
    }


}
