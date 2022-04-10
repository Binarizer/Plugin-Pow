using System;
using System.Collections.Generic;
using HarmonyLib;
using Heluo;
using Heluo.UI;
using Heluo.Data;
using Heluo.Manager;
using UnityEngine.UI;

namespace PathOfWuxia
{
    // bug修复和一些增强特性
    [System.ComponentModel.DisplayName("杂项bug修复")]
    [System.ComponentModel.Description("一些官方代码问题修复")]
    public class HookFeaturesAndFixes : IHook
    {
        public void OnRegister(PluginBinarizer plugin)
        {
        }

        // 1 吃药立即显示属性提升
        [HarmonyPostfix, HarmonyPatch(typeof(PropsUpgradableProperty), "AttachPropsEffect", new Type[] { typeof(CharacterInfoData) })]
        public static void BugFix_AttachPropsEffect(CharacterInfoData user)
        {
            user.UpgradeProperty(false);
        }

        // 2 养成菜单崩溃修正
        [HarmonyPrefix, HarmonyPatch(typeof(NurturanceOrderManager), "OpenCommunityOrder", new Type[] { typeof(string) })]
        public static bool BugFix_NurturanceOrder1(NurturanceOrderManager __instance, string communityId)
        {
            string id = string.Format("Nurturance_05_{0}", communityId);
            if (Game.Data.Get<Nurturance>().ContainsKey(id))
            {
                __instance.OpenOrder(id);
            }
            return false;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(NurturanceOrderManager), "CloseCommunityOrder", new Type[] { typeof(string) })]
        public static bool BugFix_NurturanceOrder2(NurturanceOrderManager __instance, string communityId)
        {
            string id = string.Format("Nurturance_05_{0}", communityId);
            if (Game.Data.Get<Nurturance>().ContainsKey(id))
            {
                __instance.CloseOrder(id);
            }
            return false;
        }

        // 3 防止武功没有写Nurtuance无法修炼
        [HarmonyPrefix, HarmonyPatch(typeof(NurturanceOrderTree), "CheckShow")]
        public static bool BugFix_NurturanceOrder3(NurturanceOrderTree __instance, ref bool __result)
        {
            if (__instance.Value == null)
            {
                __result = false;
                return false;
            }
            return true;
        }

        // 4 选人菜单的诸多愚蠢bug
        [HarmonyPrefix, HarmonyPatch(typeof(Party), "ClearParty")]
        public static bool BugFix_Adjustment1(Party __instance)
        {
            __instance.Clear(); // 特么连删除数组都不会的
            return false;
        }
        static int select_community_index;  // 记录当前Adjust选的是谁
        [HarmonyPrefix, HarmonyPatch(typeof(CtrlAdjustment), "UpdateCommunityProperty", new Type[] { typeof(int) })]
        public static bool BugFix_Adjustment2(CtrlAdjustment __instance, ref int index)
        {
            select_community_index = index;	// +
            return true;
        }
        [HarmonyPrefix, HarmonyPatch(typeof(CtrlAdjustment), "UpdateMemberInfo", new Type[] { typeof(int) })]
        public static bool BugFix_Adjustment3(CtrlAdjustment __instance, ref int index)
        {
            index = select_community_index;	// +
            return true;
        }
        [HarmonyPostfix, HarmonyPatch(typeof(WGInfiniteScroll), "UpdateWidget", new Type[] { typeof(int), typeof(bool) })]
        public static void BugFix_Adjustment4(WGInfiniteScroll __instance)
        {
            var scrollRect = Traverse.Create(__instance).Field("scrollRect").GetValue<ScrollRect>();
            if (scrollRect != null && scrollRect.scrollSensitivity == 0f)
            {
                scrollRect.scrollSensitivity = -100f;
            }
        }

        // 6 CtrlTeamMember的下标写错
        [HarmonyPrefix, HarmonyPatch(typeof(CtrlTeamMember), "UpdateProperty", new Type[] { typeof(List<CharacterMapping>) })]
        public static bool CtrlTeamMemberFix_UpdateProperty(CtrlTeamMember __instance, List<CharacterMapping> characterMapping)
        {
            List<PartyInfo> list = new List<PartyInfo>();
            foreach (CharacterMapping characterMapping2 in characterMapping)
            {
                CharacterInfoData characterInfoData = Game.GameData.Character[characterMapping2.InfoId];
                CharacterExteriorData characterExteriorData = Game.GameData.Exterior[characterMapping2.ExteriorId];
                list.Add(new PartyInfo
                {
                    Protrait = characterExteriorData.Protrait,
                    Element = characterInfoData.Element,
                    FullName = characterExteriorData.SurName + characterExteriorData.Name,
                    Hp = (float)characterInfoData.Property[CharacterProperty.HP].Value,
                    MaxHp = (float)characterInfoData.Property[CharacterProperty.Max_HP].Value,
                    Mp = (float)characterInfoData.Property[CharacterProperty.MP].Value,
                    MaxMp = (float)characterInfoData.Property[CharacterProperty.Max_MP].Value
                });
            }
            Traverse.Create(__instance).Field("view").GetValue<UITeamMember>().UpdateProperty(list);
            return false;
        }
    }
}
