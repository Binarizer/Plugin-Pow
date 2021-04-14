// Mod扩展存档数据类
using System;
using System.Collections;
using System.Collections.Generic;
using Heluo.Data;
using UnityEngine;
using MessagePack;
using Heluo.Utility;

namespace PathOfWuxia
{
    public class ModExtensionSaveData
    {
        public static ModExtensionSaveData Instance = new ModExtensionSaveData();

        // 显式声明可存储
        public Dictionary<string, Props> uniqueProps = new Dictionary<string, Props>();
        public Dictionary<string, Reward> uniqueReward = new Dictionary<string, Reward>();

        // 临时的不存储
        [IgnoreMember]
        public Dictionary<Type, IDictionary> tempData = new Dictionary<Type, IDictionary>();

        // 通用数据接口，给DataManager用
        public static T GetItem<T>(string Id) where T : Item
        {
            if (Id.IsNullOrEmpty()) return null;
            return GetTempItem<T>(Id) ?? GetUniqueItem<T>(Id);
        }

        public static T GetUniqueItem<T>(string id) where T : Item
        {
            Dictionary<string, T> dic = Instance.GetUniqueDictionary<T>();
            if (dic.ContainsKey(id))
            {
                return dic[id];
            }
            return default(T);
        }
        public static void AddUniqueItem<T>(T item) where T : Item
        {
            Dictionary<string, T> dic = Instance.GetUniqueDictionary<T>();
            do
            {
                item.Id = GetNextUniqueId(item.Id);
            }
            while (dic.ContainsKey(item.Id));
            Debug.Log("Try Add Unique Item: " + LZ4MessagePackSerializer.ToJson<T>(item, HeluoResolver.Instance));
            dic.Add(item.Id, item);
        }
        public static void RemoveUniqueItem<T>(string id) where T : Item
        {
            Dictionary<string, T> dic = Instance.GetUniqueDictionary<T>();
            Debug.Log("Try Remove Unique Item: " + id);
            dic.Remove(id);
        }

        public static T GetTempItem<T>(string id) where T : Item
        {
            Dictionary<string, T> dic = Instance.GetTempDictionary<T>();
            if (dic.ContainsKey(id))
            {
                return dic[id];
            }
            return default(T);
        }
        public static void AddTempItem<T>(T item) where T : Item
        {
            Dictionary<string, T> dic = Instance.GetTempDictionary<T>();
            do
            {
                item.Id = GetNextTempId(item.Id);
            }
            while (dic.ContainsKey(item.Id));
            Debug.Log("Try Add Temp Item: " + LZ4MessagePackSerializer.ToJson<T>(item, HeluoResolver.Instance));
            dic.Add(item.Id, item);
        }
        public static void RemoveTempItem<T>(string id) where T : Item
        {
            Dictionary<string, T> dic = Instance.GetTempDictionary<T>();
            Debug.Log("Try Remove Temp Item: " + id);
            dic.Remove(id);
        }

        // uniqueId: $[sourceId]#[000~999]
        public static string GetUniqueSourceId(string id)
        {
            if (id[0] != '$')
                return id;
            int end = id.LastIndexOf('#');
            return id.Substring(1, id.LastIndexOf('#') - 1);
        }
        public static string GetNextUniqueId(string id)
        {
            if (id[0] != '$')
                return "$" + id + "#000";
            int end = id.LastIndexOf('#');
            int uniqueId = int.Parse(id.Substring(end + 1));
            return id.Substring(0, id.Length - 3) + (uniqueId + 1).ToString("D3");
        }

        // tempId: ![sourceId]~[000~999]
        public static string GetTempSourceId(string id)
        {
            if (id[0] != '!')
                return id;
            int end = id.LastIndexOf('~');
            return id.Substring(1, id.LastIndexOf('~') - 1);
        }
        private static string GetNextTempId(string id)
        {
            if (id[0] != '!')
                return "!" + id + "~000";
            int end = id.LastIndexOf('~');
            int tempId = int.Parse(id.Substring(end + 1));
            return id.Substring(0, id.Length - 3) + (tempId + 1).ToString("D3");
        }

        private Dictionary<string, T> GetUniqueDictionary<T>() where T : Item
        {
            if (typeof(T) == typeof(Props))
            {
                if (this.uniqueProps == null)
                {
                    this.uniqueProps = new Dictionary<string, Props>();
                }
                return this.uniqueProps as Dictionary<string, T>;
            }
            if (typeof(T) == typeof(Reward))
            {
                if (this.uniqueReward == null)
                {
                    this.uniqueReward = new Dictionary<string, Reward>();
                }
                return this.uniqueReward as Dictionary<string, T>;
            }
            return new Dictionary<string, T>();
        }

        private Dictionary<string, T> GetTempDictionary<T>() where T : Item
        {
            Type type = typeof(T);
            if (tempData.ContainsKey(type))
                return tempData[type] as Dictionary<string, T>;
            var result = new Dictionary<string, T>();
            tempData.Add(type, result);
            return result;
        }
    }
}