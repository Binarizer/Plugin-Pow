using System;
using System.Collections.Generic;
using HarmonyLib;
using BepInEx.Configuration;
using Heluo;
using Heluo.UI;
using Heluo.Data;
using Heluo.Utility;
using Heluo.Battle;
using UnityEngine;
using UnityEngine.UI;
using Heluo.Global;
using UnityEngine.EventSystems;

namespace PathOfWuxia
{
    [System.ComponentModel.DisplayName("武功扩展")]
    [System.ComponentModel.Description("非战斗时使用恢复技能、非战斗时使用五炁朝元、战斗时切换心法")]
    public class HooknMartialArts : IHook
    {
        public void OnRegister(PluginBinarizer plugin)
        {
            nonbattleUseHealSkill = plugin.Config.Bind("扩展功能", "非战斗时使用恢复技能", false, "");
            nonbattleChangeElement = plugin.Config.Bind("扩展功能", "非战斗时使用五炁朝元", false, "");
            battleChangeMantra = plugin.Config.Bind("扩展功能", "战斗时切换心法", false, "新心法的效果下回合生效，眩晕、封技、气滞时不能切换");

            foreach (var UIBattleUnitMenu in GameObject.FindObjectsOfType<UIBattleUnitMenu>())
            {
                battleChangeMantra.SettingChanged += (o, e) =>
                {
                    showMantraButton(UIBattleUnitMenu);
                };
            }
        }

        //private static ConfigEntry<bool> nonbattle;
        private static ConfigEntry<bool> nonbattleChangeElement;
        private static ConfigEntry<bool> nonbattleUseHealSkill;
        private static ConfigEntry<bool> battleChangeMantra;


        //非战斗时使用五炁朝元
        private static CharacterInfoData characterInfoData;
        private static SkillData clickSkill;
        private static CtrlHome homeController;
        [HarmonyPostfix, HarmonyPatch(typeof(UIMartialArts), "Show")]
        public static void ShowPatch_nonbattleChangeElement(ref UIMartialArts __instance)
        {
            Console.WriteLine("ShowPatch_nonbattleChangeElement start");
            //获得特技按钮
            WGMartialArtsBtn[] martialArts = Traverse.Create(__instance).Field("martialArts").GetValue<WGMartialArtsBtn[]>();
            WGMartialArtsBtn specialButton = martialArts[5];
            Button specialButton2 = specialButton.GetComponent<Button>();
            if (specialButton2 == null)
            {
                specialButton2 = specialButton.gameObject.AddComponent<Button>();
            }
            //添加点击事件
            UIHome home = Traverse.Create(__instance).Field("home").GetValue<UIHome>();
            homeController = Traverse.Create(home).Field("controller").GetValue<CtrlHome>();
            specialButton2.onClick.AddListener(delegate () { openElementUI(); });
        }

