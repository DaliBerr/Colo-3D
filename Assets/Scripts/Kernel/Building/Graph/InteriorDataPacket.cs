using System.Collections.Generic;

namespace Kernel.Factory.Connections
{
    /// <summary>
    /// summary: 工厂内部数据包（跨 Tick 缓冲，当前 Tick 输出只会在下一 Tick 被消费）。
    /// </summary>
    public sealed class InteriorDataPacket
    {
        public PortKey PortKey { get; }
        public string PortId { get; }
        public ConnectionChannel Channel { get; }
        public object Payload { get; }

        /// <summary>
        /// summary: 创建工厂内部数据包。
        /// param: portKey 端口键
        /// param: channel 连接通道
        /// param: payload 负载数据
        /// return: 数据包实例
        /// </summary>
        public InteriorDataPacket(PortKey portKey, ConnectionChannel channel, object payload)
        {
            PortKey = portKey;
            PortId = portKey.PortId;
            Channel = channel;
            Payload = payload;
        }
    }

    /// <summary>
    /// summary: 工厂内部数据输出接口（跨 Tick 缓冲，当前 Tick 输出只会在下一 Tick 被消费）。
    /// </summary>
    public interface IInteriorDataOutput
    {
        /// <summary>
        /// summary: 收集输出的数据包。
        /// param: 无
        /// return: 数据包集合
        /// </summary>
        IEnumerable<InteriorDataPacket> CollectOutputs();
    }

    /// <summary>
    /// summary: 工厂内部数据输入接口（跨 Tick 缓冲，当前 Tick 输出只会在下一 Tick 被消费）。
    /// </summary>
    public interface IInteriorDataInput
    {
        /// <summary>
        /// summary: 接收输入的数据包（请使用 packet.PortId 区分来源端口，且须与 GetPorts() 的 PortId 完全一致）。
        /// param: packet 数据包
        /// return: 无
        /// </summary>
        void ReceiveInput(InteriorDataPacket packet);
    }
}
