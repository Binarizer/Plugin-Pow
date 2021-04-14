// 添加一些Mod药品效果
using Heluo;
using Heluo.UI;
using Heluo.Flow;
using Heluo.Data;

namespace PathOfWuxia
{
   
    public class BattleDropProp_Ext : BattleDropProp
    {
        public float Rate = 1f; // 扩展 掉落概率
        public new string ToText()
        {
            if (Rate != 1f)
                return string.Format("({0},{1},{2})", Id, Amount, Rate);
            return base.ToText();
        }
    }

    public enum PropsEffectType_Ext
    {
        LearnSkill = 101,
        LearnMantra = 102,
        Reward = 103,
        Talk = 104
    }

    [Description("习得招式")]
    public class PropsLearnSkill : PropsEffect
    {
        public PropsLearnSkill()
        {
            this.Type = (PropsEffectType)PropsEffectType_Ext.LearnSkill;
        }

        public PropsLearnSkill(string Id)
        {
            this.Type = (PropsEffectType)PropsEffectType_Ext.LearnSkill;
            this.Id = Id;
        }

        public override bool AttachPropsEffect(CharacterInfoData user)
        {
            bool result;
            try
            {
                int value = int.Parse(this.Id);
                string id = string.Empty;
                foreach (string text in user.GetEquipSkill())
                {
                    if (text != string.Empty)
                    {
                        id = text;
                        break;
                    }
                }
                new SetNpcSkillEXP
                {
                    method = Method.Add,
                    npcId = user.Id,
                    Id = id,
                    value = value
                }.GetValue();
                result = true;
            }
            catch
            {
                if (!user.Skill.ContainsKey(this.Id))
                {
                    new SetNpcSkill
                    {
                        method = Method.Add,
                        npcId = user.Id,
                        value = this.Id
                    }.GetValue();
                }
                else
                {
                    SkillData skillData = user.Skill[this.Id];
                    if (skillData.Item.RequireValue > 0)
                    {
                        new SetNpcSkillEXP
                        {
                            method = Method.Add,
                            npcId = user.Id,
                            Id = this.Id,
                            value = 100
                        }.GetValue();
                    }
                }
                result = true;
            }
            return result;
        }

        public override string ToText()
        {
            return string.Format("({0},{1})", ((PropsEffectType_Ext)Type).ToString(), this.Id);
        }

        [DisplayName("招式ID")]
        public string Id;
    }

    [Description("习得心法")]
    public class PropsLearnMantra : PropsEffect
    {
        public PropsLearnMantra()
        {
            this.Type = (PropsEffectType)PropsEffectType_Ext.LearnMantra;
        }

        public PropsLearnMantra(string Id)
        {
            this.Type = (PropsEffectType)PropsEffectType_Ext.LearnMantra;
            this.Id = Id;
        }

        public override bool AttachPropsEffect(CharacterInfoData user)
        {
            bool result;
            try
            {
                int value = int.Parse(this.Id);
                string id = user.CurrentMantra.Id;
                new SetNpcMantraEXP
                {
                    method = Method.Add,
                    npcId = user.Id,
                    Id = id,
                    value = value
                }.GetValue();
                result = true;
            }
            catch
            {
                if (!user.Mantra.ContainsKey(this.Id))
                {
                    new SetNpcMantra
                    {
                        method = Method.Add,
                        npcId = user.Id,
                        value = this.Id
                    }.GetValue();
                }
                else
                {
                    MantraData mantraData = user.Mantra[this.Id];
                    new SetNpcMantraEXP
                    {
                        method = Method.Add,
                        npcId = user.Id,
                        Id = this.Id,
                        value = 100
                    }.GetValue();
                }
                result = true;
            }
            return result;
        }

        public override string ToText()
        {
            return string.Format("({0},{1})", ((PropsEffectType_Ext)Type).ToString(), this.Id);
        }

        [DisplayName("心法ID")]
        public string Id;
    }

    [Description("运行奖励")]
    public class PropsReward : PropsEffect
    {
        public PropsReward()
        {
            this.Type = (PropsEffectType)PropsEffectType_Ext.Reward;
        }

        public PropsReward(string Id)
        {
            this.Type = (PropsEffectType)PropsEffectType_Ext.Reward;
            this.Id = Id;
        }

        public override bool AttachPropsEffect(CharacterInfoData user)
        {
            return new RewardAction
            {
                Rewardid = this.Id
            }.GetValue();
        }

        public override string ToText()
        {
            return string.Format("({0},{1})", ((PropsEffectType_Ext)Type).ToString(), this.Id);
        }

        [DisplayName("奖励ID")]
        public string Id;
    }

    [Description("运行对话")]
    public class PropsTalk : PropsEffect
    {
        public PropsTalk()
        {
            this.Type = (PropsEffectType)PropsEffectType_Ext.Talk;
        }

        public PropsTalk(string Id)
        {
            this.Type = (PropsEffectType)PropsEffectType_Ext.Talk;
            this.Id = Id;
        }

        public override bool AttachPropsEffect(CharacterInfoData user)
        {
            Game.UI.Close<UIMessageWindow>();
            Game.UI.Close<UIHome>();
            return new TalkAction
            {
                talkId = this.Id
            }.GetValue();
        }

        public override string ToText()
        {
            return string.Format("({0},{1})", ((PropsEffectType_Ext)Type).ToString(), this.Id);
        }

        [DisplayName("对话ID")]
        public string Id;
    }
}
