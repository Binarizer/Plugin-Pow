using BepInEx.Configuration;
using HarmonyLib;
using Heluo.Data;
using Heluo.Manager;
using Heluo.UI;
using System;
using System.ComponentModel;

namespace PathOfWuxia
{
    [DisplayName("养成指令")]
    [Description("显示练功时N-1次练满所需点数")]
    class HookNurturanceOrder : IHook
    {
        static ConfigEntry<bool> showThreshold;
        void IHook.OnRegister(PluginBinarizer plugin)
        {
            showThreshold = plugin.Config.Bind("界面改进", "显示练满所需点数", true, "是否提示n次练满所需相应数值");
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
    }
}
