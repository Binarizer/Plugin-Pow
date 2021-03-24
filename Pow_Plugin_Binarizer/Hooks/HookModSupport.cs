using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Timers;
using HarmonyLib;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;
using Heluo;
using Heluo.UI;
using Heluo.Data;
using Heluo.Battle;
using Heluo.Resource;
using Heluo.Utility;
using FileHelpers;

namespace PathOfWuxia
{
    // Mod支持
    public class HookModSupport : IHook
    {
        public void OnRegister(BaseUnityPlugin plugin)
        {
            modPath = plugin.Config.Bind("Mod设置", "Mod路径", "", "该项必须启动前从设置中修改才可生效");
            modTheme = plugin.Config.Bind("Mod设置", "Mod主菜单音乐", "", "下次进入主菜单生效");
            modCustomVoice = plugin.Config.Bind("Mod设置", "Mod语音开关", false, "吧友配音");
            modBattleVoicePath = plugin.Config.Bind("Mod设置", "Mod战斗语音路径", "audio/voice/um_{0}_{1}.ogg", "可更改相对路径和扩展名");
            modTalkVoicePath = plugin.Config.Bind("Mod设置", "Mod对话语音路径", "audio/voice/talk_{0}.ogg", "可更改相对路径和扩展名");
        }

        public void OnUpdate()
        {
        }

        static ConfigEntry<string> modPath;
        static ConfigEntry<string> modTheme;
        static ConfigEntry<bool> modCustomVoice;
        static ConfigEntry<string> modBattleVoicePath;
        static ConfigEntry<string> modTalkVoicePath;

        // 最简单的方式：更改ExternalResourceProvider的外部路径，但无法加载音乐
        //[HarmonyPostfix, HarmonyPatch(typeof(ExternalResourceProvider), MethodType.Constructor, new Type[] { typeof(ICoroutineRunner), typeof(Heluo.Mod.IModManager) })]
        //public static void ModPatch_Constructor(ExternalResourceProvider __instance)
        //{
        //    Traverse.Create(__instance).Field("ExternalDirectory").SetValue(modPath.Value);
        //}

        // 尝试过挂接泛型函数T Load<T>(string)，但不好用，他总会使用最后一个类的泛型注入导致其他类型无法读取，为Harmony固有问题。
        // 故使用插件的新类（抄的ExternalResourceProvider）加载外部资源，用于加载音乐等资源，绕过泛型的坑
        [HarmonyPostfix, HarmonyPatch(typeof(ResourceManager), "Reset", new Type[] { typeof(ICoroutineRunner), typeof(Heluo.Mod.IModManager), typeof(Type[]) })]
        public static void ModPatch_Reset(ResourceManager __instance, ICoroutineRunner runner)
        {
            if (!modPath.Value.IsNullOrEmpty() && Directory.Exists(Path.GetFullPath(modPath.Value)))
            {
                var provider = Traverse.Create(__instance).Field("provider").GetValue<IChainedResourceProvider>();
                var thirdSuccessor = provider.Successor.Successor;
                var modResourceProvider = new ModResourceProvider(runner, modPath.Value);
                provider.Successor = modResourceProvider;
                modResourceProvider.Successor = thirdSuccessor;

                Console.WriteLine("当前ResourceProvider链表: ");
                while (provider != null)
                {
                    Console.WriteLine(provider.GetType().ToString());
                    provider = provider.Successor;
                }
            }
        }

