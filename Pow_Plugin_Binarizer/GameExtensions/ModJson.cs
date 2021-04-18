using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using Heluo.Flow.Battle;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PathOfWuxia
{
    public static class ModJson
    {
        // Json输出设定
        private static readonly JsonSerializerSettings exportSetting = new JsonSerializerSettings
        {
            DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto,
            Binder = new OutputNodeBinder(),
            Converters = new JsonConverter[]
            {
                new Vector3Converter(),
                new EnumToStringConverter(),
                new BufferEventNodeConverter()
            }
        };
        //// Json读取设定
        //private static readonly JsonSerializerSettings importSetting = new JsonSerializerSettings
        //{
        //    DefaultValueHandling = DefaultValueHandling.Populate,
        //    TypeNameHandling = TypeNameHandling.Auto,
        //    Binder = new OutputNodeBinder(),
        //    Converters = new JsonConverter[]
        //    {
        //        new EnumToStringConverter(),
        //        new BufferEventNodeConverter()
        //    }
        //};
        public static string SimplifyTypeName(string jsonStr)
        {
            // 删除Assembly
            foreach (string name in GlobalLib.GetOutputNodeAssemblies())
            {
                jsonStr = jsonStr.Replace(string.Format(", {0}", name), "");
            }
            // 删除Namespace
            foreach (string name in GlobalLib.GetOutputNodeNameSpaces())
            {
                jsonStr = jsonStr.Replace(string.Format("{0}.", name), "");
            }
            return jsonStr;
        }
        public static T FromJson<T>(string content, JsonSerializerSettings setting)
        {
            return JsonConvert.DeserializeObject<T>(content, setting);
        }
        public static T FromJsonMod<T>(string content)
        {
            return FromJson<T>(content, exportSetting);
        }
        public static string ToJson<T>(T obj, Type type, JsonSerializerSettings setting, bool pritty = false)
        {
            return JsonConvert.SerializeObject(obj, type, pritty ? Formatting.Indented : Formatting.None, setting);
        }
        public static string ToJsonMod<T>(T obj, Type type, string outputPath = null, bool pritty = false, bool simple = true)
        {
            var str = ToJson(obj, type, exportSetting, pritty);
            if (simple)
                str = SimplifyTypeName(str);
            if (!string.IsNullOrEmpty(outputPath))
            {
                Console.WriteLine("导出到文件 " + outputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
                var sr = File.CreateText(outputPath);
                sr.Write(str);
                sr.Close();
            }
            return str;
        }

        public static T FromReaderMod<T>(JsonReader reader)
        {
            return JsonSerializer.Create(exportSetting).Deserialize<T>(reader);
        }
    }
    // Type from simplified name
    public class OutputNodeBinder : SerializationBinder
    {
        public override Type BindToType(string assemblyName, string typeName)
        {
            return GlobalLib.GetGameOutputNodeTypes().Find((Type item) => item.Name == typeName)
                ?? GlobalLib.GetModOutputNodeTypes().Find((Type item) => item.Name == typeName)
                ?? Assembly.Load(assemblyName).GetType(typeName);
        }
    }
    // UnityEngine.Vector3
    public class Vector3Converter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Vector3);
        }

        public override bool CanRead => false;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            Vector3 v = (Vector3)value;
            new JObject
            {
                { "x", v.x },
                { "y", v.y },
                { "z", v.z }
            }.WriteTo(writer);
        }
    }
    // EnumToString
    public class EnumToStringConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType.IsEnum;
        }

        public override bool CanRead => true;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.Value is string s)
            {
                return Enum.Parse(objectType, s);
            }
            return reader.Value;
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(Enum.GetName(value.GetType(), value));
        }
    }
    // BufferEventNode has no [JsonObject]
    public class BufferEventNodeConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(BufferEventNode);
        }
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject item = JObject.Load(reader);
            return new BufferEventNode
            {
                eventType = item["eventType"].ToObject<Heluo.Data.BufferTiming>(serializer),
                child = item["child"].ToObject<List<BufferNode>>(serializer)
            };
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            BufferEventNode n = value as BufferEventNode;
            new JObject
            {
                { "eventType", JToken.FromObject(n.eventType, serializer) },
                { "child", JToken.FromObject(n.child, serializer) }
            }.WriteTo(writer);
        }
    }
}
