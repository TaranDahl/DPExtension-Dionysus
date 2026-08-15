using DynamicPatcher;
using Extension.Decorators;
using Extension.Ext;
using Extension.Utilities;
using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Mutators
{
    // 数值修改：去掉了0.25秒无敌
    [Serializable]
    public class EvasiveManeuvers : BuffMutator
    {
        // Mutator
        public override string UIName => "闪避机动";
        public override string Description => "敌方单位受到伤害时将传送一小段距离。";
        public override bool IsAvailableInRPG => true;
        public override int Score => 1;
        public EvasiveManeuvers(Pointer<HouseClass> owner) : base(owner) { }
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
            var ext = TechnoExt.ExtMap.Find(techno);
            if (ext.Get(EvasiveManeuverBuff.ID) == null)
                ext.CreateDecorator<EvasiveManeuverBuff>(EvasiveManeuverBuff.ID, "EvasiveManeuverBuff");
        }
        protected override void UnbuffTechno(Pointer<TechnoClass> techno)
        {
            var ext = TechnoExt.ExtMap.Find(techno);
            ext.Remove(EvasiveManeuverBuff.ID);
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

        // EvasiveManeuvers
        private bool IsTechnoValid(Pointer<TechnoClass> techno)
        {
            if (!IsTechnoOwnerValid(techno.Ref.BaseAbstract.GetOwningHouse()))
                return false;

            // 建筑不能闪避
            if ((techno.Ref.BaseAbstract.AbstractFlags & AbstractFlags.Foot) == AbstractFlags.None)
                return false;
            return true;
        }

        [Serializable]
        private class EvasiveManeuverBuff : EventDecorator
        {
            // Decorator
            public static new DecoratorId ID => new DecoratorId((int)TechnoDecoratorIDs.EvasiveManeuverBuff);

            // EventDecorator
            public override DamageState AfterReceiveDamage(Pointer<int> pDamage, int DistanceFromEpicenter, Pointer<WarheadTypeClass> pWH, Pointer<ObjectClass> pAttacker, bool IgnoreDefenses, bool PreventPassengerEscape, Pointer<HouseClass> pAttackingHouse, DamageState result, int damageTaken)
            {
                do
                {
                    // 必须有伤害
                    if (result == DamageState.Unaffected)
                        break;
                    if (damageTaken <= 0)
                        break;
                    // 建筑不能闪避
                    var pThis = (Decorative as TechnoExt).OwnerObject;
                    if ((pThis.Ref.BaseAbstract.AbstractFlags & AbstractFlags.Foot) == AbstractFlags.None)
                        break;
                    // 在冷却不能闪避
                    if (EvadeTimer.InProgress())
                        break;
                    // 英雄不能闪避
                    if (IsHero(pThis.Ref.GetTechnoType()))
                        break;
                    // 没有武器的单位不能闪避
                    if (!pThis.Ref.IsArmed())
                        break;
                    // 友军不闪避
                    if (pThis.Ref.Owner.Ref.IsAlliedWith(pAttacker) || pThis.Ref.Owner.Ref.IsAlliedWith(pAttackingHouse))
                        break;
                    // 死了就别闪避了
                    if (result == DamageState.NowDead)
                        break;
                    EvadeTimer.Start(TimeToFrame(0, 2));
                    EvadeFrom(pAttacker.Convert<TechnoClass>());
                }
                while (false);
                return result;
            }

            // EvasiveManeuverBuff
            private TimerStruct EvadeTimer = new TimerStruct();
            private static Pointer<AnimTypeClass> EvadeFromAnim => AnimTypeClass.ABSTRACTTYPE_ARRAY.Find("WARPOUT");
            private static Pointer<AnimTypeClass> EvadeToAnim => AnimTypeClass.ABSTRACTTYPE_ARRAY.Find("WARPOUT");
            private static int EvadeRange = 5;
            private void EvadeFrom(Pointer<TechnoClass> pAttacker)
            {
                var victimExt = Decorative as TechnoExt;
                var victim = victimExt.OwnerObject;
                bool attackerCenter = pAttacker.IsNotNull && victim.Ref.BaseAbstract.DistanceFrom(pAttacker.Convert<AbstractClass>()) <= 20 * 256;
                var centerMapCrd = attackerCenter ? pAttacker.Ref.BaseAbstract.GetMapCrd() : victim.Ref.BaseAbstract.GetMapCrd();
                var cellEnum = new CellSpreadEnumerator((uint)EvadeRange);
                var validCells = new List<CellStruct>();
                foreach (var offset in cellEnum)
                {
                    var mapCrd = offset + centerMapCrd;
                    if (CanEvadeTo(victim, mapCrd))
                        validCells.Add(mapCrd);
                }
                if (validCells.Count > 0)
                {
                    var victimFoot = victim.Convert<FootClass>();
                    var mapCrd = ScenarioClass.GetRandomInList(validCells);
                    var cell = MapClass.Instance.GetCellAt(mapCrd);
                    var height = victim.Ref.Base.GetHeight();
                    var originalCrd = victim.Ref.BaseAbstract.GetCoords();
                    var targetCrd = cell.Ref.Base.GetCoords();
                    targetCrd.Z += height;
                    bool shouldNotKeepDest = victimFoot.Ref.Locomotor.GetType() == typeof(JumpjetLocomotionClass) // 是JJ
                        && victimFoot.Ref.Destination.IsNotNull && CellClass.Coord2Cell(victimFoot.Ref.Destination.Ref.GetCoords()) == CellClass.Coord2Cell(originalCrd); // 目的地在脚下
                    YRMemory.Create<AnimClass>(EvadeFromAnim, originalCrd);
                    victim.Ref.Base.Mark(MarkType.UP);
                    victim.Ref.Base.SetLocation(targetCrd);
                    victim.Ref.Base.Mark(MarkType.DOWN);
                    YRMemory.Create<AnimClass>(EvadeToAnim, targetCrd);
                    // TODO: ATC
                    var oldDest = victimFoot.Ref.Locomotor.Destination();
                    LocomotionClass.RefreshLocomotor(victimFoot);
                    if (shouldNotKeepDest)
                    {
                        victimFoot.Ref.Locomotor.ToLocomotionClass<JumpjetLocomotionClass>().Ref.LocoState = JumpjetLocomotionClass.State.Cruising;
                        victimFoot.Ref.MoveTo(targetCrd);
                    }
                    else
                    {
                        victimFoot.Ref.MoveTo(oldDest);
                    }
                }
                else
                {
                    Logger.Log("No cell to evade!");
                }
            }
            private static bool CanEvadeTo(Pointer<TechnoClass> pThis, CellStruct mapCrd)
            {
                var cell = MapClass.Instance.GetCellAt(mapCrd);
                var pType = pThis.Ref.GetTechnoType();
                // 避免卡住，目标类型必须能走在这里
                if (!cell.Ref.IsClearToMove(pType.Ref.SpeedType, pType.Ref.MovementZone))
                    return false;
                return true;
            }
        }
    }
}
