using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;
using Heluo;
using Heluo.UI;
using Heluo.Data;
using Heluo.Flow;
using Heluo.Utility;
using System.Reflection.Emit;

namespace PathOfWuxia
{
    // 半即时战斗 todo
    public class HookInitiactiveBattle : IHook
    {
        public void OnRegister(BaseUnityPlugin plugin)
        {
            initiactiveBattle = plugin.Config.Bind<bool>("扩展功能", "半即时战斗", false, "开关时序制半即时战斗系统（未完成）");
        }

        public void OnUpdate()
        {
        }

        private static ConfigEntry<bool> initiactiveBattle;
    }
}
