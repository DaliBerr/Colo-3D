namespace Kernel.Factory.Connections
{

    /// <summary>
    /// summary: 物品负载数据结构。
    /// </summary>
    public struct ItemPayload
    {
        public string ItemId { get; }
        public int Quantity { get; }
        public string Tags { get;}
        public object Metadata { get;}
        public ItemPayload(string itemId, int quantity, string tags = null, object metadata = null)
        {
            ItemId = itemId;
            Quantity = quantity;
            Tags = tags;
            Metadata = metadata;
        }
    }


    /// <summary>
    /// summary: 流体负载数据结构。
    /// </summary>
    public struct FluidPayload
    {
        public string FluidId { get; }
        public float Temperature { get; }
        public float Volume { get; }
        public string Tags { get; }
        public object Metadata { get; }
        public FluidPayload(string fluidId, float temperature, float volume, string tags = null, object metadata = null)
        {
            FluidId = fluidId;
            Temperature = temperature;
            Volume = volume;
            Tags = tags;
            Metadata = metadata;
        }
    }





}