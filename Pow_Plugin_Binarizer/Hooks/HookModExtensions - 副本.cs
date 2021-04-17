using System;
using System.Text;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;
using Heluo;
using Heluo.UI;
using Heluo.Data;
using Heluo.Data.Converter;
using Heluo.Flow;
using Heluo.Battle;
using Heluo.Flow.Battle;
using Heluo.Resource;
using Heluo.Utility;
using Newtonsoft.Json;
using System.IO;
using Heluo.Features;

namespace PathOfWuxia
{
    // Mod辅助扩展
    public class HookModExtensions : IHook
    {
        static private BaseUnityPlugin Plugin = null;

        public void OnRegister(BaseUnityPlugin plugin)
        {
            Plugin = plugin;
            ExtDrop = plugin.Config.Bind("扩展功能", "战场掉落", false, "特定关卡敌方掉落，游玩剑击江湖请勾选");
            var adv = new ConfigDescription("", null, new ConfigurationManagerAttributes { IsAdvanced = true });
            DropRateCharacter = Plugin.Config.Bind("扩展功能", "战场掉落率（人物）", 0.02f, adv);
            DropRateSkillMantra = Plugin.Config.Bind("扩展功能", "战场掉落率（秘籍）", 0.04f, adv);
            DropRateEquip = Plugin.Config.Bind("扩展功能", "战场掉落率（装备）", 0.05f, adv);

            DumpBattleFileKey = plugin.Config.Bind("Debug功能", "Dump战场文件", KeyCode.F10, adv);
            DumpBattleFileName = plugin.Config.Bind("Debug功能", "战场文件路径", "Dump/battlejs_{0}.json", adv);
            DumpMovieFileKey = plugin.Config.Bind("Debug功能", "Dump过场文件", KeyCode.F9, adv);
            DumpMovieFileName = plugin.Config.Bind("Debug功能", "过场文件路径", "Dump/moviejs_{0}.json", adv);
        }
        private static ConfigEntry<bool> ExtDrop;
        private static ConfigEntry<float> DropRateCharacter;
        private static ConfigEntry<float> DropRateSkillMantra;
        private static ConfigEntry<float> DropRateEquip;

        private static ConfigEntry<KeyCode> DumpBattleFileKey;
        private static ConfigEntry<string> DumpBattleFileName;
        private static ConfigEntry<KeyCode> DumpMovieFileKey;
        private static ConfigEntry<string> DumpMovieFileName;
        private static string LastMoviePath = null;
        private static ScheduleGraph.Bundle LastMovieBundle = null;

        static private void BindSubConfig()
        {
        }

        public void OnUpdate()
        {
            if (Input.GetKeyDown(DumpMovieFileKey.Value) && LastMovieBundle != null)
            {
                string path = string.Format(DumpMovieFileName.Value, Path.GetFileNameWithoutExtension(LastMoviePath));
                DumpMovie(LastMovieBundle, path);
            }
            if (Input.GetKeyDown(DumpBattleFileKey.Value) )
            {
                var battleRootNode = Game.BattleStateMachine?.BattleManager?.BattleSchedule?.BattleSchedule?.BattleSchedules?.Output;
                if (battleRootNode != null && battleRootNode as BattleRootNode != null )
                {
                    string path = string.Format(DumpBattleFileName.Value, Game.GameData.BattleID);
                    DumpOutputNode(battleRootNode as BattleRootNode, path);
                }
            }

            // 测试导出
            if (Input.GetKeyDown(KeyCode.T))
            {
                string source = @"G:\Steam\steamapps\common\PathOfWuxia\Mods\JJJH\config\cinematic\m1101101_00_original.json";
                string target = @"G:\Steam\steamapps\common\PathOfWuxia\Dump\movie.json";
                TestMovieConvert(source, target);
            }
        }

