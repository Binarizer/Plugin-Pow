using System;
using System.Reflection;
using System.Collections.Generic;
using HarmonyLib;
using BepInEx;
using BepInEx.Configuration;
using System.Linq;

namespace PathOfWuxia
{
    [BepInPlugin("binarizer.plugin.pow.function_sets", "功能合集 by Binarizer，修改 by 寻宇", "2.0")]
    public class PluginBinarizer : BaseUnityPlugin
    {
        /// <summary>
        /// 加载模块
        /// </summary>
        void RegisterHook(Type hookType)
        {
            try
            {
                if (Activator.CreateInstance(hookType) is IHook hook)
                {
                    hook.OnRegister(this);
                    Harmony.CreateAndPatchAll(hook.GetType());
                    hooks.Add(hook);
                    Console.WriteLine($"加载该模块成功！");
                }
                else
                {
                    Console.WriteLine($"加载该模块失败！");
                }
            }
            catch
            {
                Console.WriteLine($"加载该模块失败！");
            }
        }

        /// <summary>
        /// 卸载模块，还没搞懂，先重启吧
        /// </summary>
        void UnregisterHook(IHook hook)
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
        /// 注册各模块
        /// </summary>
        void Awake()
        {
            var adv1 = new ConfigDescription("游戏重启生效", null, new ConfigurationManagerAttributes { IsAdvanced = true, Order = 4 });
            Assembly assembly = Assembly.GetExecutingAssembly();
            Console.WriteLine($"当前程序：{assembly.FullName}");
            var hookTypes = from t in assembly.GetTypes() where typeof(IHook).IsAssignableFrom(t) && !t.IsAbstract select t;
            Console.WriteLine("美好的初始化开始，统计模块");
            foreach (var hookType in hookTypes)
            {
                Console.WriteLine($"统计模块 [{hookType.Name}]");
                moduleEntries[hookType] = Config.Bind("模块选择", hookType.Name, false, adv1);
            }
            Console.WriteLine($"可注册模块共计{moduleEntries.Count}个");
            foreach (var modulePair in moduleEntries)
            {
                if (modulePair.Value == null)
                {
                    Console.WriteLine($"设置加载失败!{modulePair.Key.Name}");
                    continue;
                }
                if (modulePair.Value.Value)
                {
                    Console.WriteLine($"加载模块 [{ modulePair.Key.Name}]");
                    RegisterHook(modulePair.Key);
                }
            }
            Console.WriteLine($"加载模块生效{hooks.Count}个");
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
