// Mod扩展存档数据类
using System;
using System.Collections;
using System.Collections.Generic;
using Heluo.Data;
using UnityEngine;
using MessagePack;

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
            return GetTempItem<T>(Id) ?? GetUniqueItem<T>(Id);
        }

        public static T GetUniqueItem<T>(string Id) where T : Item
        {
            Dictionary<string, T> dic = Instance.GetUniqueDictionary<T>();
            if (dic.ContainsKey(Id))
            {
                return dic[Id];
            }
            return default(T);
        }

        public static void AddUniqueItem<T>(T item) where T : Item
        {
            Dictionary<string, T> dic = Instance.GetUniqueDictionary<T>();
            do
            {
                item.Id = Instance.GetNextUniqueId(item.Id);
            }
            while (dic.ContainsKey(item.Id));
            Debug.Log("Try Add Item: " + LZ4MessagePackSerializer.ToJson<T>(item, HeluoResolver.Instance));
            dic.Add(item.Id, item);
        }

        public static T GetTempItem<T>(string Id) where T : Item
        {
            Dictionary<string, T> dic = Instance.GetTempDictionary<T>();
            if (dic.ContainsKey(Id))
            {
                return dic[Id];
            }
            return default(T);
        }

        public static void AddTempItem<T>(T item) where T : Item
        {
            Dictionary<string, T> dic = Instance.GetTempDictionary<T>();
            do
            {
                item.Id = Instance.GetNextTempId(item.Id);
            }
            while (dic.ContainsKey(item.Id));
            Debug.Log("Try Add Temp Item: " + LZ4MessagePackSerializer.ToJson<T>(item, HeluoResolver.Instance));
            dic.Add(item.Id, item);
        }

        private string GetNextUniqueId(string s)
        {
            int u = s.LastIndexOf('#');
            if (u == -1)
            {
                return s + "#000";
            }
            int id = int.Parse(s.Substring(u + 1));
            return s.Substring(0, s.Length - 3) + (id + 1).ToString("D3");
        }

        private string GetNextTempId(string s)
        {
            int u = s.LastIndexOf('~');
            if (u == -1)
            {
                return s + "~000";
            }
            int id = int.Parse(s.Substring(u + 1));
            return s.Substring(0, s.Length - 3) + (id + 1).ToString("D3");
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