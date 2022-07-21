using Artemis;
using BepInEx.Configuration;
using HarmonyLib;
using Heluo;
using Heluo.Data;
using Heluo.Features;
using Heluo.Features.Wearing;
using Heluo.Flow;
using Heluo.UI;
using Heluo.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PathOfWuxia
{
    [System.ComponentModel.DisplayName("角色设定")]
    [System.ComponentModel.Description("修改建模、头像、姓名")]
    class HookCharacterExterior : IHook
    {
        static ConfigEntry<string> playerExteriorId;
        static ConfigEntry<string> playerPortraitOverride;
        static ConfigEntry<string> playerSurNameOverride;
        static ConfigEntry<string> playerNameOverride;
        static EventHandler ReplacePlayerExteriorDataEventHander;
        public void OnRegister(PluginBinarizer plugin)
        {
            playerExteriorId = plugin.Config.Bind("角色设定", "主角建模", string.Empty, "设定主角建模数据源");
            playerPortraitOverride = plugin.Config.Bind("角色设定", "主角头像", string.Empty, "若已设置建模，则可为空，使用建模的头像，否则用此头像代替");
            playerSurNameOverride = plugin.Config.Bind("角色设定", "主角姓", "亦", "可修改主角的姓");
            playerNameOverride = plugin.Config.Bind("角色设定", "主角名", "天凛", "可修改主角的名");

            ReplacePlayerExteriorDataEventHander += new EventHandler((o, e) =>
            {
                ReplacePlayerExteriorData();
            });

            playerExteriorId.SettingChanged += ReplacePlayerExteriorDataEventHander;
            playerPortraitOverride.SettingChanged += ReplacePlayerExteriorDataEventHander;
            playerSurNameOverride.SettingChanged += ReplacePlayerExteriorDataEventHander;
            playerNameOverride.SettingChanged += ReplacePlayerExteriorDataEventHander;
        }

        // 4 头像模型名称性别替换
        public static void ReplacePlayerExteriorData()
        {
            Console.WriteLine("ReplacePlayerExteriorData start");
            string[] characters = new string[] { GameConfig.Player, "in0196", "in0197", "in0101", "in0115" };
            for (int i = 0; i < characters.Length; i++)
            {
                //修改每一个主角模型
                CharacterExteriorData playerExteriorData = Game.GameData.Exterior[characters[i]];

                //判断有没有该模型，如有则替换
                if (playerExteriorData != null && !playerExteriorId.Value.Trim().IsNullOrEmpty())
                {
                    string text = playerExteriorId.Value.Trim();
                    bool hasModel = false;
                    Gender gender = Gender.Male;
                    Size size = Size.Free;
                    for (int j = 0; j < 5; j++)
                    {
                        for (int k = 0; k < 5; k++)
                        {
                            string text2 = string.IsNullOrEmpty(text) ? GameConfig.DefaultModelPath : string.Format(GameConfig.ModelPath, (Gender)j, (Size)k, text);
                            GameObject gameObject = Game.Resource.Load<GameObject>(text2);
                            if (gameObject != null)
                            {
                                hasModel = true;
                                gender = (Gender)j;
                                size = (Size)k;

                                break;
                            }
                        }
                        if (hasModel)
                        {
                            break;
                        }
                    }

                    if (hasModel)
                    {
                        //playerExteriorData.Id = kv.Value.Id;
                        playerExteriorData.Model = text;
                        playerExteriorData.Gender = gender;
                        playerExteriorData.Size = size;
                        //playerExteriorData.Protrait = kv.Value.Protrait;
                        //Console.WriteLine("id:"+kv.Value.Id+ ",Model:" + kv.Value.Model+ ",Gender:" + kv.Value.Gender + ",Size:" + kv.Value.Size + ",Protrait:" + kv.Value.Protrait + ",");
                        //break;
                    }
                }
                //判断有没有该立绘，如有则替换
                if (!playerPortraitOverride.Value.Trim().IsNullOrEmpty())
                {
                    string text = playerPortraitOverride.Value.Trim();
                    bool hasPortrait = false;

                    Sprite gameObject = Game.Resource.Load<Sprite>(string.Format(GameConfig.HalfProtraitPath, text));
                    if (gameObject != null)
                    {
                        hasPortrait = true;
                    }

                    if (hasPortrait)
                    {
                        playerExteriorData.Protrait = text;
                        //Console.WriteLine("id:"+kv.Value.Id+ ",Model:" + kv.Value.Model+ ",Gender:" + kv.Value.Gender + ",Size:" + kv.Value.Size + ",Protrait:" + kv.Value.Protrait + ",");
                        //break;
                    }
                }
                //修改姓名
                if (!playerSurNameOverride.Value.Trim().IsNullOrEmpty())
                {
                    playerExteriorData.SurName = playerSurNameOverride.Value.Trim();
                }
                if (!playerNameOverride.Value.Trim().IsNullOrEmpty())
                {
                    playerExteriorData.Name = playerNameOverride.Value.Trim();
                }
            }
            Console.WriteLine("ReplacePlayerExteriorData end");
        }
        //EnterGame之后会直接开始游戏，创建playerEntity，之后再执行InitialRewards。在InitialRewards后再替换模型就晚了一些
        [HarmonyPrefix, HarmonyPatch(typeof(UIRegistration), "EnterGame")]
        public static bool StartPatch_SetPlayerModel()
        {
            ReplacePlayerExteriorData();
            return true;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(PlayerTemplate), "BuildEntity")]
        public static bool PlayerTemplatePatch_BuildEntity()
        {
            ReplacePlayerExteriorData();
            return true;
        }
        [HarmonyPostfix, HarmonyPatch(typeof(ChangeCharacterProtraitAndModel), "GetValue")]
        public static void StartPatch_SetPlayerModel2(ChangeCharacterProtraitAndModel __instance, bool __result)
        {
            if (__instance.id == GameConfig.Player)
            {
                ReplacePlayerExteriorData();
            }
        }
        [HarmonyPostfix, HarmonyPatch(typeof(ChangeCharacterIdentity), "GetValue")]
        public static void StartPatch_SetPlayerModel3(ChangeCharacterIdentity __instance, bool __result)
        {
            if (__instance.id == GameConfig.Player && __result)
            {
                ReplacePlayerExteriorData();
            }
        }

        [HarmonyPostfix, HarmonyPatch(typeof(GameData), "Initialize")]
        public static void GameDataPatch_Initialize(GameData __instance)
        {
            Console.WriteLine("SteamPlatformPatch_LoadFileAsync start");
            playerSurNameOverride.SettingChanged -= ReplacePlayerExteriorDataEventHander;
            playerNameOverride.SettingChanged -= ReplacePlayerExteriorDataEventHander;

            playerSurNameOverride.Value = __instance.Exterior["Player"].SurName;
            playerNameOverride.Value = __instance.Exterior["Player"].Name;

            playerSurNameOverride.SettingChanged += ReplacePlayerExteriorDataEventHander;
            playerNameOverride.SettingChanged += ReplacePlayerExteriorDataEventHander;
            Console.WriteLine("SteamPlatformPatch_LoadFileAsync end");
        }

        [HarmonyPostfix, HarmonyPatch(typeof(CtrlRegistration), "SetLastName")]
        public static void CtrlRegistrationPatch_SetLastName(CtrlRegistration __instance, ref string value)
        {
            playerSurNameOverride.Value = value;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(CtrlRegistration), "SetFitstName")]
        public static void CtrlRegistrationPatch_SetFitstName(CtrlRegistration __instance, ref string value)
        {
            playerNameOverride.Value = value;
        }

        // 5 防止人物模型动作出现问题
        [HarmonyPrefix, HarmonyPatch(typeof(Heluo.Actor.ActorController), "OverrideStand", new Type[] { typeof(string) })]
        public static bool StartPatch_SetPlayerModel4(Heluo.Actor.ActorController __instance, ref string clipName)
        {
            AnimationClip animationClip = __instance.GetAnimationClip(clipName);
            if (animationClip == null)
            {
                int index = clipName.IndexOf("_special_await");
                if (index >= 0)
                {
                    Console.WriteLine("OverrideStand = " + clipName);
                    string ModelName = clipName.Substring(0, index);
                    var collection = from ce in Game.Data.Get<CharacterExterior>().Values where ce.Model == ModelName select ce;
                    if (collection.Count() > 0)
                    {
                        var characterExterior = collection.First();
                        if (!characterExterior.AnimMapId.IsNullOrEmpty())
                        {
                            clipName = string.Format("{0}_special_await{1:00}", characterExterior.AnimMapId, 0);
                            animationClip = __instance.GetAnimationClip(clipName);
                            if (animationClip == null)
                            {
                                var animMap = Game.Data.Get<AnimationMapping>(characterExterior.AnimMapId);
                                foreach (var (state, clip) in animMap)
                                {
                                    if (!clip.IsNullOrEmpty() && __instance.GetAnimationClip(clip) != null)
                                    {
                                        clipName = clip;
                                        break;
                                    }
                                }
                            }
                        }
                        animationClip = __instance.GetAnimationClip(clipName);
                        if (animationClip == null)
                            clipName = characterExterior.Gender == Gender.Male ? "in0101_special_await00" : "in0115_special_await00";
                    }
                }
            }
            return true;
        }

        // 修复某些人物模型无法调查的bug
        [HarmonyPrefix, HarmonyPatch(typeof(CtrlTarget), "OnTargetChanged")]
        public static bool CtrlTargetPatch_OnTargetChanged(CtrlTarget __instance,ref InteractiveInfo info)
        {
            Console.WriteLine("CtrlTargetPatch_OnTargetChanged start");

            Entity playerEntity = Game.EntityManager.GetPlayerEntity();
            AvatarComponent component = playerEntity.GetComponent<AvatarComponent>();
            Vector3 position = playerEntity.GetComponent<ObjectComponent>().Model.transform.position;
            Bounds bounds = new Bounds();
            if (component[RendererType.head] != null)
            {
                bounds = component[RendererType.head].bounds;
            }
            else if(component[RendererType.hair] != null)
            {
                bounds = component[RendererType.hair].bounds;
            }
            else if (component[RendererType.body] != null)
            {
                bounds = component[RendererType.body].bounds;
            }
            float d = bounds.max.y - position.y + 0.5f;
            Vector3 b = bounds.center - position;
            b.y = 0f;
            Traverse.Create(__instance).Field("worldPosition").SetValue(position + b + Vector3.up * d);
            ClickType clickType = info.ClickType;
            string id = string.Format("02004{0:000}", (int)clickType);
            StringTable stringTable = Game.Data.Get<StringTable>(id);
            string text = (stringTable != null) ? stringTable.Text : null;
            string remarks = info.Remarks;
            UITarget View = Traverse.Create(__instance).Field("View").GetValue<UITarget>();
            View.UpdateView(new string[]
            {
                text,
                remarks
            }, string.Empty);

            Console.WriteLine("CtrlTargetPatch_OnTargetChanged end");

            return false;
        }
    }
}
