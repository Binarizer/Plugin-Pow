using System.Collections.Generic;
using System.Threading.Tasks;
using Heluo;
using Heluo.Data;
using Heluo.Events;
using Heluo.Resource;
using Heluo.Utility;
using Heluo.Battle;
using UnityEngine;

namespace PathOfWuxia
{
    public class ModSummonProcessStrategy : BattleProcessStrategy
    {
        public ModSummonProcessStrategy(WuxiaBattleManager manager, IDataProvider data, IResourceProvider resource) : base(manager, data, resource)
        {
        }

        public override async Task Process(BattleEventArgs arg)
        {
            AttackEventArgs e = arg as AttackEventArgs;
            string[] array = e.Skill.Item.Summonid.Split(new char[]
            {
                ','
            });
            e.Attacker.FaceTo(e.Point.transform.position);
            this.damageInfo = this.CreateDamageInfo(e.Attacker, e.Defender, e.Skill);
            this.damageInfo.FloorEffect = e.Point.gameObject;
            await 0.1f;
            for (int i = 0; i < array.Length; i++)
            {
                string summonid = array[i];
                this.manager.GetCellNearestCamera();
                WuxiaCell point = e.Point;
                WuxiaUnit unit = this.manager.AddUnit(summonid, e.Attacker.faction, (i == 0) ? point.CellNumber : -2, false);
                unit.Actor.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
                unit.transform.forward = e.Attacker.transform.forward;
                unit.gameObject.OnTerrain(unit.transform.position);
                unit.gameObject.SetActive(false);
                this.summonUnits.Add(unit);
                unit.OnTurnStart();
                // 半即时支持
                if (HookInitiactiveBattle.bTimed)
                {
                    HookInitiactiveBattle.Timed_EncourageUnit(unit);
                }
            }
            await 0.1f;
            if (GameConfig.IsSkipSkillEffect)
            {
                this.ProcessAnimationTrack(this.damageInfo);
            }
            else
            {
                Task<float> attackEventTime = this.ProcessAnimation(this.damageInfo, 0f);
                await attackEventTime;
                await attackEventTime.Result;
            }
            base.ProcessSkillMPCost(e);
            if (e.Attacker[BattleLiberatedState.No_CoolDown] == 0)
            {
                e.Skill.CurrentCD = e.Skill.MaxCD;
            }
        }

        public override void OnEffectPostProcess(EffectEventArgs e)
        {
            if (this.damageInfo != null)
            {
                foreach (WuxiaUnit unit in this.summonUnits)
                {
                    e.Target = new List<GameObject>();
                    if (this.damageInfo.FloorEffect != null)
                    {
                        e.Target.Add(unit.gameObject);
                    }
                    if (e.IsShowSumon)
                    {
                        unit.gameObject.SetActive(true);
                        unit.Actor.PlayAnimation("be0101_stand00_await02", null, WrapMode.Once);
                    }
                }
            }
        }

        private List<WuxiaUnit> summonUnits = new List<WuxiaUnit>();
    }
}
