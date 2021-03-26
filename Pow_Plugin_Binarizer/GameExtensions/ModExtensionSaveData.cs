// Mod扩展存档数据类
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Heluo;
using Heluo.Data;
using Heluo.Utility;
using UnityEngine;
using MessagePack;

namespace PathOfWuxia
{
    public class ModExtensionSaveData
    {
        // 显式声明才可存储
        public Dictionary<string, Props> uniqueProps = new Dictionary<string, Props>();
        public Dictionary<string, Reward> uniqueReward = new Dictionary<string, Reward>();

        public T GetUniqueItem<T>(string Id) where T : Item
        {
            Dictionary<string, T> dic = this.GetUniqueDictionary<T>();
            if (dic.ContainsKey(Id))
            {
                return dic[Id];
            }
            return default(T);
        }

        public void AddUniqueItem<T>(T item) where T : Item
        {
            Dictionary<string, T> dic = this.GetUniqueDictionary<T>();
            do
            {
                item.Id = this.GetNextUniqueId(item.Id);
            }
            while (dic.ContainsKey(item.Id));
            Debug.Log("Try Add Item: " + LZ4MessagePackSerializer.ToJson<T>(item, HeluoResolver.Instance));
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
    }
}