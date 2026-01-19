using System;
using System.Collections.Generic;
using Kernel.Factory.Connections;
using Kernel.Storage;
using Lonize.Logging;
using Lonize.Tick;
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
        [SerializeField] public BuildingCategory Category = BuildingCategory.Single;
        [SerializeField] public FactoryInteriorRuntime FactoryInterior = new();
        public FactoryCompositeBehaviour CompositeBehaviour;

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

    public interface IBuildingBehaviour : ITickable
    {
        void OnBind(BuildingRuntime runtime);
        // 需要时可扩展 Tick/OnPowerChanged/OnInventoryChanged 等接口

        void OnUnbind(BuildingRuntime runtime);
    }
    /// <summary>
    /// summary: 行为生命周期扩展（用于资源注销等）。
    /// </summary>
    // public interface IBuildingBehaviourLifecycle
    // {
    //     /// <summary>
    //     /// summary: 解绑回调（宿主销毁/拆除时调用）。
    //     /// param: runtime 建筑运行时
    //     /// return: 无
    //     /// </summary>
        
    // }
    // ——示例行为——

    // 发电机：持续输出功率
    public class PowerProducerBehaviour : IBuildingBehaviour
    {
        public float Power; // 正值表示发电

        public PowerProducerBehaviour(float power) { Power = power; }
        public void OnBind(BuildingRuntime r) { }

        public void OnUnbind(BuildingRuntime runtime)
        {
            // 可选实现
        }

        public void Tick(int ticks)
        {
            // throw new NotImplementedException();
        }
    }

    // 耗电设备：持续消耗功率
    public class PowerConsumerBehaviour : IBuildingBehaviour
    {
        public float Power; // 正值表示消耗

        public PowerConsumerBehaviour(float power) { Power = power; }
        public void OnBind(BuildingRuntime r) { }


        public void OnUnbind(BuildingRuntime runtime)
        {
            throw new NotImplementedException();
        }

        public void Tick(int ticks)
        {
            // throw new NotImplementedException();
        }
    }
    public class TestCounterBehaviour : BaseInteriorBehaviour
    {
        private int _interval;      // 触发间隔（来自 JSON）
        private int _tickAccumulator; // 当前积累的 tick
        private int _counter;       // 计数器
        private int _tickCounter;   // 每 Tick 递增的计数器
        private int _receivedSum;   // 接收累计值


        private const string InputPortID_Parent = "tick_in";
        private const string OutputPortID_Parent = "tick_out";
        // private const string TickInputPortId = "tick_in";
        // private const string TickOutputPortId = "tick_out";

        public TestCounterBehaviour(int interval)
        {
            _interval = Mathf.Max(1, interval); // 保护一下，防止除以0
        }

        /// <summary>
        /// summary: 绑定计数器行为并初始化状态。
        /// param: runtime 建筑运行时
        /// return: 无
        /// </summary>
        public override void OnBind(BuildingRuntime runtime)
        {
            base.OnBind(runtime);
            _counter = 0;
            _tickAccumulator = 0;
            _tickCounter = 0;
            _receivedSum = 0;
            if (!EnableOutputSelection)
            {
                SelectionMode = OutputSelectionMode.Default;
                PreferredOutputIndex = 0;
            }
            GameDebug.Log($"[TestCounter] 绑定成功！ID: {BuildingLocalId}, 间隔: {_interval}");
        }

        /// <summary>
        /// summary: 解绑计数器行为并输出统计日志。
        /// param: runtime 建筑运行时
        /// return: 无
        /// </summary>
        public override void OnUnbind(BuildingRuntime runtime)
        {
            GameDebug.Log($"[TestCounter] 解绑成功！ID: {BuildingLocalId}, 总计数: {_counter}");
        }

        /// <summary>
        /// summary: Tick 时推进计数并输出节拍。
        /// param: ticks Tick 数量
        /// return: 无
        /// </summary>
        protected override void OnTick(int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                _tickAccumulator++;
                _tickCounter++;
                EnqueueOutput(_tickCounter);

                if (_tickAccumulator >= _interval)
                {
                    _tickAccumulator -= _interval;
                    _counter++;
                    GameDebug.Log($"⏰ [TestCounter] Building {BuildingLocalId} | Tick: {_counter * _interval} | Count: {_counter}");
                }
            }
        }

        /// <summary>
        /// summary: 提供计数器端口声明列表。
        /// param: 无
        /// return: 端口声明列表
        /// </summary>
        protected override IEnumerable<PortDescriptor> BuildPorts()
        {
            int inputCount = InputPortCount < 0 ? 1 : InputPortCount;
            int outputCount = OutputPortCount < 0 ? 1 : OutputPortCount;

            int capacity = Math.Max(0, inputCount) + Math.Max(0, outputCount);
            if (capacity == 0) return Array.Empty<PortDescriptor>();

            var ports = new List<PortDescriptor>(capacity);

            if (inputCount == 1)
            {
                ports.Add(new PortDescriptor(InputPortID_Parent, PortDirection.Input, ConnectionChannel.Item, 1));
            }
            else if (inputCount > 1)
            {
                for (int i = 0; i < inputCount; i++)
                {
                    ports.Add(new PortDescriptor($"{InputPortID_Parent}_{i}", PortDirection.Input, ConnectionChannel.Item, 1));
                }
            }

            if (outputCount == 1)
            {
                ports.Add(new PortDescriptor(OutputPortID_Parent, PortDirection.Output, ConnectionChannel.Item, 1));
            }
            else if (outputCount > 1)
            {
                for (int i = 0; i < outputCount; i++)
                {
                    ports.Add(new PortDescriptor($"{OutputPortID_Parent}_{i}", PortDirection.Output, ConnectionChannel.Item, 1));
                }
            }

            return ports;
        }

        /// <summary>
        /// summary: 接收输入数据包并进行累计。
        /// param: packet 输入数据包
        /// return: 无
        /// </summary>
        protected override void OnReceiveInput(InteriorDataPacket packet)
        {
            int value = ExtractPayloadValue(packet.Payload);
            _receivedSum += value;
            GameDebug.Log($"📥 [TestCounter] Building {BuildingLocalId} 接收: {value} | 累计: {_receivedSum} | Port: {packet.PortId}");
        }
        
        /// <summary>
        /// summary: 输入端口父ID。
        /// </summary>
        protected override string InputPortIdParent => InputPortID_Parent;

        /// <summary>
        /// summary: 输出端口父ID。
        /// </summary>
        protected override string OutputPortIdParent => OutputPortID_Parent;

        /// <summary>
        /// summary: 提取数据包负载中的整数值。
        /// param: payload 数据负载
        /// return: 解析后的整数值
        /// </summary>
        private int ExtractPayloadValue(object payload)
        {
            if (payload == null) return 0;
            if (payload is int intValue) return intValue;
            if (payload is long longValue) return (int)longValue;
            if (payload is float floatValue) return Mathf.RoundToInt(floatValue);
            if (payload is double doubleValue) return (int)Math.Round(doubleValue);
            if (payload is IConvertible convertible)
            {
                try
                {
                    return convertible.ToInt32(null);
                }
                catch (Exception)
                {
                    return 0;
                }
            }

            return 0;
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
                    var tags = ResolveStringList(data.Params?["allowTags"]);
                    var itemIds = ResolveStringList(data.Params?["allowItemIds"]);
                    StorageFilterMode filterMode = ResolveStorageFilterMode(data.Params?["filterMode"]);
                    return new StorageBehaviour(cap, tags, itemIds, filterMode, pr);
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
                    // TODO: 补充完整
                    return null;
                }
                case "test_counter":
                {
                    GameDebug.Log("[Building] 创建 TestCounter 组件");
                    // 从 JSON params 读取 "interval"，默认为 20
                    int interval = data.Params?["interval"]?.Value<int>() ?? 20;
                    // 记得我们之前说过要让 Host 能够 Tick，
                    // 这里返回的对象需要在 Host 端被识别为 ITickable 并加入 TickManager 或者由 Host 驱动
                    return new TestCounterBehaviour(interval);
                }
                case "factory_interior":
                {
                    // TODO: 补充完整
                    return null;
                }
                case "interior_interface_box":
                {
                    var tags = data.Params?["allowTags"]?.ToObject<List<string>>() ?? new List<string>();
                    var itemIds = data.Params?["allowItemIds"]?.ToObject<List<string>>() ?? new List<string>();
                    StorageFilterMode filterMode = ResolveStorageFilterMode(data.Params?["filterMode"]);
                    return new InteriorInterfaceBoxBehaviour(tags, itemIds, filterMode);
                }
                case "interior_cache_box":
                {
                    int cap = data.Params?["capacity"]?.Value<int>() ?? 0;
                    var tags = data.Params?["allowTags"]?.ToObject<List<string>>() ?? new List<string>();
                    return new InteriorCacheStorageBehaviour(cap, tags);
                }
                case "miner":
                {
                    string outputItemId = data.Params?["outputItemId"]?.Value<string>() ?? "raw_ore";
                    int outputCount = data.Params?["outputCount"]?.Value<int>() ?? 1;
                    int tickInterval = data.Params?["tickInterval"]?.Value<int>() ?? 60;
                    return new MinerBehaviour(outputItemId, outputCount, tickInterval);
                }
                default:
                    GameDebug.LogWarning($"[Building] 未知组件类型: {data.Type}");
                    return null;
            }
        }

        /// <summary>
        /// summary: 解析储物过滤模式（默认 TagOnly）。
        /// param: token 过滤模式参数
        /// return: 解析后的过滤模式
        /// </summary>
        private static StorageFilterMode ResolveStorageFilterMode(JToken token)
        {
            const StorageFilterMode defaultMode = StorageFilterMode.TagOnly;
            if (token == null)
            {
                return defaultMode;
            }

            try
            {
                if (token.Type == JTokenType.Integer)
                {
                    int modeValue = token.Value<int>();
                    if (Enum.IsDefined(typeof(StorageFilterMode), modeValue))
                    {
                        return (StorageFilterMode)modeValue;
                    }
                }

                string modeText = token.Value<string>();
                if (string.IsNullOrWhiteSpace(modeText))
                {
                    return defaultMode;
                }

                string normalized = modeText.Trim().ToLowerInvariant().Replace("-", "_");
                switch (normalized)
                {
                    case "tag_only":
                        return StorageFilterMode.TagOnly;
                    case "id_only":
                        return StorageFilterMode.IdOnly;
                    case "tag_and_id":
                        return StorageFilterMode.TagAndId;
                    case "tag_or_id":
                        return StorageFilterMode.TagOrId;
                }

                if (Enum.TryParse(modeText, true, out StorageFilterMode parsed))
                {
                    return parsed;
                }
            }
            catch (Exception)
            {
                return defaultMode;
            }

            return defaultMode;
        }

        /// <summary>
        /// summary: 解析字符串数组参数（解析失败返回空列表）。
        /// param: token 参数节点
        /// return: 解析后的字符串列表
        /// </summary>
        private static List<string> ResolveStringList(JToken token)
        {
            if (token == null || token.Type != JTokenType.Array)
            {
                return new List<string>();
            }

            try
            {
                return token.ToObject<List<string>>() ?? new List<string>();
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }
    }


}
