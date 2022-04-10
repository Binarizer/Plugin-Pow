using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Heluo;
using Heluo.Resource;
using Heluo.Battle;
using Heluo.FSM.Battle;
using Heluo.UI;
using Heluo.Data;
using Heluo.Utility;
using Heluo.FSM;
using System.ComponentModel;

namespace PathOfWuxia
{
    [System.ComponentModel.DisplayName("战斗模式设置")]
    [Description("半即时战斗模式、自动战斗")]
    // 半即时战斗
    public class HookInitiactiveBattle : IHook
    {
        public void OnRegister(PluginBinarizer plugin)
        {
            initiactiveBattle = plugin.Config.Bind("战斗模式", "半即时战斗", false, "开关时序制半即时战斗系统");
            autoBattle = plugin.Config.Bind("战斗模式", "自动战斗", false, "我方自动战斗，重开战斗生效");

            plugin.onUpdate += OnUpdate;
        }

        public void OnUpdate()
        {
            if (bTimed && FSM != null)
            {
                for (int num = 0; num < UIPortraits.Count; num++)
                {
                    if (UIUnitIndex.ContainsValue(num))
                    {
                        Image image = UIPortraits[num];
                        Text text = UINames[num];
                        int order = UIPortraitOrder[num];
                        WuxiaUnit unit = UIWuxiaUnits[order];
                        Vector3 targetPos = (order == 0) ? new Vector3(68f, 620f, 0f) : new Vector3(80f, 610f - (float)order * 48f, 0f);
                        if (unit != null && unit.IsEndUnit)
                        {
                            targetPos += new Vector3(-20f, -20f, 0f);
                        }
                        image.transform.position = Vector3.Lerp(image.transform.position, targetPos, Time.deltaTime * 5f);
                    }
                }

                if (UnitMenu != null && UnitMenu.isActiveAndEnabled && wait_button != null)
                {
                    wait_button.GetComponentsInChildren<Text>(true)[1].text = "等待";
                    var unit = Traverse.Create(UnitMenu).Field("unit").GetValue<WuxiaUnit>();
                    wait_button.gameObject.SetActive(!Timed_IsWaiting(unit) && !unit.IsMoving && !unit.IsMoved && !unit.IsAction);
                }
            }
        }

        private static ConfigEntry<bool> initiactiveBattle;
        private static ConfigEntry<bool> autoBattle;
        public static bool bTimed = false;   // 实际判定，需重启战斗才可生效

        public class TimeInfo
        {
            public float value;       // 时序值
            public bool begin;    // 是否是回合开始状态
        }

        public static Dictionary<WuxiaUnit, TimeInfo> TimedValue = new Dictionary<WuxiaUnit, TimeInfo>();       // 时序表
        public static List<WuxiaUnit> TimedActives = new List<WuxiaUnit>();                                     // 未行动者
        public static List<WuxiaUnit> TimedNext = new List<WuxiaUnit>();                                        // 下回合行动者
        public static List<WuxiaUnit> TimedWaiting = new List<WuxiaUnit>();                                     // 等待者

        public static void Timed_EndUnit(bool isMoved, bool isRest)
        {
            WuxiaUnit wuxiaUnit = Timed_Current();
            if (wuxiaUnit == null)
            {
                Console.WriteLine("当前行动列表为空，出错！");
            }
            Console.WriteLine(string.Concat(new string[]
            {
                wuxiaUnit.name,
                " 行动结束, 移动=",
                isMoved.ToString(),
                " 休息=",
                isRest.ToString()
            }));
            (TimedActives.Contains(wuxiaUnit) ? TimedActives : TimedWaiting).RemoveAt(0);
            TimedNext.Add(wuxiaUnit);
            float num = (float)wuxiaUnit.info.GetUpgradeableProperty(CharacterUpgradableProperty.Dex);
            num += UnityEngine.Random.value * 0.5f * (float)wuxiaUnit.info.GetUpgradeableProperty(CharacterUpgradableProperty.Spi);
            if (!isMoved)
            {
                num *= 1.5f;
            }
            if (isRest)
            {
                num *= 1.5f;
            }
            if (TimedValue.ContainsKey(wuxiaUnit))
            {
                TimedValue[wuxiaUnit].value = (int)num;
            }
            else
            {
                TimedValue.Add(wuxiaUnit, new TimeInfo() { value = num, begin = true });
            }
            TimedNext.Sort((WuxiaUnit a, WuxiaUnit b) => TimedValue[b].value.CompareTo(TimedValue[a].value));
        }

        public static bool Timed_GetBeginTurn(WuxiaUnit unit)
        {
            return TimedValue[unit].begin;
        }

        public static void Timed_SetBeginTurn(WuxiaUnit unit, bool b)
        {
            TimedValue[unit].begin = b;
        }

        public static void Timed_AddUnit(WuxiaUnit unit)
        {
            if (unit.IsBattleEventCube)
            {
                return;
            }
            TimedActives.Insert(0, unit);
            Timed_EndUnit(true, false);
        }

        public static void Timed_RemoveUnit(WuxiaUnit unit)
        {
            if (unit.IsBattleEventCube)
            {
                return;
            }
            TimedActives.Remove(unit);
            TimedWaiting.Remove(unit);
            TimedNext.Remove(unit);
            TimedValue.Remove(unit);
        }

        public static void Timed_NextTurn()
        {
            TimedActives.Clear();
            TimedWaiting.Clear();
            TimedActives.AddRange(TimedNext);
            TimedNext.Clear();
        }

