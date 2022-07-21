using BepInEx.Configuration;
using HarmonyLib;
using Heluo.Flow;
using Heluo.Manager;
using System;

namespace PathOfWuxia
{
    [System.ComponentModel.DisplayName("锁定昼夜时间")]
    [System.ComponentModel.Description("锁定昼夜时间")]
    class HookLockTime : IHook
    {
        static ConfigEntry<bool> lockTime;

        public static NurturanceLoadScenesAction nururanceLoadScenesAction;
        public static bool isnextTime;
        public static TimeStage timeStage;
        public void OnRegister(PluginBinarizer plugin)
        {
            lockTime = plugin.Config.Bind("游戏设定", "锁定昼夜时间", false, "锁定昼夜时间，主线时开启可能卡场景");
        }
        [HarmonyPrefix, HarmonyPatch(typeof(NurturanceLoadScenesAction), "GetValue")]
        public static bool NurturanceLoadScenesActionPatch_GetValue(NurturanceLoadScenesAction __instance)
        {
            if (__instance != nururanceLoadScenesAction)
            {
                //Console.WriteLine("不相同，记录数据");
                nururanceLoadScenesAction = __instance;

                isnextTime = __instance.isNextTime;
                timeStage = __instance.timeStage;
            }

            if (lockTime.Value)
            {
                //Console.WriteLine("锁定时间");
                __instance.isNextTime = false;
                __instance.timeStage = TimeStage.None;
            }
            else
            {
                if (nururanceLoadScenesAction == __instance)
                {
                    //Console.WriteLine("没锁定的情况下相同，还原数据");
                    __instance.isNextTime = isnextTime;
                    __instance.timeStage = timeStage;
                }
            }
            return true;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(LoadScenesAction), "GetValue")]
        public static bool LoadScenesActionPatch_GetValue(LoadScenesAction __instance)
        {

            if (lockTime.Value)
            {
                __instance.isNextTime = false;
                __instance.timeStage = TimeStage.None;
            }
            return true;
        }
    }

}