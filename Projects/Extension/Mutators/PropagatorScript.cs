
using System;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DynamicPatcher;
using PatcherYRpp;
using Extension.Ext;
using Extension.Script;
using Extension.Utilities;
using System.Threading.Tasks;
using Extension.Mutators;

namespace Scripts
{
    [Serializable]
    public class Propagator : TechnoScriptable
    {
        public Propagator(TechnoExt owner) : base(owner) { }

        ExtensionReference<TechnoExt> Target;
        bool IsFormingNew = false;
        CoordStruct NewCrd;
        int NewHealth = 0;
        int counter = 0;
        private TimerStruct AlertTimer = new TimerStruct();

        private void TransformStart(TechnoExt ext)
        {
            Target.Set(ext);
        }

        private void TransformUpdate()
        {
            if (IsFormingNew)
            {
                counter++;
                if (counter == 30)
                {
                    counter = 0;
                    IsFormingNew = false;
                    var pThis = Owner.OwnerObject;
                    var owner = pThis.Ref.BaseAbstract.GetOwningHouse();
                    var list = new List<Pointer<TechnoTypeClass>>();
                    list.Add(pThis.Ref.GetTechnoType());
                    var team = Mutator.CreateTeamAtCrd(Mutator.MutatorSpawnTeam, list, owner, NewCrd, true);
                    if (team.IsNotNull)
                    {
                        var newPropagator = team.Ref.FirstUnit;
                        newPropagator.Ref.Base.Health = NewHealth;
                    }
                }
            }
            else if (Target.TryGet(out TechnoExt ext))
            {
                IsFormingNew = true;
                var pThis = Owner.OwnerObject;
                var pTarget = Target.Get().OwnerObject;
                NewCrd = pTarget.Ref.BaseAbstract.GetCoords();
                NewHealth = pThis.Ref.Base.Health >= 1 ? pThis.Ref.Base.Health : 1;
                YRMemory.Create<AnimClass>(RulesClass.Instance.Ref.WarpAway, NewCrd);
                pTarget.Ref.Base.Vanish(pThis);
            }
        }

        public override void OnUpdate()
        {
            var pThis = Owner.OwnerObject;
            if (!AlertTimer.InProgress())
            {
                AlertTimer.Start(Mutator.TimeToFrame(0, 10));
                RadarEventClass.Create(RadarEventType.EnemySensed, pThis.Ref.BaseAbstract.GetMapCrd());
            }
            TransformUpdate();
        }

        public override void OnFire(Pointer<AbstractClass> pTarget, int weaponIndex)
        {
            if (Target.Get() == null)
            {
                if (pTarget.CastToTechno(out Pointer<TechnoClass> pTechno))
                {
                    TransformStart(TechnoExt.ExtMap.Find(pTechno));
                }
            }
        }
    }
}
