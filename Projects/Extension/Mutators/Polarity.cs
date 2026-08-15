using DynamicPatcher;
using Extension.Decorators;
using Extension.Ext;
using InteropUtils;
using PatcherYRpp;
using System;
using System.Collections.Generic;

namespace Extension.Mutators
{
    [Serializable]
    public class Polarity : BuffMutator
    {
        public override string UIName => "极性不定";
        public override string Description => "每一个敌方单位不是对你的单位免疫，就是对你盟友的单位免疫。";
        public override bool IsAvailableInRPG => false;
        public override int Score => 7;

        public Polarity(Pointer<HouseClass> owner) : base(owner) { }

        private static readonly string[] AETypes = new[] { "MutPolarityAE" };
        private static bool IsCallbackRegistered = false;

        // BuffMutator
        protected override bool IsBuffEnemy => false;

        public override void Init(bool isInitial = true)
        {
            base.Init(isInitial);

            // 注册重重索敌威胁值回调
            if (!IsCallbackRegistered)
            {
                PhobosTechnoExt.RegisterCalculateExtraThreatProvider(CalculateExtraThreat);
                IsCallbackRegistered = true;
            }

            foreach (var techno in TechnoClass.Array)
            {
                if (IsTechnoValid(techno))
                {
                    BuffTechno(techno);
                }
            }
        }

        public override void Uninit()
        {
            foreach (var techno in TechnoClass.Array)
            {
                UnbuffTechno(techno);
            }

            base.Uninit();
        }

        protected override void BuffTechno(Pointer<TechnoClass> techno)
        {
            if (techno.IsNull)
            {
                return;
            }

            var polar = SelectPolarHouse();
            if (polar.IsNull)
            {
                return;
            }

            // 先清理旧效果，避免重复/脏状态
            PhobosAttachEffect.Detach(techno, AETypes, out _);

            PhobosAttachEffect.Attach(
                techno,
                polar,
                Pointer<TechnoClass>.Zero,
                Pointer<AbstractClass>.Zero,
                AETypes,
                out _);

            var ext = TechnoExt.ExtMap.Find(techno);
            ext.Remove(PolarityBuff.ID);
            ext.CreateDecorator<PolarityBuff>(PolarityBuff.ID, "MutatorPolarity", polar);
        }

        protected override void UnbuffTechno(Pointer<TechnoClass> techno)
        {
            if (techno.IsNull)
            {
                return;
            }

            PhobosAttachEffect.Detach(techno, AETypes, out _);

            var ext = TechnoExt.ExtMap.Find(techno);
            ext.Remove(PolarityBuff.ID);
        }

        public override void OnTechnoCTOR(Pointer<TechnoClass> techno)
        {
            if (IsTechnoValid(techno))
            {
                BuffTechno(techno);
            }
        }

        public override void OnTechnoChangeOwner(Pointer<TechnoClass> techno, Pointer<HouseClass> toHouse)
        {
            if (!IsTechnoOwnerValid(toHouse))
            {
                UnbuffTechno(techno);
            }
            else
            {
                BuffTechno(techno);
            }
        }

        private Pointer<HouseClass> SelectPolarHouse()
        {
            var candidates = new List<Pointer<HouseClass>>();
            foreach (var house in HouseClass.Array)
            {
                if (IsOnTheirSide(house))
                {
                    candidates.Add(house);
                }
            }

            if (candidates.Count > 0)
            {
                return ScenarioClass.GetRandomInList(candidates);
            }

            Logger.Log("Polarity: no valid Polar house found.");
            return Pointer<HouseClass>.Zero;
        }

        private bool IsTechnoValid(Pointer<TechnoClass> techno)
        {
            if (techno.IsNull)
            {
                return false;
            }

            if (!IsTechnoOwnerValid(techno.Ref.BaseAbstract.GetOwningHouse()))
            {
                return false;
            }

            return true;
        }

        private static double CalculateExtraThreat(Pointer<TechnoClass> pThis, Pointer<ObjectClass> pTarget, double originalThreat)
        {
            if (pThis.IsNull || pTarget.IsNull)
            {
                return originalThreat;
            }

            // 检查目标是否是可以检索Buff的Techno
            if ((pTarget.Ref.Base.AbstractFlags & AbstractFlags.Techno) != AbstractFlags.None)
            {
                var targetTechno = pTarget.Convert<TechnoClass>();
                var ext = TechnoExt.ExtMap.Find(targetTechno);
                
                if (ext != null)
                {
                    var buff = ext.Get(PolarityBuff.ID) as PolarityBuff;
                    if (buff != null && buff.PolarArrayIndex >= 0)
                    {
                        var pHouse = pThis.Ref.BaseAbstract.GetOwningHouse();
                        if (pHouse.IsNotNull && pHouse.Ref.ArrayIndex == buff.PolarArrayIndex)
                        {
                            return 0; // 若打不动，则去掉额外仇恨以使得单位被最后攻击
                        }
                    }
                }
            }

            return originalThreat;
        }

        [Serializable]
        private class PolarityBuff : EventDecorator
        {
            public static DecoratorId ID => new DecoratorId((int)Mutator.TechnoDecoratorIDs.PolarityBuff);

            public int PolarArrayIndex { get; private set; } // 设为可公开访问以供委托获取

            public PolarityBuff(Pointer<HouseClass> polar)
            {
                PolarArrayIndex = polar.IsNotNull ? polar.Ref.ArrayIndex : -1;
            }

            public override void OnReceiveDamage(
                Pointer<int> pDamage,
                int DistanceFromEpicenter,
                Pointer<WarheadTypeClass> pWH,
                Pointer<ObjectClass> pAttacker,
                bool IgnoreDefenses,
                bool PreventPassengerEscape,
                Pointer<HouseClass> pAttackingHouse)
            {
                if (pDamage.Ref <= 0 || PolarArrayIndex < 0 || pAttackingHouse.IsNull)
                {
                    return;
                }

                if (pAttackingHouse.Ref.ArrayIndex == PolarArrayIndex)
                {
                    pDamage.Ref = Math.Max(0, (int)(pDamage.Ref * 0.33));
                }
            }
        }
    }
}
