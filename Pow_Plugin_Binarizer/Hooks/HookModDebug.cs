using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;
using Heluo;
using Heluo.Data;
using Heluo.Data.Converter;
using Heluo.Flow;
using Heluo.Battle;
using Heluo.Utility;
using Newtonsoft.Json;
using Heluo.FSM.Main;
using Heluo.UI;

namespace PathOfWuxia
{
    // Mod辅助扩展
    public class HookModDebug : IHook
    {
        public void OnRegister(BaseUnityPlugin plugin)
        {
            var adv1 = new ConfigDescription("", null, new ConfigurationManagerAttributes { IsAdvanced = true, Order = 3 });
            DebugOn = plugin.Config.Bind("Debug功能", "调试开关", false, adv1);
            DebugOutDir = plugin.Config.Bind("Debug功能", "调试路径", "export/", adv1);

            var adv = new ConfigDescription("", null, new ConfigurationManagerAttributes { IsAdvanced = true });
            NodeDocKey = plugin.Config.Bind("Debug功能", "OutputNode说明文档导出", KeyCode.P, adv);
            NodeContent = plugin.Config.Bind("Debug功能", "OutputNode内容(原始格式)", "", adv);
            NodeFileKey = plugin.Config.Bind("Debug功能", "OutputNode输出(Json格式)", KeyCode.O, adv);
            NodeFilePath = plugin.Config.Bind("Debug功能", "OutputNode输出路径", "OutputNode.json", adv);
            JsonFormat = plugin.Config.Bind("Debug功能", "导出时使用json格式", true, adv);
            JsonPritty = plugin.Config.Bind("Debug功能", "导出json是否格式化", true, adv);
            BattleFileId = plugin.Config.Bind("Debug功能", "战斗文件Id", "", adv);
            BattleFileKey = plugin.Config.Bind("Debug功能", "战斗文件保存键", KeyCode.B, adv);
            BattleFilePath = plugin.Config.Bind("Debug功能", "战斗文件保存路径", "battle/{0}.json", adv);
            BuffFileId = plugin.Config.Bind("Debug功能", "Buff文件Id", "", adv);
            BuffFileKey = plugin.Config.Bind("Debug功能", "Buff文件保存键", KeyCode.N, adv);
            BuffFilePath = plugin.Config.Bind("Debug功能", "Buff文件保存路径", "buff/{0}.json", adv);
            MovieFileType = plugin.Config.Bind("Debug功能", "过场文件类型", MovieType.Cinematic, adv);
            MovieFileId = plugin.Config.Bind("Debug功能", "过场文件Id", "", adv);
            MovieFileKey = plugin.Config.Bind("Debug功能", "过场文件保存键", KeyCode.M, adv);
            MovieFilePath = plugin.Config.Bind("Debug功能", "过场文件保存路径", "movie/{0}.json", adv);
            SortSchedule = plugin.Config.Bind("Debug功能", "过场是否重新排序", false, adv);
            SortSchedule.SettingChanged += (o, e) => { ScheduleGraphConverter.WriteSorted = SortSchedule.Value; };
        }

        enum MovieType
        {
            Cinematic,
            Scheduler
        }
        private static ConfigEntry<bool> DebugOn;
        private static ConfigEntry<string> DebugOutDir;
        private static ConfigEntry<KeyCode> NodeDocKey;
        private static ConfigEntry<string> NodeContent;
        private static ConfigEntry<KeyCode> NodeFileKey;
        private static ConfigEntry<string> NodeFilePath;
        private static ConfigEntry<bool> JsonFormat;
        private static ConfigEntry<bool> JsonPritty;
        private static ConfigEntry<string> BattleFileId;
        private static ConfigEntry<KeyCode> BattleFileKey;
        private static ConfigEntry<string> BattleFilePath;
        private static ConfigEntry<string> BuffFileId;
        private static ConfigEntry<KeyCode> BuffFileKey;
        private static ConfigEntry<string> BuffFilePath;
        private static ConfigEntry<MovieType> MovieFileType;
        private static ConfigEntry<string> MovieFileId;
        private static ConfigEntry<KeyCode> MovieFileKey;
        private static ConfigEntry<string> MovieFilePath;
        private static ConfigEntry<bool> SortSchedule;

