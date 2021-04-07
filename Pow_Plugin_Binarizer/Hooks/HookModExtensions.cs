using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
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
            // 7 添加扩展GameAction
            Assembly assembly = Assembly.GetExecutingAssembly();
            modActionTypes = (from t in assembly.GetTypes() where t.IsSubclassOf(typeof(OutputNode)) select t).ToList();
            Console.WriteLine("Mod添加OutputNode:");
            foreach (var t in modActionTypes)
            {
                Console.WriteLine(t.Name.ToString());
            }
        }

        public void OnUpdate()
        {
        }

        static List<Type> modActionTypes;
        static List<BattleDropProp> ExtDrops = new List<BattleDropProp>();

        // 1 多重召唤
        [HarmonyPostfix, HarmonyPatch(typeof(WuxiaBattleManager), "InitBattle", new Type[] { typeof(Heluo.FSM.Battle.BattleStateMachine), typeof(string), typeof(IDataProvider), typeof(IResourceProvider), typeof(Action<BillboardArg>) })]
        public static void ModExt_InitBattle(WuxiaBattleManager __instance, IDataProvider data, IResourceProvider resource)
        {
            // 整体替换 SummonProcessStrategy 类
            Traverse.Create(__instance).Field("summonProcess").SetValue(new ModSummonProcessStrategy(__instance, data, resource));
            // 清空自定义战场奖励
            ExtDrops.Clear();
        }

        // 2 休息时Buff回调
        [HarmonyPostfix, HarmonyPatch(typeof(WuxiaUnit), "ReCover")]
        public static void ModExt_BuffOnRecover(WuxiaUnit __instance)
        {
            __instance.OnBufferEvent((BufferTiming)125);	// 125 = OnRest，休息时
        }

        // 3 秘籍类、奖励类物品扩展
        [HarmonyPrefix, HarmonyPatch(typeof(CustomEffectConverter<PropsEffectType, PropsEffect>), "CreateCustomEffect", new Type[] { typeof(string[]) })]
        public static bool ModExt_NewPropsEffect(string[] from, ref object __result)
        {
            // 使用包含扩展的方法读取
            __result = PropsEffectFormatter.Instance.Create(from);
            return false;
        }
        public static CharacterMapping GetUICharacterMapping()
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
            mapping.SetValue(GetUICharacterMapping());
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
                        PropsInfo propsInfo = new PropsInfo(key)
                        {
                            ConditionStatus = ((props.UseTime == PropsUseTime.Battle) ? PropsInfo.PropsConditionStatus.UseTimeFail_Battle : PropsInfo.PropsConditionStatus.AllPass)
                        };
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

        // 4 添加默认衍生的一堆物品（战场掉落用）
        [HarmonyPostfix, HarmonyPatch(typeof(DataManager), "Inital")]
        static void ModExt_NewProps_DefaultData(DataManager __instance)
        {
            var dict = Traverse.Create(__instance).Field("dict").GetValue<IDictionary<Type, IDictionary>>();
            IDictionary dictionary = dict[typeof(Props)];
            IDictionary dictionary4 = dict[typeof(Skill)];
            IDictionary dictionary2 = dict[typeof(Mantra)];
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
            IDictionary dictionary5 = dict[typeof(Npc)];
            IDictionary dictionary3 = dict[typeof(Reward)];
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
        [HarmonyPostfix, HarmonyPatch(typeof(WuxiaUnit), "ProcessDropProps")]
        static void ModExt_BattleGroundDrop(WuxiaUnit __instance)
        {
            List<string> drops = new List<string>();
            // a 人物加入道具
            if (!Game.GameData.Community.ContainsKey(__instance.UnitID))
                drops.Add("p_npcj_" + __instance.UnitID);
            // b 装备
            Props equip = __instance.info.Equip.GetEquip(EquipType.Weapon);
            if (equip != null)
                drops.Add(equip.Id);
            Props equip2 = __instance.info.Equip.GetEquip(EquipType.Cloth);
            if (equip2 != null)
                drops.Add(equip2.Id);
            Props equip3 = __instance.info.Equip.GetEquip(EquipType.Jewelry);
            if (equip3 != null)
                drops.Add(equip3.Id);
            // c 内功
            if (__instance.CurrentMantra != null)
                drops.Add("p_scroll_" + __instance.CurrentMantra.Id);
            // d 招式
            if (__instance.LearnedSkills != null)
            {
                foreach (var skill in __instance.LearnedSkills.Values)
                {
                    drops.Add("p_scroll_" + skill.Id);
                }
            }

            int count = UnityEngine.Random.Range(1, 5);
            for (int i = 0; i < count; ++i) 
            {
                ExtDrops.Add(new BattleDropProp_Ext()
                {
                    Id = drops.Random(),
                    Amount = 1,
                    Rate = 0.1f
                });
            }          
        }

        // 5 战场掉落物品扩展
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
            int uid = 0;
            var newlist = new List<BattleDropProp>(list);
            for (int i = 0; i < newlist.Count; i++)
            {
                BattleDropProp_Ext battleDropProp = newlist[i] as BattleDropProp_Ext;  // 改成Ext版
                if (battleDropProp.Id == "Money")
                {
                    Sprite icon = Game.Resource.Load<Sprite>(string.Format(GameConfig.PropsCategoryPath, 0));
                    // 这里稍微加强下打工天赋，战斗的金钱也增加
                    int value = (int)(battleDropProp.Amount * Game.GameData.Character[GameConfig.Player].Trait.GetTraitEffect(TraitEffectType.WorkMoney));
                    WGProps wgprops = __instance.PropsList[uid++];
                    wgprops.SetProps(icon, value.ToString(), Game.Data.Get<StringTable>("NurturanceProperty_Money").Text);
                    Game.GameData.Money += value;
                    if (uid > 9) break;    // 只有10个格子
                }
                else if (battleDropProp.Id.StartsWith("re"))
                {
                    Reward r = Game.Data.Get<Reward>(battleDropProp.Id) ?? Randomizer.GetOneFromData<Reward>(battleDropProp.Id);
                    if (r != null)
                    {
                        r.GetValue();
                    }
                }
                else if(battleDropProp.Id == "ExtDrop")
                {
                    newlist.AddRange(ExtDrops);   // 将战场掉落物也加进去
                }
                else
                {
                    Props props = Game.Data.Get<Props>(battleDropProp.Id) ?? Randomizer.GetOneFromData<Props>(battleDropProp.Id);
                    if (props != null)
                    {
                        int dropCount = 0;
                        for (int k = 0; k < battleDropProp.Amount; ++k)
                        {
                            if (UnityEngine.Random.value <= battleDropProp.Rate)
                                ++dropCount;
                        }
                        if (dropCount > 0)
                        {
                            Sprite icon = Game.Resource.Load<Sprite>(string.Format(GameConfig.PropsCategoryPath, (int)props.PropsCategory));
                            WGProps wgprops = __instance.PropsList[uid++];
                            wgprops.SetProps(icon, dropCount.ToString(), props.Name);
                            Game.GameData.Inventory.Add(props.Id, dropCount, true);
                            if (uid > 9) break;    // 只有10个格子
                        }
                    }
                }
            }
            for (; uid < 10; ++uid)
            {
                WGProps wgprops = __instance.PropsList[uid];
                wgprops.gameObject.SetActive(false);
            }
            return false;
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

        // 7 GameAction扩展
        [HarmonyPostfix, HarmonyPatch(typeof(ActionListener), "GetTypeByText", new Type[] { typeof(string) })]
        static void ModExt_AddActions(ActionListener __instance, string s, ref Type __result)
        {
            if (__result == null)
            {
                if (s.Length > 0)
                {
                    if (s[0] == '"')
                    {
                        s = s.Trim(new char[]{'"'});
                    }
                    Console.WriteLine("尝试解析扩展类型 = " + s);
                    Type action = modActionTypes.Find((Type item) => item.Name == s);
                    if (action != null)
                    {
                        Console.WriteLine("解析扩展类型成功 = " + s);
                        __result = action;
                    }
                }
            }
        }
    }
}
