using System;
using HarmonyLib;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;
using Heluo;
using Heluo.Data;
using Heluo.Flow;
using Heluo.Battle;
using Heluo.Components;
using Heluo.Controller;

namespace PathOfWuxia
{
    // 一般游戏设定功能
    public class HookGenerals : IHook
    {
        enum ProbablyMode
        {
            None,
            SmallChance,
            FixedRandomValue
        }
        static ConfigEntry<float> speedValue;
        static ConfigEntry<KeyCode> speedKey;
        static ConfigEntry<GameLevel> difficulty;
        static ConfigEntry<ProbablyMode> probablyMode;
        static ConfigEntry<int> probablyValue;
        enum CameraFocusMode
        {
            Attacker,
            Defender,
            Defender_OnHit
        }
        static ConfigEntry<CameraFocusMode> cameraFocusMode;
        static ConfigEntry<bool> cameraFree;
        static ConfigEntry<bool> cameraFree_Battle;

        public void OnRegister(BaseUnityPlugin plugin)
        {
            speedValue = plugin.Config.Bind("游戏设定", "速度值", 1.5f, "调整速度值");
            speedKey = plugin.Config.Bind("游戏设定", "速度热键", KeyCode.F2, "开关速度调节");
            difficulty = plugin.Config.Bind("游戏设定", "难度值", GameLevel.Normal, "调节游戏难度");
            difficulty.SettingChanged += OnGameLevelChange;
            probablyMode = plugin.Config.Bind("游戏设定", "随机事件方式", ProbablyMode.None, "None-原版 SmallChance-小概率事件必发生 FixedRandomValue-设定产生的随机数");
            probablyValue = plugin.Config.Bind("游戏设定", "随机事件值", 50, "SmallChance：多少被界定为小概率 FixedRandomValue：1~100对应必发生/必不发生");

            cameraFocusMode = plugin.Config.Bind("相机设置", "战斗相机跟随方式", CameraFocusMode.Attacker, "战斗时相机如何跟随，游戏默认跟随攻击者");
            cameraFree = plugin.Config.Bind("相机设置", "场景自由视角", false, "是否开启自由视角");
            cameraFree_Battle = plugin.Config.Bind("相机设置", "战斗自由视角", false, "是否开启战斗自由视角，重启战斗生效");
        }
        public void OnUpdate()
        {
            if (Input.GetKeyDown(speedKey.Value))
            {
                Time.timeScale = Time.timeScale == 1.0f ? Math.Max(0.1f, speedValue.Value) : 1.0f;
            }

            // sync difficulty
            if (Game.GameData.GameLevel != difficulty.Value)
                difficulty.Value = Game.GameData.GameLevel;
        }

        static void OnGameLevelChange(object o, EventArgs e)
        {
            if (Game.GameData != null)
            {
                Game.GameData.GameLevel = difficulty.Value;
            }
        }

        // 1 Sync await by timeScale for speed correct
        [HarmonyPrefix, HarmonyPatch(typeof(AsyncTools), "GetAwaiter", new Type[] { typeof(float) })]
        public static bool SpeedPatch(ref float seconds)
        {
            seconds = seconds / Time.timeScale;
            return true;
        }

        // 2 事件随机数调节
        [HarmonyPostfix, HarmonyPatch(typeof(Probability), "GetValue")]
        public static void ProbabilityPatch(Probability __instance, ref bool __result)
        {
            if (probablyMode.Value == ProbablyMode.FixedRandomValue)
            {
                __result = (probablyValue.Value - 1 < __instance.value);
            }
            else if (probablyMode.Value == ProbablyMode.SmallChance)
            {
                __result = (__instance.value < probablyValue.Value || __instance.value == 100f);
            }
        }

