using BepInEx.Configuration;
using HarmonyLib;
using Heluo.Battle;
using Heluo.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace PathOfWuxia
{
    [System.ComponentModel.DisplayName("一击必杀")]
    [Description("一击必杀")]
    class HookOnePunch : IHook
    {
        static ConfigEntry<bool> onePunch;
        static ConfigEntry<bool> noLockHp;
        public void OnRegister(PluginBinarizer plugin)
        {
            onePunch = plugin.Config.Bind("游戏设定", "伤害99999", false, "含攻击、反击，不会击破锁血");
            noLockHp = plugin.Config.Bind("游戏设定", "无视锁血", false, "含攻击、反击。谨慎启用，一些战斗可能会产生错误");
        }
        [HarmonyPostfix, HarmonyPatch(typeof(BattleComputer), "Calculate_Final_Damage")]
        public static void BattleComputerPatch_Calculate_Final_Damage(BattleComputer __instance, ref Damage damage, ref SkillData skill)
        {
            Console.WriteLine("BattleComputerPatch_Calculate_Final_Damage");
            if (onePunch.Value)
            {
                if (damage.Defender.faction == Faction.Enemy || damage.Defender.faction == Faction.Single || damage.Defender.faction == Faction.AbsolutelyNeutral || damage.Defender.faction == Faction.AbsoluteChaos)
                {
                    damage.final_damage = 999999999;
                    damage.IsDodge = false;//无法闪避
                    damage.IsLethal = true;//击杀
                    damage.IsInvincibility = false;//非霸体
                    damage.DamageToAttacker = 0;//反伤为0
                }
            }
            //消除锁血
            if (noLockHp.Value)
            {
                if (damage.Defender.faction == Faction.Enemy || damage.Defender.faction == Faction.Single || damage.Defender.faction == Faction.AbsolutelyNeutral || damage.Defender.faction == Faction.AbsoluteChaos)
                {
                    damage.Defender[BattleLiberatedState.Lock_HP_Percent] = 0;
                    damage.Defender[BattleLiberatedState.Lock_HP_Value] = 0;
                    List<BufferInfo> BufferList = Traverse.Create(damage.Defender.BattleBuffer).Field("BufferList").GetValue<List<BufferInfo>>();
                    for (int j = 0; j < BufferList.Count; j++)
                    {
                        BufferList[j].BufferAttributes[BattleLiberatedState.Lock_HP_Percent] = 0;
                        BufferList[j].BufferAttributes[BattleLiberatedState.Lock_HP_Value] = 0;
                    }
                    BattleAttributes Mantra_Attributes = Traverse.Create(damage.Defender).Field("Mantra_Attributes").GetValue<BattleAttributes>();
                    Mantra_Attributes[BattleLiberatedState.Lock_HP_Percent] = 0;
                    Mantra_Attributes[BattleLiberatedState.Lock_HP_Value] = 0;
                }
            }
        }
    }
}
