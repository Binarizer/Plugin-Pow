// 随机物品类
using Heluo;
using Heluo.Data;
using Heluo.UI;
using Heluo.Flow;
using UnityEngine;

namespace PathOfWuxia
{
    [Description("獎勵/玩家財產/随机道具")]
    public class RewardRandomProps : GameAction
    {
        protected string GetId()
        {
            return this.propsReg;
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
                if (this.value < 1)
                {
                    return false;
                }
                for (int i = 0; i < this.value; i++)
                {
                    Props s = Randomizer.GetOneFromData<Props>((i == 0) ? this.propsReg : "*");
                    if (s == null)
                    {
                        return false;
                    }
                    Game.GameData.Inventory.Add(s.Id, 1, true);
                    if (base.Graph != null && (bool)base.Graph.GetVariable("IsShowMessage"))
                    {
                        this.ShowMessage(s.Name, 1);
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
            return string.Format("道具編號 : {0} 的數量 {1}", this.GetId(), this.value);
        }

        [InputField("Props 正则表达式", true, null)]
        public string propsReg;

        [InputField("随机获得次数", true, null)]
        public int value;
    }
}