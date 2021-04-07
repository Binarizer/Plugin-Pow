using System;
using System.Collections.Generic;
using Heluo.Data;
using Heluo;
using Heluo.Flow;
using Heluo.UI;
using Heluo.Utility;
using UnityEngine;

namespace PathOfWuxia
{
    // Token: 0x02000B6B RID: 2923
    [Description("獎勵玩家独特物品")]
    public class RewardUniquePropsFromProps : GameAction
    {
        // Token: 0x0600423A RID: 16954 RVA: 0x00170F7C File Offset: 0x0016F17C
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
                string[] array = this.extraIds.Trim().Split(new char[]
                {
                    ','
                });
                for (int i = 0; i < this.count; i++)
                {
                    Props props = Game.Data.Get<Props>(sourceId) ?? Randomizer.GetOneFromData<Props>(sourceId);
                    if (props == null)
                    {
                        return false;
                    }
                    Props props2 = props.Clone<Props>();
                    props2.Id = "$fp_" + props.Id;
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
                    foreach (string id in array)
                    {
                        Props props3 = Game.Data.Get<Props>(id) ?? Randomizer.GetOneFromData<Props>(id);
                        if (props3 == null)
                        {
                            continue;
                        }
                        if (props3.PropsEffect != null)
                        {
                            props2.PropsEffect.AddRange(props3.PropsEffect);
                        }
                        if (props3.BuffList != null)
                        {
                            props2.BuffList.AddRange(props3.BuffList);
                        }
                        if (!props3.PropsEffectDescription.IsNullOrEmpty())
                        {
                            Props props4 = props2;
                            props4.PropsEffectDescription += string.Format("\n附加：{0}", props3.PropsEffectDescription);
                        }
                    }
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

        // Token: 0x0600423B RID: 16955 RVA: 0x00170CB0 File Offset: 0x0016EEB0
        private void ShowMessage(string name, int count)
        {
            string text = string.Format(Game.Data.Get<StringTable>("Reward10101_PropsAdd").Text, name, count);
            Game.UI.AddMessage(text, UIPromptMessage.PromptType.Normal);
        }

        // Token: 0x0600423C RID: 16956 RVA: 0x0001E9E2 File Offset: 0x0001CBE2
        public override string ToString()
        {
            return string.Format("新增装备 : {0} 數量 {1}", this.sourceId, this.count);
        }

        // Token: 0x04003A19 RID: 14873
        [InputField("物品主ID（可随机）", true, null)]
        public string sourceId;

        // Token: 0x04003A1A RID: 14874
        [InputField("数量", true, null)]
        public int count;

        // Token: 0x04003A1B RID: 14875
        [InputField("附加物品效果IDs，逗号分隔", true, null)]
        public string extraIds;
    }
}
