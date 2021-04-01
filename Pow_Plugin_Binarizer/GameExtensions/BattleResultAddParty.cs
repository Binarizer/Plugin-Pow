using System;
using Heluo;
using Heluo.Battle;
using Heluo.Data;
using Heluo.Flow;
using Heluo.Flow.Battle;

namespace PathOfWuxia
{
    [Description("動作/新增移除/加入玩家整隊")]
    [Serializable]
    public class BattleResultAddParty : BattleActionNode
    {
        public override Status GetValue()
        {
            Console.WriteLine("这特么鬼？");
            if (base.WuxiaBattleManager == null)
            {
                return Status.Error;
            }
            string[] array2 = this.CellNumber.Split(new char[]
            {
                ','
            });

            int max = ((count > 0) ? count : Game.GameData.Party.Count);
            for (int i = 0; i < max; i++)
            {
                string text = Game.GameData.Party.GetPartyByIndex(i);
                int tileNumber = 0;
                if (i < array2.Length)
                {
                    tileNumber = int.Parse(array2[i]);
                }
                else
                {
                    tileNumber = -3;    // 距离2格随机
                }
                WuxiaUnit wuxiaUnit = base.WuxiaBattleManager.AddUnit(text, Faction.Player, tileNumber, !fullHealth);
                base.WuxiaBattleManager.FacedToNearestEnemy(wuxiaUnit);
            }
            return Status.Success;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "加入全隊 : ",
                " 至 : ",
                this.CellNumber,
                " 格子"
            });
        }

        [InputField("格子編號(逗號分開，數量必須對應部隊ID)", false, null)]
        public string CellNumber;

        [InputField("数量，小于1则为全部", false, null)]
        public int count = 0;

        [InputField("是否满血", false, null)]
        public bool fullHealth = false;
    }
}
