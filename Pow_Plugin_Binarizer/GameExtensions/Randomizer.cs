// Mod数据随机类
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Heluo;
using Heluo.Data;
using Heluo.Utility;
using MoonSharp.Interpreter;

namespace PathOfWuxia
{
    public static class Randomizer
    {
        public static T GetOneFromData<T>( string pattern ) where T: Item
        {
            Type t = typeof(T);
            if (!dataRandomizer.ContainsKey(typeof(T)))
            {
                dataRandomizer.Add(t, new DataRandomizer<T>());
            }
            DataRandomizer<T> randomizer = dataRandomizer[t] as DataRandomizer<T>;
            return randomizer.Get(pattern);
        }
        public static ICollection<T> GetAllFromData<T>(string pattern) where T : Item
        {
            Type t = typeof(T);
            if (!dataRandomizer.ContainsKey(typeof(T)))
            {
                dataRandomizer.Add(t, new DataRandomizer<T>());
            }
            DataRandomizer<T> randomizer = dataRandomizer[t] as DataRandomizer<T>;
            return randomizer.GetAll(pattern);
        }

        private static Dictionary<Type, object> dataRandomizer = new Dictionary<Type, object>();
    }

    public class DataRandomizer<T> where T : Item
    {
        public T Get(string pattern)
        {
            if (pattern.IsNullOrEmpty())
                return null;
            if ( Parse(pattern) )
            {
                if (randomCache.Count > 0)
                    return randomCache.Random();
            }
            return Game.Data.Get<T>(pattern);
        }

        public ICollection<T> GetAll(string pattern)
        {
            if (pattern.IsNullOrEmpty())
                return null;
            if (Parse(pattern))
                return randomCache;
            T item = Game.Data.Get<T>(pattern);
            if (item != null)
            {
                return new T[] { item };
            }
            return new T[] { };
        }

        private bool Parse(string pattern)
        {
            Dictionary<string, T> dic = Game.Data.Get<T>();
            if (dic != null)
            {
                char fc = pattern[0];
                if (fc == '*')  //延续使用Cache
                {
                    return true;
                }
                else if (fc == '^') // 正则表达式
                {
                    randomCache = Game.Data.Get<T>( item =>
                    {
                        return Regex.IsMatch(item.Id, pattern);
                    });
                    return true;
                }
                else if (fc == '&') // 枚举id列表
                {
                    var vc2 = pattern.Substring(1).Split(new char[]{'|'});
                    randomCache = Game.Data.Get<T>( item =>
                    {
                        return vc2.Contains(item.Id);
                    });
                    return true;
                }
                else if (fc == '=') // Lua公式
                {
                    List<string> vc3 = new List<string>();
                    Script script = new Script(CoreModules.Math);
                    pattern = "return " + pattern.Substring(1);
                    randomCache = Game.Data.Get<T>( item =>
                    {
                        //try
                        //{
                            AddLuaParams(script, item);
                            return script.DoString(pattern).CastToBool();
                        //}
                        //catch
                        //{
                        //    Debug.LogError(string.Format("尝试添加Lua数据出错：{0} - {1}", item.GetType(), item.Id));
                        //    return false;
                        //}
                    });
                    return true;
                }
            }
            return false;
        }

        private static void AddLuaParams(Script script, T item)
        {
            if (!UserData.IsTypeRegistered(typeof(T)))
            {
                UserData.RegisterType(typeof(T));
            }
            script.Globals["item"] = item;
        }

        private List<T> randomCache = new List<T>();
    }
}