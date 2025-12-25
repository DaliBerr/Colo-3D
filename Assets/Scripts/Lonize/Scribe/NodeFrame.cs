using System.Collections.Generic;
using System.IO;

namespace Lonize.Scribe
{
    internal sealed class NodeFrame
    {
        // 一个 tag 只保留“最后一次出现”的字段，足够覆盖“改名/新增/替换”的常见场景
        public Dictionary<string, SerializedField> Fields { get; set; } = new();

        public void Set(string tag, SerializedField field) => Fields[tag] = field;

        public bool TryGet(string tag, out SerializedField rec) => Fields.TryGetValue(tag, out rec);

        public static NodeFrame FromLegacyBytes(byte[] payload)
        {
            var nf = new NodeFrame();
            using var ms = new MemoryStream(payload, writable: false);
            using var br = new BinaryReader(ms);
            while (TryReadLegacy(br, out var rec))
            {
                object val = DecodeLegacyValue(rec.Type, rec.Payload);
                NodeFrame node = null;
                if (rec.Type == FieldType.Node)
                    node = FromLegacyBytes(rec.Payload);
                nf.Set(rec.Tag, new SerializedField { Type = rec.Type, Value = val, Node = node });
            }
            return nf;
        }

        private static object DecodeLegacyValue(FieldType type, byte[] payload)
        {
            switch (type)
            {
                case FieldType.Int32:
                    using (var br = new BinaryReader(new MemoryStream(payload, writable: false)))
                        return br.ReadInt32();
                case FieldType.Single:
                    using (var br = new BinaryReader(new MemoryStream(payload, writable: false)))
                        return br.ReadSingle();
                case FieldType.Bool:
                    using (var br = new BinaryReader(new MemoryStream(payload, writable: false)))
                        return br.ReadBoolean();
                case FieldType.EnumInt32:
                    using (var br = new BinaryReader(new MemoryStream(payload, writable: false)))
                        return br.ReadInt32();
                case FieldType.Int64:
                    using (var br = new BinaryReader(new MemoryStream(payload, writable: false)))
                        return br.ReadInt64();
                case FieldType.String:
                case FieldType.RefId:
                    using (var br = new BinaryReader(new MemoryStream(payload, writable: false)))
                    {
                        try
                        {
                            // Codec 版会前置一个 bool 标记
                            bool has = br.ReadBoolean();
                            if (br.BaseStream.Position < br.BaseStream.Length)
                                return has ? br.ReadString() : null;
                            br.BaseStream.Position = 0;
                            return br.ReadString();
                        }
                        catch
                        {
                            return null;
                        }
                    }
                default:
                    return payload;
            }
        }

        internal static bool TryReadLegacy(BinaryReader br, out LegacyTLV rec)
        {
            rec = default;
            if (br.BaseStream.Position >= br.BaseStream.Length) return false;
            var t = (FieldType)br.ReadByte();
            var tag = br.ReadString();
            var len = br.ReadInt32();
            var buf = br.ReadBytes(len);
            rec = new LegacyTLV { Type = t, Tag = tag, Payload = buf };
            return true;
        }
    }
}