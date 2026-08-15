
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
    public class KillBot : TechnoScriptable
    {
        public KillBot(TechnoExt owner) : base(owner) { }

        int ToKill = 0;
        int Killed = 0;
        private TimerStruct AlertTimer = new TimerStruct();

        private void UpdateKillMission()
        {
            var pThis = Owner.OwnerObject;
            if (ToKill == 0)
                ToKill = pThis.Ref.GetTechnoType().Ref.Base.Strength;

            if (Killed >= ToKill)
            {
                pThis.Ref.Base.KillSelfByDamage(false);
                return;
            }

            if (!AlertTimer.InProgress())
            {
                AlertTimer.Start(Mutator.TimeToFrame(0, 10));
                RadarEventClass.Create(RadarEventType.Combat, pThis.Ref.BaseAbstract.GetMapCrd());
            }

            //if (pThis.Ref.Base.Health != Killed - ToKill)
            //    pThis.Ref.Base.Health = Killed - ToKill;
        }

        public override void OnUpdate()
        {
            UpdateKillMission();
        }

        public override DamageState OnDealDamage(Pointer<int> pDamage, int DistanceFromEpicenter, Pointer<WarheadTypeClass> pWH, Pointer<TechnoClass> pVictim, bool IgnoreDefenses, bool PreventPassengerEscape, Pointer<HouseClass> pAttackingHouse, DamageState result, int damageDealt)
        {
            if (result == DamageState.NowDead)
            {
                Killed++;
                var pThis = Owner.OwnerObject;
                pThis.Ref.Base.TakeDamage(1, false);
            }
            return result;
        }

        //public override void OnFire(Pointer<AbstractClass> pTarget, int weaponIndex)
        //{
        //    var pThis = Owner.OwnerObject;
        //    var cellEnum = new CellSpreadEnumerator((uint)(pThis.Ref.GetWeapon(weaponIndex).Ref.WeaponType.Ref.Range / 256));

        //}
    }
}
