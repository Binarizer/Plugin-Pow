using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Heluo;
using Heluo.Data;
using Heluo.UI;

namespace PathOfWuxia
{
    public static class GlobalLib
    {
        public static CharacterMapping GetUICharacterMapping()
        {
            try
            {
                var uiHome = Game.UI.Get<UIHome>();
                var ctrlHome = Traverse.Create(uiHome).Field("controller").GetValue<CtrlHome>();
                var cml = Traverse.Create(ctrlHome).Field("characterMapping").GetValue<List<CharacterMapping>>();
                if (cml.Count == 0)
                    ctrlHome.OnShow();
                var ci = Traverse.Create(ctrlHome).Field("communityIndex").GetValue<int>();
                if (ci >= cml.Count)
                    Traverse.Create(ctrlHome).Field("communityIndex").SetValue(cml.Count - 1);
                return cml[ci];
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string ModPath { get; set; }

        public static PropsCategory GetScrollType(PropsCategory skillType)
        {
            if (skillType < PropsCategory.Throwing)
            {
                return skillType - PropsCategory.Fist + PropsCategory.Fist_Secret_Scroll;
            }
            return PropsCategory.Throw_Secret_Scroll;
        }

        public static bool HasSkillType(string infoId, PropsCategory skillType)
        {
            var info = Game.GameData.Character[infoId];
            if (info == null)
                return false;
            foreach (var skill in info.Skill.Values)
            {
                if (skill.Item.Type == skillType)  // 只要学会过一项同系招式即可
                    return true;
            }
            return false;
        }
    }
}