        public static void openElementUI()
        {
            Console.WriteLine("openElementUI start");
            if (nonbattleChangeElement.Value)
            {
                //show结束时ctrlMartialArts还没当前角色数据，需要从ctrlhome处获得

                List<CharacterMapping> characterMapping = Traverse.Create(homeController).Field("characterMapping").GetValue<List<CharacterMapping>>();
                int communityIndex = Traverse.Create(homeController).Field("communityIndex").GetValue<int>();

                CharacterMapping mapping = characterMapping[communityIndex];
                characterInfoData = Game.GameData.Character[mapping.InfoId];
                clickSkill = characterInfoData.GetSkill(characterInfoData.SpecialSkill);

                //不是切换功体或召唤技能
                if (clickSkill == null || (clickSkill.Item.DamageType != DamageType.ChangeElement && clickSkill.Item.DamageType != DamageType.Summon))
                {
                    return;
                }
                //mp不足
                if (characterInfoData.MP < clickSkill.Item.RequestMP)
                {
                    string text2 = Game.Data.Get<StringTable>("SecondaryInterface1207").Text;
                    Game.UI.AddMessage(text2, UIPromptMessage.PromptType.Normal);
                    return;
                }

                //切换功体
                if (clickSkill.Item.DamageType == DamageType.ChangeElement)
                {
                    Game.MusicPlayer.Current_Volume = 0.5f;

                    //从uibattle处获得五行盘ui
                    UIBattle uiBattle = Game.UI.Open<UIBattle>();
                    WgBattleRound battle_round = uiBattle.battle_round;
                    battle_round.gameObject.SetActive(false);//隐藏右上角的回合数
                    UIAttributeList attr_list = Traverse.Create(uiBattle).Field("attr_list").GetValue<UIAttributeList>();

                    //图层设置为最前，否则会被挡住
                    Game.UI.SetParent(attr_list.transform, UIForm.Depth.Front);
                    attr_list.transform.SetAsLastSibling();

                    attr_list.Show();
                    attr_list.SetOriginElement((int)characterInfoData.Element, new Action<int>(OnElementSelect), delegate
                    {
                        Game.MusicPlayer.Current_Volume = 1f;
                    });
                }
                //召唤小熊猫，开启乖乖技能列表
                else if (nonbattleUseHealSkill.Value && clickSkill.Item.DamageType == DamageType.Summon)
                {
                    CharacterInfoData characterInfoData = Game.GameData.Character["in91001"];
                    CharacterSkillData skill = characterInfoData.Skill;
                    Props equip = characterInfoData.Equip.GetEquip(EquipType.Weapon);
                    if (equip == null)
                    {
                        return;
                    }
                    List<SkillData> list = new List<SkillData>();
                    PropsCategory propsCategory = equip.PropsCategory;
                    foreach (string key in skill.Keys)
                    {
                        SkillData skillData = skill[key];
                        if (skillData.Item == null)
                        {
                            Console.WriteLine("Skill表中找不到" + skillData.Id + "的文本");
                        }
                        else if (!(skillData.Item.Id == characterInfoData.SpecialSkill))
                        {
                            list.Add(skillData);
                        }
                    }
                    if (list.Count > 0)
                    {
                        MartialArtsWindowInfo martialArtsWindowInfo = new MartialArtsWindowInfo();
                        martialArtsWindowInfo.Mapping = new CharacterMapping();
                        Npc npc = Game.Data.Get<Npc>("in91001");
                        martialArtsWindowInfo.Mapping.Id = "in91001";
                        martialArtsWindowInfo.Mapping.InfoId = npc.CharacterInfoId;
                        martialArtsWindowInfo.Mapping.ExteriorId = npc.ExteriorId;
                        martialArtsWindowInfo.Sort = list;
                        martialArtsWindowInfo.SkillColumn = (CtrlMartialArts.UISkillColumn)5;
                        Game.UI.Open<UIMartialArtsWindow>().OpenWindow(martialArtsWindowInfo, null);
                        return;
                    }
                }
            }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(CtrlMartialArtsWindow), "ChangeMartialArts")]
        public static bool CtrlMartialArtsWindowPatch_ChangeMartialArts(ref CtrlMartialArtsWindow __instance)
        {

            Console.WriteLine("CtrlMartialArtsWindowPatch_ChangeMartialArts start");

            //如果在战斗中打开
            if (inBattleOpenMantraWindow)
            {
                //获得选择的心法
                List<MantraData> sortMantra = Traverse.Create(__instance).Field("sortMantra").GetValue<List<MantraData>>();
                int index = Traverse.Create(__instance).Field("index").GetValue<int>();
                selectMantra = sortMantra[index];

                //先解除原心法的效果，为了不和下面的重复从而弹出两次“气滞”，这里手动解除
                if (currentMantra != null && currentMantra.Item.MantraPropertyEffects != null)
                {
                    for (int i = 0; i < currentMantra.Item.MantraPropertyEffects.Count; i++)
                    {
                        MantraPropertyEffect mantraPropertyEffect = currentMantra.Item.MantraPropertyEffects[i];
                        BattleAttributes Mantra_Attributes = Traverse.Create(currentUnit).Field("Mantra_Attributes").GetValue<BattleAttributes>();
                        Mantra_Attributes[mantraPropertyEffect.Property] = 0;
                    }
                }
                if (currentMantra != null && currentMantra.Item.BufferEffects != null)
                {
                    for (int j = 0; j < currentMantra.Item.BufferEffects.Count; j++)
                    {
                        string bufferId = currentMantra.Item.BufferEffects[j].GetBufferId(currentMantra.Level);
                        if (!bufferId.IsNullOrEmpty())
                        {
                            currentUnit.RemoveBuffer(bufferId);
                        }
                    }
                }

                //替换为新的心法
                currentUnit.info.WorkMantra = selectMantra.Id;

                //更新角色界面的心法展示。试过好多反射方法都不行，不知道为什么，最终还是决定调用其本身的方法来覆盖
                currentUnit.DetachMantraEffect();

            }


            //乖乖不能通过技能窗口改变技能，防止点击技能后消失
            CharacterMapping mapping = Traverse.Create(__instance).Field("mapping").GetValue<CharacterMapping>();
            bool isGuaiguai = false;
            if (mapping.Id == "in91001")
            {
                isGuaiguai = true;
            }


            Console.WriteLine("CtrlMartialArtsWindowPatch_ChangeMartialArts end");
            return !isGuaiguai;
        }

