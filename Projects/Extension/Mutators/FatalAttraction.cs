using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicPatcher;
using Extension.Decorators;
using Extension.Ext;
using Extension.Utilities;
using PatcherYRpp;

namespace Extension.Mutators
{
    [Serializable]
    public class FatalAttraction : BuffMutator
    {
        // Mutator
        public override string UIName => "致命勾引";
        public override string Description => "敌方单位或建筑被摧毁后，你附近的任何单位将被牵拉至被它们的位置。";
        public override bool IsAvailableInRPG => true;
        public override int Score => 3;
        public FatalAttraction(Pointer<HouseClass> owner) : base(owner) { }

        public override void Init(bool isInitial = true)
        {
            base.Init();
            foreach (var techno in TechnoClass.Array)
            {
                if (!IsTechnoValid(techno))
                    continue;

                BuffTechno(techno);
            }
        }

        public override void Uninit()
        {
            foreach (var techno in TechnoClass.Array)
            {
                if (!IsTechnoValid(techno))
                    continue;

                UnbuffTechno(techno);
            }
            base.Uninit();
        }

        // BuffMutator
        protected override bool IsBuffEnemy => false;
        protected override void BuffTechno(Pointer<TechnoClass> techno)
        {
            var ext = TechnoExt.ExtMap.Find(techno);
            if (ext.Get(FatalAttractionBuff.ID) == null)
                ext.CreateDecorator<FatalAttractionBuff>(FatalAttractionBuff.ID, "FatalAttractionBuff", this);
        }
        protected override void UnbuffTechno(Pointer<TechnoClass> techno)
        {
            var ext = TechnoExt.ExtMap.Find(techno);
            ext.Remove(FatalAttractionBuff.ID);
        }

        public override void OnTechnoCTOR(Pointer<TechnoClass> techno)
        {
            if (!IsTechnoValid(techno))
                return;

            BuffTechno(techno);
        }

        public override void OnTechnoChangeOwner(Pointer<TechnoClass> techno, Pointer<HouseClass> toHouse)
        {
            if (!IsTechnoOwnerValid(toHouse))
                UnbuffTechno(techno);
            else
                BuffTechno(techno);
        }

        // FatalAttraction
        private bool IsTechnoValid(Pointer<TechnoClass> techno)
        {
            var technoOwner = techno.Ref.Owner;
            return IsTechnoOwnerValid(technoOwner);
        }

        [Serializable]
        private class FatalAttractionBuff : EventDecorator
        {
            // static
            private static Pointer<WeaponTypeClass> GetWeaponByLevel(int level)
            {
                if (level <= 3)
                    return WeaponTypeClass.ABSTRACTTYPE_ARRAY.Find("FatalAttractionLv1");
                else if (level <= 7)
                    return WeaponTypeClass.ABSTRACTTYPE_ARRAY.Find("FatalAttractionLv2");
                else
                    return WeaponTypeClass.ABSTRACTTYPE_ARRAY.Find("FatalAttractionLv3");
            }

            // Decorator
            public new static DecoratorId ID => new DecoratorId((int)TechnoDecoratorIDs.FatalAttractionBuff);

            // EventDecorator
            public override DamageState AfterReceiveDamage(Pointer<int> pDamage, int DistanceFromEpicenter, Pointer<WarheadTypeClass> pWH,
                Pointer<ObjectClass> pAttacker, bool IgnoreDefenses, bool PreventPassengerEscape, Pointer<HouseClass> pAttackingHouse, DamageState result, int damageTaken)
            {
                // 只在单位死亡时触发
                if (result != DamageState.NowDead)
                    return result;

                // 因子失效了则不引爆
                if (!myMutator.IsActive())
                    return result;

                TechnoExt victimExt = Decorative as TechnoExt;
                var victim = victimExt.OwnerObject;
                var deathCrd = victim.Ref.Base.Base.GetCoords();

                // 根据等级选择武器
                int level = (int)GetLevel(victim.Ref.GetTechnoType());
                var weapon = GetWeaponByLevel(level);

                if (weapon.IsNull)
                    return result;

                var firer = victim.Ref.BaseAbstract.GetOwningHouse();
                if (firer.IsNull)
                    return result;

                // 在死亡位置引爆武器
                var pCell = MapClass.Instance.GetCellAt(deathCrd);
                WeaponTypeExt.Detonate(
                    weapon,
                    deathCrd,
                    Pointer<AbstractClass>.Zero,
                    victim,
                    firer);

                return result;
            }

            // FatalAttractionBuff
            public FatalAttraction myMutator { get; private set; }

            public FatalAttractionBuff(FatalAttraction mutator)
            {
                myMutator = mutator;
            }
        }
    }
}
