﻿using System;
using HarmonyLib;
using UnityEngine;
using BepInEx.Configuration;
using Heluo;
using Heluo.Data;
using Heluo.FSM.Player;

namespace PathOfWuxia
{
    // 一般游戏设定功能
    [System.ComponentModel.DisplayName("速度、难度设定")]
    [System.ComponentModel.Description("速度、难度设定")]
    public class HookGenerals : IHook
    {
        static ConfigEntry<float> speedValue;
        static ConfigEntry<KeyCode> speedKey;
        static ConfigEntry<float> playerScale;
        static ConfigEntry<GameLevel> difficulty;
        bool speedOn = false;

        public void OnRegister(PluginBinarizer plugin)
        {
            speedValue = plugin.Config.Bind("游戏设定", "速度值", 1.5f, "调整速度值");
            speedKey = plugin.Config.Bind("游戏设定", "速度热键", KeyCode.F2, "开关速度调节");
            playerScale = plugin.Config.Bind("游戏设定", "主角模型尺寸", 1f, 
                new ConfigDescription("修改主角在自由活动和战斗中的模型尺寸", new AcceptableValueRange<float>(0.75f, 1.5f)));
            difficulty = plugin.Config.Bind("游戏设定", "难度值", GameLevel.Normal, "调节游戏难度");
            difficulty.SettingChanged += OnGameLevelChange;
            plugin.onUpdate += OnUpdate;
        }

        public void OnUpdate()
        {
            if (Input.GetKeyDown(speedKey.Value))
            {
                speedOn = !speedOn;
                if (!speedOn)
                {
                    Time.timeScale = 1.0f;
                }
            }
            if (speedOn)
            {
                Time.timeScale = Math.Max(Time.timeScale, speedValue.Value);
            }

            // sync settings
            var psm = Game.EntityManager.GetComponent<PlayerStateMachine>(GameConfig.Player);
            if (psm != null)
            {
                GameObject playerModel = Traverse.Create(psm).Property("ObjectComponent")?.Property("Model")?.GetValue<GameObject>();
                if (playerModel != null)
                    playerModel.transform.localScale = Vector3.one * playerScale.Value;
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

        // 1 Sync await by timeScale for speed correct
        [HarmonyPrefix, HarmonyPatch(typeof(AsyncTools), "GetAwaiter", new Type[] { typeof(float) })]
        public static bool SpeedPatch(ref float seconds)
        {
            seconds = seconds / Time.timeScale;
            return true;
        }
    }
}