        //不在战斗中则隐藏无用的buff效果提示
        [HarmonyPostfix, HarmonyPatch(typeof(UIAttributeList), "OnElementHover")]
        public static void OnElementHoverPatch_nonbattleChangeElement(ref UIAttributeList __instance)
        {
            Console.WriteLine("OnElementHoverPatch_nonbattleChangeElement start");

            if (!GameGlobal.IsInBattle)
            {
                WGAbilityInfo abilityInfo = __instance.abilityInfo;
                abilityInfo.gameObject.SetActive(false);
            }
            Console.WriteLine("OnElementHoverPatch_nonbattleChangeElement end");
        }

        //点击五行按钮后的callback，实际切换操作在这里进行
        public static void OnElementSelect(int element)
        {
            Console.WriteLine("OnElementSelect start");
            Game.MusicPlayer.Current_Volume = 1f;
            //修改功体，扣除mp，更新界面信息
            characterInfoData.Element = (Element)element;
            characterInfoData.MP -= clickSkill.Item.RequestMP;
            homeController.UpdateBasicInfo(true);
            homeController.UpdateCharacterProperty(true);
            //水功体的回血回蓝功能要不要加上呢……如果加上不就等于白嫖了么
            Console.WriteLine("OnElementSelect end");
        }


        //非战斗时使用恢复技能
        public static UITeamMember uiTeamMember;
        public static SkillData selectSkill;
        //技能主界面的选择技能
        [HarmonyPostfix, HarmonyPatch(typeof(CtrlMartialArts), "UpdateIntroduction")]
        public static void UpdateIntroductionPatch_nonbattleUseHealSkill(ref CtrlMartialArtsWindow __instance, ref int index)
        {
            Console.WriteLine("UpdateIntroductionPatch_nonbattleUseHealSkill start");


            if (nonbattleUseHealSkill.Value && index < 4)
            {
                //获得当前鼠标指向技能
                CharacterMapping mapping = Traverse.Create(__instance).Field("mapping").GetValue<CharacterMapping>();

                CharacterInfoData source = Game.GameData.Character[mapping.InfoId];
                selectSkill = source.GetSkill((SkillColumn)index);

                showUITeamMember(source, selectSkill);
            }
            Console.WriteLine("UpdateIntroductionPatch_nonbattleUseHealSkill end");
        }

        //技能选择窗口的选择技能
        [HarmonyPostfix, HarmonyPatch(typeof(CtrlMartialArtsWindow), "UpdateIntroduction")]
        public static void UpdateIntroductionPatch2_nonbattleUseHealSkill(ref CtrlMartialArtsWindow __instance, ref int index)
        {
            Console.WriteLine("UpdateIntroductionPatch2_nonbattleUseHealSkill start");


            if (nonbattleUseHealSkill.Value)
            {
                //获得当前鼠标指向技能
                CharacterMapping mapping = Traverse.Create(__instance).Field("mapping").GetValue<CharacterMapping>();

                CharacterInfoData source = Game.GameData.Character[mapping.InfoId];


                CtrlMartialArts.UISkillColumn skillColumn = Traverse.Create(__instance).Field("skillColumn").GetValue<CtrlMartialArts.UISkillColumn>();
                List<SkillData> sortSkills = Traverse.Create(__instance).Field("sortSkills").GetValue<List<SkillData>>();
                if (skillColumn == CtrlMartialArts.UISkillColumn.Mantra)
                {
                    return;
                }
                else
                {
                    if (sortSkills.Count <= 0 || index >= sortSkills.Count)
                    {
                        Console.WriteLine(string.Format("MartialArts 的 Scroll 給出的Index出現問題, Index: {0}", index));
                        return;
                    }
                    selectSkill = sortSkills[index];
                }

                showUITeamMember(source, selectSkill);
            }
            Console.WriteLine("UpdateIntroductionPatch2_nonbattleUseHealSkill end");
        }

