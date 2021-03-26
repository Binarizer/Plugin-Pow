// Mod用随机战场布局辅助类
using System;
using System.Collections.Generic;
using Heluo.Battle;
using Heluo.Data;
using Heluo.Utility;
using UnityEngine;

namespace PathOfWuxia
{
    public static class AddUnitHelper
    {
        private static List<int> GetAllCells(WuxiaBattleManager m)
        {
            List<int> list = new List<int>();
            foreach (WuxiaCell wuxiaCell in m.GetAllCell())
            {
                if (wuxiaCell.Walkable && wuxiaCell.Unit == null)
                {
                    list.Add(wuxiaCell.CellNumber);
                }
            }
            return list;
        }

        private static List<int> GetCellsInRange(WuxiaBattleManager m, int cellNumber, int range)
        {
            WuxiaCell wuxiaCellByNumber = m.GridGenerator.GetWuxiaCellByNumber(cellNumber);
            List<int> list = new List<int>();
            foreach (WuxiaCell wuxiaCell in m.GetCellRange(wuxiaCellByNumber.Coord, Faction.None, range, true, true))
            {
                if (wuxiaCell.Walkable && wuxiaCell.Unit == null)
                {
                    list.Add(wuxiaCell.CellNumber);
                }
            }
            return list;
        }
        public static void ProcessUnitId(WuxiaBattleManager m, ref string id)
        {
            Console.WriteLine("id="+id);
            Npc npc = Randomizer.GetOneFromData<Npc>(id);
            if ( npc != null )
                id = npc.Id;
        }

        public static void ProcessCellNumber(WuxiaBattleManager m, ref int num)
        {
            try
            {
                if (num == -1)
                {
                    AddUnitHelper._LastCellNumber = AddUnitHelper.GetAllCells(m).Random<int>();
                    num = AddUnitHelper._LastCellNumber;
                }
                else if (num >= 0)
                {
                    AddUnitHelper._LastCellNumber = num;
                }
                else if (AddUnitHelper._LastCellNumber <= 0)
                {
                    AddUnitHelper._LastCellNumber = AddUnitHelper.GetAllCells(m).Random<int>();
                    num = AddUnitHelper._LastCellNumber;
                }
                else
                {
                    int range = -num - 1;
                    num = AddUnitHelper.GetCellsInRange(m, AddUnitHelper._LastCellNumber, range).Random<int>();
                }
            }
            catch
            {
                Debug.LogError("这个格子数字错误：" + num);
            }
        }

        private static int _LastCellNumber;
    }
}