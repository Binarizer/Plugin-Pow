using System;
using System.Text;
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
using FileHelpers;
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
            DebugOn = plugin.Config.Bind("Debug功能", "总开关", false, adv1);
            var adv = new ConfigDescription("", null, new ConfigurationManagerAttributes { IsAdvanced = true });
            PrittyPrinting = plugin.Config.Bind("Debug功能", "是否格式化", true, adv);
            BattleFileId = plugin.Config.Bind("Debug功能", "战斗文件Id", "", adv);
            BattleFileKey = plugin.Config.Bind("Debug功能", "战斗文件保存键", KeyCode.B, adv);
            BattleFilePath = plugin.Config.Bind("Debug功能", "战斗文件保存路径", "/battle/{0}.json", adv);
            BuffFileId = plugin.Config.Bind("Debug功能", "Buff文件Id", "", adv);
            BuffFileKey = plugin.Config.Bind("Debug功能", "Buff文件保存键", KeyCode.N, adv);
            BuffFilePath = plugin.Config.Bind("Debug功能", "Buff文件保存路径", "/buff/{0}.json", adv);
            MovieFileType = plugin.Config.Bind("Debug功能", "过场文件类型", MovieType.Cinematic, adv);
            MovieFileId = plugin.Config.Bind("Debug功能", "过场文件Id", "", adv);
            MovieFileKey = plugin.Config.Bind("Debug功能", "过场文件保存键", KeyCode.M, adv);
            MovieFilePath = plugin.Config.Bind("Debug功能", "过场文件保存路径", "/movie/{0}.json", adv);
        }

        enum MovieType
        {
            Cinematic,
            Scheduler
        }
        private static ConfigEntry<bool> DebugOn;
        private static ConfigEntry<bool> PrittyPrinting;
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

        public void OnUpdate()
        {
            if (!DebugOn.Value)
                return;
            if (Input.GetKeyDown(MovieFileKey.Value) && !string.IsNullOrEmpty(MovieFileId.Value))
            {
                // movie                
                string original = Game.Resource.LoadString(string.Format(MovieFileType.Value == MovieType.Cinematic?GameConfig.CinematicPath:GameConfig.SchedulerPath, MovieFileId.Value));
                string target = string.Format(MovieFilePath.Value, MovieFileId.Value);
                // 官方读取设定
                JsonSerializerSettings originalSetting = new JsonSerializerSettings
                {
                    Converters = new JsonConverter[]
                    {
                        new OutputNodeJsonConverter()
                    }
                };
                var obj = ModJson.FromJson<ScheduleGraph.Bundle>(original, originalSetting);
                Console.WriteLine("obj.Type =" + obj?.GetType());
                var strJsonMod = ModJson.ToJsonMod(obj, typeof(ScheduleGraph.Bundle), target, true);
                Console.WriteLine("Json版 = " + strJsonMod);

                // 测试读取并对比重新通过Json构建的是否有差
                var obj2 = ModJson.FromJson<ScheduleGraph.Bundle>(strJsonMod, originalSetting);   // 这里由于不好改constructor, 就把原版读取兼容了json模式
                string str2 = ModJson.ToJson(obj2, typeof(ScheduleGraph.Bundle), originalSetting, true);
                Console.WriteLine("原始脚本 = " + original);
                Console.WriteLine("重构脚本 = " + str2);
            }
            if (Input.GetKeyDown(BattleFileKey.Value) && !string.IsNullOrEmpty(BattleFileId.Value))
            {
                // battle schedule
                string source = Game.Resource.LoadString(string.Format(GameConfig.BattleSchedulePath, GameConfig.Language, BattleFileId.Value + ".json"));
                BattleSchedule dataObj = new FileHelperEngine<BattleSchedule>(Encoding.UTF8).ReadString(source)[0];
                var obj = dataObj.BattleSchedules.Output;
                string target = string.Format(BattleFilePath.Value, BattleFileId.Value);
                ExportOutputNode(obj, target);
            }
            if (Input.GetKeyDown(BuffFileKey.Value) && !string.IsNullOrEmpty(BuffFileId.Value))
            {
                // buff
                string source = Game.Resource.LoadString(string.Format(GameConfig.ButtleBufferPath, GameConfig.Language, BuffFileId.Value + ".json"));
                Heluo.Data.Buffer dataObj = new FileHelperEngine<Heluo.Data.Buffer>(Encoding.UTF8).ReadString(source)[0];
                var obj = dataObj.BufferEffect.Output;
                string target = string.Format(BuffFilePath.Value, BuffFileId.Value);
                ExportOutputNode(obj, target);
            }
        }

        void ExportOutputNode(IOutput obj, string target)
        {
            Console.WriteLine("obj.Type =" + obj.GetType());
            var strJsonMod = ModJson.ToJsonMod(obj, typeof(OutputNode), target, true);
            Console.WriteLine("Json版 = " + strJsonMod);

            // 对比重新通过Json构建的是否有差
            var obj2 = ModJson.FromJsonMod<OutputNode>(strJsonMod);
            string str2 = OutputNodeConvert.Serialize(obj2);
            Console.WriteLine("重构脚本 = " + str2);
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
        //[HarmonyPostfix, HarmonyPatch(typeof(WuxiaBattleBuffer), "AddBuffer", new Type[] { typeof(WuxiaUnit), typeof(string), typeof(bool), typeof(bool) })]
        //public static void Patch_BuffId(string bufferId)
        //{
        //    BuffFileId.Value = bufferId;
        //}

        // Movie加载
        [HarmonyPrefix, HarmonyPatch(typeof(OutputNodeJsonConverter), "ReadJson", new Type[] { typeof(JsonReader), typeof(Type), typeof(object), typeof(JsonSerializer) })]
        public static bool Patch_MovieLoadJson(ref object __result, JsonReader reader)
        {
            if (reader.TokenType == JsonToken.String)
                __result = OutputNodeConvert.Deserialize(reader.Value.ToString());
            else
            {
                // 增加json加载
                //Console.WriteLine("检测到Json模式Node!");
                __result = ModJson.FromReaderMod<OutputNode>(reader);
            }
            return false;
        }
        // Buff/Battle 加载
        [HarmonyPrefix, HarmonyPatch(typeof(OutputNodeConvert), "Deserialize", new Type[] { typeof(string) })]
        public static bool Patch_JsonConvert(string str, ref OutputNode __result)
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
                    __result = ModJson.FromJsonMod<OutputNode>(content);
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
    }
}
