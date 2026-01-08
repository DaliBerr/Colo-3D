using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kernel.Building;
using Newtonsoft.Json.Linq;

namespace Lonize.Scribe
{
        public static class Scribe_Collections
    {
        public static void Look(string tag, ref List<int> list)
        {
            if (Scribe.mode == ScribeMode.Saving)
            {
                Scribe.WriteField(FieldType.ListInt, tag, list);
            }
            else if (Scribe.mode == ScribeMode.Loading)
            {
                if (!Scribe.TryGetField(tag, out var rec) || rec.Type != FieldType.ListInt) { list = null; return; }
                if (rec.Value is byte[] bytes)
                {
                    using var br = new BinaryReader(new MemoryStream(bytes));
                    int n = br.ReadInt32();
                    if (n < 0) { list = null; return; }
                    list = new List<int>(n);
                    for (int i = 0; i < n; i++) list.Add(br.ReadInt32());
                }
                else if (rec.Value is JArray arr)
                {
                    list = arr.ToObject<List<int>>();
                }
                else if (rec.Value is IEnumerable<int> ints)
                {
                    list = new List<int>(ints);
                }
                else list = null;
            }
        }
        public static void Look(string tag, ref List<string> list)
        {
            if (Scribe.mode == ScribeMode.Saving)
            {
                Scribe.WriteField(FieldType.ListStr, tag, list);
            }
            else if (Scribe.mode == ScribeMode.Loading)
            {
                if (!Scribe.TryGetField(tag, out var rec) || rec.Type != FieldType.ListStr) { list = null; return; }
                if (rec.Value is byte[] bytes)
                {
                    using var br = new BinaryReader(new MemoryStream(bytes));
                    int n = br.ReadInt32();
                    if (n < 0) { list = null; return; }
                    list = new List<string>(n);
                    for (int i = 0; i < n; i++)
                    {
                        bool has = br.ReadBoolean();
                        list.Add(has ? br.ReadString() : null);
                    }
                }
                else if (rec.Value is JArray arr)
                {
                    list = arr.ToObject<List<string>>();
                }
                else if (rec.Value is IEnumerable<string> strs)
                {
                    list = new List<string>(strs);
                }
                else list = null;
            }
        }
        public static void Look(string tag, ref List<SaveBuildingInstance> list)
        {
            if (Scribe.mode == ScribeMode.Saving)
            {
                Scribe.WriteField(FieldType.ListSaveBuildingInstance, tag, list);
            }
            else if (Scribe.mode == ScribeMode.Loading)
            {
                if (!Scribe.TryGetField(tag, out var rec) || rec.Type != FieldType.ListSaveBuildingInstance) { list = null; return; }
                if (rec.Value is byte[] bytes)
                {
                    using var br = new BinaryReader(new MemoryStream(bytes));
                    int n = br.ReadInt32();
                    if (n < 0) { list = null; return; }
                    list = new List<SaveBuildingInstance>(n);
                    for (int i = 0; i < n; i++)
                    {
                        bool has = br.ReadBoolean();
                        if (has)
                        {
                            var instance = new SaveBuildingInstance
                            {
                                DefId = br.ReadString(),
                                RuntimeId = br.ReadInt64(),
                                CellX = br.ReadInt32(),
                                CellY = br.ReadInt32(),
                                RotSteps = br.ReadByte(),
                                // HP = br.ReadInt32()
                            };

                            int statCount = br.ReadInt32();
                            instance.StatKeys = new string[statCount];
                            instance.StatValues = new float[statCount];
                            for (int j = 0; j < statCount; j++)
                            {
                                instance.StatKeys[j] = br.ReadString();
                                instance.StatValues[j] = br.ReadSingle();
                            }
                            if (br.BaseStream.Position < br.BaseStream.Length)
                            {
                                int interiorCount = br.ReadInt32();
                                if (interiorCount > 0)
                                {
                                    instance.InteriorBuildings = new List<SaveFactoryBuildingInstance>(interiorCount);
                                    for (int k = 0; k < interiorCount; k++)
                                    {
                                        bool hasInterior = br.ReadBoolean();
                                        if (!hasInterior)
                                        {
                                            instance.InteriorBuildings.Add(null);
                                            continue;
                                        }

                                        var interior = new SaveFactoryBuildingInstance
                                        {
                                            DefId = br.ReadString(),
                                            ParentId = br.ReadInt64(),
                                            localId = br.ReadInt32(),
                                            CellX = br.ReadInt32(),
                                            CellY = br.ReadInt32(),
                                            // RotSteps = br.ReadByte(),
                                            // HP = br.ReadInt32()
                                        };

                                        int interiorStatCount = br.ReadInt32();
                                        interior.StatKeys = new string[interiorStatCount];
                                        interior.StatValues = new float[interiorStatCount];
                                        for (int m = 0; m < interiorStatCount; m++)
                                        {
                                            interior.StatKeys[m] = br.ReadString();
                                            interior.StatValues[m] = br.ReadSingle();
                                        }

                                        instance.InteriorBuildings.Add(interior);
                                    }
                                }
                            }
                            if (br.BaseStream.Position < br.BaseStream.Length)
                            {
                                int linkCount = br.ReadInt32();
                                if (linkCount > 0)
                                {
                                    instance.InteriorLinks = new List<SaveFactoryConnectionLink>(linkCount);
                                    for (int k = 0; k < linkCount; k++)
                                    {
                                        bool hasLink = br.ReadBoolean();
                                        if (!hasLink)
                                        {
                                            instance.InteriorLinks.Add(null);
                                            continue;
                                        }

                                        var link = new SaveFactoryConnectionLink
                                        {
                                            LinkId = br.ReadInt64(),
                                            AFactoryId = br.ReadInt64(),
                                            ALocalId = br.ReadInt64(),
                                            APortId = br.ReadString(),
                                            BFactoryId = br.ReadInt64(),
                                            BLocalId = br.ReadInt64(),
                                            BPortId = br.ReadString(),
                                            Channel = (Kernel.Factory.Connections.ConnectionChannel)br.ReadInt32()
                                        };
                                        instance.InteriorLinks.Add(link);
                                    }
                                }
                            }
                            list.Add(instance);
                        }
                        else
                        {
                            list.Add(null);
                        }
                    }
                }
                else if (rec.Value is JArray arr)
                {
                    list = arr.ToObject<List<SaveBuildingInstance>>();
                }
                else if (rec.Value is IEnumerable<SaveBuildingInstance> instances)
                {
                    list = new List<SaveBuildingInstance>(instances);
                }
                else list = null;
            }
        }

