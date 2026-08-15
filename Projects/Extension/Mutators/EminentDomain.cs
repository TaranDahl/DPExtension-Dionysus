using Extension.Decorators;
using Extension.Ext;
using Extension.Utilities;
using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Mutators
{
    [Serializable]
    public class EminentDomain : BuffMutator
    {
        // Mutator
        public override string UIName => "强行征用";
        public override string Description => "敌人摧毁你的建筑后将获得建筑的控制权。";
        public override bool IsAvailableInRPG => true;
        public override int Score => 1;
        public EminentDomain(Pointer<HouseClass> owner) : base(owner) { }
        public override void Init(bool isInitial = true)
        {
            base.Init();
            foreach (var techno in TechnoClass.Array)
            {
                if (IsTechnoValid(techno))
                    BuffTechno(techno);
            }
        }
        public override void Uninit()
        {
            foreach (var techno in TechnoClass.Array)
            {
                if (IsTechnoValid(techno))
                    UnbuffTechno(techno);
            }
            base.Uninit();
        }

        // BuffMutator
        protected override bool IsBuffEnemy => true;
        protected override void BuffTechno(Pointer<TechnoClass> techno)
        {
            var ext = TechnoExt.ExtMap.Find(techno);
            if (ext.Get(EminentDomainBuff.ID) == null)
                ext.CreateDecorator<EminentDomainBuff>(EminentDomainBuff.ID, "EminentDomainBuff", this);
        }
        protected override void UnbuffTechno(Pointer<TechnoClass> techno)
        {
            var ext = TechnoExt.ExtMap.Find(techno);
            ext.Remove(EminentDomainBuff.ID);
        }
        public override void OnTechnoCTOR(Pointer<TechnoClass> techno)
        {
            if (IsTechnoValid(techno))
                BuffTechno(techno);
        }
        public override void OnTechnoChangeOwner(Pointer<TechnoClass> techno, Pointer<HouseClass> toHouse)
        {
            if (!IsTechnoOwnerValid(toHouse))
                UnbuffTechno(techno);
            else
                BuffTechno(techno);
        }

        // EminentDomain
        private bool IsTechnoValid(Pointer<TechnoClass> techno)
        {
            if (!IsTechnoOwnerValid(techno.Ref.BaseAbstract.GetOwningHouse()))
                return false;

            // 非建筑不能被强征
            if ((techno.Ref.BaseAbstract.AbstractFlags & AbstractFlags.Foot) != AbstractFlags.None)
                return false;
            return true;
        }

        [Serializable]
        private class EminentDomainBuff : MutatorEventDecorator
        {
            public EminentDomainBuff(Mutator mutator) : base(mutator) { }

            // Decorator
            public static new DecoratorId ID => new DecoratorId((int)TechnoDecoratorIDs.EminentDomainBuff);
            public override int Priority => -1;

            // EventDecorator
            public override DamageState AfterReceiveDamage(Pointer<int> pDamage, int DistanceFromEpicenter, Pointer<WarheadTypeClass> pWH, Pointer<ObjectClass> pAttacker, bool IgnoreDefenses, bool PreventPassengerEscape, Pointer<HouseClass> pAttackingHouse, DamageState result, int damageTaken)
            {
                do
                {
                    // 必须已死亡
                    if (result != DamageState.NowDead)
                        break;
                    // 必须是建筑
                    var pThis = (Decorative as TechnoExt).OwnerObject;
                    if ((pThis.Ref.BaseAbstract.AbstractFlags & AbstractFlags.Foot) != AbstractFlags.None)
                        break;
                    // 必须是对方的
                    if (!myMutator.IsOnTheirSide(pThis.Ref.BaseAbstract.GetOwningHouse()))
                        break;
                    // 伤害不能来自自己以外的友军
                    if (pAttacker.IsNotNull && pAttacker != pThis.Convert<ObjectClass>() && pThis.Ref.BaseAbstract.GetOwningHouse().Ref.IsAlliedWith(pAttacker))
                        break;
                    // 强征不能阻挡移动
                    if (pAttacker.IsNotNull)
                    {
                        bool blocked = true;
                        // 必须和至少一个敌人基地位置陆地联通
                        foreach (var house in HouseClass.Array)
                        {
                            if (!blocked)
                                break;

                            // 目标必须是敌人
                            if (!myMutator.IsOnTheirSide(house))
                                continue;

                            foreach (var building in house.Ref.Buildings)
                            {
                                if (!blocked)
                                    break;

                                var buildingCell = building.Ref.Base.Base.GetCell();
                                var targetLandType = buildingCell.Ref.LandType;

                                // 目标必须是保持存活的建筑
                                if (TargetTechnoMutator.GetKeepAliveAbility(building.Convert<TechnoClass>()) == 0)
                                    continue;

                                // 必须联通
                                if (MapClass.IsInSameZone(pThis.Ref.BaseAbstract.GetMapCrd(), buildingCell.Ref.MapCoords, MovementZone.Infantry, false, false, false) // 和强征建筑联通
                                    && !MapClass.IsInSameZone(pAttacker.Ref.Base.GetMapCrd(), buildingCell.Ref.MapCoords, MovementZone.Infantry, false, false, false)) // 和当前单位位置不联通
                                    continue;
                                blocked = false;
                            }
                        }
                        if (blocked)
                            break;
                    }
                    // 找一个作战方
                    var toHouse = Pointer<HouseClass>.Zero;
                    if (myMutator.IsOnOurSide(pAttackingHouse))
                        toHouse = pAttackingHouse;
                    else if (pAttacker.IsNotNull && myMutator.IsOnOurSide(pAttacker.Ref.Base.GetOwningHouse()))
                        toHouse = pAttacker.Ref.Base.GetOwningHouse();
                    else
                        toHouse = myMutator.GetRandomHouseOnOurSide();
                    if (toHouse.IsNotNull)
                    {
                        // 免疫本次伤害
                        result = DamageState.Unaffected;
                        // 血量回满
                        pThis.Ref.Base.Health = pThis.Ref.GetTechnoType().Ref.Base.Strength;
                        // 改变所有者
                        pThis.Ref.SetOwningHouse(toHouse);
                    }
                }
                while (false);
                return result;
            }
        }
    }
}
