using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using BepInEx;
using MessagePack;
using Newtonsoft.Json;

namespace PathOfWuxia
{
    [BepInPlugin("binarizer.plugin.pow.function_sets", "功能合集 by Binarizer", "1.01")]
    public class PluginBinarizer : BaseUnityPlugin
    {
        void RegisterHook(IHook hook)
        {
            hook.OnRegister(this);
            Harmony.CreateAndPatchAll( hook.GetType() );
            hooks.Add(hook);
        }

        private List<IHook> hooks = new List<IHook>();

        [MessagePackObject]
        public class Test
        {
            [Key(0)]
            public string ts;
            [Key(1)]
            public float f;
        }

        void Awake()
        {
            Console.WriteLine("美好的初始化开始");

            RegisterHook(new HookModSupport());
            RegisterHook(new HookGenerals());
            RegisterHook(new HookNewGame());
            RegisterHook(new HookFeaturesAndFixes());
            RegisterHook(new HookMoreAccessories());
            RegisterHook(new HookModExtensions());
            RegisterHook(new HookUniqueItem());
            RegisterHook(new HookInitiactiveBattle());
        }

        void Start()
        {
            Console.WriteLine("美好的第一帧开始");
        }

        void Update()
        {
            foreach(IHook hook in hooks)
            {
                hook.OnUpdate();
            }
        }
    }
}
