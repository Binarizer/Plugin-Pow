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
using Heluo.Battle;
using Heluo.FSM.Battle;
using System.Reflection.Emit;

namespace PathOfWuxia
{
    // 战斗中获得招式经验、招式等级自定义
    public class HookSkillExp : IHook
    {
        public void OnRegister(BaseUnityPlugin plugin)
        {
            skillExpInBattle = plugin.Config.Bind("扩展功能", "开关战斗招式经验", false, "开关战斗经验获取");
            skillExpRate = plugin.Config.Bind("扩展功能", "战斗招式经验倍率", 0.04f, "获取招式经验的倍率");
            skillMaxLevel = plugin.Config.Bind("扩展功能", "招式等级上限", 10, "招式等级上限");
        }

        public void OnUpdate()
        {
        }

        private static ConfigEntry<bool> skillExpInBattle;
        private static ConfigEntry<float> skillExpRate;
        private static ConfigEntry<int> skillMaxLevel;

        // 升级函数提取
        public static int CalculateNurturanceExp(CharacterInfoData data, CharacterUpgradablePropertyTable _property, int require_status)
        {
            int player_status = 4000;
            if (_property <= CharacterUpgradablePropertyTable.Vit)
            {
                if (_property == CharacterUpgradablePropertyTable.Str)
                {
                    player_status = data.GetUpgradeableProperty(CharacterUpgradableProperty.Str);
                }
                if (_property == CharacterUpgradablePropertyTable.Vit)
                {
                    player_status = data.GetUpgradeableProperty(CharacterUpgradableProperty.Vit);
                }
            }
            else
            {
                if (_property == CharacterUpgradablePropertyTable.Dex)
                {
                    player_status = data.GetUpgradeableProperty(CharacterUpgradableProperty.Dex);
                }
                if (_property == CharacterUpgradablePropertyTable.Spi)
                {
                    player_status = data.GetUpgradeableProperty(CharacterUpgradableProperty.Spi);
                }
                if (_property == CharacterUpgradablePropertyTable.Sum)
                {
                    player_status = data.GetUpgradeableProperty(CharacterUpgradableProperty.Str) + data.GetUpgradeableProperty(CharacterUpgradableProperty.Vit) + data.GetUpgradeableProperty(CharacterUpgradableProperty.Dex) + data.GetUpgradeableProperty(CharacterUpgradableProperty.Spi);
                }
            }
            return Mathf.Clamp(Game.Data.Get<GameFormula>("skill_exp").EvaluateToInt(new Dictionary<string, int>
            {
                {
                    "player_status",
                    player_status
                },
                {
                    "require_status",
                    require_status
                }
            }), 25, 900);
        }

        // 1 在PlayAbility状态时增加经验
        static void AddExpInBattle(WuxiaUnit unit, string skillId)
        {
            SkillData skillDataLearned = unit.LearnedSkills[skillId];
            if (skillDataLearned.Item.RequireValue > 0 && skillExpRate.Value > 0f)
            {
                int num = CalculateNurturanceExp(unit.info, skillDataLearned.Item.RequireAttribute, skillDataLearned.Item.RequireValue);
                num = Math.Max(1, (int)(num * skillExpRate.Value));
                try
                {
                    unit.info.AddSkillExp(skillId, num);
                }
                catch
                {
                    Console.WriteLine("出招者的Info有问题：" + unit.CharacterInfoId);
                }
                skillDataLearned.AddExp(num);
            }
        }
        // 玩家部分
        [HarmonyPostfix, HarmonyPatch(typeof(UnitPlayAbility), "OnEnable")]
        public static void SkillExpPatch_AddExp(ref UnitPlayAbility __instance)
        {
            if (skillExpInBattle.Value)
            {
                Type t = Traverse.CreateWithType("UnitPlayAbilityEventArgs").GetValue<Type>();
                object args = Traverse.Create(__instance).Field("args").GetValue();
                WuxiaUnit attacker = t.GetField("Attacker").GetValue(args) as WuxiaUnit;
                SkillData skillData = t.GetField("UseSkill").GetValue(args) as SkillData;
                AddExpInBattle(attacker, skillData.Id);
            }
        }
        // AI部分暂时放弃 因为可以很多人公用一套info

