using System.Collections.Generic;
using HarmonyLib;
using BepInEx.Configuration;
using Heluo;
using Heluo.UI;
using Heluo.Data;
using Heluo.Utility;
using Heluo.Flow;

namespace PathOfWuxia
{
    // 双修系统（对练）
    [System.ComponentModel.DisplayName("切磋开关")]
    [System.ComponentModel.Description("出游指令替换为切磋")]
    public class HookDuelPractice : IHook
    {
        static ConfigEntry<bool> duelOn;

        public void OnRegister(PluginBinarizer plugin)
        {
            duelOn = plugin.Config.Bind("扩展功能", "切磋开关", false, "开启后交友-出游指令会被替换为此模式");
            duelOn.SettingChanged += (o, e) =>
            {
                if (duelOn.Value)
                {
                    if (string.IsNullOrEmpty(Game.Resource?.LoadString("config/cinematic/mmod_duel.json")))
                        duelOn.Value = false;
                }
            };
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
                    __instance.CreateTipInfo(WGTip.TipType.Title, Game.Data.Get<StringTable>("SecondaryInterface1004").Text, "")
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
                // Duel
                string s = travelMovie.GetValue<string>();
                GlobalLib.SetReplaceText("[npc0]", s.Substring(4, 6));   // m605[]
                {
                    new RunCinematicAction
                    {
                        cinematicId = "mmod_duel"
                    }.GetValue();
                }
                travelMovie.SetValue(string.Empty);
                return false;
            }
            return true;
        }
    }
}