        public static void Timed_WaitUnit()
        {
            if (TimedActives.Count <= 0)
            {
                Console.WriteLine("列表为空，出错");
                return;
            }
            WuxiaUnit item = TimedActives[0];
            TimedActives.RemoveAt(0);
            TimedWaiting.Insert(0, item);
        }

        public static WuxiaUnit Timed_Current()
        {
            if (TimedActives.Count > 0)
            {
                return TimedActives[0];
            }
            if (TimedWaiting.Count > 0)
            {
                return TimedWaiting[0];
            }
            return null;
        }

        public static bool Timed_IsWaiting(WuxiaUnit unit)
        {
            return TimedWaiting.Contains(unit);
        }

        public static void Timed_EncourageUnit(WuxiaUnit unit)
        {
            if (TimedActives.Contains(unit))
            {
                TimedActives.Remove(unit);
            }
            if (TimedNext.Contains(unit))
            {
                TimedNext.Remove(unit);
            }
            if (!TimedWaiting.Contains(unit))
            {
                TimedActives.Insert((TimedActives.Count > 0) ? 1 : 0, unit);
            }
        }

        // 1 战斗开始，添加/删除单位
        static WuxiaBattleManager BM;
        static BattleStateMachine FSM;
        [HarmonyPostfix, HarmonyPatch(typeof(WuxiaBattleManager), "InitBattle", new Type[] { typeof(BattleStateMachine), typeof(string), typeof(IDataProvider), typeof(IResourceProvider), typeof(Action<BillboardArg>) })]
        public static void TimedPatch_Begin(ref WuxiaBattleManager __instance, BattleStateMachine bsm)
        {
            bTimed = initiactiveBattle.Value;   // 总开关
            BM = __instance;
            FSM = bsm;
            TimedValue.Clear();
            TimedActives.Clear();
            TimedWaiting.Clear();
            TimedNext.Clear();
            UIWuxiaUnits.Clear();
            UIUnitIndex.Clear();
            UIPortraits.Clear();
            UINameBgs.Clear();
            UINames.Clear();
        }
        [HarmonyPostfix, HarmonyPatch(typeof(WuxiaBattleUnit), "CreateUnit", new Type[] { typeof(string), typeof(Faction), typeof(int), typeof(bool) })]
        public static void TimedPatch_AddUnit(WuxiaBattleUnit __instance, WuxiaUnit __result)
        {
            if (bTimed)
            {
                Timed_AddUnit(__result);    // 添加单位
            }
        }
        [HarmonyPrefix, HarmonyPatch(typeof(WuxiaBattleUnit), "OnUnitDestory"/*mdzz*/, new Type[] { typeof(WuxiaUnit), typeof(bool) })]
        public static bool TimedPatch_RemoveUnit(WuxiaBattleUnit __instance, WuxiaUnit unit)
        {
            if (bTimed)
            {
                Timed_RemoveUnit(unit);    // 去除单位
                UpdateTimedUI();
            }
            return true;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(WuxiaUnit), "EncourageUnit")]
        public static bool TimedPatch_EncourageUnit(WuxiaUnit __instance)
        {
            if (bTimed)
            {
                Timed_EncourageUnit(__instance);    // 顺序提前
            }
            return true;
        }

