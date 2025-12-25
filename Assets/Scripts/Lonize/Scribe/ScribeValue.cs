using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Lonize.Scribe
{
    public static class Scribe_Values
    {
        public static void Look(string tag, ref int value, int defaultValue = 0)
        {
            if (Scribe.mode == ScribeMode.Saving)
            {
                var tmp = value;
                Scribe.WriteField(FieldType.Int32, tag, tmp);
            }
            else if (Scribe.mode == ScribeMode.Loading)
            {
                if (Scribe.TryGetField(tag, out var rec) && rec.Type == FieldType.Int32)
                {
                    value = ReadInt(rec.Value, defaultValue);
                }
                else value = defaultValue;
            }
            else throw new InvalidOperationException();
        }

        public static void Look(string tag, ref float value, float defaultValue = 0f)
        {
            if (Scribe.mode == ScribeMode.Saving)
            {
                var tmp = value;
                Scribe.WriteField(FieldType.Single, tag, tmp);
            }
            else if (Scribe.mode == ScribeMode.Loading)
            {
                if (Scribe.TryGetField(tag, out var rec) && rec.Type == FieldType.Single)
                {
                    value = rec.Value is byte[] bytes ? new BinaryReader(new MemoryStream(bytes)).ReadSingle() : Convert.ToSingle(rec.Value ?? defaultValue);
                }
                else value = defaultValue;
            }
        }

        public static void Look(string tag, ref bool value, bool defaultValue = false)
        {
            if (Scribe.mode == ScribeMode.Saving)
            {
                var tmp = value;
                Scribe.WriteField(FieldType.Bool, tag, tmp);
            }
            else if (Scribe.mode == ScribeMode.Loading)
            {
                if (Scribe.TryGetField(tag, out var rec) && rec.Type == FieldType.Bool)
                {
                    value = rec.Value is byte[] bytes ? new BinaryReader(new MemoryStream(bytes)).ReadBoolean() : Convert.ToBoolean(rec.Value ?? defaultValue);
                }
                else value = defaultValue;
            }
        }

        public static void Look(string tag, ref string value, string defaultValue = null)
        {
            if (Scribe.mode == ScribeMode.Saving)
            {
                if (value == null) Scribe.WriteField(FieldType.Null, tag, null);
                else
                {
                    var tmp = value;
                    Scribe.WriteField(FieldType.String, tag, tmp);
                }
            }
            else if (Scribe.mode == ScribeMode.Loading)
            {
                if (!Scribe.TryGetField(tag, out var rec))
                {
                    value = defaultValue;
                }
                else if (rec.Type == FieldType.Null)
                {
                    value = null;
                }
                else if (rec.Type == FieldType.String)
                {
                    value = rec.Value as string;
                }
                else value = defaultValue;
            }
        }
        public static void Look(string tag, ref long value, long defaultValue = 0L)
        {
            if (Scribe.mode == ScribeMode.Saving)
            {
                var tmp = value;
                Scribe.WriteField(FieldType.Int64, tag, tmp);
            }
            else if (Scribe.mode == ScribeMode.Loading)
            {
                if (Scribe.TryGetField(tag, out var rec) && rec.Type == FieldType.Int64)
                {
                    var raw = rec.Value;
                    if (raw is byte[] bytes)
                        raw = new BinaryReader(new MemoryStream(bytes)).ReadInt64();
                    value = Convert.ToInt64(raw);
                }
                else value = defaultValue;
            }
        }
        public static void LookEnum<TEnum>(string tag, ref TEnum value, TEnum defaultValue = default)
            where TEnum : struct, Enum
        {
            if (Scribe.mode == ScribeMode.Saving)
            {
                var tmp = value;
                Scribe.WriteField(FieldType.EnumInt32, tag, Convert.ToInt32(tmp));
            }
            else if (Scribe.mode == ScribeMode.Loading)
            {
                if (Scribe.TryGetField(tag, out var rec) && rec.Type == FieldType.EnumInt32)
                {
                    var raw = rec.Value;
                    if (raw is byte[] bytes)
                        raw = new BinaryReader(new MemoryStream(bytes)).ReadInt32();
                    value = (TEnum)Enum.ToObject(typeof(TEnum), Convert.ToInt32(raw));
                }
                else value = defaultValue;
            }
        }
        public static void LookDictStrEnumInt32<TEnum>(string tag, ref Dictionary<string, TEnum> dict, Dictionary<string, TEnum> defaultValue = null)
            where TEnum : struct, Enum
        {
            if (Scribe.mode == ScribeMode.Saving)
            {
                var tmp = dict;
                Scribe.WriteField(FieldType.DictStrEnumInt32, tag, tmp);
            }
            else if (Scribe.mode == ScribeMode.Loading)
            {
                if (Scribe.TryGetField(tag, out var rec) && rec.Type == FieldType.DictStrEnumInt32)
                {
                    var rawDict = rec.Value as Dictionary<string, object>;
                    if (rawDict != null)
                    {
                        dict = new Dictionary<string, TEnum>();
                        foreach (var kvp in rawDict)
                        {
                            int intVal = ReadInt(kvp.Value, 0);
                            TEnum enumVal = (TEnum)Enum.ToObject(typeof(TEnum), intVal);
                            dict[kvp.Key] = enumVal;
                        }
                    }
                    else
                    {
                        dict = defaultValue;
                    }
                }
                else dict = defaultValue;
            }
        }

        private static int ReadInt(object raw, int defaultValue)
        {
            if (raw is byte[] bytes)
                return new BinaryReader(new MemoryStream(bytes)).ReadInt32();
            if (raw is JValue jv && jv.Type == JTokenType.Integer) return jv.Value<int>();
            if (raw == null) return defaultValue;
            return Convert.ToInt32(raw);
        }

        
    }
}