        // 2. 用于战斗的技能不标主人，不参与增加属性
        [HarmonyPrefix, HarmonyPatch(typeof(WuxiaUnit), "LearnedSkills", MethodType.Getter)]
        public static bool SkillExpPatch_WuxiaUnit(ref WuxiaUnit __instance, ref CharacterSkillData __result)
        {
            var _learnedskills = Traverse.Create(__instance).Field("_learnedskills").GetValue<CharacterSkillData>();
            if (_learnedskills == null || _learnedskills.Count != __instance.info.Skill.Count)
            {
                _learnedskills = new CharacterSkillData();
                foreach (SkillData skillData in __instance.info.Skill.Values)
                {
                    SkillData skillData2 = new SkillData
                    {
                        // CharacterId = this.info.Id, 这里删除
                        Id = skillData.Id,
                        MaxLevel = skillData.MaxLevel,
                        Level = skillData.Level,
                        Exp = skillData.Exp
                    };
                    _learnedskills.Add(skillData2.Id, skillData2);
                }
            }
            __result = _learnedskills;
            return false;
        }

        // 3. Skill升级时加点跳过没主人的
        [HarmonyPrefix, HarmonyPatch(typeof(SkillData), "OnLevelUp", new Type[] { typeof(int) })]
        public static bool SkillExpPatch_SkillData1(ref SkillData __instance, int level)
        {
            if (__instance.Item != null && __instance.Item.Rewards != null && !__instance.CharacterId.IsNullOrEmpty())
            {
                __instance.Item.Rewards.SetVariable("SkillLevel", level);
                __instance.Item.Rewards.SetVariable("CharacterId", __instance.CharacterId);
                __instance.Item.Rewards.GetValue();
            }
            if (__instance.Level > 9 && __instance.MaxLevel < skillMaxLevel.Value)
            {
                __instance.MaxLevel = skillMaxLevel.Value;
            }
            return false;
        }

        // 4. Skill最高等级变更
        [HarmonyPrefix, HarmonyPatch(typeof(Upgradeable), "AddExp", new Type[] { typeof(int) })]
        public static bool SkillExpPatch_SkillData2(ref Upgradeable __instance, int count)
        {
            if (__instance is SkillData)
            {
                if (__instance.Level > 9 && __instance.MaxLevel < skillMaxLevel.Value)
                {
                    __instance.MaxLevel = skillMaxLevel.Value;
                }
            }
            return true;
        }

        // 5. Skill升级EXP公式
        [HarmonyPrefix, HarmonyPatch(typeof(SkillData), "GetMaxExpByLevel", new Type[] { typeof(int) })]
        public static bool SkillExpPatch_SkillData3(ref SkillData __instance, int level, ref int __result)
        {
            if (level > 9)
            {
                __result = 100 * (1 << Math.Min(level - 9, 24));
            }
            __result = 100;
            return false;
        }

