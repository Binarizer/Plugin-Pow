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
    // Token: 0x02000B6C RID: 2924
    [Description("獎勵玩家独特装备")]
    public class RewardUniqueEquipByValue : GameAction
    {
        // Token: 0x0600423E RID: 16958 RVA: 0x0017114C File Offset: 0x0016F34C
        public override bool GetValue()
        {
            bool result;
            if (!Application.isPlaying)
            {
                result = false;
            }
            else
            {
                if (this.count < 1)
                {
                    return false;
                }
                for (int i = 0; i < this.count; i++)
                {
                    Props props = Game.Data.Get<Props>(sourceId) ?? Randomizer.GetOneFromData<Props>(sourceId);
                    if (props == null)
                    {
                        return false;
                    }
                    Props props2 = props.Clone<Props>();
                    props2.Id = "$eq_" + props.Id;
                    props2.Name = string.Format("<color=#FFEEDD>{0}+</color>", props.Name);
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
                    List<CharacterProperty> list = ((CharacterProperty[])Enum.GetValues(typeof(CharacterProperty))).ToList<CharacterProperty>();
                    list.Remove(CharacterProperty.Affiliation);
                    list.Remove(CharacterProperty.Contribution);
                    list.Remove(CharacterProperty.HP);
                    list.Remove(CharacterProperty.MP);
                    int num = this.value;
                    List<string> list2 = new List<string>();
                    while (num > 0 && list.Count > 0)
                    {
                        CharacterProperty characterProperty = list.Random<CharacterProperty>();
                        int num2;
                        int num3;
                        if (characterProperty < CharacterProperty.Attack)
                        {
                            num2 = UnityEngine.Random.Range(0, num + this.value / 2);
                            num3 = num2 * 5;
                        }
                        else if (characterProperty < CharacterProperty.Hit)
                        {
                            num2 = UnityEngine.Random.Range(-this.value / 2, num + this.value);
                            num3 = num2;
                        }
                        else if (characterProperty < CharacterProperty.Move)
                        {
                            num2 = UnityEngine.Random.Range(-this.value / 2, num + this.value);
                            num3 = num2 / 10;
                            num2 = num3 * 10;
                        }
                        else
                        {
                            num2 = UnityEngine.Random.Range(0, num + this.value / 2);
                            num3 = (num2 + 80) / 200;
                            num2 = Math.Max(0, num3 * 200 - 80);
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
                            list2.Add(string.Format("{0}{1}{2}", Game.Data.Get<StringTable>("Property_" + Enum.GetName(typeof(CharacterProperty), characterProperty)).Text, (num3 > 0) ? "+" : "-", num3));
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
                    Game.GameData.Inventory.Add(props2.Id, 1, true);
                    if (base.Graph != null && (bool)base.Graph.GetVariable("IsShowMessage"))
                    {
                        this.ShowMessage(props2.Name, 1);
                    }
                }
                result = true;
            }
            return result;
        }

        // Token: 0x0600423F RID: 16959 RVA: 0x00170CB0 File Offset: 0x0016EEB0
        private void ShowMessage(string name, int count)
        {
            string text = string.Format(Game.Data.Get<StringTable>("Reward10101_PropsAdd").Text, name, count);
            Game.UI.AddMessage(text, UIPromptMessage.PromptType.Normal);
        }

        // Token: 0x06004240 RID: 16960 RVA: 0x0001E9FF File Offset: 0x0001CBFF
        public override string ToString()
        {
            return string.Format("新增装备 : {0} 數量 {1}", this.sourceId, this.count);
        }

        // Token: 0x04003A1C RID: 14876
        [InputField("物品主ID（可随机）", true, null)]
        public string sourceId;

        // Token: 0x04003A1D RID: 14877
        [InputField("附加值", true, null)]
        public int value;

        // Token: 0x04003A1E RID: 14878
        [InputField("数量", true, null)]
        public int count;
    }
}
