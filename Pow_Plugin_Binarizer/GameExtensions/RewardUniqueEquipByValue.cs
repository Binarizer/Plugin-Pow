using System;
using System.Collections.Generic;
using System.Linq;
using Heluo;
using Heluo.Data;
using Heluo.UI;
using Heluo.Flow;
using Heluo.Utility;
using UnityEngine;

namespace PathOfWuxia
{
    [Description("独特装备(重铸)")]
    public class RewardUniqueEquipByValue : GameAction
    {
        // 通用函数
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
                    PropsBattleProperty propsBattleProperty = new PropsBattleProperty();
                    propsBattleProperty.Type = PropsEffectType.BattleProperty;
                    propsBattleProperty.Property = characterProperty;
                    propsBattleProperty.isForever = false;
                    propsBattleProperty.Value = num3;
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

        public override bool GetValue()
        {
            bool result;
            if (!Application.isPlaying)
            {
                result = false;
            }
            else
            {
                if (count < 1)
                {
                    return false;
                }
                for (int i = 0; i < this.count; i++)
                {
                    Props uniqueProps = CreateUniquePropsByValue(sourceId, value);
                    if (uniqueProps != null)
                    {
                        Game.GameData.Inventory.Add(uniqueProps.Id, 1, true);
                        if (base.Graph != null && (bool)base.Graph.GetVariable("IsShowMessage"))
                        {
                            this.ShowMessage(uniqueProps.Name, 1);
                        }
                    }
                }
                result = true;
            }
            return result;
        }

        private void ShowMessage(string name, int count)
        {
            string text = string.Format(Game.Data.Get<StringTable>("Reward10101_PropsAdd").Text, name, count);
            Game.UI.AddMessage(text, UIPromptMessage.PromptType.Normal);
        }

        public override string ToString()
        {
            return string.Format("新增装备 : {0} 數量 {1}", this.sourceId, this.count);
        }

        [InputField("物品主ID（可随机）", true, null)]
        public string sourceId;

        [InputField("附加值", true, null)]
        public int value;

        [InputField("数量", true, null)]
        public int count;
    }
}
