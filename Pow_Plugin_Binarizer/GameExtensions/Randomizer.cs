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
    // Mod数据随机类
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
                        return AddLuaParams(script, item) && script.DoString(pattern).CastToBool();
                    });
                    return true;
                }
            }
            return false;
        }

        private static bool AddLuaParams(Script script, T item)
        {
            if (!UserData.IsTypeRegistered(typeof(T)))
            {
                UserData.RegisterType(typeof(T));
            }
            if (item.GetType() != typeof(Npc))
            {
                script.Globals["item"] = item;
            }
            else
            {
                // 传NPC信息没太大用处，改传CharacterInfo的信息
                Npc npc = item as Npc;
                try
                {
                    CharacterExteriorData exterior = null;
                    CharacterInfoData info = null;
                    if (!npc.ExteriorId.IsNullOrEmpty())
                    {
                        exterior = Game.GameData.Exterior[npc.ExteriorId];
                    }
                    if (exterior != null && !exterior.InfoId.IsNullOrEmpty())
                    {
                        info = Game.GameData.Character[exterior.InfoId];
                    }
                    else if (!npc.CharacterInfoId.IsNullOrEmpty())
                    {
                        info = Game.GameData.Character[npc.CharacterInfoId];
                    }

                    if (info == null || info.Id.IsNullOrEmpty())
                        return false;

                    if (!UserData.IsTypeRegistered<CharacterInfoData>())
                        UserData.RegisterType<CharacterInfoData>(InteropAccessMode.Default, null);
                    script.Globals["item"] = info;

                    foreach (object obj in Enum.GetValues(typeof(CharacterUpgradableProperty)))
                    {
                        CharacterUpgradableProperty prop = (CharacterUpgradableProperty)obj;
                        int value = info.GetUpgradeableProperty(prop);
                        script.Globals[obj.ToString().ToLower()] = value;
                    }
                    foreach (object obj2 in Enum.GetValues(typeof(CharacterProperty)))
                    {
                        CharacterProperty prop2 = (CharacterProperty)obj2;
                        int value2 = info.Property[prop2].Value;
                        script.Globals[obj2.ToString().ToLower()] = value2;
                    }
                }
                catch
                {
                    Console.WriteLine(string.Format("NPC信息出错 npc={0}, exId={1}, infoId={2}", npc.Id, npc.ExteriorId, npc.CharacterInfoId));
                    return false;
                }
            }
            return true;
        }

        private List<T> randomCache = new List<T>();
    }
}