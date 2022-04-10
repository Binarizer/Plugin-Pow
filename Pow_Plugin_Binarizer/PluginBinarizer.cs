using System;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using BepInEx;
using BepInEx.Configuration;
using System.Linq;
using System.ComponentModel;

namespace PathOfWuxia
{
    [BepInPlugin("binarizer.plugin.pow.function_sets", "功能合集 by Binarizer，修改 by 寻宇", "2.1.0")]
    public class PluginBinarizer : BaseUnityPlugin
    {
        /// <summary>
        /// 加载
        /// </summary>
        void RegisterHook(Type t)
        {
            try
            {
                IHook hook = Activator.CreateInstance(t) as IHook;
                hook.OnRegister(this);
                Harmony.CreateAndPatchAll(t);
                hooks.Add(hook);
                Console.WriteLine($"Patch {t.Name} Success!");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Patch {t.Name} Failed! Exception={e}");
                moduleEntries[t].Value = false;
            }
        }

        /// <summary>
        /// 卸载，还没搞懂，先重启吧
        /// </summary>
        void UnregisterHook(Type t)
        {
            //hook.OnUnregister(this);
            //Harmony
            //Console.WriteLine("Unpatch " + hook.GetType().Name);
            //hooks.Remove(hook);
        }

        private Dictionary<Type, ConfigEntry<bool>> moduleEntries = new Dictionary<Type, ConfigEntry<bool>>();
        private List<IHook> hooks = new List<IHook>();
        public Action onUpdate;

        /// <summary>
        /// 注册各模块的钩子
        /// </summary>
        void Awake()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            Console.WriteLine($"当前程序：{assembly.FullName}");
            var hookTypes = from t in assembly.GetTypes() where typeof(IHook).IsAssignableFrom(t) && !t.IsAbstract select t;
            Console.WriteLine("美好的初始化开始，统计钩子模块");
            foreach (var hookType in hookTypes)
            {
                DisplayNameAttribute displayName = (DisplayNameAttribute)hookType.GetCustomAttribute(typeof(DisplayNameAttribute));
                DescriptionAttribute description = (DescriptionAttribute)hookType.GetCustomAttribute(typeof(DescriptionAttribute));
                var adv1 = new ConfigDescription(description.Description, null, new ConfigurationManagerAttributes { IsAdvanced = true, Order = 4 });
                moduleEntries[hookType] = Config.Bind("模块选择", displayName.DisplayName, false, adv1);
                Console.WriteLine($"计入模块 [{displayName.DisplayName}]");
            }

            foreach (var modulePair in moduleEntries)
            {
                if (modulePair.Value.Value)
                    RegisterHook(modulePair.Key);
            }
            Console.WriteLine($"可注册钩子模块共计{moduleEntries.Count}个");
        }

        void Start()
        {
            Console.WriteLine("美好的第一帧开始");
        }

        void Update()
        {
            onUpdate?.Invoke();
        }
    }
}
