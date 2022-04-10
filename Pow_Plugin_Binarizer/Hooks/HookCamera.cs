using System;
using HarmonyLib;
using UnityEngine;
using BepInEx.Configuration;
using Heluo.Battle;
using Heluo.Components;
using Heluo.Controller;
using System.ComponentModel;

namespace PathOfWuxia
{
    [System.ComponentModel.DisplayName("相机设置")]
    [Description("相机设置")]
    // 自由相机功能
    public class HookCamera : IHook
    {
        enum CameraFocusMode
        {
            Attacker,
            Defender,
            Defender_OnHit
        }
        static ConfigEntry<CameraFocusMode> cameraFocusMode;
        static ConfigEntry<bool> cameraFree;
        static ConfigEntry<bool> cameraFree_Battle;
        static ConfigEntry<float> zoomSpeed;

        public void OnRegister(PluginBinarizer plugin)
        {
            cameraFocusMode = plugin.Config.Bind("相机设置", "战斗相机跟随方式", CameraFocusMode.Attacker, "战斗时相机如何跟随，游戏默认跟随攻击者");
            cameraFree = plugin.Config.Bind("相机设置", "场景自由视角", false, "是否开启自由视角");
            cameraFree_Battle = plugin.Config.Bind("相机设置", "战斗自由视角", false, "是否开启战斗自由视角，重启战斗生效"); 
			zoomSpeed = plugin.Config.Bind("相机设置", "缩放速度", 20f, "相机缩放速度");
        }

        /// <summary>
        /// 战斗相机跟随模式1
        /// </summary>
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
        /// <summary>
        /// 战斗相机跟随模式2
        /// </summary>
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

        /// <summary>
        /// 战斗自由视角
        /// </summary>
        [HarmonyPostfix, HarmonyPatch(typeof(GameCamera), "SetBattleCamera")]
        public static void CameraPatch_FreeBattle(GameCamera __instance)
        {
            if (cameraFree_Battle.Value)
            {
                __instance.ylocked = false;
                __instance.minDistance = 0;
                __instance.maxDistance = 10000;
                __instance.ZoomSpeed = zoomSpeed.Value;
            }
        }

        /// <summary>
        /// 平时自由视角1
        /// </summary>
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
        /// <summary>
        /// 平时自由视角2
        /// </summary>
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
                    param.yMinLimit = -90;
                    param.yMaxLimit = 90;
                    param.y = Traverse.Create(__instance).Method("ClampAngle", new object[] { param.y, param.yMinLimit, param.yMaxLimit }).GetValue<float>();
                }
            }
        }
        [HarmonyPostfix, HarmonyPatch(typeof(CameraController), "Zoom", new Type[] { typeof(float) })]
        /// <summary>
        /// 平时自由视角3
        /// </summary>
        public static void CameraPatch_Free3(CameraController __instance, float s)
        {
            if (cameraFree.Value)
            {
                var cameraMode = Traverse.Create(__instance).Field("mode").GetValue<GameCamera.CameraMode>();
                if (cameraMode == GameCamera.CameraMode.LimitFollow)
                {
                    var param = Traverse.Create(__instance).Field("param").GetValue<GameCamera>();
                    param.distance -= s * zoomSpeed.Value * Time.deltaTime;
                    param.minDistance = 0;
                    param.maxDistance = 10000;
                    param.distance = Traverse.Create(__instance).Method("ClampAngle", new object[] { param.distance, param.minDistance, param.maxDistance }).GetValue<float>();
                }
            }
        }
    }
}