        // 2 UI!!
        public static List<Image> UIPortraits = new List<Image>();
        public static List<Text> UINames = new List<Text>();
        public static WuxiaUnit UISelected;
        public static WuxiaUnit UITarget;
        public static WuxiaUnit UIHeal;
        public static List<Image> UINameBgs = new List<Image>();
        public static Dictionary<WuxiaUnit, int> UIUnitIndex = new Dictionary<WuxiaUnit, int>();
        public static List<int> UIPortraitOrder = new List<int>();
        public static List<WuxiaUnit> UIWuxiaUnits = new List<WuxiaUnit>();
        public static void UpdateTimedDisplay(List<WuxiaUnit> timedUnits)
        {
            UIWuxiaUnits = timedUnits;
            for (int i = 0; i < timedUnits.Count; i++)
            {
                WuxiaUnit wuxiaUnit = timedUnits[i];
                int num;
                if (UIUnitIndex.ContainsKey(wuxiaUnit))
                {
                    num = UIUnitIndex[wuxiaUnit];
                }
                else
                {
                    num = 0;
                    while (UIUnitIndex.ContainsValue(num))
                    {
                        num++;
                    }
                    UIUnitIndex.Add(wuxiaUnit, num);
                }
                Image image;
                Image image2;
                Text text;
                if (UIPortraits.Count <= num)
                {
                    GameObject gameObject = new GameObject("Image");
                    gameObject.transform.SetParent(FSM.UI.battle_round.transform, false);
                    image = gameObject.AddComponent<Image>();
                    UIPortraits.Add(image);
                    image.transform.position = ((i == 0) ? new Vector3(68f, 620f, 0f) : new Vector3(80f, 610f - (float)i * 48f, 0f));
                    GameObject gameObject2 = new GameObject("textbg");
                    image2 = gameObject2.AddComponent<Image>();
                    image2.sprite = Game.Resource.Load<Sprite>("image/ui/uimain/Main_TitleBtn.png");
                    gameObject2.transform.SetParent(gameObject.transform, false);
                    UINameBgs.Add(image2);
                    GameObject gameObject3 = new GameObject("Text");
                    text = gameObject3.AddComponent<Text>();
                    text.font = Game.Resource.Load<Font>("Assets/Font/kaiu.ttf");
                    text.alignment = TextAnchor.MiddleLeft;
                    gameObject3.transform.SetParent(gameObject2.transform, false);
                    UINames.Add(text);
                    UIPortraitOrder.Add(i);
                }
                else
                {
                    image = UIPortraits[num];
                    image2 = UINameBgs[num];
                    text = UINames[num];
                    UIPortraitOrder[num] = i;
                }
                if (i < 10)
                {
                    image.gameObject.SetActive(true);
                    image2.gameObject.SetActive(true);
                    text.gameObject.SetActive(true);
                    image.rectTransform.sizeDelta = ((i == 0) ? new Vector2(60f, 60f) : new Vector2(45f, 45f));
                    image.sprite = Game.Resource.Load<Sprite>(string.Format(GameConfig.HeadProtraitPath, wuxiaUnit.ProtraitId));
                    image2.rectTransform.sizeDelta = ((i == 0) ? new Vector2(140f, 40f) : new Vector2(105f, 30f));
                    image2.transform.localPosition = ((i == 0) ? new Vector3(78f, 0f, 0f) : new Vector3(60f, 0f, 0f));
                    text.fontSize = ((i == 0) ? 20 : 15);
                    text.transform.localPosition = new Vector3(40f, 0f, 0f);
                    if (wuxiaUnit == UISelected)
                    {
                        image.color = Color.yellow;
                    }
                    else if (wuxiaUnit == UITarget)
                    {
                        image.color = Color.red;
                    }
                    else if (wuxiaUnit == UIHeal)
                    {
                        image.color = Color.green;
                    }
                    else
                    {
                        image.color = Color.white;
                    }
                    if (wuxiaUnit.IsEndUnit)
                    {
                        Color color = image.color * 0.4f;
                        image.color = new Color(color.r, color.g, color.b, 1f);
                    }
                    text.text = wuxiaUnit.FullName;
                    text.rectTransform.sizeDelta = new Vector2(120f, 20f);
                    switch (wuxiaUnit.faction)
                    {
                        case Faction.Player:
                        case Faction.Teamofplayer:
                            image2.color = new Color(0.6f, 0.878f, 0f, 0.3f);
                            break;
                        case Faction.Enemy:
                            image2.color = new Color(0.992f, 0.259f, 0.051f, 0.3f);
                            break;
                        case Faction.Single:
                        case Faction.AbsolutelyNeutral:
                        case Faction.AbsoluteChaos:
                            image2.color = new Color(0.894f, 0.765f, 0.22f, 0.3f);
                            break;
                    }
                    if (i > 5)
                    {
                        image.color *= (float)(10 - i) * 0.2f;
                        image2.color *= (float)(10 - i) * 0.2f;
                        text.color = Color.white * ((float)(15 - i) * 0.1f);
                    }
                }
                else
                {
                    image.gameObject.SetActive(false);
                    image2.gameObject.SetActive(false);
                    text.gameObject.SetActive(false);
                }
            }
            foreach (WuxiaUnit wuxiaUnit2 in UIUnitIndex.Keys.ToList<WuxiaUnit>())
            {
                if (!timedUnits.Contains(wuxiaUnit2))
                {
                    UIUnitIndex.Remove(wuxiaUnit2);
                }
            }
            for (int j = 0; j < UIPortraits.Count; j++)
            {
                if (!UIUnitIndex.ContainsValue(j))
                {
                    UIPortraits[j].gameObject.SetActive(false);
                    UINameBgs[j].gameObject.SetActive(false);
                    UINames[j].gameObject.SetActive(false);
                }
            }
        }
        public static void UpdateTimedUI()
        {
            List<WuxiaUnit> list = new List<WuxiaUnit>();
            list.AddRange(TimedActives);
            list.AddRange(TimedWaiting);
            list.AddRange(TimedNext);
            UpdateTimedDisplay(list);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIBattle), "OpenAttackInfo", new Type[] { typeof(int), typeof(int), typeof(int), typeof(DamageDirection), typeof(bool), typeof(WuxiaUnit) })]
        public static void TimedPatch_Color1(UIBattle __instance, WuxiaUnit _unit)
        {
            if (bTimed)
            {
                UITarget = _unit;
                UpdateTimedUI();
            }
        }
        [HarmonyPostfix, HarmonyPatch(typeof(UIBattle), "CloseAttackInfo")]
        public static void TimedPatch_Color2(UIBattle __instance)
        {
            if (bTimed)
            {
                UITarget = null;
                UpdateTimedUI();
            }
        }
        [HarmonyPostfix, HarmonyPatch(typeof(UIBattle), "OpenHealInfo", new Type[] { typeof(int), typeof(int), typeof(int), typeof(bool), typeof(WuxiaUnit) })]
        public static void TimedPatch_Color3(UIBattle __instance, WuxiaUnit _unit)
        {
            if (bTimed)
            {
                UIHeal = _unit;
                UpdateTimedUI();
            }
        }
        [HarmonyPostfix, HarmonyPatch(typeof(UIBattle), "CloseHealInfo")]
        public static void TimedPatch_Color4(UIBattle __instance)
        {
            if (bTimed)
            {
                UIHeal = null;
                UpdateTimedUI();
            }
        }
        [HarmonyPostfix, HarmonyPatch(typeof(UIBattle), "UpdateUnitInfo", new Type[] { typeof(WuxiaUnit) })]
        public static void TimedPatch_Color5(UIBattle __instance, WuxiaUnit unit)
        {
            if (bTimed)
            {
                UISelected = null;
                UpdateTimedUI();
            }
        }

        // 3. 等待按钮
        // 单线程游戏 用全局变量即可
        public static bool UnitIsRest;
        public static bool UnitIsMoved;
        public static bool UnitWantWait;
        private static WGBattleUnitMenu UnitMenu;
        public static Action WaitClick;
        private static WGBtn wait_button;
        private static WGBtn rest_button;

        [HarmonyPrefix, HarmonyPatch(typeof(WGBattleUnitMenu), "Show")]
        public static bool TimedPatch_WaitBtn1(WGBattleUnitMenu __instance)
        {
            UnitMenu = __instance;
            Console.WriteLine("UnitMenu=" + UnitMenu);
            if (wait_button == null || rest_button == null)
            {
                WGBtn[] componentsInChildren = __instance.GetComponentsInChildren<WGBtn>();
                rest_button = componentsInChildren[componentsInChildren.Length - 1];
                wait_button = rest_button.Instantiate<WGBtn>();
                wait_button.transform.SetParent(rest_button.transform.parent, false);
                wait_button.transform.localPosition += new Vector3(0f, 150f, 0f);
                Sprite sprite = Game.Resource.Load<Sprite>("Image/UI/UIBattle/Battle_skill_skip.png");
                wait_button.GetComponentsInChildren<Image>()[3].sprite = sprite;
                var p = Traverse.Create(wait_button.PointerClick).Field("m_PersistentCalls");
                p.Method("RemoveListener", 3).GetValue();   // remove OnRestClick in persistent calls
                wait_button.PointerEnter.AddListener(delegate (BaseEventData eventData)
                {
                    Traverse.Create(__instance).Field("rest_info").GetValue<GameObject>().SetActive(false);
                });
                wait_button.PointerClick.AddListener(OnWaitClick);
            }
            wait_button.gameObject.SetActive(bTimed);
            return true;
        }
        public static void OnWaitClick(BaseEventData eventData)
        {
            Action action = WaitClick;
            if (WaitClick != null)
            {
                Traverse.Create(FSM.UI).Field("unit_menu").Field("Block").GetValue<GameObject>().SetActive(true);// this.Block.SetActive(true);
                UnitWantWait = true;
                WaitClick();
            }
        }

        // 4 跳过选人
        [HarmonyPrefix, HarmonyPatch(typeof(BeginUnit), "ChangeUnit", new Type[] { typeof(bool) })]
        public static bool TimedPatch_ChangeUnit1(BeginUnit __instance)
        {
            if (bTimed)
                return false;
            return true;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(BattleState), "ChangeUnit", new Type[] { typeof(bool) })]
        public static bool TimedPatch_ChangeUnit2(BattleState __instance)
        {
            if (bTimed)
            {
                var t = Traverse.Create(__instance);
                var selected = t.Property("SelectedUnit");
                selected.SetValue(Timed_Current());
                return false;
            }
            return true;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(BeginUnit), "OnCellClicked", new Type[] { typeof(WuxiaCell), typeof(PointerEventData.InputButton) })]
        public static bool TimedPatch_ChangeUnit3(BeginUnit __instance, WuxiaCell cell, PointerEventData.InputButton btn)
        {
            if (bTimed && cell.Unit != null && btn == PointerEventData.InputButton.Left)
                return false;
            return true;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(WaitInput), "OnCellClicked", new Type[] { typeof(WuxiaCell), typeof(PointerEventData.InputButton) })]
        public static bool TimedPatch_ChangeUnit4(WaitInput __instance, WuxiaCell cell, PointerEventData.InputButton btn)
        {
            if (bTimed && cell.Unit != null && btn == PointerEventData.InputButton.Left)
                return cell.Unit == Timed_Current();
            return true;
        }

        // 5. 状态机！！
        [HarmonyPrefix, HarmonyPatch(typeof(BattleState), "FirstUnitSelect")]
        public static bool TimedPatch_FirstUnit(BattleState __instance)
        {
            if (autoBattle.Value)
            {
                __instance.SendEvent("AI");
                return false;
            }

            if (bTimed)
            {
                var t = Traverse.Create(__instance);
                var selected = t.Property("SelectedUnit");
                selected.SetValue(Timed_Current());
                if (selected.GetValue<WuxiaUnit>() != null)
                {
                    WuxiaCell cell2 = t.Property("SelectedUnit").GetValue<WuxiaUnit>().Cell;
                    t.Property("BattleManager").GetValue<WuxiaBattleManager>().CameraLookAt = cell2.transform.position;
                    __instance.SendEvent("FINISHED");
                    return false;
                }
                __instance.SendEvent("ENDTURN");
                return false;
            }
            return true;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(BeginUnit), "OnEnable")]
        public static bool TimedPatch_BeginUnit2(BeginUnit __instance)
        {
            var t = Traverse.Create(__instance);
            // base.OnEnable();
            var inputInfo = typeof(InputState).GetField("InputInfos", BindingFlags.Static | BindingFlags.NonPublic);
            if (inputInfo.GetValue(null) == null)
            {
                var bii = new List<BattleInputInfo>();
                for (int i = 0; i < 36; i++)
                {
                    BattleInputInfo item = new BattleInputInfo();
                    bii.Add(item);
                }
                inputInfo.SetValue(null, bii);
            }
            Game.Input.Push(__instance);

            BM.Time = BattleEventToggleTime.BeginUnit;
            if (bTimed)
            {
                WuxiaUnit wuxiaUnit = Timed_Current();
                if (wuxiaUnit != null && !Timed_IsWaiting(wuxiaUnit))
                {
                    if (Timed_GetBeginTurn(wuxiaUnit))
                    {
                        wuxiaUnit.OnBufferEvent(BufferTiming.BeginTurn);
                        Timed_SetBeginTurn(wuxiaUnit, false);
                    }
                    BM.OnBattleEvent(BattleEventToggleTime.BeginUnit, Array.Empty<object>());
                    WaitClick = new Action( ()=>
                    {
                        UnitWantWait = true;
                        UnitIsRest = false;
                        __instance.SendEvent("FINISHED");
                    } );
                }
            }
            else
            {
                BM.OnBattleEvent(BattleEventToggleTime.BeginUnit, Array.Empty<object>());
            }
            if (!BM.IsEvent)
            {
                t.Method("InitBeginUnit").GetValue();// this.InitBeginUnit();
                FSM.UI.SkillClick = new Action<SkillData>(__instance.OnSkillClick);
                FSM.UI.RestClick = new Action(__instance.OnRestClick);
            }
            return false;
        }


        static bool IsContinuous_Beheading;
        [HarmonyPrefix, HarmonyPatch(typeof(UnitPlayAbility), "Perform")]
        public static bool UnitPlayAbilityPatch_getIsContinuous_Beheading(UnitPlayAbility __instance)
        {
            var t = Traverse.Create(__instance);
            var args = t.Field("args");
            Console.WriteLine("args:" + args);

            WuxiaUnit Attacker = args.Field("Attacker").GetValue<WuxiaUnit>();
            Console.WriteLine("Attacker:" + Attacker);

            IsContinuous_Beheading = Attacker.IsContinuous_Beheading;
            Console.WriteLine("IsContinuous_Beheading:" + IsContinuous_Beheading);

            return true;
        }




        [HarmonyPrefix, HarmonyPatch(typeof(EndUnit), "OnEnable")]
        public static bool TimedPatch_End1(EndUnit __instance)
        {
            Console.WriteLine("EndUnit.OnEnable()");
            var t = Traverse.Create(__instance);
            var selected = t.Property("SelectedUnit");
            Console.WriteLine("bTimed:" + bTimed+ ",UnitWantWait:" + UnitWantWait);
            if (bTimed && UnitWantWait)   // 处理等待
            {
                UnitWantWait = false;     // 重置等待
                Timed_WaitUnit();
                UpdateTimedUI();
                FSM.UI.CloseMenu();
                FSM.UI.CloseUnitInfo();
                WuxiaUnit wuxiaUnit = Timed_Current();
                if (wuxiaUnit != null && wuxiaUnit.faction == Faction.Player)
                {
                    t.Method("FirstUnitSelect").GetValue();//__instance.FirstUnitSelect();                    
                    __instance.SendEvent("FINISHED");
                    return false;
                }
                //__instance.SelectedUnit = null;
                selected.SetValue(null);
                __instance.SendEvent("ENDTURN");
                return false;
            }
            BM.Time = BattleEventToggleTime.EndUnit;
            BM.OnBattleEvent(BattleEventToggleTime.EndUnit, Array.Empty<object>());
            FSM.UI.CloseMenu();
            FSM.UI.CloseUnitInfo();
            Console.WriteLine("1");
            Console.WriteLine("BM.IsEvent:" + BM.IsEvent);
            if (!BM.IsEvent)
            {
                var fsmvar = Traverse.Create((GameStateMachine)FSM);
                var endUnitEventArgs = fsmvar.Field("eventArgs");
                Console.WriteLine("endUnitEventArgs:" + endUnitEventArgs);
                Console.WriteLine("IsContinuous_Beheading:" + endUnitEventArgs.Field("IsContinuous_Beheading").GetValue<bool>());
                Console.WriteLine("IsContinuous_Beheading:" + IsContinuous_Beheading);
                if (selected.GetValue<WuxiaUnit>() != null)
                {
                    WuxiaUnit selectedUnit = selected.GetValue<WuxiaUnit>();
                    if (selectedUnit != null)
                    {
                        selectedUnit.OnUnitEnd();
                    }
                    WuxiaUnit selectedUnit2 = selected.GetValue<WuxiaUnit>();
                    if (selectedUnit2 != null)
                    {
                        selectedUnit2.OnTurnEnd();
                    }
                    if (endUnitEventArgs != null && IsContinuous_Beheading)
                    {
                        BM.SendBillboard(new BillboardArg
                        {
                            Pos = selected.GetValue<WuxiaUnit>().transform.position,
                            Numb = 0,
                            MessageType = Heluo.Battle.MessageType.Continuous
                        });
                        BM.OnBufferEvent(BufferTiming.Continuous_Beheading);
                        WuxiaUnit selectedUnit3 = selected.GetValue<WuxiaUnit>();
                        if (selectedUnit3 != null)
                        {
                            Console.WriteLine("selectedUnit3.OnTurnStart()");
                            selectedUnit3.OnTurnStart();
                        }
                    }
                    if (selected.GetValue<WuxiaUnit>()[BattleLiberatedState.InfiniteAction] > 0)
                    {
                        WuxiaUnit selectedUnit3 = selected.GetValue<WuxiaUnit>();
                        if (selectedUnit3 != null)
                        {
                            selectedUnit3.OnTurnStart();
                        }
                    }
                    if (selected.GetValue<WuxiaUnit>()[BattleLiberatedState.Encourage] > 0)
                    {
                        WuxiaUnit selectedUnit4 = selected.GetValue<WuxiaUnit>();
                        if (selectedUnit4 != null)
                        {
                            selectedUnit4.OnTurnStart();
                        }
                    }
                    selected.GetValue<WuxiaUnit>().OnBufferEvent(BufferTiming.EndUnit);
                }
                t.Method("OnUnitEnd").GetValue();//this.OnUnitEnd();
            }
            return false;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(EndUnit), "OnUnitEnd")]
        public static bool TimedPatch_End2(EndUnit __instance)
        {
            var t = Traverse.Create(__instance);
            var selected = t.Property("SelectedUnit");
            BM.BattleWinLose.CheckWinLose(BattleEventToggleTime.EndTurn);
            if (BM.BattleWinLose.Type > WinLoseType.None)
            {
                __instance.SendEvent("ENDTURN");
            }
            else if (bTimed)
            {
                WuxiaUnit wuxiaUnit = Timed_Current();
                if (wuxiaUnit != null && wuxiaUnit.IsEndUnit)
                {
                    Timed_EndUnit(wuxiaUnit.IsMoved, UnitIsRest);
                    UpdateTimedUI();
                    if (__instance.CheckIsEndTurn())
                    {
                        __instance.SendEvent("ENDTURN");
                        //base.SelectedUnit = null;
                        selected.SetValue(null);
                        return false;
                    }
                }
                wuxiaUnit = Timed_Current();
                if (wuxiaUnit != null && wuxiaUnit.faction == Faction.Player)
                {
                    t.Method("FirstUnitSelect").GetValue();       //base.FirstUnitSelect();
                    __instance.SendEvent("FINISHED");
                }
                else
                {
                    selected.SetValue(null);
                    __instance.SendEvent("ENDTURN");
                }
            }
            else if (__instance.CheckIsEndTurn())
            {
                __instance.SendEvent("ENDTURN");
                //base.SelectedUnit = null;
                selected.SetValue(null);
            }
            else
            {
                if (selected.GetValue<WuxiaUnit>() == null)
                {
                    t.Method("FirstUnitSelect").GetValue();       //base.FirstUnitSelect();
                }
                if (selected.GetValue<WuxiaUnit>().IsEndUnit)
                {
                    //__instance.SelectedUnit = __instance.FindNext(false);
                    selected.SetValue(Traverse.Create(__instance).Method("FindNext", false).GetValue<WuxiaUnit>());
                }
                __instance.SendEvent("FINISHED");
            }
            return false;
        }

        public static async void OnEnable_Timed(BeginTurn state)
        {
            var t = Traverse.Create(state);
            var selected = t.Property("SelectedUnit");
            WuxiaUnit wuxiaUnit = Timed_Current();
            if (wuxiaUnit != null)
            {
                if (wuxiaUnit.faction != Faction.Player || autoBattle.Value)
                {
                    state.SendEvent("AI");
                }
                else
                {
                    if (wuxiaUnit[BattleRestrictedState.Fear] > 0)
                    {
                        await FearAllyAI_Timed(state);
                    }
                    t.Method("FirstUnitSelect").GetValue();//state.FirstUnitSelect();
                    state.SendEvent("FINISHED");
                }
            }
            else
            {
                BM.Time = BattleEventToggleTime.BeginTurn;
                BM.OnBattleEvent(BattleEventToggleTime.BeginTurn, Array.Empty<object>());
                BM.OnBattleEvent(BattleEventToggleTime.BeginAITurn, Array.Empty<object>());
                if (!BM.IsEvent)
                {
                    await FSM.UI.battleTurn.NextTurnAsync(true);
                    FSM.UI.battleTurn.Hide();
                    foreach (WuxiaUnit wuxiaUnit2 in BM.WuxiaUnits)
                    {
                        if (!wuxiaUnit2.IsDead)
                        {
                            wuxiaUnit2.OnTurnStart();
                        }
                    }
                    Timed_NextTurn();
                    WuxiaUnit wuxiaUnit3 = Timed_Current();
                    if (wuxiaUnit3 != null)
                    {
                        BM.Turn++;
                        if (wuxiaUnit3.faction != Faction.Player || autoBattle.Value)
                        {
                            state.SendEvent("AI");
                            return;
                        }
                        if (wuxiaUnit3[BattleRestrictedState.Fear] > 0)
                        {
                            await FearAllyAI_Timed(state);
                        }
                        t.Method("FirstUnitSelect").GetValue(); //state.FirstUnitSelect();
                        state.SendEvent("FINISHED");
                    }
                }
            }
            UpdateTimedUI();
        }
        public static async Task FearAllyAI_Timed(BeginTurn state)
        {
            var t = Traverse.Create(state);
            var selected = t.Property("SelectedUnit");
            while (Timed_Current() != null && Timed_Current()[BattleRestrictedState.Fear] > 0)
            {
                await 0.25f;
                WuxiaUnit unit = Timed_Current();
                unit.OnBufferEvent(BufferTiming.BeginUnit);
                BM.CameraLookAt = unit.Cell.transform.position;
                List<WuxiaCell> moveInRange = t.Method("ShowMoveRange", unit).GetValue<List<WuxiaCell>>();//state.ShowMoveRange(unit);

                var ai = Traverse.Create(t.Property("FearAI").GetValue());
                ai.Field("Current").SetValue(unit);
                await 0.1f;
                AIActionInfo aiactionInfo = ai.Method("Evaluate", moveInRange).GetValue<AIActionInfo>();//state.FearAI.Evaluate(moveInRange);
                List<WuxiaCell> list = aiactionInfo?.path;
                List<WuxiaCell> shortestPath = new List<WuxiaCell>();
                int num = unit[BattleProperty.Move];
                foreach (WuxiaCell wuxiaCell in moveInRange)
                {
                    wuxiaCell.UnMark();
                }
                if (list.HasData<WuxiaCell>())
                {
                    foreach (WuxiaCell wuxiaCell2 in list)
                    {
                        shortestPath.Add(wuxiaCell2);
                        if (num == 0)
                        {
                            break;
                        }
                        wuxiaCell2.Mark(CellMarkType.WalkPath);
                        num--;
                    }
                    unit.Move(shortestPath[0], shortestPath);
                    while (unit.IsMoving)
                    {
                        await 0;
                    }
                    foreach (WuxiaCell wuxiaCell3 in shortestPath)
                    {
                        wuxiaCell3.UnMark();
                    }
                    unit.Actor.Move = false;
                }
                unit.OnUnitEnd();
                if (!unit.IsDead)
                {
                    unit.OnBufferEvent(BufferTiming.EndUnit);
                    Timed_EndUnit(unit.IsMoved, !unit.IsMoved);
                    UpdateTimedUI();
                }
            }
        }
        [HarmonyPrefix, HarmonyPatch(typeof(BeginTurn), "OnEnable")]
        public static bool TimedPatch_BeginTurn1(BeginTurn __instance)
        {
            Console.WriteLine("BeginTurn.OnEnable()");
            if (bTimed)
            {
                OnEnable_Timed(__instance);
                return false;
            }
            return true;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(EndTurn), "OnEnable")]
        public static bool TimedPatch_EndTurn1(EndTurn __instance)
        {
            if (bTimed)
            {
                if (Timed_Current() != null && BM.BattleWinLose.Type == WinLoseType.None)
                {
                    // 依然有人未行动，跳到BeginTurn开始其回合
                    __instance.SendEvent("BEGINTURN");
                    return false;
                }
                else
                {
                    // 同时结束所有人的回合，所有回调都要调
                    BM.OnBattleEvent(FSM.CurrentFaction != Faction.Enemy ? BattleEventToggleTime.EndAITurn : BattleEventToggleTime.EndTurn, Array.Empty<object>());
                    // 正常执行结束回合
                    return true;
                }
            }
            return true;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(EndTurn), "EndedTurn")]
        public static bool TimedPatch_EndTurn2(EndTurn __instance)
        {
            if (bTimed)
            {
                foreach (WuxiaUnit wuxiaUnit in BM.WuxiaUnits)
                {
                    wuxiaUnit.OnTurnEnd();
                    wuxiaUnit.OnUnitEnd();
                    wuxiaUnit.OnBufferEvent(BufferTiming.EndTurn);
                }
                return false;
            }
            return true;
        }

        // 6 AI！！
        public static async void SimpleAIAsync_Timed(AITurn state)
        {
            var t = Traverse.Create(state);
            BM.Time = BattleEventToggleTime.BeginAITurn;
            while (Timed_Current() != null && (Timed_Current().faction != Faction.Player || autoBattle.Value))
            {
                WuxiaUnit unit = Timed_Current();
                List<WuxiaUnit> second = (from u in BM.WuxiaUnits
                                            where u.faction == unit.faction
                                            select u).ToList<WuxiaUnit>();
                BM.WuxiaUnits.Except(second).ToList<WuxiaUnit>();
                if (Timed_GetBeginTurn(unit))
                {
                    unit.OnBufferEvent(BufferTiming.BeginTurn);
                    unit.CalculationNumber_Of_Movements();
                    Timed_SetBeginTurn(unit,false);
                }
                BM.OnBattleEvent(BattleEventToggleTime.BeginUnit, Array.Empty<object>());
                if (BM.IsEvent)
                {
                    Console.WriteLine("AI回合，等待事件結束");
                }
                else
                {
                    await 0.25f;
                    bool isMove = false;
                    bool isRest = true;
                    if (unit.IsBattleEventCube)
                    {
                        Timed_EndUnit(false, true);
                        continue;
                    }
                    unit.OnBufferEvent(BufferTiming.BeginUnit);
                    if (!t.Field("disable").GetValue<bool>())   //*state.disable*/
                    {
                        BM.CameraLookAt = unit.Cell.transform.position;
                        List<WuxiaCell> moveInRange = t.Method("ShowMoveRange", unit).GetValue<List<WuxiaCell>>();//state.ShowMoveRange(unit);
                        await 0.1f;
                        AIActionInfo useinfo = Traverse.Create(t.Method("GetBattleAI", unit).GetValue()).Method("Evaluate", moveInRange).GetValue<AIActionInfo>();//state.GetBattleAI(unit).Evaluate(moveInRange);
                        if (useinfo == null)
                        {
                            foreach (WuxiaCell wuxiaCell in moveInRange)
                            {
                                wuxiaCell.UnMark();
                            }
                            Timed_EndUnit(isMove, isRest);
                            UpdateTimedUI();
                            unit.ReCover();
                            BM.OnBattleEvent(BattleEventToggleTime.EndUnit, Array.Empty<object>());
                            unit.OnBufferEvent(BufferTiming.EndUnit);
                            continue;
                        }
                        if (useinfo.skill != null && useinfo.attackCell == unit.Cell)
                        {
                            useinfo.attackCell = useinfo.moveEnd;
                        }
                        AIActionInfo aiactionInfo = useinfo;
                        List<WuxiaCell> list = aiactionInfo?.path;
                        List<WuxiaCell> shortestPath = new List<WuxiaCell>();
                        int num = unit[BattleProperty.Move];
                        foreach (WuxiaCell wuxiaCell2 in moveInRange)
                        {
                            wuxiaCell2.UnMark();
                        }
                        if (list.HasData<WuxiaCell>())
                        {
                            foreach (WuxiaCell wuxiaCell3 in list)
                            {
                                shortestPath.Add(wuxiaCell3);
                                if (num == 0)
                                {
                                    break;
                                }
                                wuxiaCell3.Mark(CellMarkType.WalkPath);
                                num--;
                            }
                            unit.Move(shortestPath[0], shortestPath);
                            while (unit.IsMoving)
                            {
                                isMove = true;
                                await 0;
                            }
                            foreach (WuxiaCell wuxiaCell4 in shortestPath)
                            {
                                wuxiaCell4.UnMark();
                            }
                            unit.Actor.Move = false;
                        }
                        if (unit[BattleRestrictedState.Daze] > 0 || unit[BattleRestrictedState.Seal] > 0 || unit.IsAction)
                        {
                            unit.OnUnitEnd();
                        }
                        else if (useinfo.attackCell != null)
                        {
                            WuxiaCell point = useinfo.attackCell;
                            List<WuxiaCell> attackInRange = t.Method("ShowAttackRange", useinfo.skill, unit.Cell).GetValue<List<WuxiaCell>>();//state.ShowAttackRange(useinfo.skill, unit.Cell);
                            await 0.1f;
                            foreach (WuxiaCell wuxiaCell5 in attackInRange)
                            {
                                wuxiaCell5.UnMark();
                            }
                            List<WuxiaCell> targetInRange = state.GetTargetInRange(useinfo.skill, attackInRange, unit.Cell, point);
                            await 0.1f;
                            foreach (WuxiaCell wuxiaCell6 in targetInRange)
                            {
                                wuxiaCell6.UnMark();
                            }
                            List<WuxiaUnit> list2 = new List<WuxiaUnit>();
                            for (int i = 0; i < targetInRange.Count; i++)
                            {
                                if (targetInRange[i].Unit != null && Traverse.Create(BM).Method("CheckSkillUnit", targetInRange[i], useinfo.skill, unit).GetValue<bool>()/*BM.CheckSkillUnit(targetInRange[i], useinfo.skill, unit)*/ && !list2.Contains(targetInRange[i].Unit))
                                {
                                    list2.Add(targetInRange[i].Unit);
                                }
                            }
                            if (list2.Count > 0 || useinfo.skill.Item.DamageType == DamageType.Summon)
                            {
                                if (unit.SpecialSkill == "specialskill0101" && list2.Count > 0 && useinfo.skill.Item.DamageType == DamageType.Damage)
                                {
                                    t.Method("PlayChangeElement", unit.LearnedSkills["specialskill0101"], unit, list2).GetValue();//state.PlayChangeElement(unit.LearnedSkills["specialskill0101"], unit, list2);
                                }
                                t.Method("PlayAbility", useinfo.skill, point, unit, list2).GetValue();//state.PlayAbility(useinfo.skill, point, unit, list2);
                            }
                            while (t.Field("isPlayAbility").GetValue<bool>())//state.isPlayAbility)
                            {
                                BattleGlobalVariable.AddUsedSkill(unit.UnitID, useinfo.skill.Item.Id);
                                isRest = false;
                                await 0;
                            }
                            if (!(unit.SpecialSkill == useinfo.skill.Id))
                            {
                                unit.OnUnitEnd();
                            }
                        }
                        else
                        {
                            unit.OnUnitEnd();
                        }
                        if (unit.IsEndUnit)
                        {
                            if (!unit.IsDead)
                            {
                                Timed_EndUnit(isMove, isRest);
                                UpdateTimedUI();
                            }
                            BM.OnBattleEvent(BattleEventToggleTime.EndUnit, Array.Empty<object>());
                        }
                        if (!unit.IsDead)
                        {
                            unit.OnBufferEvent(BufferTiming.EndUnit);
                            continue;
                        }
                        continue;
                    }
                }
                return;
            }
            await 0.1f;
            while (BM.IsEvent)
            {
                Console.WriteLine("AI回合，等待事件結束");
                await 0.1f;
            }
            if (Timed_Current() != null)
            {
                t.Method("FirstUnitSelect").GetValue();//state.FirstUnitSelect();
            }
            FSM.SendEvent("ENDTURN");
        }
        [HarmonyPrefix, HarmonyPatch(typeof(AITurn), "OnEnable")]
        public static bool TimedPatch_AIEnable(AITurn __instance)
        {
            Console.WriteLine("AITurn.OnEnable()");
            var t = Traverse.Create(__instance);
            t.Method("initialization").GetValue();//__instance.initialization();

            Game.Input.Push(__instance);    //base.OnEnable();
            t.Field("disable").SetValue(false);//__instance.disable = false;
            if (bTimed)
            {
                SimpleAIAsync_Timed(__instance);
                return false;
            }
            t.Method("SimlpeAIAsync").GetValue();// __instance.SimlpeAIAsync();
            return false;
        }
    }

}
