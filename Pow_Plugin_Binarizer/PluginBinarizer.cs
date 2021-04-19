using System;
using System.Collections.Generic;
using HarmonyLib;
using BepInEx;

namespace PathOfWuxia
{
    [BepInPlugin("binarizer.plugin.pow.function_sets", "功能合集 by Binarizer", "1.2")]
    public class PluginBinarizer : BaseUnityPlugin
    {
        void RegisterHook(IHook hook)
        {
            hook.OnRegister(this);
            Harmony.CreateAndPatchAll( hook.GetType() );
            hooks.Add(hook);
        }

        private List<IHook> hooks = new List<IHook>();

        void Awake()
        {
            Console.WriteLine("美好的初始化开始");

            RegisterHook(new HookModSupport());
            RegisterHook(new HookGenerals());
            RegisterHook(new HookNewGame());
            RegisterHook(new HookFeaturesAndFixes());
            RegisterHook(new HookMoreAccessories());
            RegisterHook(new HookModExtensions());
            RegisterHook(new HookModDebug());
            RegisterHook(new HookUniqueItem());
            RegisterHook(new HookSkillExp());
            RegisterHook(new HookTeamManage());
            RegisterHook(new HookInitiactiveBattle());
            RegisterHook(new HookDuelPractice());
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
