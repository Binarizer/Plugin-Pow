using Heluo;
using Heluo.Data;
using Heluo.UI;
using Heluo.Flow;
using UnityEngine;

namespace PathOfWuxia
{
    [Description("独特装备(重铸)")]
    public class RewardUniqueEquipByValue : GameAction
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
                if (count < 1)
                {
                    return false;
                }
                for (int i = 0; i < this.count; i++)
                {
                    Props uniqueProps = GlobalLib.CreateUniquePropsByValue(sourceId, value);
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
