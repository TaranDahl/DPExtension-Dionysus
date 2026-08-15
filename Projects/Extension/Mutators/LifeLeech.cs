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
    public class LifeLeech : BuffMutator
    {
        // Mutator
        public override string UIName => "生命吸取";
        public override string Description => "敌方单位和建筑在造成伤害时偷取生命值或护盾。";
        public override bool IsAvailableInRPG => true;
        public override int Score => 1;
        public LifeLeech(Pointer<HouseClass> owner) : base(owner) { }
        
        public override void Init(bool isInitial = true)
        {
            base.Init();
            // 生效时，给所有符合条件的敌方单位贴buff
            foreach (var techno in TechnoClass.Array)
            {
                if (IsTechnoValid(techno))
                    BuffTechno(techno);
            }
        }
        
        public override void Uninit()
        {
            // 失效时移除所有敌方单位身上的buff
            foreach (var techno in TechnoClass.Array)
            {
                if (IsTechnoValid(techno))
                    UnbuffTechno(techno);
            }
            base.Uninit();
        }

        // BuffMutator
        protected override bool IsBuffEnemy => false;
        
        protected override void BuffTechno(Pointer<TechnoClass> techno)
        {
            var ext = TechnoExt.ExtMap.Find(techno);
            if (ext.Get(LifeLeechBuff.ID) == null)
                ext.CreateDecorator<LifeLeechBuff>(LifeLeechBuff.ID, "LifeLeechBuff", this);
        }
        
        protected override void UnbuffTechno(Pointer<TechnoClass> techno)
        {
            var ext = TechnoExt.ExtMap.Find(techno);
            ext.Remove(LifeLeechBuff.ID);
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

        // LifeLeech
        private bool IsTechnoValid(Pointer<TechnoClass> techno)
        {
            if (!IsTechnoOwnerValid(techno.Ref.BaseAbstract.GetOwningHouse()))
                return false;

            return true;
        }

        [Serializable]
        private class LifeLeechBuff : EventDecorator
        {
            // Decorator
            public static new DecoratorId ID => new DecoratorId((int)TechnoDecoratorIDs.LifeLeechBuff);

            // EventDecorator
            public override DamageState OnDealDamage(Pointer<int> pDamage, int DistanceFromEpicenter, Pointer<WarheadTypeClass> pWH, 
                Pointer<TechnoClass> pVictim, bool IgnoreDefenses, bool PreventPassengerEscape, Pointer<HouseClass> pAttackingHouse, 
                DamageState result, int damageDealt)
            {
                // 如果因子失效了则不恢复
                if (!myMutator.IsActive())
                    return result;

                // 必须有伤害
                if (damageDealt <= 0)
                    return result;

                // 受害者必须是敌人
                var attacker = (Decorative as TechnoExt).OwnerObject;
                if (!myMutator.IsOnTheirSide(pVictim.Ref.BaseAbstract.GetOwningHouse()))
                    return result;

                // 为攻击者恢复等量的生命
                int recoveredHealth = Math.Min(damageDealt, attacker.Ref.GetTechnoType().Ref.Base.Strength - attacker.Ref.Base.Health);
                if (recoveredHealth > 0)
                {
                    attacker.Ref.Base.Health += recoveredHealth;
                }

                return result;
            }

            // LifeLeechBuff
            public LifeLeech myMutator { get; private set; }
            
            public LifeLeechBuff(LifeLeech mutator)
            {
                myMutator = mutator;
            }
        }
    }
}