        static void TestMovieConvert(string source, string target)
        {
            var settringImport = new JsonSerializerSettings
            {
                Converters = new JsonConverter[]
                {
                    new OutputNodeJsonConverter()
                }
            };
            string original = File.ReadAllText(source);
            Console.WriteLine(original);
            ScheduleGraph.Bundle bundle = JsonConvert.DeserializeObject<ScheduleGraph.Bundle>(original, settringImport);
            Console.WriteLine("bundle="+bundle);

            DumpMovie(bundle, target);
        }

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
        [HarmonyPrefix, HarmonyPatch(typeof(CtrlMedicine), "OnShow")]
        public static bool ModExt_NewPropsUI(CtrlMedicine __instance)
        {
            var mapping = Traverse.Create(__instance).Field("mapping");
            var sort = Traverse.Create(__instance).Field("sort").GetValue<List<PropsInfo>>();
            mapping.SetValue(GlobalLib.GetUICharacterMapping());
            sort.Clear();
            foreach (KeyValuePair<string, InventoryData> keyValuePair in Game.GameData.Inventory)
            {
                string key = keyValuePair.Key;
                Props props = Game.Data.Get<Props>(key);
                if (props != null && props.PropsType == PropsType.Medicine)
                {
                    bool show = false;
                    if (props.CanUseID != null && props.CanUseID.Count > 0)
                    {
                        for (int i = 0; i < props.CanUseID.Count; i++)
                        {
                            string text = props.CanUseID[i];
                            if (!text.IsNullOrEmpty() && text == mapping.GetValue<CharacterMapping>().Id)
                            {
                                show = true;
                                break;
                            }
                        }
                    }
                    else
                    {
                        show = true;
                        if (props.PropsCategory >= PropsCategory.Fist_Secret_Scroll && props.PropsCategory <= PropsCategory.Throw_Secret_Scroll && key.StartsWith("p_scroll"))
                        {
                            foreach (var pe in props.PropsEffect)
                            {
                                if (pe is PropsLearnSkill pls)
                                {
                                    PropsCategory skillType = Game.Data.Get<Skill>(pls.Id).Type;
                                    if (!GlobalLib.HasSkillType(mapping.GetValue<CharacterMapping>().InfoId, skillType))
                                    {
                                        show = false;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    if (show)
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
            // 为每种心法和招式添加对应秘籍
            var DictProps = __instance.Get<Props>(); 
            var DictSkill = __instance.Get<Skill>();
            var DictMantra = __instance.Get<Mantra>();
            foreach (Skill skill in DictSkill.Values)
            {
                Props props = new Props
                {
                    Id = "p_scroll_" + skill.Id,
                    Description = skill.Description,
                    PropsType = PropsType.Medicine,
                    PropsCategory = GlobalLib.GetScrollType(skill.Type),
                    Name = skill.Name + "秘籍",
                    PropsEffect = new List<PropsEffect>
                    {
                        new PropsLearnSkill(skill.Id)
                    },
                    PropsEffectDescription = "学会招式：" + skill.Name
                };
                DictProps.Add(props.Id, props);
            }
            foreach (Mantra mantra in DictMantra.Values)
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
                DictProps.Add(props2.Id, props2);
            }
            // 为每个NPC添加人物加入道具
            var DictNpc = __instance.Get<Npc>();
            var DictReward = __instance.Get<Reward>();
            foreach (Npc npc in DictNpc.Values)
            {
                Props props3 = new Props
                {
                    Id = "p_npcj_" + npc.Id,
                    Description = npc.Name + "加入",
                    PropsType = PropsType.Quest,
                    PropsCategory = PropsCategory.Other_Quest,
                    Name = npc.Name + "加入",
                    PropsEffect = new List<PropsEffect>
                        {
                            new PropsReward("re_npcj_" + npc.Id)
                        },
                    PropsEffectDescription = "加入队友：" + npc.Name
                };
                DictProps.Add(props3.Id, props3);
                string s = "{\"LogicalNode\":[{\"CommunityAction\":\"" + npc.Id + "\",True}],0}";
                Reward reward = new Reward
                {
                    Id = "re_npcj_" + npc.Id,
                    Description = npc.Name + "加入",
                    IsShowMessage = true,
                    Rewards = new BaseFlowGraph(OutputNodeConvert.Deserialize(s))
                };
                DictReward.Add(reward.Id, reward);
            }
        }
        [HarmonyPostfix, HarmonyPatch(typeof(CtrlInventory), "Sort")]
        static void ModExt_CommunityJoin(CtrlInventory __instance)
        {
            var array = Traverse.Create(__instance).Field("sortInv").Field("array").GetValue() as List<PropsInfo>[];
            if (array[2]!= null)
            {
                foreach(PropsInfo pi in array[2])
                {
                    if (pi.Id.StartsWith("p_npcj_"))
                        pi.ConditionStatus = PropsInfo.PropsConditionStatus.AllPass;    // 人物加入道具可直接使用
                }
            }
        }
        [HarmonyPrefix, HarmonyPatch(typeof(CtrlFormInventory), "UseProps")]
        static bool ModExt_CommunityJoin2(CtrlFormInventory __instance)
        {
            if (__instance.GetType() == typeof(CtrlInventory) )
            {
                var t = Traverse.Create(__instance);
                var sort = t.Field("sort").GetValue<List<PropsInfo>>();
                int propsIndex = t.Field("propsIndex").GetValue<int>();
                string pid = sort[propsIndex].Item.Id;
                if (pid.StartsWith("p_npcj_"))
                {
                    // 在Inventory菜单使用人物加入道具
                    Game.GameData.Inventory.UseProps(pid, Game.GameData.Character["Player"]);
                    t.Method("Sort").GetValue();
                    t.Method("UpdateInventory", 2).GetValue();
                    Traverse.Create(Game.UI.Get<UIHome>()).Property("controller").Method("OnShow").GetValue();
                }
                return false;
            }
            return true;
        }
        [HarmonyPostfix, HarmonyPatch(typeof(WuxiaUnit), "ProcessDropProps")]
        static void ModExt_BattleGroundDrop(WuxiaUnit __instance)
        {
            if (__instance.faction != Faction.Enemy)
                return;

            // a 人物加入道具
            if (!Game.GameData.Community.ContainsKey(__instance.UnitID))
            {
                ExtDrops.Add(new BattleDropProp_Ext()
                {
                    Id = "p_npcj_" + __instance.UnitID,
                    Amount = 1,
                    Rate = DropRateCharacter.Value
                });
            }
            // b 装备
            Props equip = __instance.info.Equip.GetEquip(EquipType.Weapon);
            if (equip != null)
            {
                ExtDrops.Add(new BattleDropProp_Ext()
                {
                    Id = equip.Id,
                    Amount = 1,
                    Rate = DropRateEquip.Value
                });
            }
            Props equip2 = __instance.info.Equip.GetEquip(EquipType.Cloth);
            if (equip2 != null)
            {
                ExtDrops.Add(new BattleDropProp_Ext()
                {
                    Id = equip2.Id,
                    Amount = 1,
                    Rate = DropRateEquip.Value
                });
            }
            Props equip3 = __instance.info.Equip.GetEquip(EquipType.Jewelry);
            if (equip3 != null)
            {
                ExtDrops.Add(new BattleDropProp_Ext()
                {
                    Id = equip3.Id,
                    Amount = 1,
                    Rate = DropRateEquip.Value
                });
            }
            // c 内功
            if (__instance.CurrentMantra != null)
            {
                ExtDrops.Add(new BattleDropProp_Ext()
                {
                    Id = "p_scroll_" + __instance.CurrentMantra.Id,
                    Amount = 1,
                    Rate = DropRateSkillMantra.Value
                });
            }
            // d 招式
            if (__instance.LearnedSkills != null)
            {
                foreach (var skill in __instance.LearnedSkills.Values)
                {
                    ExtDrops.Add(new BattleDropProp_Ext()
                    {
                        Id = "p_scroll_" + skill.Id,
                        Amount = 1,
                        Rate = DropRateSkillMantra.Value
                    });
                }
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
            var newlist = new List<BattleDropProp>();
            if (list != null)
                newlist.AddRange(list);
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
                Console.WriteLine(string.Format("元tile={0}, 新tile={1}", tileNumber, tile));
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

        // 7 武器装备按武功筛选排序
        [HarmonyPostfix, HarmonyPatch(typeof(Inventory), "CheckCanUse", new Type[] { typeof(string), typeof(string) })]
        public static void ModExt_PropsCanUse(Inventory __instance, ref bool __result, string id, string CharacterId)
        {
            // Mod武器大多没写限定，须通过检测武功筛选
            if (__result == false)
                return;
            Props props = Game.Data.Get<Props>(id);
            if (props != null && props.PropsType == PropsType.Weapon)
            {
                __result = GlobalLib.HasSkillType(CharacterId, props.PropsCategory);
            }
        }
        static void SortInventory(InventoryWindowInfo inventoryWindowInfo, bool removeUnavailable = true)
        {
            if (removeUnavailable)
                inventoryWindowInfo.Sort.RemoveAll(props => { return props.ConditionStatus != PropsInfo.PropsConditionStatus.AllPass; });
            inventoryWindowInfo.Sort.Sort((a, b) => a.Item.PropsCategory.CompareTo(b.Item.PropsCategory));
        }
        [HarmonyPrefix, HarmonyPatch(typeof(UIEquip), "OpenPropsWindow", new Type[] { typeof(InventoryWindowInfo) })]
        public static bool ModExt_PropsCanUse2(InventoryWindowInfo inventoryWindowInfo)
        {
            SortInventory(inventoryWindowInfo);
            return true;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(UIBattle), "OpenWeaponWindow", new Type[] { typeof(InventoryWindowInfo) })]
        public static bool ModExt_PropsCanUse3(InventoryWindowInfo inventoryWindowInfo)
        {
            SortInventory(inventoryWindowInfo);
            return true;
        }

        // 8 GameAction扩展
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
                    Type action = GlobalLib.GetModOutputNodeTypes().Find((Type item) => item.Name == s);
                    if (action != null)
                    {
                        Console.WriteLine("解析扩展类型成功 = " + s);
                        __result = action;
                    }
                }
            }
        }

        // 9 各种脚本文件dump，支持Json格式战斗
        [HarmonyPostfix, HarmonyPatch(typeof(SchedulerComponent), "GetScheduleGraph", new Type[] { typeof(string) })]
        public static void Dump_Movie1(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                LastMoviePath = path;
            }
        }
        [HarmonyPostfix, HarmonyPatch(typeof(ScheduleGraph), MethodType.Constructor, new Type[] { typeof(string) })]
        public static void Dump_Movie2(ref ScheduleGraph __instance, string jsonString)
        {
            ScheduleGraph.Bundle bundle = JsonConvert.DeserializeObject<ScheduleGraph.Bundle>(jsonString, new JsonConverter[]
            {
                new OutputNodeJsonConverter()
            });
            LastMovieBundle = bundle;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(OutputNodeConvert), "Deserialize", new Type[] { typeof(string) })]
        public static bool Dump_JsonConvert(string str, ref OutputNode __result)
        {
            if (str.StartsWith("[JSON", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    string content;
                    if (str.StartsWith("[JSONFILE", StringComparison.CurrentCultureIgnoreCase))
                    {
                        var array = Game.Resource.LoadBytes(str.Substring(10)); // remove [JSONFILE]
                        content = Encoding.UTF8.GetString(array);
                    }
                    else
                    {
                        content = str.Substring(6); // remove [JSON]
                    }
                    Console.WriteLine("parse json: " + content);
                    __result = ParseOutputNode(content);
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                    Debug.LogError("解析Json错误" + str);
                    throw;
                }
                return false;
            }
            return true;
        }

        static OutputNode ParseOutputNode(string content)
        {
            var importSetting = new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
                TypeNameHandling = TypeNameHandling.Objects,
                Binder = new OutputNodeBinder()
            };
            return JsonConvert.DeserializeObject(content, importSetting) as OutputNode;
        }

        public static void DumpOutputNode(OutputNode node, string path = null)
        {
            Console.WriteLine("导出Json格式 nodeType = " + node.GetType());
            var setting = new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
                TypeNameHandling = TypeNameHandling.Objects,
                Binder = new OutputNodeBinder()
            };
            string jsonStr = JsonConvert.SerializeObject(node, Formatting.Indented, setting);
            //Console.WriteLine("未简化的Json = " + jsonStr);
            foreach(string name in GlobalLib.GetOutputNodeAssemblies())
            {
                jsonStr = jsonStr.Replace(string.Format(", {0}", name), "");
            }
            foreach (string name in GlobalLib.GetOutputNodeNameSpaces())
            {
                jsonStr = jsonStr.Replace(string.Format("{0}.", name), "");
            }
            Console.WriteLine(jsonStr);
            if (!string.IsNullOrEmpty(path))
            {
                Console.WriteLine("导出到文件 " + path);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var sr = File.CreateText(path);
                sr.Write(jsonStr);
                sr.Close();
            }

            // 对比重新通过Json构建的是否有差
            OutputNode node2 = JsonConvert.DeserializeObject(jsonStr, setting) as OutputNode;
            string str2 = OutputNodeConvert.Serialize(node2);
            Console.WriteLine("重构建的原版脚本 = " + str2);
        }

