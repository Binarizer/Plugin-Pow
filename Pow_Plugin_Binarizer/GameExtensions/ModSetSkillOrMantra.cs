using System;
using Heluo;
using Heluo.Data;
using Heluo.UI;
using Heluo.Flow;
using UnityEngine;

namespace PathOfWuxia
{
    [Description("ModSetSkillOrMantra")]
    public class ModSetSkillOrMantra : CalculatorAction
	{
		public override bool GetValue()
		{
			if (!Application.isPlaying)
			{
				return false;
			}
			if (string.IsNullOrEmpty(characterInfoId))
			{
				return false;
			}
			if (Game.GameData == null)
			{
				return false;
			}
			CharacterInfoData characterInfoData = Game.GameData.Character[characterInfoId];
			string itemName;
			if (isSkill)
			{
				if (Game.Data.Get<Skill>(skillOrMantraId) == null)
				{
					Console.WriteLine($"ModSetSkillOrMantra: 找不到技能{value}");
					return false;
				}
				if (value > 0)
				{
					characterInfoData.Skill[skillOrMantraId].Exp = Execute(characterInfoData.Skill[skillOrMantraId].Exp);
					characterInfoData.Skill[skillOrMantraId].AddExp(0);
				}
				else
				{
					switch (method)
					{
						case Method.Assign:
						case Method.Add:
							characterInfoData.LearnSkill(skillOrMantraId, false, true);
							break;
						case Method.Sub:
						case Method.Clear:
							characterInfoData.AbolishSkill(skillOrMantraId);
							break;
					}
				}
				SkillData skillData = characterInfoData.Skill[skillOrMantraId];
				itemName = skillData.Item.Name;
			}
			else
            {
				if (Game.Data.Get<Mantra>(skillOrMantraId) == null)
				{
					Console.WriteLine($"ModSetSkillOrMantra: 找不到心法{skillOrMantraId}");
					return false;
				}
				if (value > 0)
				{
					characterInfoData.Mantra[skillOrMantraId].Exp = Execute(characterInfoData.Mantra[skillOrMantraId].Exp);
					characterInfoData.Mantra[skillOrMantraId].AddExp(0);
				}
				else
				{
					switch (method)
					{
						case Method.Assign:
						case Method.Add:
							characterInfoData.LearnMantra(skillOrMantraId);
							break;
						case Method.Sub:
						case Method.Clear:
							characterInfoData.AbolishMantra(skillOrMantraId);
							break;
					}
				}
				MantraData mantraData = characterInfoData.Mantra[skillOrMantraId];
				itemName = mantraData.Item.Name;
			}
			if (Graph != null)
			{
				INodeGraph graph = Graph;
				if (!(bool)((graph != null) ? graph.GetVariable("IsShowMessage") : null))
				{
					return true;
				}
			}
			ShowMessage(itemName);
			return true;
		}

		protected void ShowMessage(string name)
		{
			if (method <= Method.Add)
			{
				string locIdLearn = isSkill ? "Reward30201_NpcLearnSkill" : "Reward30801_NpcLearnMantra";
				string locIdExp = isSkill ? "Reward30401_NpcGetSkillExp" : "Reward31001_NpcGetMantraExp";
				string locId = (value == 0) ? locIdLearn : locIdExp;
				string format = Game.Data.Get<StringTable>(locId).Text;
				string text = string.Format(format, "", name, value);
				Game.UI.AddMessage(text, UIPromptMessage.PromptType.Special);
			}
		}

		[InputField("角色InfoId", true, null)]
		public string characterInfoId;

		[InputField("是否为招式，否则为内功", true, null)]
		public bool isSkill;

		[InputField("招式名", true, null)]
		public string skillOrMantraId;
	}
}