        public void OnUpdate()
        {
            if (!DebugOn.Value)
                return;
            if (Input.GetKeyDown(NodeDocKey.Value))
            {
                string target = DebugOutDir.Value + "NodeHelpDoc.json";
                ModOutputNodeConverter.ExportDoc(target);
            }
            if (Input.GetKeyDown(NodeFileKey.Value) && !string.IsNullOrEmpty(NodeContent.Value))
            {
                // OutputNode
                string target = DebugOutDir.Value + NodeFilePath.Value;
                OutputNode obj = OutputNodeConvert.Deserialize(NodeContent.Value);
                var strJsonMod = ModJson.ToJsonMod(obj, typeof(OutputNode), JsonPritty.Value);
                Console.WriteLine("Json版 = " + strJsonMod);
                GlobalLib.ToFile(strJsonMod, target);
            }
            if (Input.GetKeyDown(MovieFileKey.Value) && !string.IsNullOrEmpty(MovieFileId.Value))
            {
                // movie                
                string source = string.Format(MovieFileType.Value == MovieType.Cinematic ? GameConfig.CinematicPath : GameConfig.SchedulerPath, MovieFileId.Value);
                string target = string.Format(DebugOutDir.Value + MovieFilePath.Value, MovieFileId.Value);
                var obj = ModJson.FromJsonResource<ScheduleGraph.Bundle>(source);
                if (JsonFormat.Value)
                {
                    var strJsonMod = ModJson.ToJsonMod(obj, typeof(ScheduleGraph.Bundle), JsonPritty.Value);
                    Console.WriteLine("Json版 = " + strJsonMod);
                    GlobalLib.ToFile(strJsonMod, target);

                    // 测试读取并对比重新通过Json构建的是否有差
                    var obj2 = ModJson.FromJsonMod<ScheduleGraph.Bundle>(strJsonMod);
                    JsonSerializerSettings originalSetting = new JsonSerializerSettings
                    {
                        Converters = new JsonConverter[]
                        {
                        new OutputNodeJsonConverter()
                        }
                    };
                    string str2 = ModJson.ToJson(obj2, typeof(ScheduleGraph.Bundle), originalSetting, JsonPritty.Value);
                    Console.WriteLine("重构脚本 = " + str2);
                }
                else
                {
                    GlobalLib.ToFile(Game.Resource.LoadString(source), target);
                }
            }
            if (Input.GetKeyDown(BattleFileKey.Value) && !string.IsNullOrEmpty(BattleFileId.Value))
            {
                // battle schedule
                string source = string.Format(GameConfig.BattleSchedulePath, GameConfig.Language, BattleFileId.Value + ".json");
                string target = string.Format(DebugOutDir.Value + BattleFilePath.Value, BattleFileId.Value);
                if (JsonFormat.Value)
                {
                    BattleSchedule obj = ModJson.FromJsonResource<BattleSchedule>(source);
                    var strJsonMod = ModJson.ToJsonMod(obj, typeof(BattleSchedule), JsonPritty.Value);
                    Console.WriteLine("Json版 = " + strJsonMod);
                    GlobalLib.ToFile(strJsonMod, target);
                }
                else
                {
                    GlobalLib.ToFile(Game.Resource.LoadString(source), target);
                }
            }
            if (Input.GetKeyDown(BuffFileKey.Value) && !string.IsNullOrEmpty(BuffFileId.Value))
            {
                // buff
                string source = string.Format(GameConfig.ButtleBufferPath, GameConfig.Language, BuffFileId.Value + ".json");
                string target = string.Format(DebugOutDir.Value + BuffFilePath.Value, BuffFileId.Value);
                if (JsonFormat.Value)
                {
                    Heluo.Data.Buffer obj = ModJson.FromJsonResource<Heluo.Data.Buffer>(source);
                    var strJsonMod = ModJson.ToJsonMod(obj, typeof(Heluo.Data.Buffer), true);
                    Console.WriteLine("Json版 = " + strJsonMod);
                    GlobalLib.ToFile(strJsonMod, target);
                }
                else
                {
                    GlobalLib.ToFile(Game.Resource.LoadString(source), target);
                }
            }
        }