        // 3 战斗相机跟随模式
        [HarmonyPostfix, HarmonyPatch(typeof(BattleProcessStrategy), "ProcessAnimation", new Type[] { typeof(DamageInfo), typeof(float) })]
        public static void CameraPatch_FocusMode(BattleProcessStrategy __instance, DamageInfo damageInfo)
        {
            if (cameraFocusMode.Value == CameraFocusMode.Defender)
            {
                if (damageInfo != null)
                {
                    for (int i = 0; i < damageInfo.damages.Count; i++)
                    {
                        Damage damage = damageInfo.damages[i];
                        if (i == 0)
                        {
                            var manager = Traverse.Create(__instance).Field("manager").GetValue<WuxiaBattleManager>();
                            manager.CameraLookAt = damage.Defender.Cell.transform.position;
                        }
                    }
                }
            }
        }
        [HarmonyPostfix, HarmonyPatch(typeof(BattleProcessStrategy), "HitTarget", new Type[] { typeof(bool) })]
        public static void CameraPatch_FocusMode2(BattleProcessStrategy __instance)
        {
            if (cameraFocusMode.Value == CameraFocusMode.Defender_OnHit)
            {
                var damageInfo = Traverse.Create(__instance).Field("damageInfo").GetValue<DamageInfo>();
                if (damageInfo != null)
                {
                    for (int i = 0; i < damageInfo.damages.Count; i++)
                    {
                        Damage damage = damageInfo.damages[i];
                        if (i == 0)
                        {
                            var manager = Traverse.Create(__instance).Field("manager").GetValue<WuxiaBattleManager>();
                            manager.CameraLookAt = damage.Defender.Cell.transform.position;
                        }
                    }
                }
            }
        }

        // 4 战斗自由视角
        [HarmonyPostfix, HarmonyPatch(typeof(GameCamera), "SetBattleCamera")]
        public static void CameraPatch_FreeBattle(GameCamera __instance)
        {
            if (cameraFree_Battle.Value)
            {
                __instance.minDistance = 3f;
                __instance.ylocked = false;
            }
        }

        // 5 平时自由视角
        [HarmonyPrefix, HarmonyPatch(typeof(CameraController), "UpdateLimitFollow", new Type[] { typeof(float) })]
        public static bool CameraPatch_Free1(CameraController __instance, float deltaTime)
        {
            if (cameraFree.Value)
            {
                __instance.UpdateTransform(deltaTime);
                return false;
            }
            return true;
        }
        [HarmonyPostfix, HarmonyPatch(typeof(CameraController), "OnCameraDrag", new Type[] { typeof(float), typeof(float) })]
        public static void CameraPatch_Free2(CameraController __instance, float dx, float dy)
        {
            if (cameraFree.Value)
            {
                var cameraMode = Traverse.Create(__instance).Field("mode").GetValue<GameCamera.CameraMode>();
                if (cameraMode == GameCamera.CameraMode.LimitFollow)
                {
                    var param = Traverse.Create(__instance).Field("param").GetValue<GameCamera>();
                    param.x += dx * param.HorizontalSpeed;
                    param.y -= dy * param.VerticalSpeed;
                    param.y = Traverse.Create(__instance).Method("ClampAngle", new object[] { param.y, param.yMinLimit, param.yMaxLimit }).GetValue<float>();
                }
            }
        }
        [HarmonyPostfix, HarmonyPatch(typeof(CameraController), "Zoom", new Type[] { typeof(float) })]
        public static void CameraPatch_Free3(CameraController __instance, float s)
        {
            if (cameraFree.Value)
            {
                var cameraMode = Traverse.Create(__instance).Field("mode").GetValue<GameCamera.CameraMode>();
                if (cameraMode == GameCamera.CameraMode.LimitFollow)
                {
                    var param = Traverse.Create(__instance).Field("param").GetValue<GameCamera>();
                    param.distance -= s * param.ZoomSpeed * Time.deltaTime;
                    param.distance = Traverse.Create(__instance).Method("ClampAngle", new object[] { param.distance, param.minDistance, param.maxDistance }).GetValue<float>();
                }
            }
        }
    }
}