        //显示左侧队友UI
        public static void showUITeamMember(CharacterInfoData source, SkillData selectSkill)
        {

            Console.WriteLine("showUITeamMember start");
            //先销毁原UI
            if (uiTeamMember != null)
            {
                UnityEngine.Object.DestroyImmediate(uiTeamMember.gameObject);
            }

            //如果是恢复技能且伤害公式不为0
            if ((selectSkill.Item.DamageType == DamageType.Heal || selectSkill.Item.DamageType == DamageType.MpRecover) && selectSkill.Item.Damage != "nodamage" && selectSkill.Item.Damage != "0damage")
            {
                List<CharacterInfoData> list = new List<CharacterInfoData>();
                //暂时不做养成界面的非队友治疗，角色太多左边UI放不下，界面更新也麻烦
                //养成界面队伍中只有自己，但应该可以互相治疗
                /* if (Game.GameData.Round.CurrentStage == Heluo.Manager.TimeStage.Free)
                 {
                     foreach (KeyValuePair<string,CommunityData> community in Game.GameData.Community)
                     {
                         CharacterInfoData target = Game.GameData.Character[community.Key];
                         list.Add(target);
                     }
                 }
                 //大地图的话只有队伍中能互相治疗
                 else
                 {*/
                //获得当前队友
                foreach (string text in Game.GameData.Party)
                {
                    CharacterInfoData target = Game.GameData.Character[text];
                    list.Add(target);
                }
                //}

                //如果当前角色不在队伍中则不能用恢复技能，防止远程治疗。如果是乖乖的回血技能则看锺若昕是否在队伍中
                if (!list.Contains(source) && (source.Id == "in91001" && !list.Contains(Game.GameData.Character["in0103"])))
                {
                    return;
                }


                //展示左侧的队友列表UI
                uiTeamMember = Game.UI.Open<UITeamMember>();

                //给每个队友的头像加上按钮和点击事件
                List<UITeamMemberInfo> infos = Traverse.Create(uiTeamMember).Field("infos").GetValue<List<UITeamMemberInfo>>();
                for (int i = 0; i < infos.Count; i++)
                {
                    GameObject buttonGO = new GameObject("buttonGO");
                    buttonGO.transform.SetParent(infos[i].Protrait.transform, false);
                    Button button = buttonGO.AddComponent<Button>();

                    //给按钮随便添加一个透明的图，否则没法点击
                    Image image = buttonGO.AddComponent<Image>();
                    image.sprite = Game.Resource.Load<Sprite>("Image/UI/UIAlchemy/alchemy_stove.png");
                    image.color = new Color(255, 255, 255, 0);

                    int partyIndex = i;//临时变量存储，否则下一步addListener传不过去

                    button.onClick.AddListener(delegate {
                        nonbattleUseHealSkillAction(source, list, partyIndex, selectSkill);
                    });

                }
            }
            Console.WriteLine("showUITeamMember end");
        }

        //关闭技能选择页面时销毁左侧队友UI
        [HarmonyPostfix, HarmonyPatch(typeof(UIMartialArtsWindow), "Close")]
        public static void ClosePatch_nonbattleUseHealSkill(ref UIMartialArtsWindow __instance)
        {
            Console.WriteLine("ClosePatch_nonbattleUseHealSkill start");
            if(uiTeamMember != null)
            {
                UnityEngine.Object.Destroy(uiTeamMember.gameObject);
            }
            Console.WriteLine("ClosePatch_nonbattleUseHealSkill end");
        }