        public static void DumpMovie(ScheduleGraph.Bundle node, string path = null)
        {
            Console.WriteLine("导出Json格式 nodeType = " + node.GetType());
            var settingExport = new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.Ignore,
                TypeNameHandling = TypeNameHandling.Auto,
                Converters = new JsonConverter[]
                {
                    new Vector3Converter()
                    //new ModOutputNodeJsonConverter()
                }
            };
            string jsonStr = JsonConvert.SerializeObject(node, settingExport);
            object o = JsonConvert.DeserializeObject(jsonStr);
            var settingFormat = new JsonSerializerSettings
            {
                Converters = new JsonConverter[]
                {
                    new OutputNodeTypeConverter()
                }
            };
            jsonStr = JsonConvert.SerializeObject(o, Formatting.Indented, settingFormat);

            Console.WriteLine(jsonStr);
            if (!string.IsNullOrEmpty(path))
            {
                Console.WriteLine("导出到文件 " + path);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var sr = File.CreateText(path);
                sr.Write(jsonStr);
                sr.Close();
            }

            var settingImport = new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.Populate,
                TypeNameHandling = TypeNameHandling.Objects,
                Binder = new OutputNodeBinder(),
                Converters = new JsonConverter[]
                {
                    //new ModOutputNodeJsonConverter()
                }
            };
            var node2 = JsonConvert.DeserializeObject<ScheduleGraph.Bundle>(jsonStr, settingImport);
            var settingOriginal = new JsonSerializerSettings
            {
                Converters = new JsonConverter[]
                {
                    new OutputNodeJsonConverter()
                }
            };
            string str2 = JsonConvert.SerializeObject(node, Formatting.Indented, settingOriginal);
            Console.WriteLine("重构建的原版脚本 = " + str2);
        }

    }
}
