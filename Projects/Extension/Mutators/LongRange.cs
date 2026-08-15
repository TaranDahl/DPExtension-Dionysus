using DynamicPatcher;
using Extension.Ext;
using Extension.Utilities;
using InteropUtils;
using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Mutators
{
    [Serializable]
    public class LongRange : BuffMutator
    {
        // Mutator
        public override string UIName => "超远视距";
        public override string Description => "敌方单位和建筑的武器射程与视野范围提高。";
        public override bool IsAvailableInRPG => true;
        public override int Score => 2;
        public LongRange(Pointer<HouseClass> owner) : base(owner) { }
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
        protected override bool IsBuffEnemy => false;
        protected override void BuffTechno(Pointer<TechnoClass> techno)
        {
            PhobosAttachEffect.Attach(
                techno,
                techno.Ref.BaseAbstract.GetOwningHouse(),
                techno,
                techno.Convert<AbstractClass>(),
                AETypes,
                out _);
        }
        protected override void UnbuffTechno(Pointer<TechnoClass> techno)
        {
            PhobosAttachEffect.Detach(techno, AETypes, out _);
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

        // LongRange
        private static readonly string[] AETypes = new[] { "MutLongRangeAE" };
        private bool IsTechnoValid(Pointer<TechnoClass> techno)
        {
            return IsTechnoOwnerValid(techno.Ref.BaseAbstract.GetOwningHouse());
        }
    }
}