        // 1 Mod支持增删表格
        [HarmonyPostfix, HarmonyPatch(typeof(DataManager), "ReadData", new Type[] { typeof(string) })]
        public static void ModPatch_DataAppendRemove(ref DataManager __instance, string path)
        {
            var dict = Traverse.Create(__instance).Field("dict").GetValue() as Dictionary<Type, System.Collections.IDictionary>;
            Type type = typeof(Item);
            foreach (Type itemType in from t in type.Assembly.GetTypes() where t.IsSubclassOf(type) && !t.HasAttribute<Hidden>(false) select t)
            {
                if (!itemType.HasAttribute<JsonConfig>(false) && dict.ContainsKey(itemType))
                {
                    Type csvType = typeof(CsvDataSource<>).MakeGenericType(new Type[] { itemType });
                    var dic = dict[itemType];
                    Console.WriteLine("dic=" + dic);
                    try
                    {
                        byte[] array3 = Game.Resource.LoadBytes(path + itemType.Name + "_modify.txt");
                        if (array3 != null)
                        {
                            var dicModify = (Activator.CreateInstance(csvType, new object[]
                            {
                                    array3
                            }) as System.Collections.IDictionary);
                            foreach (var key in dicModify.Keys)
                            {
                                if (dic.Contains(key))
                                    dic[key] = dicModify[key];
                                else
                                    dic.Add(key, dicModify[key]);
                            }
                        }

                        byte[] array4 = Game.Resource.LoadBytes(path + itemType.Name + "_remove.txt");
                        if (array4 != null)
                        {
                            var dicRemove = (Activator.CreateInstance(csvType, new object[]
                            {
                                    array4
                            }) as System.Collections.IDictionary);
                            foreach (var key in dicRemove.Keys)
                            {
                                if (dic.Contains(key))
                                    dic.Remove(key);
                            }
                        }
                    }
                    catch (ConvertException ex)
                    {
                        Debug.LogError(string.Concat(new object[]
                        {
                        "增删 ",
                        itemType.Name,
                        " 時發生錯誤 !!\r\n行數 : ",
                        ex.LineNumber,
                        ", 欄位 : ",
                        ex.ColumnNumber,
                        ", 類型 = ",
                        ex.FieldType.Name,
                        ", 名稱 = ",
                        ex.FieldName,
                        "\r\n",
                        ex
                        }));
                    }
                }
            }
        }
        // 2 修改主题音乐
        [HarmonyPrefix, HarmonyPatch(typeof(MusicPlayer), "ChangeMusic", new Type[] { typeof(string), typeof(float), typeof(float), typeof(bool), typeof(bool), typeof(bool) })]
        public static bool ModPatch_ChangeTheme(ref string _name)
        {
            if (_name == "In_theme_01.wav" && modTheme.Value != string.Empty)
                _name = modTheme.Value;
            return true;
        }

        // 3 玩家自定义配音-战斗
        private static Dictionary<string, List<AudioClip>> _battleVoices = new Dictionary<string, List<AudioClip>>();
        private static System.Timers.Timer _voiceTimer;

        public static void PlayCustomizedVoice(AudioClip clip)
        {
            AudioSource ss = Traverse.Create(Game.MusicPlayer).Field("single_source").GetValue<AudioSource>();
            var currVol = Traverse.Create(Game.MusicPlayer).Field("current_volume_percent");
            if (ss == null)
            {
                return;
            }
            ss.Stop();
            ss.spatialBlend = 0f;
            ss.gameObject.transform.localPosition = Vector3.zero;
            ss.volume = GameConfig.SoundVolume;
            ss.PlayOneShot(clip);
            currVol.SetValue(0.2f);
            Game.MusicPlayer.SetVolume();
            if (_voiceTimer == null)
            {
                _voiceTimer = new System.Timers.Timer();
                _voiceTimer.Elapsed += delegate (object source, ElapsedEventArgs e)
                {
                    currVol.SetValue(1f);
                    Game.MusicPlayer.SetVolume();
                };
            }
            _voiceTimer.Stop();
            _voiceTimer.Interval = (double)(clip.length * 1000f);
            _voiceTimer.Start();
        }
        public static void PlayCvByCharacter(string id)
        {
            List<AudioClip> list;
            if (!_battleVoices.ContainsKey(id))
            {
                list = new List<AudioClip>();
                for (int i = 0; i < 5; i++)
                {
                    AudioClip audioClip = Game.Resource.Load<AudioClip>(string.Format(modBattleVoicePath.Value, id, i));
                    if (audioClip != null)
                    {
                        list.Add(audioClip);
                    }
                }
                _battleVoices.Add(id, list);
            }
            else
            {
                list = _battleVoices[id];
            }
            if (list.Count > 0)
            {
                PlayCustomizedVoice(list[UnityEngine.Random.Range(0, list.Count)]);
            }
        }
        [HarmonyPostfix, HarmonyPatch(typeof(UIBattle), "OpenBattleStatus", new Type[] { typeof(WuxiaUnit) })]
        public static void ModPatch_BattleVoice(WuxiaUnit _unit)
        {
            if (modCustomVoice.Value)
            {
                PlayCvByCharacter(_unit.ExteriorId);
            }
        }
        // 4 玩家自定义配音-对话
        public static bool PlayCvByPath(string soundPath)
        {
            AudioClip audioClip = Game.Resource.Load<AudioClip>(soundPath);
            if (audioClip == null)
            {
                return false;
            }
            PlayCustomizedVoice(audioClip);
            return true;
        }
        [HarmonyPostfix, HarmonyPatch(typeof(CtrlTalk), "SetMessageView", new Type[] { typeof(Talk) })]
        public static void ModPatch_TalkVoice(Talk talk)
        {
            if (modCustomVoice.Value)
            {
                PlayCvByPath(string.Format(modTalkVoicePath.Value, talk.Id));
            }
        }
    }
}
