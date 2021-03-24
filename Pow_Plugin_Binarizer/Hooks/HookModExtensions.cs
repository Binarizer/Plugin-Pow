using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using BepInEx;
using Heluo;
using Heluo.UI;
using Heluo.Data;
using Heluo.Data.Converter;
using Heluo.Flow;
using Heluo.Battle;
using Heluo.Resource;
using Heluo.Utility;

namespace PathOfWuxia
{
    // Mod扩展
    public class HookModExtensions : IHook
    {
        public void OnRegister(BaseUnityPlugin plugin)
        {
        }

        public void OnUpdate()
        {
        }

        // 1 多重召唤
        [HarmonyPostfix, HarmonyPatch(typeof(WuxiaBattleManager), "InitBattle", new Type[] { typeof(Heluo.FSM.Battle.BattleStateMachine), typeof(string), typeof(IDataProvider), typeof(IResourceProvider), typeof(Action<BillboardArg>) })]
        public static void ModExt_InitBattle(WuxiaBattleManager __instance, IDataProvider data, IResourceProvider resource)
        {
            // 整体替换 SummonProcessStrategy 类
            Traverse.Create(__instance).Field("summonProcess").SetValue(new DualSummonProcessStrategy(__instance, data, resource));
        }

        // 2 休息时Buff回调
        [HarmonyPostfix, HarmonyPatch(typeof(WuxiaUnit), "ReCover")]
        public static void ModExt_BuffOnRecover(WuxiaUnit __instance)
        {
            __instance.OnBufferEvent((BufferTiming)125);	// 125 = OnRest，休息时
        }

