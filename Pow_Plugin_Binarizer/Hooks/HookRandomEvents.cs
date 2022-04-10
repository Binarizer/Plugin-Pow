using System;
using System.Collections.Generic;
using HarmonyLib;
using BepInEx;
using BepInEx.Configuration;
using Heluo.Flow;

namespace PathOfWuxia
{
    // 随机事件
    [System.ComponentModel.DisplayName("随机事件设置")]
    [System.ComponentModel.Description("随机事件")]
    public class HookRandomEvents : IHook
    {
        enum ProbablyMode
        {
            None,
            SmallChance,
            FixedRandomValue
        }
        static ConfigEntry<ProbablyMode> probablyMode;
        static ConfigEntry<int> probablyValue;

        public void OnRegister(PluginBinarizer plugin)
        {
            probablyMode = plugin.Config.Bind("游戏设定", "随机事件方式", ProbablyMode.None, "None-原版 SmallChance-小概率事件必发生 FixedRandomValue-设定产生的随机数");
            probablyValue = plugin.Config.Bind("游戏设定", "随机事件值", 50, "SmallChance：多少被界定为小概率 FixedRandomValue：1~100对应必发生/必不发生");
        }

        // 2 事件随机数调节
        [HarmonyPostfix, HarmonyPatch(typeof(Probability), "GetValue")]
        public static void ProbabilityPatch(Probability __instance, ref bool __result)
        {
            if (probablyMode.Value == ProbablyMode.FixedRandomValue)
            {
                __result = (probablyValue.Value - 1 < __instance.value);
            }
            else if (probablyMode.Value == ProbablyMode.SmallChance)
            {
                __result = (__instance.value < probablyValue.Value || __instance.value == 100f);
            }
        }
    }
}
