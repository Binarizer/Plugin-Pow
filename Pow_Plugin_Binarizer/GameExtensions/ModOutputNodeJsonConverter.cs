using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Heluo.Data;
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

    // BattleEventTiming可视化
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

    // 可以接受简化版的类型名
    public class OutputNodeBinder : SerializationBinder
    {
        public static readonly JsonSerializerSettings exportSetting = new JsonSerializerSettings
        {
            DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto,
            Converters = new JsonConverter[]
            {
                new Vector3Converter(),
                new EnumToStringConverter()
            }
        };
        public static readonly JsonSerializerSettings importSetting = new JsonSerializerSettings
        {
            DefaultValueHandling = DefaultValueHandling.Populate,
            TypeNameHandling = TypeNameHandling.Auto,
            Binder = new OutputNodeBinder(),
            Converters = new JsonConverter[]
            {
                new EnumToStringConverter()
            }
        };
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
        public override Type BindToType(string assemblyName, string typeName)
        {
            return GlobalLib.GetGameOutputNodeTypes().Find((Type item) => item.Name == typeName)
                ?? GlobalLib.GetModOutputNodeTypes().Find((Type item) => item.Name == typeName)
                ?? Assembly.Load(assemblyName).GetType(typeName);
        }
    }
}