        // 显示BuffInfo
        static void AppendInfoAndDisplay(WGAbilityInfo __instance, string str)
        {
            var t = Traverse.Create(__instance);
            t.Field("infos").Method("Add", __instance.CreateTipInfo(WGTip.TipType.Context, str, "")).GetValue();
            t.Field("ability_tip").Method("ShowTip", new Type[] { typeof(List<TipInfo>) }, new object[] { t.Field("infos").GetValue() }).GetValue();
        }
        [HarmonyPostfix, HarmonyPatch(typeof(WGAbilityInfo), "ShowTip", new Type[] {typeof(BufferInfo) })]
        public static void Patch_DisplayBuffId(WGAbilityInfo __instance, BufferInfo info)
        {
            if (DebugOn.Value)
            {
                BuffFileId.Value = info.BufferId;
                AppendInfoAndDisplay(__instance, string.Format("BuffID=[{0}]", info.BufferId));
            }
        }
        [HarmonyPostfix, HarmonyPatch(typeof(WGAbilityInfo), "ShowTip", new Type[] { typeof(MantraData) })]
        public static void Patch_DisplayMantraBuffId(WGAbilityInfo __instance, MantraData data)
        {
            if (DebugOn.Value)
            {
                if (data.Item.BufferEffects != null && data.Item.BufferEffects.Count > 0)
                {
                    var bs = from b in data.Item.BufferEffects select string.Format("{0}:[{1}]", b.MartraLevel, b.BufferId);
                    AppendInfoAndDisplay(__instance, string.Format("Buffs={0}", string.Join(",", bs)));
                }         
            }
        }
        [HarmonyPostfix, HarmonyPatch(typeof(WGAbilityInfo), "ShowTip", new Type[] { typeof(SkillData) })]
        public static void Patch_DisplaySkillBuffId(WGAbilityInfo __instance, SkillData skill)
        {
            if (DebugOn.Value)
            {
                string s = "";
                if (skill.Item.SelfBuffList != null && skill.Item.SelfBuffList.Count > 0)
                {
                    var bs = from b in skill.Item.SelfBuffList select b;
                    s += string.Format("SelfBuffs=[{0}]\n", string.Join(",", bs));
                }
                if (skill.Item.TargetBuffList != null && skill.Item.TargetBuffList.Count > 0)
                {
                    var bs = from b in skill.Item.TargetBuffList select b;
                    s += string.Format("TargetBuffs=[{0}]", string.Join(",", bs));
                }
                if (!string.IsNullOrEmpty(s))
                    AppendInfoAndDisplay(__instance, s);
            }
        }
        [HarmonyPostfix, HarmonyPatch(typeof(WGAbilityInfo), "ShowTip", new Type[] { typeof(Props) })]
        public static void Patch_DisplayPropsBuffId(WGAbilityInfo __instance, Props props)
        {
            if (DebugOn.Value)
            {
                if (props.BuffList != null && props.BuffList.Count > 0)
                {
                    var bs = from b in props.BuffList select b;
                    AppendInfoAndDisplay(__instance, string.Format("Buffs=[{0}]", string.Join(",", bs)));
                }
            }
        }

        // 捕捉dump文件
        [HarmonyPostfix, HarmonyPatch(typeof(InCinematic), "Execute")]
        public static void Patch_MovieId(InCinematic __instance)
        {
            MovieFileType.Value = MovieType.Cinematic;
            CinematicEventArgs cinematicEventArgs = Traverse.Create(Game.FSM).Property("eventArgs").GetValue() as CinematicEventArgs;
            MovieFileId.Value = cinematicEventArgs?.CinematicId;
        }
        [HarmonyPostfix, HarmonyPatch(typeof(WuxiaBattleSchedule), "InitBattleScheduleData", new Type[] { typeof(string) })]
        public static void Patch_BattleId(string ScheduleID)
        {
            BattleFileId.Value = ScheduleID;
        }

    }
}
