using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Heluo.Data.Converter;
using Heluo.Flow;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PathOfWuxia
{
    // 暂时没用到
    public class ModOutputNodeJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(OutputNode).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            Console.WriteLine("当前解析类型=" + objectType);
            Console.WriteLine("当前Reader值=" + reader.Value);
            Console.WriteLine("当前值=" + existingValue);
            var importSetting = new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.Populate,
                TypeNameHandling = TypeNameHandling.Objects,
                Binder = new OutputNodeBinder(),
                Converters = new JsonConverter[]
                {
                    //new Vector3Converter()
                }
            };
            return JsonConvert.DeserializeObject(reader.Value.ToString(), importSetting);
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            //Console.WriteLine("当前写入类型=" + value.GetType());
            var exportSetting = new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.Ignore,
                TypeNameHandling = TypeNameHandling.Objects,
                Converters = new JsonConverter[]
                {
                    new Vector3Converter()
                }
            };            
            string jsonStr = JsonConvert.SerializeObject(value, exportSetting);
            writer.WriteRawValue(jsonStr);
        }
    }
    // Unity的Vector3写入
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
            writer.WriteRawValue(JsonUtility.ToJson(value, false));
        }
    }
    // 用于简化类型名
    public class NodeTypeConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(string);
        }
        public override bool CanRead => false;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            string s = value as string;
            if (writer.Path.EndsWith(".$type"))
            {
                // 删除Assembly
                foreach (string name in GlobalLib.GetOutputNodeAssemblies())
                {
                    s = s.Replace(string.Format(", {0}", name), "");
                }
                // 删除Namespace
                foreach (string name in GlobalLib.GetOutputNodeNameSpaces())
                {
                    s = s.Replace(string.Format("{0}.", name), "");
                }
            }
            writer.WriteValue(s);
        }
    }

    // 可以接受简化版的类型名
    public class OutputNodeBinder : SerializationBinder
    {
        public override Type BindToType(string assemblyName, string typeName)
        {
            return GlobalLib.GetGameOutputNodeTypes().Find((Type item) => item.Name == typeName)
                ?? GlobalLib.GetModOutputNodeTypes().Find((Type item) => item.Name == typeName)
                ?? Assembly.Load(assemblyName).GetType(typeName);
        }
    }
}
