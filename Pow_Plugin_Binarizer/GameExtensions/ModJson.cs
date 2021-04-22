using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Heluo.Data.Converter;
using Heluo.Flow;
using Heluo.Flow.Battle;
using Heluo.Utility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PathOfWuxia
{
    public static class ModJson
    {
        private static readonly JsonSerializerSettings settingMod = new JsonSerializerSettings
        {
            DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            TypeNameHandling = TypeNameHandling.Auto,
            Binder = new ModOutputNodeConverter.Binder(),
            Converters = new JsonConverter[]
            {
                new Vector3Converter(),
                new EnumToStringConverter(),
                new BufferEventNodeConverter(),
                new BattleAndBuffGraphConverter(),
                //new ModOutputNodeConverter(),
                new ScheduleGraphConverter()
            }
        };
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
        public static string ToJsonMod<T>(T obj, Type type, bool pritty = false, bool simple = true)
        {
            var str = ToJson(obj, type, settingMod, pritty);

            if (simple)
            {
                str = ModOutputNodeConverter.SimplifyTypeName(str);
            }

            return str;
        }

        public static T FromReaderMod<T>(JsonReader reader)
        {
            return JsonSerializer.Create(settingMod).Deserialize<T>(reader);
        }

        public static T FromJsonResource<T>(string path, bool replaceText = false) where T : class
        {
            string source = Heluo.Game.Resource.LoadString(path);
            if (replaceText && string.IsNullOrEmpty(source))
            {
                // filename replace
                source = Heluo.Game.Resource.LoadString(GlobalLib.ReplaceText(source));
            }
            if (string.IsNullOrEmpty(source))
            {
                Console.WriteLine("找不到Json资源: " + path);
                return null;
            }
            if (source[0] != '{')
            {
                // original load from FileHelperEngine
                var fileHelperEngine = new FileHelpers.FileHelperEngine<T>(System.Text.Encoding.UTF8);
                return fileHelperEngine.ReadString(source)[0];
            }
            else
            {
                // load from valid json
                if (replaceText)
                {
                    // content replace
                    source = GlobalLib.ReplaceText(source);
                }
                return FromJsonMod<T>(source);
            }
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
            JToken token = JToken.Load(reader);
            return token.ToObject(objectType);
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(Enum.GetName(value.GetType(), value));
        }
    }
    // Mod OutputNode Json Converter
    public class ModOutputNodeConverter : JsonConverter
    {
        // override methods
        public override bool CanConvert(Type objectType)
        {
            return Valid(objectType);
        }
        public static bool Valid(Type objectType)
        {
            return typeof(OutputNode).IsAssignableFrom(objectType);
        }

        public override bool CanRead => true;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
            {
                // original import method
                return OutputNodeConvert.Deserialize(reader.Value.ToString());
            }
            // mod import
            Console.WriteLine("检测到非String格式Node：" + reader.TokenType);
            JObject o = JObject.Load(reader);
            Type type = objectType;
            var typeName = o["$type"]?.Value<string>();
            if (typeName != null)
                type = GetNodeType(typeName, objectType);
            object result = Activator.CreateInstance(type);
            var fields = result.GetType().GetFields();
            foreach (var info in fields)
            {
                JToken t = o[info.Name];
                if ( t != null)
                {
                    info.SetValue(result, t.ToObject(info.FieldType, serializer));
                }
            }
            return result;
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            Type type = value.GetType();
            JObject o = new JObject
            {
                {"$type", type.Name }
            };
            TypeInfo ti = TypeInfos[type.Name];
            var defaultValue = ti.defaults.Find(x => x.GetType() == type);
            if (defaultValue == null)
            {
                defaultValue = Activator.CreateInstance(value.GetType()) as OutputNode;
                ti.defaults.Add(defaultValue);
            }
            Console.WriteLine("type = " + type.Name);
            var fieldInfos = from f in type.GetFields() where !f.HasAttribute<JsonIgnoreAttribute>() && !f.IsLiteral select f;
            foreach (FieldInfo info in fieldInfos)
            {
                Console.WriteLine("name = "+info.Name);
                var v = info.GetValue(value);
                var d = info.GetValue(defaultValue);
                if (!v.Equals(d))
                    o.Add(info.Name, JToken.FromObject(v, serializer));
            }
            o.WriteTo(writer);
        }

        // static class & methods
        public static Type GetNodeType(string typeName, Type baseType = null)
        {
            if (TypeInfos.ContainsKey(typeName))
            {
                TypeInfo ti = TypeInfos[typeName];
                if (baseType == null)
                    return ti.types.First();
                return ti.types.Find(x => x.IsSubclassOf(baseType));
            }
            return null;
        }

        public static string SimplifyTypeName(string jsonStr)
        {
            foreach (Assembly assembly in assemblies)
            {
                jsonStr = jsonStr.Replace(string.Format(", {0}", assembly.GetName().Name), "");
            }
            foreach (string name in Namespaces)
            {
                jsonStr = jsonStr.Replace(string.Format("{0}.", name), "");
            }
            return jsonStr;
        }
        public class Binder : SerializationBinder
        {
            public override Type BindToType(string assemblyName, string typeName)
            {
                return GetNodeType(typeName) ?? Assembly.Load(assemblyName).GetType(typeName);
            }
        }

        // Properties & Fields
        public static Dictionary<string, TypeInfo> TypeInfos
        {
            get
            {
                if (typeInfos == null)
                {
                    typeInfos = new Dictionary<string, TypeInfo>();
                    foreach (var assembly in assemblies)
                    {
                        foreach (var type in from t in assembly.GetTypes() where Valid(t) select t)
                        {
                            if (!typeInfos.ContainsKey(type.Name))
                            {
                                typeInfos.Add(type.Name, new TypeInfo());
                            }
                            typeInfos[type.Name].types.Add(type);
                        }
                    }
                }
                return typeInfos;
            }
        }
        public static List<string> Namespaces
        {
            get
            {
                if (namespaces == null)  
                {
                    namespaces = new List<string>();
                    foreach (TypeInfo info in TypeInfos.Values)
                    {
                        foreach (Type t in info.types)
                            if (!namespaces.Contains(t.Namespace))
                                namespaces.Add(t.Namespace);
                    }
                    namespaces.Sort((a, b) => b.Length.CompareTo(a.Length));// 替换时从长到短保证正确
                }
                return namespaces;
            }
        }
        static readonly List<Assembly> assemblies = new List<Assembly>()
        {
            Assembly.GetAssembly(typeof(OutputNode)),
            Assembly.GetExecutingAssembly()
        };
        public class TypeInfo
        {
            public List<Type> types = new List<Type>();
            public List<OutputNode> defaults = new List<OutputNode>();
        }
        static Dictionary<string, TypeInfo> typeInfos = null;
        static List<string> namespaces = null;
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
    // specific for battle & buff json file (read/write only BattleRootNode/BufferRootNode, which are collection of OutputNode<Battle.Status>), and static loaders
    public class BattleAndBuffGraphConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(BattleBehaviourGraph) || objectType == typeof(BufferBehaviourGraph);
        }
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject item = JObject.Load(reader);
            BaseFlowGraph result = Activator.CreateInstance(objectType) as BaseFlowGraph;
            result.Output = item.ToObject<OutputNode<Status>>(serializer);
            return result;
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            BaseFlowGraph graph = value as BaseFlowGraph;
            JObject item = (JObject)JToken.FromObject(graph.Output, serializer);
            item.AddFirst(new JProperty("$type", graph.Output.GetType().Name));
            item.WriteTo(writer);
        }
    }
    // Rebulid ScheduleGraph with Metadata (list of OutputNode<bool>)
    public class ScheduleGraphConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(ScheduleGraph.Bundle);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JObject item = JObject.Load(reader);
            var result = new ScheduleGraph.Bundle
            {
                Name = item["Name"].Value<string>()
            };

            if (!(item["Nodes"] is JArray nodes))
                return result;
            else
                result.Nodes = new List<ScheduleGraph.Instruction>();

            // build metadata
            metaToIndex.Clear();
            metaToIndex.Add("#end", -1);
            for (int i = 0; i < nodes.Count; ++i)
            {
                metadata[result] = new Dictionary<int, string>();
                JObject o = (JObject)nodes[i];
                var id = o.GetValue("$id");
                if (id != null)
                {
                    string idstr = id.Value<string>();
                    metaToIndex[idstr] = i;
                    metadata[result][i] = idstr;
                }
            }

            TokenToIndex(item["EntryIndex"], ref result.EntryIndex);

            for (int i = 0; i < nodes.Count; ++i)
            {
                JObject o = (JObject)nodes[i];
                JToken branch = o.GetValue("BranchCondition");
                if (branch == null)
                {
                    JToken token = o.GetValue("Node");
                    ActionNode actionNode;
                    if (token.Type == JTokenType.String)
                    {
                        // 原脚本模式
                        actionNode = OutputNodeConvert.Deserialize(token.Value<string>()) as ActionNode;
                    }
                    else
                    {
                        actionNode = token.ToObject<ActionNode>(serializer);
                    }
                    ScheduleGraph.Instruction ins = new ScheduleGraph.Instruction
                    {
                        Next = i + 1,
                        Prallel = -1,
                        Node = actionNode
                    };
                    TokenToIndex(o["Next"], ref ins.Next);
                    TokenToIndex(o["Prallel"], ref ins.Prallel);
                    result.Nodes.Add(ins);
                }
                else
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
                    TokenToIndex(o["Pass"], ref ba.trueNodeIndex);
                    TokenToIndex(o["Fail"], ref ba.falseNodeIndex);
                    TokenToIndex(o["Prallel"], ref ins.Prallel);
                    ins.Node = ba;
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

            if (WriteSorted)
                SortPreorder(bundle);

            var jo = new JObject { { "Name", bundle.Name } };
            if (bundle.EntryIndex != 0)
                jo.Add("EntryIndex", GetMetaId(bundle, bundle.EntryIndex));
            JArray nodes = new JArray();
            for (int i = 0; i < bundle.Nodes.Count; ++i)
            {
                ScheduleGraph.Instruction n = bundle.Nodes[i];
                JObject o = new JObject();
                if (n.Node is BranchAction branch)
                {
                    var node = (JObject)JToken.FromObject(branch.conditionNode, serializer);
                    node.AddFirst(new JProperty("$type", branch.conditionNode.GetType().Name));
                    o.Add("BranchCondition", node);
                    if (branch.trueNodeIndex != i + 1)
                    {
                        o.Add("Pass", GetMetaId(bundle, branch.trueNodeIndex));
                    }
                    if (branch.falseNodeIndex != i + 1)
                    {
                        o.Add("Fail", GetMetaId(bundle, branch.falseNodeIndex));
                    }
                }
                else
                {
                    var node = (JObject)JToken.FromObject(n.Node, serializer);
                    node.AddFirst(new JProperty("$type", n.Node.GetType().Name));
                    o.Add("Node", node);
                    if (n.Next != i + 1)
                        o.Add("Next", GetMetaId(bundle, n.Next));
                }
                if (n.Prallel >= 0)
                    o.Add("Prallel", GetMetaId(bundle, n.Prallel));
                nodes.Add(o);
            }
            // write metaId
            foreach (var p in metadata[bundle])
            {
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

        static Dictionary<ScheduleGraph.Bundle, Dictionary<int, string>> metadata = new Dictionary<ScheduleGraph.Bundle, Dictionary<int, string>>();
        static Dictionary<string, int> metaToIndex = new Dictionary<string, int>();
        static string GetMetaId(ScheduleGraph.Bundle bundle, int index)
        {
            if (!metadata[bundle].ContainsKey(index))
                metadata[bundle].Add(index, "#" + (index >= 0 ? index.ToString() : "end"));
            return metadata[bundle][index];
        }
        static void TokenToIndex(JToken token, ref int index)
        {
            if (token == null)
                return;
            if (token.Type == JTokenType.String)
            {
                string key = token.Value<string>();
                if (metaToIndex.ContainsKey(key))
                    index = metaToIndex[token.Value<string>()];
                else
                    Console.WriteLine("解析错误，不存在编号为\""+key+"\"的节点，默认为下个节点");
            }
            else
            {
                index = token.Value<int>();
            }
        }

        static public bool WriteSorted = false;
        Dictionary<int, int> preOrderMap = new Dictionary<int, int>();
        List<ScheduleGraph.Instruction> ordered = null;
        void SortPreorder(ScheduleGraph.Bundle bundle)
        {
            preOrderMap.Clear();
            ordered = new List<ScheduleGraph.Instruction>();
            // traversal valid nodes
            PreOrderTraversal(bundle, bundle.EntryIndex);
            // traversal dummy nodes
            for (int i = 0; i < bundle.Nodes.Count; ++i)
            {
                if (!preOrderMap.ContainsKey(i))
                    PreOrderTraversal(bundle, i);
            }
            // reassign indices
            foreach (var ins in ordered)
            {
                ReassignIndex(bundle, ref ins.Next);
                ReassignIndex(bundle, ref ins.Prallel);
                if (ins.Node is BranchAction b)
                {
                    ReassignIndex(bundle, ref b.trueNodeIndex);
                    ReassignIndex(bundle, ref b.falseNodeIndex);
                }
            }
            // reset nodes
            bundle.EntryIndex = 0;
            bundle.Nodes = ordered;
            ordered = null;
        }
        void PreOrderTraversal(ScheduleGraph.Bundle bundle, int i)
        {
            if (i < 0 || i >= bundle.Nodes.Count)
                return;
            if (preOrderMap.ContainsKey(i))
                return;

            preOrderMap[i] = ordered.Count;
            ScheduleGraph.Instruction ins = bundle.Nodes[i];
            ordered.Add(ins);

            PreOrderTraversal(bundle, ins.Next);
            PreOrderTraversal(bundle, ins.Prallel);
            if (ins.Node is BranchAction b)
            {
                PreOrderTraversal(bundle, b.trueNodeIndex);
                PreOrderTraversal(bundle, b.falseNodeIndex);
            }
        }
        void ReassignIndex(ScheduleGraph.Bundle bundle, ref int i)
        {
            if (preOrderMap.ContainsKey(i))
                i = preOrderMap[i];
        }
    }
}
