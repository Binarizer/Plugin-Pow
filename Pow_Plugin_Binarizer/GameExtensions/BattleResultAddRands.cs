using System;
using System.Collections.Generic;
using System.Linq;
using Heluo.Battle;
using Heluo.Data;
using Heluo;
using Heluo.Flow;
using Heluo.Flow.Battle;

namespace PathOfWuxia
{
    // Token: 0x02000780 RID: 1920
    [Description("動作/新增移除/加入複數随机角色")]
    [Serializable]
    public class BattleResultAddRands : BattleActionNode
    {
        // Token: 0x06002C7B RID: 11387 RVA: 0x000F030C File Offset: 0x000EE50C
        public override Status GetValue()
        {
            if (WuxiaBattleManager == null)
            {
                return Status.Error;
            }
            for (int i = 0; i < Count; i++)
            {
                string pattern = (i == 0) ? Pattern : "*";
                Console.WriteLine("Random Pattern " + pattern);
                Npc npc = Randomizer.GetOneFromData<Npc>(pattern);
                if (npc == null)
                    return Status.Error;
                int tileNumber = (i == 0) ? CellNumber : -Range - 1;
                WuxiaUnit wuxiaUnit = WuxiaBattleManager.AddUnit(npc.Id, Faction, tileNumber, !FullHealth);
                WuxiaBattleManager.FacedToNearestEnemy(wuxiaUnit);
            }
            return Status.Success;
        }

        public override string ToString()
        {
            return string.Concat(new string[]
            {
                "加入複數随机角色 : ",
                Pattern,
                " 至 : ",
                CellNumber.ToString(),
                " 格子"
            });
        }

        // Token: 0x040028E7 RID: 10471
        [InputField("表达式", false, null)]
        public string Pattern;

        // Token: 0x040028E8 RID: 10472
        [InputField("陣營", false, null)]
        public Faction Faction;

        // Token: 0x040028E7 RID: 10471
        [InputField("部隊数量", false, null)]
        public int Count = 1;

        // Token: 0x040028E9 RID: 10473
        [InputField("首个格子編號", false, null)]
        public int CellNumber = -1;

        // Token: 0x040028E9 RID: 10473
        [InputField("随机间距", false, null)]
        public int Range = 4;

        // Token: 0x040028E8 RID: 10472
        [InputField("是否恢复", false, null)]
        public bool FullHealth = true;
    }
}
