

using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Lonize.Scribe
{
     public static class Scribe_Polymorph
    {
        /// <summary>List&lt;ISaveItem&gt;：元素作为 Node 写入，前置写入 TypeId（string）</summary>
        public static void LookList(string tag, ref List<ISaveItem> list)
        {
            if (Scribe.mode == ScribeMode.Saving)
            {
                if (list == null) { Scribe.WriteField(FieldType.ListPoly, tag, null); return; }

                var payload = new List<Dictionary<string, object>>(list.Count);
                var frameField = typeof(Scribe).GetField("_frameStack", BindingFlags.NonPublic | BindingFlags.Static);
                var frames = (Stack<NodeFrame>)frameField.GetValue(null);

                foreach (var it in list)
                {
                    var typeId = it?.TypeId ?? string.Empty;
                    if (it == null)
                    {
                        payload.Add(new Dictionary<string, object> { { "TypeId", typeId }, { "Node", null } });
                        continue;
                    }

                    var frame = new NodeFrame();
                    frames.Push(frame);
                    it.ExposeData();
                    frames.Pop();
                    payload.Add(new Dictionary<string, object> { { "TypeId", typeId }, { "Node", frame } });
                }

                Scribe.WriteField(FieldType.ListPoly, tag, payload);
            }
            else if (Scribe.mode == ScribeMode.Loading)
            {
                if (!Scribe.TryGetField(tag, out var rec) || rec.Type != FieldType.ListPoly) { list = null; return; }

                var frameField = typeof(Scribe).GetField("_frameStack", BindingFlags.NonPublic|BindingFlags.Static);
                var frames = (Stack<NodeFrame>)frameField.GetValue(null);

                if (rec.Value is byte[] bytes)
                {
                    using var br = new BinaryReader(new MemoryStream(bytes));
                    int n = br.ReadInt32();
                    if (n < 0) { list = null; return; }
                    list = new List<ISaveItem>(n);
                    for (int i = 0; i < n; i++)
                    {
                        var typeId = br.ReadString();
                        int len = br.ReadInt32();
                        if (string.IsNullOrEmpty(typeId) || len == 0) { list.Add(null); continue; }

                        if (!PolymorphRegistry.TryCreate(typeId, out var obj))
                        {
                            br.ReadBytes(len);
                            list.Add(null);
                            continue;
                        }

                        var buf = br.ReadBytes(len);
                        using var ms = new MemoryStream(buf);
                        using var r = new BinaryReader(ms);
                        if (!NodeFrame.TryReadLegacy(r, out var elemRec) || elemRec.Type != FieldType.Node) { list.Add(null); continue; }

                        frames.Push(NodeFrame.FromLegacyBytes(elemRec.Payload));
                        obj.ExposeData();
                        frames.Pop();

                        list.Add(obj);
                    }
                }
                else
                {
                    List<Dictionary<string, object>> payload = null;
                    if (rec.Value is JArray arr)
                        payload = arr.ToObject<List<Dictionary<string, object>>>();
                    else if (rec.Value is IEnumerable<Dictionary<string, object>> dicts)
                        payload = dicts.ToList();

                    if (payload == null) { list = null; return; }
                    list = new List<ISaveItem>(payload.Count);
                    foreach (var entry in payload)
                    {
                        var typeId = entry.TryGetValue("TypeId", out var tid) ? tid as string : null;
                        if (string.IsNullOrEmpty(typeId)) { list.Add(null); continue; }
                        if (!PolymorphRegistry.TryCreate(typeId, out var obj)) { list.Add(null); continue; }
                        var frame = entry.TryGetValue("Node", out var nodeObj) ? nodeObj as NodeFrame : null;
                        if (frame == null && entry.TryGetValue("Node", out var maybeToken) && maybeToken is JToken token)
                            frame = token.ToObject<NodeFrame>();

                        frames.Push(frame ?? new NodeFrame());
                        obj.ExposeData();
                        frames.Pop();
                        list.Add(obj);
                    }
                }
            }
            else throw new System.InvalidOperationException();
        }
    }
}