        public static void nonbattleUseHealSkillAction(CharacterInfoData attacker, List<CharacterInfoData> defender, int index, SkillData skill)
        {
            Console.WriteLine("nonbattleUseHealSkillAction start");

            //mp不足
            if (attacker.MP < skill.Item.RequestMP)
            {
                string text2 = Game.Data.Get<StringTable>("SecondaryInterface1207").Text;
                Game.UI.AddMessage(text2, UIPromptMessage.PromptType.Normal);
                return;
            }

            //最小距离大于0则不能给自己治疗
            if (selectSkill.Item.MinRange > 0 && defender[index] == attacker)
            {
                Game.UI.AddMessage("该技能不能给自己治疗", UIPromptMessage.PromptType.Normal);
                return;
            }

            //最大距离等于0则只能给自己治疗
            if (selectSkill.Item.MaxRange == 0 && defender[index] != attacker)
            {
                Game.UI.AddMessage("该技能只能给自己治疗", UIPromptMessage.PromptType.Normal);
                return;
            }


            //是否群体回复
            int startIndex = index;
            int endIndex = index + 1;
            if (skill.Item.TargetArea == TargetArea.Fan || skill.Item.TargetArea == TargetArea.LineGroup || skill.Item.TargetArea == TargetArea.RingGroup)
            {
                startIndex = 0;
                endIndex = defender.Count;
            }

            //所有加血对象是否已满血
            bool isAllFullHp = true;
            for (int i = startIndex; i < endIndex; i++)
            {
                if (defender[i].HP < defender[i].Property[CharacterProperty.Max_HP].Value)
                {
                    isAllFullHp = false;
                    break;
                }
            }
            if (isAllFullHp)
            {
                Game.UI.AddMessage("HP已满", UIPromptMessage.PromptType.Normal);
                return;
            }

            BattleComputer battleComputer = Singleton<BattleComputer>.Instance;
            BattleComputerFormula BattleComputerFormula = Traverse.Create(battleComputer).Field("BattleComputerFormula").GetValue<BattleComputerFormula>();
            BattleFormulaProperty BattleComputerProperty = Traverse.Create(battleComputer).Field("BattleComputerProperty").GetValue<BattleFormulaProperty>();

            attacker.MP -= skill.Item.RequestMP;

            for (int i = startIndex; i < endIndex; i++)
            {
                //原代码用WuxiaUnit参与计算，涉及Grid格子数据，直接调用比较麻烦，所以我这边就把逻辑抄一遍
                //只留下和回复技能有关的数据(删来删去发现好像只剩下暴击了)，如果有遗漏以后再补（真的会有人发现么）
                battleComputer.Initialize();

                AttachBattleComputerProperty(attacker, defender[i]);//记录攻击者和防御者的属性

                float basic_damage = battleComputer.Calculate_Basic_Damage(true);//基础回复值


                float num = BattleComputerFormula["critical_rate"].Evaluate(BattleComputerProperty.GetDictionary());
                num = Mathf.Clamp(num, 0f, 100f);
                int probability = UnityEngine.Random.Range(1, 100); ;
                bool IsCritical = probability <= num;//是否暴击


                float skill_coefficient = battleComputer.Calculate_Skill_Coefficient(skill);//技能倍率

                float num2 = IsCritical ? BattleComputerFormula["damage_coefficient_of_critical"].Evaluate(BattleComputerProperty.GetDictionary()) : 1f;

                float num7 = basic_damage * num2;

                int final_damage = 1;
                //实际上回血技能应该都是+倍率
                if (skill.Item.Algorithm == Algorithm.Addition)
                {
                    final_damage = (int)(num7 + skill_coefficient);
                }
                else
                {
                    final_damage = (int)(num7 * skill_coefficient);
                }

                if (selectSkill.Item.DamageType == DamageType.Heal)
                {
                    defender[i].HP += final_damage;
                }
                else if (selectSkill.Item.DamageType == DamageType.MpRecover)
                {
                    defender[i].MP += final_damage;
                }

            }


            //刷新左侧ui
            CtrlTeamMember controller = Traverse.Create(uiTeamMember).Field("controller").GetValue<CtrlTeamMember>();
            controller.OnShow();

            //刷新主界面角色信息
            homeController.UpdateCharacterProperty(true);

            Console.WriteLine("nonbattleUseHealSkillAction end");
        }

        //记录攻击者和防御者的属性
        public static void AttachBattleComputerProperty(CharacterInfoData attacker, CharacterInfoData defender)
        {
            Console.WriteLine("AttachBattleComputerProperty start");
            BattleComputer battleComputer = Singleton<BattleComputer>.Instance;
            BattleFormulaProperty BattleComputerProperty = Traverse.Create(battleComputer).Field("BattleComputerProperty").GetValue<BattleFormulaProperty>();
            BattleComputerFormula BattleComputerFormula = Traverse.Create(battleComputer).Field("BattleComputerFormula").GetValue<BattleComputerFormula>();

            BattleComputerProperty.Clear();
            foreach (object obj in Enum.GetValues(typeof(NurturanceProperty)))
            {
                NurturanceProperty prop = (NurturanceProperty)obj;
                string key = string.Format("attacker_{0}", prop.ToString().ToLower());
                int value = attacker.GetUpgradeableProperty((CharacterUpgradableProperty)obj);
                string key2 = string.Format("defender_{0}", prop.ToString().ToLower());
                int value2 = defender.GetUpgradeableProperty((CharacterUpgradableProperty)obj);
                BattleComputerProperty[key] = value;
                BattleComputerProperty[key2] = value2;
                BattleComputerProperty[prop.ToString().ToLower()] = value;
            }
            foreach (object obj2 in Enum.GetValues(typeof(BattleProperty)))
            {
                BattleProperty battleProperty = (BattleProperty)obj2;
                string format = "attacker_{0}";
                BattleProperty battleProperty2 = battleProperty;
                string key3 = string.Format(format, battleProperty2.ToString().ToLower());
                int value3 = attacker.Property[(CharacterProperty)obj2].Value;
                string format2 = "defender_{0}";
                battleProperty2 = battleProperty;
                string key4 = string.Format(format2, battleProperty2.ToString().ToLower());
                int value4 = defender.Property[(CharacterProperty)obj2].Value;
                BattleComputerProperty[key3] = value3;
                BattleComputerProperty[key4] = value4;
                if (battleProperty == BattleProperty.Move)
                {
                    break;
                }
            }

            BattleComputerProperty["attacker_element"] = (int)attacker.Element;
            BattleComputerProperty["defender_element"] = (int)defender.Element;

            BattleComputerProperty["defender_max_hp"] = defender.Property[CharacterProperty.Max_HP].Value;
            float num = BattleComputerFormula["basic_attack_of_counter"].Evaluate(BattleComputerProperty.GetDictionary());
            BattleComputerProperty.Add("basic_attack_of_counter", (int)num);

            Console.WriteLine("AttachBattleComputerProperty end");
        }

