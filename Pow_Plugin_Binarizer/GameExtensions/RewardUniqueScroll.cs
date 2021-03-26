using System;
using System.Collections.Generic;
using Heluo.Data;
using Heluo.UI;
using Heluo;
using Heluo.Flow;
using UnityEngine;

namespace PathOfWuxia
{
    [Description("獎勵玩家独有秘籍")]
    public class RewardUniqueScroll : GameAction
    {
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
                    Props props;
                    if (!this.isMantra)
                    {
                        Skill skill = Randomizer.GetOneFromData<Skill>(this.sourceId);
                        props = new Props
                        {
                            Id = "$scroll_" + skill.Id,
                            Description = skill.Description,
                            PropsType = PropsType.Medicine,
                            PropsCategory = skill.Type - 101 + 401,
                            Name = skill.Name + "秘籍",
                            PropsEffect = new List<PropsEffect>
                            {
                                new PropsLearnSkill(skill.Id)
                            },
                            PropsEffectDescription = "学会独特招式：" + skill.Name
                        };
                    }
                    else
                    {
                        Mantra mantra = Randomizer.GetOneFromData<Mantra>(this.sourceId);
                        props = new Props
                        {
                            Id = "$scroll_" + mantra.Id,
                            Description = mantra.Description,
                            PropsType = PropsType.Medicine,
                            PropsCategory = PropsCategory.InternalStyle_Secret_Scroll,
                            Name = mantra.Name + "秘籍",
                            PropsEffect = new List<PropsEffect>
                            {
                                new PropsLearnMantra(mantra.Id)
                            },
                            PropsEffectDescription = "学会独特心法：" + mantra.Name
                        };
                    }
                    HookUniqueItem.exData.AddUniqueItem<Props>(props);
                    Game.GameData.Inventory.Add(props.Id, 1, true);
                    if (base.Graph != null && (bool)base.Graph.GetVariable("IsShowMessage"))
                    {
                        this.ShowMessage(props.Name, 1);
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
            return string.Format("新增秘籍 : {0} 數量 {1}", this.sourceId, this.count);
        }

        [InputField("是否为心法", true, null)]
        public bool isMantra;

        [InputField("秘籍ID（可随机）", true, null)]
        public string sourceId;

        [InputField("数量", true, null)]
        public int count;
    }
}