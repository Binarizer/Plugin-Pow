using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Heluo;
using Heluo.Data;
using Heluo.UI;
using Heluo.Utility;

namespace PathOfWuxia
{
    public static class GlobalLib
    {
        public static CharacterMapping GetUICharacterMapping()
        {
            try
            {
                var uiHome = Game.UI.Get<UIHome>();
                var ctrlHome = Traverse.Create(uiHome).Field("controller").GetValue<CtrlHome>();
                var cml = Traverse.Create(ctrlHome).Field("characterMapping").GetValue<List<CharacterMapping>>();
                if (cml.Count == 0)
                    ctrlHome.OnShow();
                var ci = Traverse.Create(ctrlHome).Field("communityIndex").GetValue<int>();
                if (ci >= cml.Count)
                    Traverse.Create(ctrlHome).Field("communityIndex").SetValue(cml.Count - 1);
                return cml[ci];
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static ModResourceProvider ModResource { get; set; }

        public static PropsCategory GetScrollType(PropsCategory skillType)
        {
            if (skillType < PropsCategory.Throwing)
            {
                return skillType - PropsCategory.Fist + PropsCategory.Fist_Secret_Scroll;
            }
            return PropsCategory.Throw_Secret_Scroll;
        }

        public static bool HasSkillType(string infoId, PropsCategory skillType)
        {
            var info = Game.GameData.Character[infoId];
            if (info == null)
                return false;
            foreach (var skill in info.Skill.Values)
            {
                if (skill.Item.Type == skillType)  // 只要学会过一项同系招式即可
                    return true;
            }
            return false;
        }

        public static Props CreateUniquePropsByValue(string sourceId, int value)
        {
            Props props = Game.Data.Get<Props>(sourceId) ?? Randomizer.GetOneFromData<Props>(sourceId);
            if (props == null)
            {
                return null;
            }
            Props props2 = props.Clone<Props>();
            if (props2.PropsEffect == null)
            {
                props2.PropsEffect = new List<PropsEffect>();
            }
            if (props2.BuffList == null)
            {
                props2.BuffList = new List<string>();
            }
            if (props2.PropsEffectDescription == null)
            {
                props2.PropsEffectDescription = "";
            }
            List<CharacterProperty> list = ((CharacterProperty[])Enum.GetValues(typeof(CharacterProperty))).ToList();
            list.Remove(CharacterProperty.Affiliation);
            list.Remove(CharacterProperty.Contribution);
            list.Remove(CharacterProperty.HP);
            list.Remove(CharacterProperty.MP);
            int num = value;
            List<string> list2 = new List<string>();
            while (num > 0 && list.Count > 0)
            {
                CharacterProperty characterProperty = list.Random();
                int num2;
                int num3;
                if (characterProperty < CharacterProperty.Attack)
                {
                    num2 = UnityEngine.Random.Range(0, num + value / 2);
                    num3 = num2 * 3 / 20 * 20;
                    num2 = num3 / 3;
                }
                else if (characterProperty < CharacterProperty.Hit)
                {
                    num2 = UnityEngine.Random.Range(-value / 2, num + value);
                    num3 = num2 / 5 * 5;
                    num2 = num3;
                }
                else if (characterProperty < CharacterProperty.Move)
                {
                    num2 = UnityEngine.Random.Range(-value / 2, num + value);
                    num3 = num2 / 5;
                    num2 = num3 * 5;
                }
                else
                {
                    num2 = UnityEngine.Random.Range(0, num + value / 2);
                    num3 = (num2 + 40) / 100;
                    num2 = Math.Max(0, num3 * 100 - 40);
                }
                if (num3 != 0)
                {
                    list.Remove(characterProperty);
                    PropsBattleProperty propsBattleProperty = new PropsBattleProperty
                    {
                        Type = PropsEffectType.BattleProperty,
                        Property = characterProperty,
                        isForever = false,
                        Value = num3
                    };
                    props2.PropsEffect.Add(propsBattleProperty);
                    list2.Add(string.Format("{0}{1}{2}", Game.Data.Get<StringTable>("Property_" + Enum.GetName(typeof(CharacterProperty), characterProperty)).Text, (num3 > 0) ? "+" : "", num3));
                    num -= num2;
                }
            }
            Props props3 = props2;
            props3.PropsEffectDescription = string.Concat(new object[]
            {
                props3.PropsEffectDescription,
                "\n附加：",
                string.Join("，", list2)
            });
            ModExtensionSaveData.AddUniqueItem(props2);
            return props2;
        }
        public static string DuelInfoId { get; set; }
    }
}
