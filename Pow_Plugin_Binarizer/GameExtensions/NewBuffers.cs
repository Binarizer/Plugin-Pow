// 添加一些Mod会用到的Buff
using Heluo.Battle;

namespace Heluo.Flow.Battle
{
    [Description("自身技能等级")]
    public class BufferSkillLevelCondition : BufferCompareCondition
    {
        protected override int GetProperty()
        {
            INodeGraph graph = base.Graph;
            BufferInfo bufferInfo = graph?.GetVariable<BufferInfo>("BufferInfo");
            if (bufferInfo != null)
            {
                if (Game.GameData.Character.ContainsKey(bufferInfo.Unit.CharacterInfoId))
                {
                    if (Game.GameData.Character[bufferInfo.Unit.CharacterInfoId].Skill.ContainsKey(this.SkillId))
                    {
                        return Game.GameData.Character[bufferInfo.Unit.CharacterInfoId].Skill[this.SkillId].Level;
                    }
                }
                else if (bufferInfo.Unit.LearnedSkills.ContainsKey(this.SkillId))
                {
                    return bufferInfo.Unit.LearnedSkills[this.SkillId].Level;
                }
            }
            return 0;
        }

        [InputField("技能Id", false, null)]
        public string SkillId;
    }

    [Description("自身心法等级")]
    public class BufferMantraLevelCondition : BufferCompareCondition
    {
        protected override int GetProperty()
        {
            INodeGraph graph = base.Graph;
            BufferInfo bufferInfo = graph?.GetVariable<BufferInfo>("BufferInfo");
            if (bufferInfo != null)
            {
                if (Game.GameData.Character.ContainsKey(bufferInfo.Unit.CharacterInfoId))
                {
                    if (Game.GameData.Character[bufferInfo.Unit.CharacterInfoId].Mantra.ContainsKey(this.MantraId))
                    {
                        return Game.GameData.Character[bufferInfo.Unit.CharacterInfoId].Mantra[this.MantraId].Level;
                    }
                }
                else if (bufferInfo.Unit.CurrentMantra.Id == this.MantraId)
                {
                    return bufferInfo.Unit.CurrentMantra.Level;
                }
            }
            return 0;
        }

        [InputField("心法Id", false, null)]
        public string MantraId;
    }

    [Description("增益條件/攻擊者是否某人")]
    public class AttackerCharacterIdCondition : BufferCondition
    {
        public override Status GetValue()
        {
            WuxiaUnit attacker = BattleGlobalVariable.CurrentDamage.Attacker;
            Status result;
            if (attacker == null)
            {
                result = Status.Failure;
            }
            else if (attacker.CharacterInfoId == this.CharacterId == this.IsMatch)
            {
                result = Status.Success;
            }
            else
            {
                result = Status.Failure;
            }
            return result;
        }

        [InputField("指定的character id", false, null)]
        public string CharacterId = string.Empty;

        [InputField("是否对应", false, null)]
        public bool IsMatch = true;
    }

    [Description("增益條件/判斷队友数量")]
    public class PartyNumberCondition : BufferCompareCondition
    {
        protected override int GetProperty()
        {
            int result = 0;
            INodeGraph graph = base.Graph;
            BufferInfo bufferInfo = graph?.GetVariable<BufferInfo>("BufferInfo");
            if (bufferInfo != null)
            {
                Data.Faction faction = bufferInfo.Unit.faction;
                foreach (WuxiaUnit unit in bufferInfo.Manager.UnitGenerator.WuxiaUnits)
                {
                    if (!unit.IsDead && !unit.IsEnemy(faction))
                    {
                        result++;
                    }
                }
            }
            return result;
        }
    }

    [Description("增益條件/判斷防禦者性别")]
    public class DefenderGenderCondition : BufferCondition
    {
        public override Status GetValue()
        {
            Damage currentDamage = BattleGlobalVariable.CurrentDamage;
            Status result;
            if (currentDamage == null)
            {
                result = Status.Failure;
            }
            else if (currentDamage.Defender.Gender == this.Gender)
            {
                result = Status.Success;
            }
            else
            {
                result = Status.Failure;
            }
            return result;
        }

        [InputField("性别", false, null)]
        public Data.Gender Gender;
    }
   }
