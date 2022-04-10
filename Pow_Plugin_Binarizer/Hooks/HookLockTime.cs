using BepInEx.Configuration;
using HarmonyLib;
using Heluo.Flow;

namespace PathOfWuxia
{
    [System.ComponentModel.DisplayName("锁定昼夜时间")]
    [System.ComponentModel.Description("锁定昼夜时间")]
    class HookLockTime : IHook
    {
        static ConfigEntry<bool> lockTime;
        public void OnRegister(PluginBinarizer plugin)
        {
            lockTime = plugin.Config.Bind("游戏设定", "锁定昼夜时间", false, "锁定昼夜时间");
        }
        [HarmonyPrefix, HarmonyPatch(typeof(NurturanceLoadScenesAction), "GetValue")]
        public static bool NurturanceLoadScenesActionPatch_GetValue(NurturanceLoadScenesAction __instance)
        {
            if (lockTime.Value)
            {
                __instance.isNextTime = false;
                __instance.timeStage = 0;
            }
            return true;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(LoadScenesAction), "GetValue")]
        public static bool LoadScenesActionPatch_GetValue(LoadScenesAction __instance)
        {
            if (lockTime.Value)
            {
                __instance.isNextTime = false;
                __instance.timeStage = 0;
            }
            return true;
        }
    }

}