        // 6. UI 先完全替换，IL注入太麻烦
        [HarmonyPrefix, HarmonyPatch(typeof(WGAbilityInfo), "ShowTip", new Type[] { typeof(SkillData) })]
        public static bool SkillExpPatch_UI(ref WGAbilityInfo __instance, SkillData skill)
        {
            if (skill == null || skill.Item == null)
            {
                return true;
            }
            __instance.gameObject.SetActive(true);
            var infos = Traverse.Create(__instance).Field("infos").GetValue<List<TipInfo>>();
            var unit = Traverse.Create(__instance).Field("unit").GetValue<WuxiaUnit>();
            infos.Clear();
            infos.Add(__instance.CreateTipInfo(WGTip.TipType.VeryBigTitle, skill.Item.Name, ""));
            // +
            if (skill.Item.RequireValue > 0 && skillExpInBattle.Value)
            {
                string title = string.Format("Lv.{0} {1}/{2}", skill.Level, skill.Exp, skillMaxLevel.Value);
                if (skill.Level == skill.MaxLevel)
                {
                    title = "Lv.Max";
                }
                infos.Add(__instance.CreateTipInfo(WGTip.TipType.Title, title, ""));
            }
            string text = Game.Data.Get<StringTable>("SecondaryInterface1401").Text;
            infos.Add(__instance.CreateTipInfo(WGTip.TipType.TitleImportantValue, text, skill.Item.RequestMP.ToString()));
            string text2 = Game.Data.Get<StringTable>("SecondaryInterface1402").Text;
            string value = skill.GetPredictionDamage(unit.GetFormulaProperty(), unit.info, 0, unit).ToString();
            infos.Add(__instance.CreateTipInfo(WGTip.TipType.TitleValue, text2, value));
            string text3 = Game.Data.Get<StringTable>("SecondaryInterface1403").Text;
            string value2 = string.Empty;
            if (skill.Item.MinRange == 0 && skill.Item.MaxRange == 0)
            {
                value2 = Game.Data.Get<StringTable>("SecondaryInterface0706").Text;
            }
            else
            {
                value2 = skill.Item.MinRange + " - " + skill.Item.MaxRange;
            }
            infos.Add(__instance.CreateTipInfo(WGTip.TipType.TitleValue, text3, value2));
            string text4 = string.Empty;
            switch (skill.Item.TargetArea)
            {
                case TargetArea.Line:
                case TargetArea.LineCharge:
                    text4 = Game.Data.Get<StringTable>("SecondaryInterface0702").Text;
                    break;
                case TargetArea.Fan:
                    text4 = Game.Data.Get<StringTable>("SecondaryInterface0703").Text;
                    break;
                case TargetArea.Ring:
                    if (skill.Item.AOE > 0)
                    {
                        text4 = Game.Data.Get<StringTable>("SecondaryInterface0705").Text;
                        text4 = string.Format(text4, skill.Item.AOE);
                    }
                    else
                    {
                        text4 = Game.Data.Get<StringTable>("SecondaryInterface0701").Text;
                    }
                    break;
                case TargetArea.RingGroup:
                    text4 = Game.Data.Get<StringTable>("SecondaryInterface0704").Text;
                    break;
                case TargetArea.LineGroup:
                    text4 = Game.Data.Get<StringTable>("SecondaryInterface0708").Text;
                    break;
            }
            string text5 = Game.Data.Get<StringTable>("SecondaryInterface1404").Text;
            infos.Add(__instance.CreateTipInfo(WGTip.TipType.TitleValue, text5, text4));
            string text6 = Game.Data.Get<StringTable>("SecondaryInterface1405").Text;
            infos.Add(__instance.CreateTipInfo(WGTip.TipType.TitleValue, text6, skill.MaxCD.ToString()));
            string text7 = Game.Data.Get<StringTable>("SecondaryInterface1406").Text;
            infos.Add(__instance.CreateTipInfo(WGTip.TipType.Title, text7, ""));
            infos.Add(__instance.CreateTipInfo(WGTip.TipType.Context, skill.Item.Description, ""));
            Traverse.Create(__instance).Field("ability_tip").GetValue<WGTip>().ShowTip(infos);
            return false;
        }

        // 7. 其他超过10的条件Fix
        [HarmonyPrefix, HarmonyPatch(typeof(CtrlFormMartialArts), "LevelToString", new Type[] { typeof(int) })]
        public static bool SkillExpPatch_BreakTen1(ref CtrlFormMartialArts __instance, ref int level)
        {
            if (level > 10) { level = 10; }
            return true;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(CheckSkillLevel), "GetValue")]
        public static bool SkillExpPatch_BreakTen2(ref CheckSkillLevel __instance, ref bool __result)
        {
            __result = false;
            if (!Application.isPlaying)
                return false;
            string id = Traverse.Create(__instance).Method("GetId").GetValue<string>();
            if (id.IsNullOrEmpty())
                return false;
            CharacterSkillData skill = Game.GameData.Character[id].Skill;
            if (!skill.ContainsKey(__instance.skillid))
                return false;
            SkillData skillData = skill[__instance.skillid];
            int level = (skillData.Level > 10) ? 10 : skillData.Level;
            __result = skillData != null && __instance.Execute(level);
            return false;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(CheckLevelMaxMantrasAndSkills), "GetValue")]
        public static bool SkillExpPatch_BreakTen3(ref CheckLevelMaxMantrasAndSkills __instance, ref bool __result)
        {
            __result = false;
            if (!Application.isPlaying)
                return false;
            string id = Traverse.Create(__instance).Method("GetId").GetValue<string>();
            if (id.IsNullOrEmpty())
                return false;
            CharacterInfoData characterInfoData = Game.GameData.Character[id];
            CharacterMantraData mantra = characterInfoData.Mantra;
            CharacterSkillData skill = characterInfoData.Skill;
            // m: for achevement correct
            int current = (from x in mantra
                            where x.Value.Level >= 10
                            select x).Count() + (from x in skill
                                                where x.Value.Level >= 10
                                                select x).Count();
            __result = __instance.Execute(current);
            return false;
        }
    }
}