        public static MantraData currentMantra;
        public static WGAbilityInfo ability_info;
        public static WGMantraBtn mantraBtn;
        public static WuxiaUnit currentUnit;

        //显示心法按钮
        [HarmonyPostfix, HarmonyPatch(typeof(UIBattleUnitMenu), "set_all_button")]
        public static void WGBattleUnitMenuPatch_set_all_button(ref UIBattleUnitMenu __instance, ref WuxiaUnit unit)
        {
            Console.WriteLine("WGBattleUnitMenuPatch_set_all_button start");
            currentUnit = unit;
            if(currentUnit != null)
            {
                currentMantra = currentUnit.CurrentMantra;
            }
            showMantraButton(__instance);

            Console.WriteLine("WGBattleUnitMenuPatch_set_all_button end");
        }

        public static void showMantraButton(UIBattleUnitMenu __instance)
        {

            Console.WriteLine("showMantraButton start");
            WGSkillBtn[] skill_buttons = Traverse.Create(__instance).Field("skill_buttons").GetValue<WGSkillBtn[]>();
            WGSkillBtn special_skill_button = Traverse.Create(__instance).Field("special_skill_button").GetValue<WGSkillBtn>();
            ability_info = Traverse.Create(__instance).Field("ability_info").GetValue<WGAbilityInfo>();

            if (currentUnit != null)
            {

                var trans = special_skill_button.transform.parent.Find("MantraBtn");

                if (trans == null && currentUnit != null)
                {
                    //把原有的技能按钮往左移
                    for (int i = 0; i < skill_buttons.Length; i++)
                    {
                        skill_buttons[i].gameObject.transform.position = new Vector3(skill_buttons[i].gameObject.transform.position.x - 80, skill_buttons[i].gameObject.transform.position.y, skill_buttons[i].gameObject.transform.position.z);
                    }
                    special_skill_button.gameObject.transform.position = new Vector3(special_skill_button.gameObject.transform.position.x - 80, special_skill_button.gameObject.transform.position.y, special_skill_button.gameObject.transform.position.z);

                    //创建心法按钮
                    GameObject gameObject = new GameObject("MantraBtn");
                    gameObject.transform.SetParent(special_skill_button.transform.parent, false);
                    gameObject.transform.position = new Vector3(special_skill_button.gameObject.transform.position.x + 80, special_skill_button.gameObject.transform.position.y, special_skill_button.gameObject.transform.position.z);

                    mantraBtn = gameObject.AddComponent<WGMantraBtn>();
                    mantraBtn.gameObject.SetActive(false);

                    //添加鼠标移入移出事件（按钮变大高亮、变小不高亮）
                    if (mantraBtn.PointerEnter == null)
                    {
                        mantraBtn.PointerEnter = new WGBtn.TriggerEvent();
                    }
                    mantraBtn.PointerEnter.AddListener(OnMantraHighlighed);
                    if (mantraBtn.PointerExit == null)
                    {
                        mantraBtn.PointerExit = new WGBtn.TriggerEvent();
                    }
                    mantraBtn.PointerExit.AddListener(OnMantraUnHighlighed);
                    if (mantraBtn.PointerClick == null)
                    {
                        mantraBtn.PointerClick = new WGBtn.TriggerEvent();
                    }
                    mantraBtn.PointerClick.AddListener(OnMantraClick);
                }
                else
                {
                    mantraBtn = trans.gameObject.GetComponent<WGMantraBtn>();
                }
                if (battleChangeMantra.Value)
                {
                    currentMantra = currentUnit.CurrentMantra;
                    bool @lock = false;
                    //眩晕、封技、气滞时不能切换
                    if (currentUnit[BattleRestrictedState.Daze] > 0 || currentUnit[BattleRestrictedState.Seal] > 0 || currentUnit[BattleRestrictedState.Dyspnea] > 0 || currentMantra == null)
                    {
                        @lock = true;
                    }

                    mantraBtn.SetMantra(currentMantra, @lock);
                    mantraBtn.gameObject.SetActive(true);
                }
                else
                {
                    mantraBtn.gameObject.SetActive(false);
                }


            }
            Console.WriteLine("showMantraButton end");
        }

