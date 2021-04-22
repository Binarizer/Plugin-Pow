// 对决奖励Action
using System.Collections.Generic;
using Heluo;
using Heluo.Data;
using Heluo.UI;
using Heluo.Flow;
using UnityEngine;

namespace PathOfWuxia
{
    [Description("切磋奖励（数值差）")]
    public class RewardDuelProperty : GameAction
    {
        static readonly CharacterUpgradableProperty[] propertyTypes = new CharacterUpgradableProperty[]
        {
            CharacterUpgradableProperty.Str,
            CharacterUpgradableProperty.Vit,
            CharacterUpgradableProperty.Dex,
            CharacterUpgradableProperty.Spi
        };
        static readonly string[] messages = new string[]
        {
            "NurturanceProperty_Str",
            "NurturanceProperty_Vit",
            "NurturanceProperty_Dex",
            "NurturanceProperty_Spi"
        };
        public override bool GetValue()
        {
            if (Application.isPlaying)
            {
                string fromId = GlobalLib.ReplaceText(fromInfoId);
                string toId = GlobalLib.ReplaceText(toInfoId);
                CharacterInfoData from = Game.GameData.Character[fromId];
                CharacterInfoData to = Game.GameData.Character[toId];
                CharacterExteriorData toEx = Game.GameData.Exterior[toId];
                if (from == null || to == null || toEx == null || from == to)
                    return false;

                float totalDelta = 0;
                for (int i = 0; i < propertyTypes.Length; ++i)
                {
                    var propertyType = propertyTypes[i];
                    totalDelta += from.GetUpgradeableProperty(propertyType) - to.GetUpgradeableProperty(propertyType);
                }
                float scale = scalar * (2f - totalDelta / 2000f);

                List<string> list = new List<string>();
                for (int i = 0; i < propertyTypes.Length; ++i)
                {
                    var propertyType = propertyTypes[i];
                    int delta = from.GetUpgradeableProperty(propertyType) - to.GetUpgradeableProperty(propertyType) + baseValue;
                    int exp = (int)Mathf.Max(0f, delta * scale);
                    if (exp >= 100)
                    {
                        to.AddUpgradeablePropertyExp(propertyType, exp);
                        to.UpgradeProperty(false);
                        list.Add(string.Format("{0}提升{1}", Game.Data.Get<StringTable>(messages[i]).Text, exp / 100));
                    }
                }
                if (list.Count > 0)
                {
                    Game.UI.AddMessage(string.Format("{0}经切磋，{1}", toEx.FullName(), string.Join("，", list)), UIPromptMessage.PromptType.Normal);
                }
                else
                {
                    Game.UI.AddMessage(string.Format("{0}由于四维超出对手过多，切磋后未获提升", toEx.FullName()), UIPromptMessage.PromptType.Normal);
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        public override string ToString()
        {
            return string.Format("{0}与{1}切磋获得属性", toInfoId, fromInfoId);
        }

        [InputField("从谁获得", true, null)]
        public string fromInfoId;

        [InputField("谁获得", true, null)]
        public string toInfoId = "Player";

        [InputField("数值基数", true, null)]
        public int baseValue = 50;

        [InputField("数值系数", true, null)]
        public float scalar = 2f;
    }
}