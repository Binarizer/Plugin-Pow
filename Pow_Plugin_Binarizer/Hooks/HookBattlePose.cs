using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using BepInEx.Configuration;
using Heluo;
using Heluo.Data;
using Heluo.Battle;
using Heluo.Utility;
using System.ComponentModel;

namespace PathOfWuxia
{
    // 切换战斗姿势
    [System.ComponentModel.DisplayName("切换战斗姿势")]
    [Description("通过F7/F8切换战斗姿势")]
    public class HookBattlePose : IHook
    {
        static ConfigEntry<KeyCode> changeAnim;
        static ConfigEntry<KeyCode> changeAnimBack;

        public void OnRegister(PluginBinarizer plugin)
        {
            changeAnim = plugin.Config.Bind("游戏设定", "切换姿势(特殊)", KeyCode.F7, "切换特化战斗姿势(随机选择)");
            changeAnimBack = plugin.Config.Bind("游戏设定", "切换姿势(还原)", KeyCode.F8, "切换回默认战斗姿势");

            plugin.onUpdate += OnUpdate;
        }

        public void OnUpdate()
        {
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

    }
}
