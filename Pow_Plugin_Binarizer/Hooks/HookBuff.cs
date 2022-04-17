using System;
using BepInEx.Configuration;
using HarmonyLib;
using Heluo.Battle;
using Heluo;
using System.ComponentModel;
using Heluo.UI;
using System.Collections.Generic;
using Heluo.Data;
using System.Reflection;

namespace PathOfWuxia
{
    [System.ComponentModel.DisplayName("显示隐藏buff")]
    [Description("显示隐藏buff,buff效果信息增强")]
    class HookBuff : IHook
    {
        private static ConfigEntry<bool> showHideBuff;
        private static ConfigEntry<bool> showBuffEffect;


        public void OnRegister(PluginBinarizer plugin)
        {
            showHideBuff = plugin.Config.Bind("界面改进", "显示隐藏buff", false, "显示隐藏buff（图标暂时显示为特质buff图标） 最好配合mod：fixedBuff使用");
            showBuffEffect = plugin.Config.Bind("界面改进", "buff效果信息增强", false, "显示buff的更多效果信息");
        }


        //给不显示的buff加上图标（暂时统一用特质buff图标）
        [HarmonyPrefix, HarmonyPatch(typeof(WuxiaBattleBuffer), "AddBuffer", new Type[] { typeof(WuxiaUnit), typeof(Heluo.Data.Buffer), typeof(BufferType) })]
        public static bool AddBufferPatch_showHideBuff(ref WuxiaBattleBuffer __instance, ref Heluo.Data.Buffer buffer)
        {
            Console.WriteLine("AddBufferPatch_showHideBuff");
            if (showHideBuff.Value)
            {
                if (buffer == null)
                {
                    Logger.LogError("要附加的Buffer是空的", "AddBuffer", "D:\\Work\\PathOfWuxia2018_Update\\Assets\\Scripts\\Battle\\WuxiaBattleBuffer.cs", 154);
                    return true;
                }
                Console.WriteLine(buffer.Id + buffer.Name + ":" + buffer.Desc + "," + buffer.IconName);
                if (buffer.IconName == null || buffer.IconName.Equals(string.Empty))
                {
                    buffer.IconName = "buff_trait";
                }
            }
            return true;
        }


        //buff效果信息增强
        [HarmonyPostfix, HarmonyPatch(typeof(WGAbilityInfo), "CreateBattleAttributesTipinfo", new Type[] { typeof(BufferInfo) })]
        public static void WGAbilityInfoPatch_CreateBattleAttributesTipinfo(ref WGAbilityInfo __instance, ref BufferInfo info)
        {
            if (showBuffEffect.Value)
            {
                Console.WriteLine("WGAbilityInfoPatch_CreateBattleAttributesTipinfo");
                foreach (object obj in Enum.GetValues(typeof(BattleAttributesType)))
                {
                    BattleAttributesType type = (BattleAttributesType)obj;
                    if (info.BufferAttributes.HasData(type))
                    {
                        switch (type)
                        {
                            case BattleAttributesType.LiberatedState:
                                Console.WriteLine("BattleAttributesType.LiberatedState");
                                __instance.CreateLiberatedStateTipinfo(info);
                                break;
                            case BattleAttributesType.RestrictedState:
                                Console.WriteLine("BattleAttributesType.RestrictedState");
                                __instance.CreateRestrictedStateTipinfo(info);
                                break;
                        }
                    }
                }
            }
        }

        //解放属性
        [HarmonyPrefix, HarmonyPatch(typeof(WGAbilityInfo), "CreateBattleAttributesTipinfo", new Type[] { typeof(BattleLiberatedState),typeof(int) })]
        public static bool WGAbilityInfoPatch_CreateBattleAttributesTipinfo(ref WGAbilityInfo __instance, ref BattleLiberatedState prop,ref int value, ref TipInfo  __result)
        {
            if (showBuffEffect.Value)
            {
                Console.WriteLine("WGAbilityInfoPatch_CreateBattleAttributesTipinfo");
                string title = string.Empty;
                title = prop.GetType().GetField(prop.ToString()).GetCustomAttribute<Heluo.DisplayNameAttribute>().Name;
                //title = Game.Data.Get<StringTable>("BattleLiberatedState_" + prop.ToString()).Text;
                __result = __instance.CreateTipInfo(WGTip.TipType.TitleImportantValue, title, value.ToString());
                return false;
            }
            return true;
        }


        //限制属性
        [HarmonyPrefix, HarmonyPatch(typeof(WGAbilityInfo), "CreateBattleAttributesTipinfo", new Type[] { typeof(BattleRestrictedState), typeof(int) })]
        public static bool WGAbilityInfoPatch_CreateBattleAttributesTipinfo(ref WGAbilityInfo __instance, ref BattleRestrictedState prop, ref int value, ref TipInfo __result)
        {
            if (showBuffEffect.Value)
            {
                Console.WriteLine("WGAbilityInfoPatch_CreateBattleAttributesTipinfo");
                string title = string.Empty;
                title = prop.GetType().GetField(prop.ToString()).GetCustomAttribute<Heluo.DisplayNameAttribute>().Name;
                //title = Game.Data.Get<StringTable>("BattleLiberatedState_" + prop.ToString()).Text;
                __result = __instance.CreateTipInfo(WGTip.TipType.TitleImportantValue, title, value.ToString());
                return false;
            }
            return true;
        }

    }
}