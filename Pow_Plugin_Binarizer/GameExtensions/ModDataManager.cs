// Mod 数据库 - 包含DataManager, 用来制作UniqueItem
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Heluo.Resource;
using Heluo;
using Heluo.Data;
using HarmonyLib;

namespace PathOfWuxia
{
    public class ModDataManager : IDataProvider
    {
        public ModDataManager()
        {
            data = Game.Data as DataManager;
            dict = Traverse.Create(data).Field("dict").GetValue<IDictionary<Type, IDictionary>>();
        }

        private DataManager data;                       // 原数据管理类
        private IDictionary<Type, IDictionary> dict;    // 直接操作数据

        public void Reset(IResourceProvider resource, string path)
        {
            data.Reset(resource, path);
        }

        public void Add<T>(T item) where T : Item
        {
            data.Add(item);
        }

        public T Get<T>(string id) where T : Item
        {
            return data.Get<T>(id);
        }

        public List<T> Get<T>(params string[] id) where T : Item
        {
            return data.Get<T>(id);
        }

        public List<T> Get<T>(Func<T, bool> filter) where T : Item
        {
            return data.Get(filter);
        }

        public Dictionary<string, T> Get<T>() where T : Item
        {
            return data.Get<T>();
        }

        public void Reset<T>(IResourceProvider resource, string path) where T : Item
        {
            data.Reset<T>(resource, path);
        }

        public Task RestAsync(IResourceProvider resource, string path)
        {
            return data.RestAsync(resource, path);
        }
    }
}

