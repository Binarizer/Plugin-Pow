using System;
using System.Collections.Generic;
using System.Linq;
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
using Heluo.Utility;
using Heluo.Platform;
using Steamworks;
using Heluo.FSM.Player;
using Heluo.Generate;

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
        static ConfigEntry<int> saveCount;
        static ConfigEntry<KeyCode> changeAnim;
        static ConfigEntry<KeyCode> changeAnimBack;
        static ConfigEntry<float> playerScale;
        static ConfigEntry<float> moveSpeed;
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

        public IEnumerable<Type> GetRegisterTypes()
        {
            return new Type[] { GetType() };
        }
        public void OnRegister(BaseUnityPlugin plugin)
        {
            speedValue = plugin.Config.Bind("游戏设定", "速度值", 1.5f, "调整速度值");
            speedKey = plugin.Config.Bind("游戏设定", "速度热键", KeyCode.F2, "开关速度调节");
            saveCount = plugin.Config.Bind("游戏设定", "存档数量", 20,
                new ConfigDescription("存档数量上限", new AcceptableValueRange<int>(20, 100)));
            playerScale = plugin.Config.Bind("游戏设定", "主角模型尺寸", 1f, 
                new ConfigDescription("修改主角在自由活动和战斗中的模型尺寸", new AcceptableValueRange<float>(0.75f, 1.5f)));
            moveSpeed = plugin.Config.Bind("游戏设定", "移动速度", 2.6f, "修改玩家在大地图的移动速度。如果太快可能会穿模");
            moveSpeed.SettingChanged += (o, e) =>
            {
                if (moveSpeed.Value > 0f)
                    Game.EntityManager.GetComponent<PlayerStateMachine>(GameConfig.Player).forwardRate = moveSpeed.Value;
            };
            difficulty = plugin.Config.Bind("游戏设定", "难度值", GameLevel.Normal, "调节游戏难度");
            difficulty.SettingChanged += OnGameLevelChange;
            probablyMode = plugin.Config.Bind("游戏设定", "随机事件方式", ProbablyMode.None, "None-原版 SmallChance-小概率事件必发生 FixedRandomValue-设定产生的随机数");
            probablyValue = plugin.Config.Bind("游戏设定", "随机事件值", 50, "SmallChance：多少被界定为小概率 FixedRandomValue：1~100对应必发生/必不发生");
            changeAnim = plugin.Config.Bind("游戏设定", "切换姿势(特殊)", KeyCode.F7, "切换特化战斗姿势(随机选择)");
            changeAnimBack = plugin.Config.Bind("游戏设定", "切换姿势(还原)", KeyCode.F8, "切换回默认战斗姿势");

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

            if (Input.GetKeyDown(changeAnim.Value) && Game.BattleStateMachine != null)
            {
                if (IdleAnimOverrides == null)
                {
                    BuildIdleAnimOverrides();
                }
                WuxiaUnit unit = Traverse.Create(Game.BattleStateMachine).Field("_currentUnit").GetValue<WuxiaUnit>();
                if (unit != null && IdleAnimOverrides != null && IdleAnimOverrides.Count > 0)
                {
                    string randomIdleAnim = IdleAnimOverrides.Random();
                    AnimationClip animationClip = Game.Resource.Load<AnimationClip>(GameConfig.AnimationPath + randomIdleAnim + ".anim");
                    if (animationClip != null)
                    {
                        var list = new[] { ("idle", animationClip) };
                        unit.Actor.Override(list);
                    }
                }
            }
            if (Input.GetKeyDown(changeAnimBack.Value) && Game.BattleStateMachine != null)
            {
                WuxiaUnit unit = Traverse.Create(Game.BattleStateMachine).Field("_currentUnit").GetValue<WuxiaUnit>();
                if (unit != null)
                {
                    var weapon = unit.info.Equip.GetEquip(EquipType.Weapon);
                    var weaponType = weapon?.PropsCategory.ToString();
                    unit.Actor.OverrideDefault(Traverse.Create(unit).Field("exterior").GetValue<CharacterExteriorData>(), weaponType);
                }
            }

            // sync settings
            var psm = Game.EntityManager.GetComponent<PlayerStateMachine>(GameConfig.Player);
            if (psm != null)
            {
                GameObject playerModel = Traverse.Create(psm).Property("ObjectComponent")?.Property("Model")?.GetValue<GameObject>();
                if (playerModel != null)
                    playerModel.transform.localScale = Vector3.one * playerScale.Value;
                moveSpeed.Value = psm.forwardRate;
            }
            var playerInBattle = Game.BattleStateMachine?.BattleManager?.UnitGenerator[GameConfig.Player];
            if (playerInBattle != null)
            {
                playerInBattle.transform.localScale = Vector3.one * playerScale.Value;
            }
            difficulty.Value = Game.GameData.GameLevel;
        }

        static void OnGameLevelChange(object o, EventArgs e)
        {
            if (Game.GameData != null)
            {
                Game.GameData.GameLevel = difficulty.Value;
            }
        }

        private static List<string> IdleAnimOverrides;
        static void BuildIdleAnimOverrides()
        {
            var idles = from animMap in Game.Data.Get<AnimationMapping>(am => !am.Idle.IsNullOrEmpty()) select animMap.Idle;
            IdleAnimOverrides = idles.Distinct().ToList();
            var stands = from animMap in Game.Data.Get<AnimationMapping>(am => !am.Stand.IsNullOrEmpty()) select animMap.Stand;
            IdleAnimOverrides.AddRange(stands.Distinct());

            Console.WriteLine("特殊动作表：" + string.Join(",", idles));
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

        // 6 存档数量上限
        [HarmonyPrefix, HarmonyPatch(typeof(SteamPlatform), "ListSaveHeaderFile", new Type[] { typeof(GameSaveType) })]
        public static bool SaveCountPatch_ListSaveHeaderFile(SteamPlatform __instance, GameSaveType Type, ref List<PathOfWuxiaSaveHeader> __result)
        {
            if (Type == GameSaveType.Manual)
            {
                List<PathOfWuxiaSaveHeader> list = new List<PathOfWuxiaSaveHeader>();
                string format = "PathOfWuxia_{0:00}.save";
                for (int i = 0; i < saveCount.Value; i++)
                {
                    PathOfWuxiaSaveHeader pathOfWuxiaSaveHeader = null;
                    string text = string.Format(format, i);
                    if (SteamRemoteStorage.FileExists(text))
                    {
                        __instance.GetSaveFileHeader(text, ref pathOfWuxiaSaveHeader);
                    }
                    else
                    {
                        pathOfWuxiaSaveHeader = new PathOfWuxiaSaveHeader();
                    }
                    if (pathOfWuxiaSaveHeader == null)
                    {
                        pathOfWuxiaSaveHeader = new PathOfWuxiaSaveHeader();
                    }
                    list.Add(pathOfWuxiaSaveHeader);
                }
                __result = list;
                return false;
            }
            return true;
        }
    }
}
