using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using Heluo.Flow;
using Heluo.Flow.Battle;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PathOfWuxia
{
    public static class ModJson
    {
        // Json输出设定
        private static readonly JsonSerializerSettings settingMod = new JsonSerializerSettings
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
                new BufferEventNodeConverter(),
                //new ModOutputNodeConverter(),
                //new ScheduleGraphConverter()
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
            return FromJson<T>(content, settingMod);
        }
        public static string ToJson<T>(T obj, Type type, JsonSerializerSettings setting, bool pritty = false)
        {
            return JsonConvert.SerializeObject(obj, type, pritty ? Formatting.Indented : Formatting.None, setting);
        }
        public static string ToJsonMod<T>(T obj, Type type, string outputPath = null, bool pritty = false, bool simple = true)
        {
            var str = ToJson(obj, type, settingMod, pritty);
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
            return JsonSerializer.Create(settingMod).Deserialize<T>(reader);
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
    // Mod OutputNode
    public class ModOutputNodeConverter : JsonConverter
    {
        Dictionary<string, OutputNode> defaults = new Dictionary<string, OutputNode>();

        public override bool CanConvert(Type objectType)
        {
            return typeof(OutputNode).IsAssignableFrom(objectType);
        }

        public override bool CanRead => false;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var typename = value.GetType().Name;
            if (!defaults.ContainsKey(value.GetType().Name))
            {
                defaults.Add(typename, Activator.CreateInstance(value.GetType()) as OutputNode);
            }
            var defaultValue = defaults[typename];
            JObject o = new JObject
            {
                {"$type", typename }
            };
            var fields = value.GetType().GetFields();
            foreach (var info in fields)
            {
                if (info.GetCustomAttribute<InputFieldAttribute>() != null || info.GetCustomAttribute<ArgumentAttribute>() != null)
                {
                    var v = info.GetValue(value);
                    var d = info.GetValue(defaultValue);
                    if (!v.Equals(d))
                    {
                        o.Add(info.Name, JToken.FromObject(v, serializer));
                        Console.WriteLine("Added");
                    }
                }
            }
            o.WriteTo(writer);
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
    // Rebulid Graph with Metadata
    public class ScheduleGraphConverter : JsonConverter
    {
        static Dictionary<ScheduleGraph.Bundle, Dictionary<int, string>> metadata = new Dictionary<ScheduleGraph.Bundle, Dictionary<int, string>>();
        static string GetMetaId(ScheduleGraph.Bundle bundle, int index)
        {
            if (!metadata[bundle].ContainsKey(index))
                metadata[bundle].Add(index, "#" + (index >= 0 ? index.ToString() : "end"));
            return metadata[bundle][index];
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(ScheduleGraph.Bundle);
        }

        static Dictionary<string, int> metaToIndex = new Dictionary<string, int>();
        static int TokenToIndex(JToken token)
        {
            if (token.Type == JTokenType.String)
            {
                return metaToIndex[token.Value<string>()];
            }
            else
            {
                return token.Value<int>();
            }
        }
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject item = JObject.Load(reader);
            var result = new ScheduleGraph.Bundle
            {
                Name = item["Name"].Value<string>(),
                EntryIndex = item["EntryIndex"].Value<int>()
            };
            var nodes = item["Nodes"] as JArray;

            // build metadata
            metaToIndex.Clear();
            for (int i = 0; i < nodes.Count; ++i)
            {
                metadata[result] = new Dictionary<int, string>();
                var o = (JObject)nodes[i];
                var id = o.GetValue("$id");
                if (id != null)
                {
                    string idstr = id.Value<string>();
                    metaToIndex[idstr] = i;
                    metadata[result][i] = idstr;
                }
            }

            for (int i = 0; i < nodes.Count; ++i)
            {
                var o = (JObject)nodes[i];
                JToken branch = o.GetValue("BranchCondition");
                if (branch != null)
                {
                    ScheduleGraph.Instruction ins = new ScheduleGraph.Instruction
                    {
                        Next = -1,
                        Prallel = -1
                    };
                    BranchAction ba = new BranchAction
                    {
                        conditionNode = branch.ToObject<OutputNode<bool>>(serializer),
                        trueNodeIndex = i + 1,
                        falseNodeIndex = i + 1
                    };
                    JToken token = o.GetValue("NextTrue");
                    if (token != null)
                    {
                        ba.trueNodeIndex = TokenToIndex(token);
                    }
                    token = o.GetValue("NextFalse");
                    if (token != null)
                    {
                        ba.falseNodeIndex = TokenToIndex(token);
                    }
                    ins.Node = ba;
                    result.Nodes.Add(ins);
                }
                else
                {
                    ScheduleGraph.Instruction ins = new ScheduleGraph.Instruction
                    {
                        Next = i + 1,
                        Prallel = -1,
                        Node = o.GetValue("Node").ToObject<ActionNode>(serializer)
                    };
                    JToken t = o.GetValue("Next");
                    if (t != null)
                    {
                        ins.Next = TokenToIndex(t);
                    }
                    t = o.GetValue("Prallel");
                    if (t != null)
                    {
                        ins.Prallel = TokenToIndex(t);
                    }
                    result.Nodes.Add(ins);
                }
            }
            return result;
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            ScheduleGraph.Bundle bundle = value as ScheduleGraph.Bundle;
            if (!metadata.ContainsKey(bundle))
                metadata[bundle] = new Dictionary<int, string>();

            var jo = new JObject
            {
                { "Name", bundle.Name },
                { "EntryIndex", GetMetaId(bundle, bundle.EntryIndex) }
            };
            JArray nodes = new JArray();
            for ( int i = 0; i < bundle.Nodes.Count; ++i)
            {
                ScheduleGraph.Instruction n = bundle.Nodes[i];
                JObject o = new JObject();
                if (n.Node is BranchAction branch)
                {
                    o.Add("BranchCondition", JToken.FromObject(branch.conditionNode, serializer));
                    if (branch.trueNodeIndex != i + 1)
                    {
                        o.Add("NextTrue", GetMetaId(bundle, branch.trueNodeIndex));
                    }
                    if (branch.falseNodeIndex != i + 1)
                    {
                        o.Add("NextFalse", GetMetaId(bundle, branch.falseNodeIndex));
                    }
                }
                else
                {
                    o.Add("Node", JToken.FromObject(n.Node, serializer));
                    if (n.Next != i + 1)
                        o.Add("Next", GetMetaId(bundle, n.Next));
                    if (n.Prallel >= 0)
                        o.Add("Prallel", GetMetaId(bundle, n.Prallel));
                }
                nodes.Add(o);
            }
            // write metaId
            foreach (var p in metadata[bundle])
            {
                Console.WriteLine(string.Format("key={0}, value={1}", p.Key, p.Value));
                int i = p.Key;
                if (i >= 0 && i < nodes.Count)
                {
                    JObject node = (JObject)nodes[i];
                    node.AddFirst(new JProperty("$id", p.Value));
                }
            }
            jo.Add("Nodes", nodes);
            jo.WriteTo(writer);
        }
    }
}
