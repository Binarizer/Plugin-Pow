using System;
using System.Collections.Generic;
using HarmonyLib;
using BepInEx;
using BepInEx.Configuration;
using Heluo;
using Heluo.UI;
using Heluo.Data;
using Heluo.Manager;
using Heluo.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace PathOfWuxia
{
    // bug修复和一些增强特性
    public class HookFeaturesAndFixes : IHook
    {
        public IEnumerable<Type> GetRegisterTypes()
        {
            return new Type[] { GetType() };
        }
        void IHook.OnRegister(BaseUnityPlugin plugin)
        {
            elementPos = plugin.Config.Bind("界面改进", "五行位置", new Vector3(-80, 15, 0), "调整五行位置");
            elementTextPos = plugin.Config.Bind("界面改进", "名字位置", new Vector3(8, 18, 0), "调整名字位置");
            elementKey = plugin.Config.Bind("界面改进", "五行显示热键", KeyCode.F3, "战斗时显示五行。调整位置后需开关一次生效");
            showThreshold = plugin.Config.Bind("界面改进", "显示练满所需点数", true, "是否提示n次练满所需相应数值");
            showFavExp = plugin.Config.Bind("界面改进", "显示友好度", true, "是否显示各种有关友好度的数值");
        }

        void IHook.OnUpdate()
        {
            if (Input.GetKeyDown(elementKey.Value))
            {
                elementShow = !elementShow;
                foreach (var wgbar in GameObject.FindObjectsOfType<WgBar>())
                {
                    ProcessElementDisplay(wgbar);
                }
            }
        }

        static ConfigEntry<Vector3> elementPos;
        static ConfigEntry<Vector3> elementTextPos;
        static ConfigEntry<KeyCode> elementKey;
        static bool elementShow = true;
        static ConfigEntry<bool> showThreshold;
        static ConfigEntry<bool> showFavExp;

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

        // 5 血条显示五行
        public static void ProcessElementDisplay(WgBar wgbar)
        {
            Image element;
            Text name;
            Slider hp = Traverse.Create(wgbar).Field("hp").GetValue<Slider>();
            var trans = hp.transform.Find("ElementImage");
            var trans2 = hp.transform.Find("UnitName");
            if (trans == null && wgbar.Unit != null)
            {
                GameObject gameObject = new GameObject("ElementImage");
                gameObject.transform.SetParent(hp.transform, false);
                element = gameObject.AddComponent<Image>();
                element.rectTransform.sizeDelta = new Vector2(50f, 50f);
                element.color = new Color(1f, 1f, 1f, 0.8f);
                element.transform.localPosition = elementPos.Value;
                element.sprite = Game.Resource.Load<Sprite>(string.Format(GameConfig.ElementPath, wgbar.Unit.Element));
                GameObject gameObject2 = new GameObject("UnitName");
                gameObject2.transform.SetParent(hp.transform, false);
                name = gameObject2.AddComponent<Text>();
                name.text = wgbar.Unit.FullName;
                name.font = Game.Resource.Load<Font>("Assets/Font/kaiu.ttf");
                name.fontSize = 18;
                name.alignment = TextAnchor.MiddleLeft;
                name.rectTransform.sizeDelta = new Vector2(120f, 20f);
                name.transform.localPosition = elementTextPos.Value;
            }
            else
            {
                element = trans.gameObject.GetComponent<Image>();
                name = trans2.gameObject.GetComponent<Text>();
            }
            if (elementShow)
            {
                element.gameObject.SetActive(true);
                element.transform.localPosition = elementPos.Value;
                name.gameObject.SetActive(true);
                name.transform.localPosition = elementTextPos.Value;
            }
            else
            {
                element.gameObject.SetActive(false);
                name.gameObject.SetActive(false);
            }
        }
        [HarmonyPostfix, HarmonyPatch(typeof(WgBar), "Unit", MethodType.Setter)]
        public static void UnitElementPatch(WgBar __instance)
        {
            ProcessElementDisplay(__instance);
        }
        [HarmonyPostfix, HarmonyPatch(typeof(WgBar), "OnPropertyChange", new Type[] { typeof(BattleProperty), typeof(int) })]
        public static void UnitElementPatch2(WgBar __instance, BattleProperty prop)
        {
            if (__instance != null)
            {
                Slider hp = Traverse.Create(__instance).Field("hp").GetValue<Slider>();
                if (hp != null && hp.transform != null)
                {
                    var trans = hp.transform.Find("ElementImage");
                    if (prop == BattleProperty.Element && trans != null && __instance.Unit != null)
                    {
                        var element = trans.gameObject.GetComponent<Image>();
                        element.sprite = Game.Resource.Load<Sprite>(string.Format(GameConfig.ElementPath, __instance.Unit.Element));
                    }
                }
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

        // 7 显示需要多少点一次修炼到10
        const int MAX_STATUS = 5000;
        internal static int GetThresholdStatus(int begin, int fTarget, Func<int, int> f)
        {
            // binary search
            int lb = begin;
            int rb = MAX_STATUS;
            while (lb < rb)
            {
                int mid = (lb + rb) >> 1;
                int exp = f(mid);
                if (exp < fTarget)
                    lb = mid + 1;
                else
                    rb = mid;
            }
            return lb;
        }
        [HarmonyPostfix, HarmonyPatch(typeof(CtrlNurturance), "OnOrderSelect", new Type[] { typeof(WGNurturanceBtn) })]
        public static void NurturanceValueDisplay(CtrlNurturance __instance, WGNurturanceBtn btn)
        {
            if (!showThreshold.Value)
                return;
            NurturanceOrderTree tree = btn.tree;
            if (tree == null || (tree.Value.Fuction != NurturanceFunction.Skill && tree.Value.Fuction != NurturanceFunction.Mantra))
                return;
            var Instance = Traverse.Create(__instance);
            NurturanceUIInfo uiInfo = Instance.Field("UIInfo").GetValue<NurturanceUIInfo>();
            CharacterInfoData player = Instance.Field("Player").GetValue<CharacterInfoData>();
            int playerStatus;
            int requireStatus;
            int requireExp;
            float traitEffect;
            float additionCoe;
            if (tree.Value.Fuction == NurturanceFunction.Skill)
            {
                if (!player.Skill.ContainsKey(tree.DoorPlate))
                    return;
                SkillData skillData = player.Skill[tree.DoorPlate];
                playerStatus = Instance.Method("GetPlayerStatus", skillData.Item.RequireAttribute).GetValue<int>();
                requireStatus = skillData.Item.RequireValue;
                requireExp = 100 * (10 - skillData.Level) - skillData.Exp;
                traitEffect = player.Trait.GetTraitEffect(TraitEffectType.SkillQuicken, (int)skillData.Item.Type);
                additionCoe = Instance.Method("GetAdditionCoe", tree.Value).GetValue<float>();
            }
            else// (tree.Value.Fuction == NurturanceFunction.Mantra)
            {
                MantraData mantraData = player.Mantra[tree.DoorPlate];
                if (mantraData == null)
                    return;
                playerStatus = Instance.Method("GetPlayerStatus", mantraData.Item.RequireAttribute).GetValue<int>();
                requireStatus = mantraData.Item.RequireValue;
                requireExp = 100 * (10 - mantraData.Level) - mantraData.Exp;
                traitEffect = player.Trait.GetTraitEffect(TraitEffectType.MantraQuicken);
                additionCoe = Instance.Method("GetAdditionCoe", tree.Value).GetValue<float>();
            }

            int expFromStatus(int status)
            {
                int value = Instance.Method("CalculateAbilityExp", status, requireStatus).GetValue<int>();
                value = Instance.Method("GetValueByEmotion", (int)(value * (traitEffect + additionCoe))).GetValue<int>();
                return value;
            }

            int exp = expFromStatus(playerStatus);
            int n = (requireExp + exp - 1) / exp;
            if (n > 1)
            {
                int thresholdStatus = GetThresholdStatus(playerStatus, (requireExp + n - 2) / (n - 1), expFromStatus);
                string s = thresholdStatus < MAX_STATUS ? thresholdStatus.ToString() : MAX_STATUS.ToString() + "+";
                s = playerStatus + "/" + s;
                uiInfo.TipInfos.Insert(2, new TipInfo { type = WGTip.TipType.TitleValue, title = (n - 1) + "次练满需", value = s });
            }
            if (n > 0)
            {
                uiInfo.TipInfos.Insert(2, new TipInfo { type = WGTip.TipType.TitleValue, title = "练满回合数", value = n.ToString() });
                Instance.Field("view").GetValue<UINurturance>().ShowTip(uiInfo.TipInfos);
            }
        }

        // 8 Display Relation Ship
        [HarmonyPostfix, HarmonyPatch(typeof(UIRelationship), "UpdateRelationship", new Type[] { typeof(RelationshipInfo) })]
        public static void ShowRelationship_UpdateRelationship(UIRelationship __instance, RelationshipInfo _info)
        {
            var t = Traverse.Create(__instance);
            Text expText = Traverse.Create(__instance).Field("expbar").GetValue<Slider>().GetComponentInChildren<Text>();
            if (expText != null)
            {
                UnityEngine.Object.Destroy(expText);
            }
            if (showFavExp.Value)
            {
                GameObject gameObject = new GameObject("Text");
                gameObject.transform.SetParent(t.Field("expbar").GetValue<Slider>().transform, false);
                expText = gameObject.AddComponent<Text>();
                FavorabilityData favorability = Game.GameData.Community[t.Field("currentId").GetValue<string>()].Favorability;
                expText.text = favorability.Exp + " / " + favorability.GetMaxExpByLevel(favorability.Level);
                expText.font = Game.Resource.Load<Font>("Assets/Font/kaiu.ttf");
                expText.fontSize = 25;
                expText.alignment = TextAnchor.MiddleLeft;
                expText.rectTransform.sizeDelta = new Vector2(120f, 40f);
                expText.transform.localPosition = new Vector3(-5f, 50f, 0f);
            }
        }
        [HarmonyPostfix, HarmonyPatch(typeof(Props), "PropsEffectDescription", MethodType.Getter)]
        public static void ShowRelationship_Props(Props __instance, ref string __result)
        {
            if (!showFavExp.Value || __instance.PropsType != PropsType.Present)
                return;
            List<string> strFav = new List<string>();
            foreach (var propsEffect in __instance.PropsEffect)
            {
                if (propsEffect is PropsFavorable pf)
                {
                    strFav.Add( string.Format("{0}{1}+{2}", Game.GameData.Exterior[pf.Npcid].FullName(), Game.Data.Get<StringTable>("General_Favorability").Text, pf.Value));
                }
            }
            __result += string.Join("，", strFav);
        }
    }
}
