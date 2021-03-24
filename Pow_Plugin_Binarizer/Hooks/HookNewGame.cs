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
    // 开局设定
    public class HookNewGame : IHook
    {
        public void OnRegister(BaseUnityPlugin plugin)
        {
            newGameAttributePoint = plugin.Config.Bind("开局设定", "属性点", 50, "设置开局属性点");
            newGameTraitPoint = plugin.Config.Bind("开局设定", "特性点", 1, "设置开局特性点");
            newGameExteriorId = plugin.Config.Bind("开局设定", "主角建模", string.Empty, "设定新开局时的主角建模数据源，请通过CharacterExterior表格查找，使用第一列ID");
            newGamePortraitOverride = plugin.Config.Bind("开局设定", "主角头像", string.Empty, "若已设置建模，则可为空，使用建模的头像，否则用此头像代替");
        }

        public void OnUpdate()
        {
        }

        static ConfigEntry<int> newGameAttributePoint;
        static ConfigEntry<int> newGameTraitPoint;
        static ConfigEntry<string> newGameExteriorId;
        static ConfigEntry<string> newGamePortraitOverride;

        // 1 可选多个特性
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(CtrlRegistration), "OnTraitClick")]
        public static IEnumerable<CodeInstruction> StartPatch_UnlockTraitCount(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            codes[47].opcode = OpCodes.Ldc_I4_M1;   // 原本为Ldc_I4_2
            return codes.AsEnumerable();
        }

        // 2 纪录开局数据
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
            dicePoint = newGameAttributePoint.Value + attrPoint - 50;
            Tpoint.SetValue(dicePoint);

            var Tpoint2 = Traverse.Create(__instance).Field("traitPoint");
            int traitPoint = Tpoint2.GetValue<int>();
            Tpoint2.SetValue(newGameTraitPoint.Value + traitPoint - 1);
        }

        // 3 新随机方法
        static void updateAttributes(CtrlRegistration instance)
        {
            var data = Traverse.Create(instance).Field("player_info_data").GetValue<CharacterInfoData>();
            var Tpoint = Traverse.Create(instance).Field("point");
            FourAttributesInfo fourAttributesInfo = new FourAttributesInfo();
            fourAttributesInfo.Str = data.GetUpgradeableProperty(CharacterUpgradableProperty.Str).ToString();
            fourAttributesInfo.Vit = data.GetUpgradeableProperty(CharacterUpgradableProperty.Vit).ToString();
            fourAttributesInfo.Dex = data.GetUpgradeableProperty(CharacterUpgradableProperty.Dex).ToString();
            fourAttributesInfo.Spr = data.GetUpgradeableProperty(CharacterUpgradableProperty.Spi).ToString();
            fourAttributesInfo.Point = Tpoint.GetValue<int>().ToString();
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
            updateAttributes(__instance);
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
            updateAttributes(__instance);
            return false;
        }

        // 4 头像模型替换
        [HarmonyPostfix, HarmonyPatch(typeof(CtrlRegistration), "InitialRewards")]
        public static void StartPatch_SetPlayerModel(CtrlRegistration __instance)
        {
            CharacterExteriorData playerExterior = Traverse.Create(__instance).Field("player_extreior").GetValue<CharacterExteriorData>();
            if (playerExterior != null && newGameExteriorId.Value != string.Empty)
            {
                CharacterExterior characterExterior = Game.Data.Get<CharacterExterior>(newGameExteriorId.Value);
                if (characterExterior != null)
                {
                    string s = (playerExterior.Gender == Gender.Male) ? "in0101" : "in0115";
                    CharacterExterior characterExterior2 = Game.Data.Get<CharacterExterior>(s);
                    characterExterior.Model = characterExterior.Model;
                    characterExterior.Gender = characterExterior.Gender;
                    characterExterior.Size = characterExterior.Size;

                    playerExterior.Model = characterExterior.Model;
                    playerExterior.Gender = characterExterior.Gender;
                    playerExterior.Size = characterExterior.Size;
                    playerExterior.Protrait = newGamePortraitOverride.Value.IsNullOrEmpty() ? characterExterior.Protrait : newGamePortraitOverride.Value;
                }
            }
        }
        [HarmonyPostfix, HarmonyPatch(typeof(ChangeCharacterProtraitAndModel), "GetValue")]
        public static void StartPatch_SetPlayerModel2(ChangeCharacterProtraitAndModel __instance, bool __result)
        {
            if (__instance.id == GameConfig.Player && __result)
            {
                CharacterExteriorData playerExterior = Game.GameData.Exterior[GameConfig.Player];
                if (newGameExteriorId.Value != string.Empty)
                {
                    CharacterExterior characterExterior = Game.Data.Get<CharacterExterior>(newGameExteriorId.Value);
                    if (characterExterior != null)
                    {
                        playerExterior.Model = characterExterior.Model;
                        playerExterior.Gender = characterExterior.Gender;
                        playerExterior.Size = characterExterior.Size;
                        playerExterior.Protrait = characterExterior.Protrait;
                    }
                }
                if (newGamePortraitOverride.Value != string.Empty)
                {
                    playerExterior.Protrait = newGamePortraitOverride.Value;
                }
            }
        }
        [HarmonyPostfix, HarmonyPatch(typeof(ChangeCharacterIdentity), "GetValue")]
        public static void StartPatch_SetPlayerModel3(ChangeCharacterIdentity __instance, bool __result)
        {
            if (__instance.id == GameConfig.Player && __result)
            {
                CharacterExteriorData playerExterior = Game.GameData.Exterior[GameConfig.Player];
                if (newGameExteriorId.Value != string.Empty)
                {
                    CharacterExterior characterExterior = Game.Data.Get<CharacterExterior>(newGameExteriorId.Value);
                    if (characterExterior != null)
                    {
                        playerExterior.Protrait = characterExterior.Protrait;
                    }
                }
                if (newGamePortraitOverride.Value != string.Empty)
                {
                    playerExterior.Protrait = newGamePortraitOverride.Value;
                }
            }
        }

        // 5 防止人物模型动作出现问题
        [HarmonyPrefix, HarmonyPatch(typeof(Heluo.Actor.ActorController), "OverrideStand", new Type[] { typeof(string) })]
        public static bool StartPatch_SetPlayerModel4(Heluo.Actor.ActorController __instance, ref string clipName)
        {
            AnimationClip animationClip = __instance.GetAnimationClip(clipName);
            if (animationClip == null)
            {
                int index = clipName.IndexOf("_special_await");
                if (index >= 0)
                {
                    Console.WriteLine("OverrideStand = " + clipName);
                    string ModelName = clipName.Substring(0, index);
                    var collection = from ce in Game.Data.Get<CharacterExterior>().Values where ce.Model == ModelName select ce;
                    if (collection.Count() > 0)
                    {
                        var characterExterior = collection.First();
                        if (!characterExterior.AnimMapId.IsNullOrEmpty())
                            clipName = string.Format("{0}_special_await{1:00}", characterExterior.AnimMapId, 0);
                        animationClip = __instance.GetAnimationClip(clipName);
                        if (animationClip == null)
                            clipName = characterExterior.Gender == Gender.Male ? "in0101_special_await00" : "in0115_special_await00";
                    }
                }
            }
            return true;
        }
    }
}