        public static void LookDeep<T>(string tag, ref List<T> list) where T : class, IExposable, new()
        {
            if (Scribe.mode == ScribeMode.Saving)
            {
                if (list == null) { Scribe.WriteField(FieldType.ListDeep, tag, null); return; }
                var nodes = new List<NodeFrame>(list.Count);
                foreach (var item in list)
                {
                    if (item == null)
                    {
                        nodes.Add(null);
                        continue;
                    }
                    var frame = new NodeFrame();
                    var stackField = typeof(Scribe).GetField("_frameStack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    var frames = (Stack<NodeFrame>)stackField.GetValue(null);
                    frames.Push(frame);
                    item.ExposeData();
                    frames.Pop();
                    nodes.Add(frame);
                }
                Scribe.WriteField(FieldType.ListDeep, tag, nodes);
            }
            else if (Scribe.mode == ScribeMode.Loading)
            {
                if (!Scribe.TryGetField(tag, out var rec) || rec.Type != FieldType.ListDeep) { list = null; return; }
                var framesField = typeof(Scribe).GetField("_frameStack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var framesStack = (Stack<NodeFrame>)framesField.GetValue(null);

                if (rec.Value is byte[] bytes)
                {
                    using var br = new BinaryReader(new MemoryStream(bytes));
                    int n = br.ReadInt32();
                    if (n < 0) { list = null; return; }
                    list = new List<T>(n);
                    for (int i = 0; i < n; i++)
                    {
                        int len = br.ReadInt32();
                        if (len == 0) { list.Add(null); continue; }
                        var buf = br.ReadBytes(len);
                        using var ms = new MemoryStream(buf);
                        using var r = new BinaryReader(ms);
                        if (!NodeFrame.TryReadLegacy(r, out var elemRec) || elemRec.Type != FieldType.Node) { list.Add(null); continue; }
                        framesStack.Push(NodeFrame.FromLegacyBytes(elemRec.Payload));
                        var t = new T(); t.ExposeData();
                        framesStack.Pop();
                        list.Add(t);
                    }
                }
                else
                {
                    var nodes = rec.Value switch
                    {
                        JArray arr => arr.ToObject<List<NodeFrame>>(),
                        IEnumerable<NodeFrame> n => n.ToList(),
                        _ => null
                    };
                    if (nodes == null) { list = null; return; }
                    list = new List<T>(nodes.Count);
                    foreach (var nf in nodes)
                    {
                        if (nf == null) { list.Add(null); continue; }
                        framesStack.Push(nf);
                        var t = new T(); t.ExposeData();
                        framesStack.Pop();
                        list.Add(t);
                    }
                }
            }
        }
    }
}
