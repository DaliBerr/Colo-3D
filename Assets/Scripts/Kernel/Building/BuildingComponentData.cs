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
    public class TestCounterBehaviour : IBuildingBehaviour, IInteriorPortProvider, IInteriorDataOutput, IInteriorDataInput
    {
        private int _interval;      // 触发间隔（来自 JSON）
        private int _tickAccumulator; // 当前积累的 tick
        private int _counter;       // 计数器
        private long _buildingId;   // 绑定的建筑 ID，方便看日志
        private long _factoryId;    // 父工厂ID（用于端口键）
        private int _tickCounter;   // 每 Tick 递增的计数器
        private int _receivedSum;   // 接收累计值
        private readonly Queue<int> _pendingOutputs = new();


        private const string InputPortID_Parent = "tick_in";
        private const string OutputPortID_Parent = "tick_out";
        private int _inputPortCount = -1;
        private int _outputPortCount = -1;
        // private const string TickInputPortId = "tick_in";
        // private const string TickOutputPortId = "tick_out";

        public TestCounterBehaviour(int interval)
        {
            _interval = Mathf.Max(1, interval); // 保护一下，防止除以0
        }

        public void OnBind(BuildingRuntime runtime)
        {
            _buildingId = runtime.BuildingID;
            _counter = 0;
            _tickAccumulator = 0;
            _tickCounter = 0;
            _receivedSum = 0;
            _factoryId = 0;
            _pendingOutputs.Clear();
            GameDebug.Log($"[TestCounter] 绑定成功！ID: {_buildingId}, 间隔: {_interval}");
        }
        public void OnUnbind(BuildingRuntime runtime)
        {
            GameDebug.Log($"[TestCounter] 解绑成功！ID: {_buildingId}, 总计数: {_counter}");
        }

        /// <summary>
        /// summary: Tick 时推进计数并输出数据包。
        /// param: ticks Tick 数量
        /// return: 无
        /// </summary>
        public void Tick(int ticks)
        {
            if (ticks <= 0) return;

            UpdatePortContext();

            for (int i = 0; i < ticks; i++)
            {
                _tickAccumulator++;
                _tickCounter++;
                EnqueueOutput(_tickCounter);

                if (_tickAccumulator >= _interval)
                {
                    _tickAccumulator -= _interval;
                    _counter++;
                    GameDebug.Log($"⏰ [TestCounter] Building {_buildingId} | Tick: {_counter * _interval} | Count: {_counter}");
                }
            }
        }

        /// <summary>
        /// summary: 提供测试计数器的端口声明列表。
        /// param: 无
        /// return: 端口声明列表
        /// </summary>
        public IEnumerable<PortDescriptor> GetPorts()
        {
            var ui = (_inputPortCount < 0 || _outputPortCount < 0) ? FindInteriorUI() : null;
            int inputCount = _inputPortCount;
            int outputCount = _outputPortCount;

            if (inputCount < 0)
            {
                inputCount = ui != null ? (ui.InputButtons?.Count ?? 0) : 1;
                if (ui != null) _inputPortCount = inputCount;
            }

            if (outputCount < 0)
            {
                outputCount = ui != null ? (ui.OutputButtons?.Count ?? 0) : 1;
                if (ui != null) _outputPortCount = outputCount;
            }

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

        private Kernel.UI.IInteriorBuildingUI FindInteriorUI()
        {
            var uis = UnityEngine.Object.FindObjectsByType<Kernel.UI.IInteriorBuildingUI>(UnityEngine.FindObjectsSortMode.None);
            if (uis == null || uis.Length == 0) return null;

            for (int i = 0; i < uis.Length; i++)
            {
                var ui = uis[i];
                if (ui == null) continue;
                if (_buildingId > 0 && ui.BuildingLocalId == _buildingId)
                {
                    return ui;
                }
            }

            return null;
        }

        /// <summary>
        /// summary: 收集内部输出数据包（每 Tick 一个）。
        /// param: 无
        /// return: 数据包集合
        /// </summary>
        public IEnumerable<InteriorDataPacket> CollectOutputs()
        {
            if (_pendingOutputs.Count == 0) return Array.Empty<InteriorDataPacket>();

            UpdatePortContext();
            var outputPortId = ResolveOutputPortId();
            if (_factoryId <= 0 || string.IsNullOrEmpty(outputPortId))
            {
                return Array.Empty<InteriorDataPacket>();
            }

            var key = new PortKey(_factoryId, _buildingId, outputPortId);
            var packets = new List<InteriorDataPacket>(_pendingOutputs.Count);
            while (_pendingOutputs.Count > 0)
            {
                int payload = _pendingOutputs.Dequeue();
                packets.Add(new InteriorDataPacket(key, ConnectionChannel.Item, payload));
            }

            return packets;
        }

        /// <summary>
        /// summary: 接收内部输入数据包并累加/记录。
        /// param: packet 输入数据包
        /// return: 无
        /// </summary>
        public void ReceiveInput(InteriorDataPacket packet)
        {
            if (packet == null) return;
            if (!IsMatchingInputPort(packet.PortId)) return;

            int value = ExtractPayloadValue(packet.Payload);
            _receivedSum += value;
            GameDebug.Log($"📥 [TestCounter] Building {_buildingId} 接收: {value} | 累计: {_receivedSum} | Port: {packet.PortId}");
        }

        /// <summary>
        /// summary: 将输出值加入缓冲队列。
        /// param: value 输出值
        /// return: 无
        /// </summary>
        private void EnqueueOutput(int value)
        {
            _pendingOutputs.Enqueue(value);
        }

        /// <summary>
        /// summary: 更新端口上下文信息（父工厂ID、端口数量）。
        /// param: 无
        /// return: 无
        /// </summary>
        private void UpdatePortContext()
        {
            if (_factoryId > 0 && _inputPortCount >= 0 && _outputPortCount >= 0) return;

            var ui = FindInteriorUI();
            if (ui == null) return;

            if (_factoryId <= 0)
            {
                _factoryId = ui.BuildingParentId;
            }

            if (_inputPortCount < 0)
            {
                _inputPortCount = ui.InputButtons?.Count ?? 0;
            }

            if (_outputPortCount < 0)
            {
                _outputPortCount = ui.OutputButtons?.Count ?? 0;
            }
        }

        /// <summary>
        /// summary: 解析主输出端口ID。
        /// param: 无
        /// return: 输出端口ID
        /// </summary>
        private string ResolveOutputPortId()
        {
            if (_outputPortCount > 1)
            {
                return $"{OutputPortID_Parent}_0";
            }

            return OutputPortID_Parent;
        }

        /// <summary>
        /// summary: 判断输入端口是否匹配当前行为关注的端口。
        /// param: portId 端口ID
        /// return: 是否匹配
        /// </summary>
        private bool IsMatchingInputPort(string portId)
        {
            if (string.IsNullOrEmpty(portId)) return false;

            if (_inputPortCount > 1)
            {
                return portId.StartsWith($"{InputPortID_Parent}_", StringComparison.Ordinal)
                       || string.Equals(portId, InputPortID_Parent, StringComparison.Ordinal);
            }

            return string.Equals(portId, InputPortID_Parent, StringComparison.Ordinal);
        }

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
    public class StorageBehaviour : IBuildingBehaviour
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
        public void Tick(int ticks)
        {
            // throw new NotImplementedException();
        }
    }

    // 简易生产机：配方（输入若干物品，耗时，产出若干物品），此处只示例数据形态
    public class ProducerBehaviour : IBuildingBehaviour
    {
        public float CraftTime;
        public Dictionary<string, int> Inputs = new();
        public Dictionary<string, int> Outputs = new();
        public void OnBind(BuildingRuntime r) { }

        public void OnUnbind(BuildingRuntime runtime)
        {
            // throw new NotImplementedException();
        }

        public ProducerBehaviour(float t, Dictionary<string, int> i, Dictionary<string, int> o)
        {
            CraftTime = t;
            Inputs = i ?? new();
            Outputs = o ?? new();
        }
        public void Tick(int ticks)
        {
            // throw new NotImplementedException();
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