        //按钮高亮显示
        private static void OnMantraHighlighed(BaseEventData arg0)
        {
            Console.WriteLine("OnMantraHighlighed start");
            if (currentMantra != null)
            {
                //显示说明
                Console.WriteLine("显示说明");
                ability_info.ShowTip(currentMantra);
            }

            //显示高亮圈
            Console.WriteLine("显示高亮圈");
            Image image;
            var trans = mantraBtn.transform.Find("MantraBtnBar");

            if (trans == null)
            {
                GameObject gameObject = new GameObject("MantraBtnBar");
                image = gameObject.AddComponent<Image>();
                image.sprite = Game.Resource.Load<Sprite>("image/ui/uibattle/battle_trick_barbase.png");
                image.transform.localPosition = new Vector3(0, 0, 0);
                image.rectTransform.sizeDelta = new Vector3(150, 150);
                gameObject.transform.SetParent(mantraBtn.transform, false);
            }
            else
            {
                image = trans.gameObject.GetComponent<Image>();
            }
            image.gameObject.SetActive(true);

            Console.WriteLine("放大按钮");
            //放大按钮
            mantraBtn.GetComponentsInChildren<Image>()[0].rectTransform.sizeDelta = new Vector3(105, 105);
            Console.WriteLine("OnMantraHighlighed end");
        }

        //按钮取消高亮
        public static void OnMantraUnHighlighed(BaseEventData arg0)
        {
            Console.WriteLine("OnMantraUnHighlighed start");
            //隐藏说明
            ability_info.Hide();

            //隐藏高亮圈
            var MantraBtnBar = mantraBtn.transform.Find("MantraBtnBar");
            if (MantraBtnBar != null)
            {
                MantraBtnBar.GetComponent<Image>().gameObject.SetActive(false);
            }

            //缩小按钮
            mantraBtn.GetComponentsInChildren<Image>()[0].rectTransform.sizeDelta = new Vector3(90, 90);
            Console.WriteLine("OnMantraUnHighlighed end");
        }

        //心法按钮定义，继承技能按钮
        public class WGMantraBtn : WGSkillBtn
        {
            public MantraData MantraData { get; private set; }

            public void SetMantra(MantraData _mantra, bool _lock)
            {
                Console.WriteLine("SetMantra start");
                this.MantraData = _mantra;
                if (_mantra != null)
                {
                    if (_mantra.Item == null)
                    {
                        this.Hide();
                    }
                    else
                    {
                        this.UpdateMantra(_mantra);
                        this.Show();
                    }
                }
                else
                {
                    this.Hide();
                }
                //无心法时显示的图标
                var trans = mantraBtn.transform.Find("skillBase");
                Image skill_lock = Traverse.Create(this).Field("skill_lock").GetValue<Image>();
                if (trans == null)
                {
                    GameObject skillBaseObj = new GameObject("skillBase");
                    Image skillBase = skillBaseObj.AddComponent<Image>();
                    skillBase.sprite = Game.Resource.Load<Sprite>("image/ui/uibattle/battle_skill_base.png");
                    skillBase.transform.localPosition = new Vector3(0, 0, 0);
                    skillBaseObj.transform.SetParent(mantraBtn.transform, false);

                    GameObject skillBase2obj = new GameObject("skillBase2");
                    Image skillBase2 = skillBase2obj.AddComponent<Image>();
                    skillBase2.sprite = Game.Resource.Load<Sprite>("image/ui/uibattle/battle_skill_base_2.png");
                    skillBase2.transform.localPosition = new Vector3(0, 0, 0);
                    skillBase2obj.transform.SetParent(skillBaseObj.transform, false);

                    GameObject propscategorymentalemptyObj = new GameObject("propscategorymentalempty");
                    Image propscategorymentalempty = propscategorymentalemptyObj.AddComponent<Image>();
                    propscategorymentalempty.sprite = Game.Resource.Load<Sprite>("image/icon/propscategorymentalempty.png");
                    propscategorymentalempty.transform.localPosition = new Vector3(0, 0, 0);
                    propscategorymentalemptyObj.transform.SetParent(skillBase2obj.transform, false);

                    skill_lock = skillBase;
                    Traverse.Create(this).Field("skill_lock").SetValue(skill_lock);
                }
                skill_lock.gameObject.SetActive(_lock);
                Console.WriteLine("SetMantra end");
            }

