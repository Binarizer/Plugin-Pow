using System;
using BepInEx.Configuration;
using HarmonyLib;
using Heluo.Battle;
using Heluo;
using System.ComponentModel;

namespace PathOfWuxia
{
    [System.ComponentModel.DisplayName("显示隐藏buff")]
    [Description("显示隐藏buff")]
    class HookBuff : IHook
    {
        private static ConfigEntry<bool> showHideBuff;


        public void OnRegister(PluginBinarizer plugin)
        {
            showHideBuff = plugin.Config.Bind("界面改进", "显示隐藏buff", false, "显示隐藏buff（图标暂时显示为特质buff图标） 最好配合mod：fixedBuff使用");
        }


        //给不显示的buff加上图标（暂时统一用特质buff图标）
        [HarmonyPrefix, HarmonyPatch(typeof(WuxiaBattleBuffer), "AddBuffer", new Type[] { typeof(WuxiaUnit), typeof(Heluo.Data.Buffer), typeof(BufferType) })]
        public static bool AddBufferPatch_showHideBuff(ref WuxiaBattleBuffer __instance, ref Heluo.Data.Buffer buffer)
        {
            if (showHideBuff.Value)
            {
                if (buffer == null)
                {
                    Logger.LogError("要附加的Buffer是空的", "AddBuffer", "D:\\Work\\PathOfWuxia2018_Update\\Assets\\Scripts\\Battle\\WuxiaBattleBuffer.cs", 154);
                    return true;
                }
                if (buffer.IconName == null || buffer.IconName.Equals(string.Empty))
                {
                    buffer.IconName = "buff_trait";
                }
            }
            return true;
        }

    }
}