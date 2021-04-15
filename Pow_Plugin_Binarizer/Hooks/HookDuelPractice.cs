using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;
using Heluo;
using Heluo.UI;
using Heluo.Data;
using Heluo.Utility;
using UnityEngine.UI;
using Heluo.Flow;

namespace PathOfWuxia
{
    // 双修系统（对练）
    public class HookDuelPractice : IHook
    {
        static ConfigEntry<bool> duelOn;
        static ConfigEntry<bool> duelNextRound;
        static ConfigEntry<string> duelCinematic;

        public void OnRegister(BaseUnityPlugin plugin)
        {
            duelOn = plugin.Config.Bind("结对练习", "结对练习开关", false, "开启结伴练习模式，开启后交友-出游指令会被替换为此模式");
            duelNextRound = plugin.Config.Bind("结对练习", "结对练习是否过回合", true, "结束后是否过回合");
            duelCinematic = plugin.Config.Bind("结对练习", "结对练习影片", "", "此功能需要mod支持");
        }

        public void OnUpdate()
        {
        }

        // UI Tips
        [HarmonyPrefix, HarmonyPatch(typeof(UIRelationship), "ShowTravelTip")]
        public static bool Duel_TravelTip(ref UIRelationship __instance)
        {
            var t = Traverse.Create(__instance);
            if (duelOn.Value && !t.Field("bCanRankUp").GetValue<bool>())
            {
                List<TipInfo> list = new List<TipInfo>
                {
                    __instance.CreateTipInfo(WGTip.TipType.BigTitle, "相互学习，可提升互补属性", ""),
                    __instance.CreateTipInfo(WGTip.TipType.FacilityContext, "精神-20", ""),
                    __instance.CreateTipInfo(WGTip.TipType.Title, Game.Data.Get<StringTable>(duelNextRound.Value?"SecondaryInterface1004":"SecondaryInterface1010").Text, "")
                };
                t.Field("travel_tip").Method("ShowTip", list).GetValue();
                return false;
            }
            return true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(CtrlNurturance), "ShowMovie")]
        public static bool Duel_TravelClick(ref CtrlNurturance __instance)
        {
            var t = Traverse.Create(__instance);
            var travelMovie = t.Field("UIInfo").Field("travelmovienumber");
            if (!travelMovie.GetValue<string>().IsNullOrEmpty() && duelOn.Value)
            {
                string s = travelMovie.GetValue<string>();
                Console.WriteLine("需要执行TravelMovie=" + s);
                if (s.EndsWith("0_00"))
                {
                    new RunNurturanceAction
                    {
                        cinematicId = s
                    }.GetValue();
                }
                else
                {
                    // Duel
                    GlobalLib.DuelInfoId = s.Substring(4, 6);   // m605[]
                    if (duelCinematic.Value.IsNullOrEmpty())
                    {
                        // 默认方式，很蠢
                        new NurturanceChangeBackground
                        {
                            backid = "M01_08_2D"
                        }.GetValue();
                        new NurturanceTransitionsAction
                        {
                            isTransitionOut = false
                        }.GetValue();
                        new RewardDuelProperty
                        {
                            toInfoId = GameConfig.Player,
                            fromInfoId = "##"   // GlobalLib.DuelInfoId replace
                        }.GetValue();
                        new RewardDuelProperty
                        {
                            toInfoId = "##",   // GlobalLib.DuelInfoId replace
                            fromInfoId = GameConfig.Player
                        }.GetValue();
                        new RewardEmotionAction
                        {
                            method = Method.Sub,
                            value = 20
                        }.GetValue();
                        new NurturanceTransitionsAction
                        {
                            isTransitionOut = true
                        }.GetValue();
                        if (duelNextRound.Value)
                        {
                            if (Game.GameData.Round.CurrentTime == (int)TimeType.Day)
                            {
                                new NurturanceLoadScenesAction
                                {
                                    mapId = "S0202",
                                    isNextTime = true,
                                    timeStage = Heluo.Manager.TimeStage.None
                                }.GetValue();
                            }
                            else
                            {
                                new NurturanceLoadScenesAction
                                {
                                    mapId = "S0202",
                                    isNextTime = false,
                                    timeStage = Heluo.Manager.TimeStage.Begin
                                }.GetValue();
                            }
                        }
                        else
                        {
                            new NurturanceLoadScenesAction
                            {
                                mapId = "S0202",
                                isNextTime = false,
                                timeStage = Heluo.Manager.TimeStage.None
                            }.GetValue();
                        }
                    }
                    else
                    {
                        new RunNurturanceAction
                        {
                            cinematicId = duelCinematic.Value
                        }.GetValue();
                    }
                }
                travelMovie.SetValue(string.Empty);
                return false;
            }
            return true;
        }
    }
}