            //显示心法图标
            private void UpdateMantra(MantraData skill)
            {
                Console.WriteLine("UpdateMantra start");
                Image icon = Traverse.Create(this).Field("icon").GetValue<Image>();
                if (icon == null)
                {
                    var trans = mantraBtn.transform.Find("icon");
                    if (trans == null)
                    {
                        GameObject iconObj = new GameObject("icon");
                        iconObj.transform.SetParent(mantraBtn.transform, false);
                        icon = iconObj.AddComponent<Image>();
                    }

                    Traverse.Create(this).Field("icon").SetValue(icon);
                    icon.sprite = skill.Item.Icon;
                    icon.rectTransform.sizeDelta = new Vector3(90, 90);
                }
                if (icon != null && icon.gameObject != null)
                {
                    if (skill != null)
                    {
                        icon.gameObject.SetActive(true);
                    }
                    else
                    {
                        icon.gameObject.SetActive(false);
                    }
                }
                /*Text skill_name = Traverse.Create(this).Field("skill_name").GetValue<Text>();
                if (skill_name == null)
                {
                    var trans = mantraBtn.transform.Find("skill_name");
                    if (trans == null)
                    {
                        GameObject textObj = new GameObject("skill_name");
                        textObj.transform.SetParent(mantraBtn.transform, false);
                        skill_name = textObj.AddComponent<Text>();
                    }

                    Traverse.Create(this).Field("skill_name").SetValue(skill_name);
                    skill_name.text = skill.Item.Name;
                    skill_name.transform.position = new Vector3(100,100,100);
                }
                if (skill_name != null)
                {
                    skill_name.text = skill.Item.Name;
                }*/
                Console.WriteLine("UpdateMantra end");
            }
            //隐藏心法
            public override void Hide()
            {
                Console.WriteLine("Hide start");
                Image icon = Traverse.Create(this).Field("icon").GetValue<Image>();
                if(icon != null && icon.gameObject != null)
                {
                    icon.gameObject.SetActive(false);
                }
                Console.WriteLine("Hide end");
            }

            public override void Show()
            {
                base.gameObject.SetActive(true);
            }
        }

        //点击心法按钮时打开心法列表界面
        public static bool inBattleOpenMantraWindow = false;
        public static void OnMantraClick(BaseEventData arg0)
        {
            Console.WriteLine("OnMantraClick start");
            inBattleOpenMantraWindow = true;
            SortMantra();
            Console.WriteLine("OnMantraClick end");
        }

        //心法列表
        public static MantraData selectMantra;
        public static void SortMantra()
        {
            Console.WriteLine("SortMantra start");
            CharacterInfoData characterInfoData = Game.GameData.Character[currentUnit.CharacterInfoId];
            Dictionary<string, MantraData> mantra = characterInfoData.Mantra;
            List<MantraData> list = new List<MantraData>();
            foreach (MantraData mantraData in mantra.Values)
            {
                if (!(mantraData.Id == characterInfoData.WorkMantra))
                {
                    list.Add(mantraData);
                }
            }
            if (list.Count > 0)
            {
                MartialArtsWindowInfo martialArtsWindowInfo = new MartialArtsWindowInfo();
                CharacterMapping mapping = new CharacterMapping();
                if (currentUnit.CharacterInfoId == "Player")
                {
                    mapping.Id = (mapping.InfoId = (mapping.ExteriorId = currentUnit.CharacterInfoId));
                }
                else
                {
                    Npc npc = Game.Data.Get<Npc>(currentUnit.CharacterInfoId);
                    if (npc == null)
                    {
                        return;
                    }
                    mapping.Id = currentUnit.CharacterInfoId;
                    mapping.InfoId = npc.CharacterInfoId;
                    mapping.ExteriorId = npc.ExteriorId;
                }
                martialArtsWindowInfo.Mapping = mapping;
                martialArtsWindowInfo.Sort = list;
                martialArtsWindowInfo.SkillColumn = CtrlMartialArts.UISkillColumn.Mantra;
                Game.UI.Open<UIMartialArtsWindow>().OpenWindow(martialArtsWindowInfo, new Action<bool>(MartialArtsWindow_OnResult));
                return;
            }
            string text = Game.Data.Get<StringTable>("SecondaryInterface0902").Text;
            OpenLearnedEmptyWindow(text);
            Console.WriteLine("SortMantra end");
        }

        //提示无可用心法
        public static void OpenLearnedEmptyWindow(string message)
        {
            Console.WriteLine("OpenLearnedEmptyWindow start");
            Game.UI.OpenMessageWindow(message, null, true);
            Console.WriteLine("OpenLearnedEmptyWindow end");
        }
        //结果回调
        private static void MartialArtsWindow_OnResult(bool result)
        {
            Console.WriteLine("MartialArtsWindow_OnResult start");
            inBattleOpenMantraWindow = false;
            if (result)
            {
                //如果点击了新心法则更新按钮信息
                currentMantra = selectMantra;
                mantraBtn.SetMantra(currentMantra, false);
            }
            Console.WriteLine("MartialArtsWindow_OnResult end");
        }
    }
}
