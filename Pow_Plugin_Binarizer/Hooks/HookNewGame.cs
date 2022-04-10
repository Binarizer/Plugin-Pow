using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using BepInEx.Configuration;
using Heluo.UI;
using Heluo.Data;

namespace PathOfWuxia
{
    // 开局设定
    [System.ComponentModel.DisplayName("开局设定")]
    [System.ComponentModel.Description("开局点数设定")]
    public class HookNewGame : IHook
    {
        public void OnRegister(PluginBinarizer plugin)
        {
            newGameAttributePoint = plugin.Config.Bind("开局设定", "增加属性点", 0, "设置开局增加多少属性点");
            newGameTraitPoint = plugin.Config.Bind("开局设定", "增加特性点", 0, "设置开局增加多少特性点");
        }

        static ConfigEntry<int> newGameAttributePoint;
        static ConfigEntry<int> newGameTraitPoint;

        /// <summary>
        /// 纪录开局数据
        /// </summary>
        private static Dictionary<CharacterUpgradableProperty, int> newAttributeValues;  // +
        private static int dicePoint;	// +
        [HarmonyPostfix, HarmonyPatch(typeof(CtrlRegistration), "CreateNewPlayer")]
        public static void StartPatch_CreateNewPlayer(CtrlRegistration __instance)
        {
            var data = Traverse.Create(__instance).Field("player_info_data").GetValue<CharacterInfoData>();
            newAttributeValues = new Dictionary<CharacterUpgradableProperty, int>
            {
                {
                    CharacterUpgradableProperty.Str,
                    data.UpgradeableProperty[CharacterUpgradableProperty.Str].Level
                },
                {
                    CharacterUpgradableProperty.Vit,
                    data.UpgradeableProperty[CharacterUpgradableProperty.Vit].Level
                },
                {
                    CharacterUpgradableProperty.Dex,
                    data.UpgradeableProperty[CharacterUpgradableProperty.Dex].Level
                },
                {
                    CharacterUpgradableProperty.Spi,
                    data.UpgradeableProperty[CharacterUpgradableProperty.Spi].Level
                }
            };

            var Tpoint = Traverse.Create(__instance).Field("point");
            int attrPoint = Tpoint.GetValue<int>();
            dicePoint = newGameAttributePoint.Value + attrPoint;
            Tpoint.SetValue(dicePoint);

            var Tpoint2 = Traverse.Create(__instance).Field("traitPoint");
            int traitPoint = Tpoint2.GetValue<int>();
            Tpoint2.SetValue(newGameTraitPoint.Value + traitPoint);
        }

        // 3 新随机方法
        static void UpdateAttributes(CtrlRegistration instance)
        {
            var data = Traverse.Create(instance).Field("player_info_data").GetValue<CharacterInfoData>();
            var Tpoint = Traverse.Create(instance).Field("point");
            FourAttributesInfo fourAttributesInfo = new FourAttributesInfo
            {
                Str = data.GetUpgradeableProperty(CharacterUpgradableProperty.Str).ToString(),
                Vit = data.GetUpgradeableProperty(CharacterUpgradableProperty.Vit).ToString(),
                Dex = data.GetUpgradeableProperty(CharacterUpgradableProperty.Dex).ToString(),
                Spr = data.GetUpgradeableProperty(CharacterUpgradableProperty.Spi).ToString(),
                Point = Tpoint.GetValue<int>().ToString()
            };
            data.UpgradeProperty(true);
            Traverse.Create(instance).Field("view").GetValue<UIRegistration>().UpdateFourAttributes(fourAttributesInfo);
        }
        [HarmonyPrefix, HarmonyPatch(typeof(CtrlRegistration), "DiceValue")]
        public static bool StartPatch_DiceValue(CtrlRegistration __instance)
        {
            var data = Traverse.Create(__instance).Field("player_info_data").GetValue<CharacterInfoData>();
            List<UpgradeableProperty> list = new List<UpgradeableProperty>();
            foreach (var key in newAttributeValues.Keys)
            {
                data.SetUpgradeablePropertyLevel(key, newAttributeValues[key]);
                list.Add(data.UpgradeableProperty[key]);
            }
            List<int> diceVal = new List<int>
            {
                0,
                dicePoint
            };
            for (int i = 0; i < list.Count - 1; i++)
            {
                diceVal.Add(UnityEngine.Random.Range(0, dicePoint));
            }
            diceVal.Sort();
            for (int j = 0; j < list.Count; j++)
            {
                list[j].Level += diceVal[j + 1] - diceVal[j];
            }

            Traverse.Create(__instance).Field("point").SetValue(0);
            UpdateAttributes(__instance);
            return false;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(CtrlRegistration), "set_character_upgradable_property", new Type[] { typeof(CharacterUpgradableProperty), typeof(int) })]
        public static bool StartPatch_SetProperty(CtrlRegistration __instance, CharacterUpgradableProperty property, int value)
        {
            var Tpoint = Traverse.Create(__instance).Field("point");
            int num = Tpoint.GetValue<int>() - value;
            if (num < 0)
            {
                return false;
            }
            var data = Traverse.Create(__instance).Field("player_info_data").GetValue<CharacterInfoData>();
            int num2 = data.GetUpgradeablePropertyLevel(property);
            num2 = (int)Mathf.Lerp((float)num2, (float)(num2 + value), 1f);
            if (num2 < newAttributeValues[property])
            {
                return false;
            }
            data.SetUpgradeablePropertyLevel(property, num2);
            Tpoint.SetValue(num);
            UpdateAttributes(__instance);
            return false;
        }
    }
}