        // 3 秘籍类、奖励类物品扩展
        [HarmonyPostfix, HarmonyPatch(typeof(CustomEffectConverter<PropsEffectType, PropsEffect>), "CreateCustomEffect", new Type[] { typeof(string[]) })]
        public static void ModExt_NewPropsEffect(string[] from, ref object __result)
        {
            if (__result == null)
            {
                try
                {
                    PropsEffectType_Ext type = (PropsEffectType_Ext)(Enum.Parse(typeof(PropsEffectType_Ext), from[0].Trim()));
                    switch (type)
                    {
                        case PropsEffectType_Ext.LearnSkill:
                            __result = new PropsLearnSkill(from[1].Trim());
                            break;
                        case PropsEffectType_Ext.LearnMantra:
                            __result = new PropsLearnMantra(from[1].Trim());
                            break;
                        case PropsEffectType_Ext.Reward:
                            __result = new PropsReward(from[1].Trim());
                            break;
                        case PropsEffectType_Ext.Talk:
                            __result = new PropsTalk(from[1].Trim());
                            break;
                        default:
                            break;
                    }
                }
                catch (Exception)
                {
                }
            }
        }
        public static CharacterMapping UICharacterMapping()
        {
            try
            {
                var uiHome = Game.UI.Get<UIHome>();
                var ctrlHome = Traverse.Create(uiHome).Field("controller").GetValue<CtrlHome>();
                var cml = Traverse.Create(ctrlHome).Field("characterMapping").GetValue<List<CharacterMapping>>();
                if (cml.Count == 0)
                    ctrlHome.OnShow();
                var ci = Traverse.Create(ctrlHome).Field("communityIndex").GetValue<int>();
                if (ci >= cml.Count)
                    Traverse.Create(ctrlHome).Field("communityIndex").SetValue(cml.Count - 1);
                return cml[ci];
            }
            catch (Exception)
            {
                return null;
            }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(CtrlMedicine), "OnShow")]
        public static bool ModExt_NewPropsUI(CtrlMedicine __instance)
        {
            var mapping = Traverse.Create(__instance).Field("mapping");
            var sort = Traverse.Create(__instance).Field("sort").GetValue<List<PropsInfo>>();
            mapping.SetValue(UICharacterMapping());
            sort.Clear();
            foreach (KeyValuePair<string, InventoryData> keyValuePair in Game.GameData.Inventory)
            {
                string key = keyValuePair.Key;
                Props props = Game.Data.Get<Props>(key);
                if (props != null && props.PropsType == PropsType.Medicine)
                {
                    bool flag = false;
                    if (props.CanUseID != null)
                    {
                        if (props.CanUseID.Count == 0)
                        {
                            flag = true;
                        }
                        else
                        {
                            for (int i = 0; i < props.CanUseID.Count; i++)
                            {
                                string text = props.CanUseID[i];
                                if (!text.IsNullOrEmpty() && text == mapping.GetValue<CharacterMapping>().Id)
                                {
                                    flag = true;
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        flag = true;
                    }
                    if (flag)
                    {
                        PropsInfo propsInfo = new PropsInfo(key);
                        propsInfo.ConditionStatus = ((props.UseTime == PropsUseTime.Battle) ? PropsInfo.PropsConditionStatus.UseTimeFail_Battle : PropsInfo.PropsConditionStatus.AllPass);
                        sort.Add(propsInfo);
                    }
                }
            }
            var view = Traverse.Create(__instance).Field("view").GetValue<UIMedicine>();
            view.UpdateProps(sort.Count);
            if (sort.Count <= 0)
            {
                view.UpdatePropsIntroduction(null);
            }
            return false;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(CtrlMedicine), "OnCharacterChanged", new Type[] { typeof(CharacterMapping) })]
        static bool ModExt_NewPropsUI2(CtrlMedicine __instance, CharacterMapping mapping)
        {
            var map = Traverse.Create(__instance).Field("mapping");
            if (mapping != map.GetValue<CharacterMapping>())
            {
                map.SetValue(mapping);
                __instance.OnShow();
            }
            return false;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(CtrlHome), "ChangeCharacter", new Type[] { typeof(int) })]
        static bool ModExt_NewPropsUI3(CtrlHome __instance, ref int index)
        {
            if (index < 0 || index > Traverse.Create(__instance).Field("characterMapping").GetValue<List<CharacterMapping>>().Count)
            {
                index = 0;
            }
            return true;
        }

        // 4 战场掉落物品扩展
        [HarmonyPrefix, HarmonyPatch(typeof(BattleArea), "AddDropProps")]
        static bool ModExt_DropProps()
        {
            //函数内容统统删掉，和UI统一放在一起
            return false;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(BattleDropPropListConverter), "StringToField", new Type[] { typeof(string) })]
        static bool ModExt_DropPropsConverter1(BattleDropPropListConverter __instance, string from, ref object __result)
        {
            var list = new List<BattleDropProp>();
            var converter = Traverse.Create(__instance).Field("converter").GetValue<BattleDropPropConverter>();
            // 把愚蠢的改*换掉，正则表达式要用到
            foreach (string from2 in from.Substring(1, from.Length - 2).Split(new string[] { ")(" }, StringSplitOptions.None))
            {
                var battleDropProp = (BattleDropProp)converter.StringToField(from2);
                if (battleDropProp != null)
                {
                    list.Add(battleDropProp);
                }
            }
            __result = list as object;
            return false;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(BattleDropPropConverter), "CreateBattleDropProp", new Type[] { typeof(string[]) })]
        static bool ModExt_DropPropsConverter2(ref object __result, string[] from)
        {
            // 改成Ext版
            var result = new BattleDropProp_Ext();
            try
            {
                result.Id = from[0].Trim();
                if (from.Length > 1)
                {
                    result.Amount = int.Parse(from[1].Trim());
                }
                if (from.Length > 2)
                {
                    result.Rate = float.Parse(from[2].Trim());
                }
                __result = result as object;
            }
            catch (Exception)
            {
                __result = null;
            }
            return false;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(BattleDropPropConverter), "FieldToString", new Type[] { typeof(object) })]
        static bool ModExt_DropPropsConverter3(ref string __result, object from)
        {
            // 改成Ext版
            var battleDropProp = (BattleDropProp_Ext)from;
            if (battleDropProp != null)
            {
                __result = battleDropProp.ToText();
            }
            else
            {
                __result = "";
            }
            return false;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(WGRewardList), "SetReward", new Type[] { typeof(List<BattleDropProp>) })]
        static bool ModExt_DropProps_UI(WGRewardList __instance, List<BattleDropProp> list)
        {
            int i = 0;
            for (int j = 0; j < __instance.PropsList.Count; j++)
            {
                WGProps wgprops = __instance.PropsList[j];
                if (list == null || i >= list.Count)
                {
                    wgprops.gameObject.SetActive(false);
                }
                else
                {
                    BattleDropProp_Ext battleDropProp = list[i] as BattleDropProp_Ext;  // 改成Ext版
                    if (UnityEngine.Random.value <= battleDropProp.Rate)
                    {
                        if (battleDropProp.Id == "Money")
                        {
                            Sprite icon = Game.Resource.Load<Sprite>(string.Format(GameConfig.PropsCategoryPath, 0));
                            // 这里稍微加强下打工天赋，战斗的金钱也增加
                            int value = (int)(battleDropProp.Amount * Game.GameData.Character[GameConfig.Player].Trait.GetTraitEffect(TraitEffectType.WorkMoney));
                            string amount = value.ToString();
                            string text = Game.Data.Get<StringTable>("NurturanceProperty_Money").Text;
                            wgprops.SetProps(icon, amount, text);
                            Game.GameData.Money += value;
                        }
                        else if (battleDropProp.Id.StartsWith("re"))
                        {
                            j--;
                            Reward r = Game.Data.Get<Reward>(battleDropProp.Id);
                            if (r != null)
                            {
                                r.GetValue();
                            }
                        }
                        // todo: 独特物品掉落
                        //else
                        //{
                        //}
                        else
                        {
                            Props props = Randomizer.GetOneFromData<Props>(battleDropProp.Id);      // 随机掉落接口
                            if (props != null)
                            {
                                Sprite icon2 = Game.Resource.Load<Sprite>(string.Format(GameConfig.PropsCategoryPath, (int)props.PropsCategory));
                                string amount2 = battleDropProp.Amount.ToString();
                                string name = props.Name;
                                wgprops.SetProps(icon2, amount2, name);
                                Game.GameData.Inventory.Add(props.Id, battleDropProp.Amount, true);
                            }
                        }
                    }
                    else
                    {
                        j--;
                    }
                }
                i++;
            }
            return false;
        }

        // 5 添加默认衍生的一堆物品
        [HarmonyPostfix, HarmonyPatch(typeof(DataManager), "Inital")]
        static void ModExt_NewProps_DefaultData(DataManager __instance)
        {
            Dictionary<string, Props> dictionary = __instance.Get<Props>();
            Dictionary<string, Skill> dictionary4 = __instance.Get<Skill>();
            Dictionary<string, Mantra> dictionary2 = __instance.Get<Mantra>();
            // 添加武功秘籍
            foreach (Skill skill in dictionary4.Values)
            {
                Props props = new Props
                {
                    Id = "p_scroll_" + skill.Id,
                    Description = skill.Description,
                    PropsType = PropsType.Medicine,
                    PropsCategory = skill.Type - 101 + 401,
                    Name = skill.Name + "秘籍",
                    PropsEffect = new List<PropsEffect>
                        {
                            new PropsLearnSkill(skill.Id)
                        },
                    PropsEffectDescription = "学会招式：" + skill.Name
                };
                dictionary.Add(props.Id, props);
            }
            // 添加心法秘籍
            foreach (Mantra mantra in dictionary2.Values)
            {
                Props props2 = new Props
                {
                    Id = "p_scroll_" + mantra.Id,
                    Description = mantra.Description,
                    PropsType = PropsType.Medicine,
                    PropsCategory = PropsCategory.InternalStyle_Secret_Scroll,
                    Name = mantra.Name + "秘籍",
                    PropsEffect = new List<PropsEffect>
                        {
                            new PropsLearnMantra(mantra.Id)
                        },
                    PropsEffectDescription = "学会心法：" + mantra.Name
                };
                dictionary.Add(props2.Id, props2);
            }
            // 添加人物加入道具
            Dictionary<string, Npc> dictionary5 = __instance.Get<Npc>();
            Dictionary<string, Reward> dictionary3 = __instance.Get<Reward>();
            foreach (Npc npc in dictionary5.Values)
            {
                Props props3 = new Props
                {
                    Id = "p_npcj_" + npc.Id,
                    Description = npc.Name + "加入",
                    PropsType = PropsType.Medicine,
                    PropsCategory = PropsCategory.Other_Secret_Scroll,
                    Name = npc.Name + "加入",
                    PropsEffect = new List<PropsEffect>
                        {
                            new PropsReward("re_npcj_" + npc.Id)
                        },
                    PropsEffectDescription = "加入队友：" + npc.Name
                };
                dictionary.Add(props3.Id, props3);
                string s = "{\"LogicalNode\":[{\"CommunityAction\":\"" + npc.Id + "\",True}],0}";
                Reward reward = new Reward
                {
                    Id = "re_npcj_" + npc.Id,
                    Description = npc.Name + "加入",
                    IsShowMessage = true,
                    Rewards = new BaseFlowGraph(OutputNodeConvert.Deserialize(s))
                };
                dictionary3.Add(reward.Id, reward);
            }
        }

        // 6 战场布置扩展
        [HarmonyPrefix, HarmonyPatch(typeof(WuxiaBattleManager), "AddUnit", new Type[] { typeof(string), typeof(Faction), typeof(int), typeof(bool) })]
        static bool ModExt_AddUnitOnBattleGround(WuxiaBattleManager __instance, string unitid, int tileNumber, Faction faction, bool isParty, ref WuxiaUnit __result)
        {
            WuxiaUnit result = null;
            int times = 0;
            while (result == null && times < 10)
            {
                int tile = tileNumber;
                AddUnitHelper.ProcessCellNumber(__instance, ref tile);
                try
                {
                    WuxiaUnit wuxiaUnit = __instance.UnitGenerator.CreateUnit(unitid, faction, tile, isParty);
                    wuxiaUnit.UnitDestroyed += __instance.OnUnitDestroyed;
                    result = wuxiaUnit;
                }
                catch (Exception e)
                {
                    Debug.LogError(string.Format("AddUnit失败： id={0} faction={1} tile={2} isparty={3} error={4}，再次尝试", new object[]
                    {
                        unitid,
                        faction,
                        tile,
                        isParty,
                        e.ToString()
                    }));
                    times++;
                    result = null;
                }
            }
            if (result == null)
            {
                UnityEngine.Debug.LogError("尝试10次无果，请彻查地图格子设置");
            }
            __result = result;
            return false;
        }

        // 7 随机物品
        [HarmonyPrefix, HarmonyPatch(typeof(ActionListener), "GetTypeByText", new Type[] { typeof(string) })]
        static bool ModExt_AddActions(ActionListener __instance, string s, ref Type __result)
        {
            if (s == "RewardRandomProps" || s == "\"RewardRandomProps\"")
            {
                __result = typeof(RewardRandomProps);
                return false;
            }
            return true;
        }
    }
}
