
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
using static System.Net.Mime.MediaTypeNames;

namespace Scripts
{
    [Serializable]
    public class PurifyBeam : TechnoScriptable
    {
        public PurifyBeam(TechnoExt owner) : base(owner) { }

        private static Pointer<WeaponTypeClass> PurifyWeapon => new Pointer<WeaponTypeClass>(WeaponTypeClass.ABSTRACTTYPE_ARRAY.Find("Purify"));
        private static Pointer<AnimTypeClass> PurifyBeamAnim => new Pointer<AnimTypeClass>(AnimTypeClass.ABSTRACTTYPE_ARRAY.Find("PurifyBeamAnim"));
        int counter = 0;
        int updateDelay = 0;
        ExtensionReference<TechnoExt> Target;
        bool mutatorFound = false;
        PurifierBeam myMutator = null;
        private ColorStruct LaserInnerColor = new ColorStruct(255, 0, 0);
        private ColorStruct LaserOuterColor = new ColorStruct(255, 0, 0);
        private ColorStruct LaserOuterSpread = new ColorStruct(0, 0, 0);
        private SwizzleablePointer<LaserDrawClass> myLaser = new SwizzleablePointer<LaserDrawClass>(Pointer<LaserDrawClass>.Zero);
        private TimerStruct AlertTimer = new TimerStruct();

        private bool IsTargetSelectedByOtherBeam(Pointer<TechnoClass> target)
        {
            if (myMutator == null)
            {
                return true;
            }

            List<Pointer<TechnoClass>> list = new List<Pointer<TechnoClass>>();
            foreach (var beamExt in myMutator.Beams)
            {
                var ext = beamExt.Get();
                if (ext == null)
                {
                    continue;
                }
                if (Owner == ext)
                    continue;
                if (target.Convert<AbstractClass>() == ext.OwnerObject.Ref.Target)
                    return true;
            }
            return false;
        }
        private void SetTargetToAttack(Pointer<TechnoClass> target)
        {
            var ext = TechnoExt.ExtMap.Find(target);
            var pThis = Owner.OwnerObject;
            Target.Set(ext);
            pThis.Ref.SetDestination(target.Convert<AbstractClass>());
            pThis.Ref.SetTarget(target.Convert<AbstractClass>());
            pThis.Ref.BaseMission.QueueMission(Mission.Attack, true);
        }
        private void ResetTargetAndLeave(Pointer<CellClass> target)
        {
            var pThis = Owner.OwnerObject;
            Target.Set(null);
            pThis.Ref.SetDestination(target);
            pThis.Ref.SetTarget(Pointer<AbstractClass>.Zero);
            pThis.Ref.BaseMission.QueueMission(Mission.Move, true);
        }
        private bool SelectPurifyTarget()
        {
            if (myMutator == null)
                return false;

            var pThis = Owner.OwnerObject;
            Pointer<TechnoClass> target = Pointer<TechnoClass>.Zero;
            int bestDist = int.MaxValue;
            foreach (var techno in TechnoClass.Array)
            {
                var owner = techno.Ref.BaseAbstract.GetOwningHouse();
                if (myMutator.IsOnTheirSide(owner) // 敌人
                    && !IsTargetSelectedByOtherBeam(techno) // 不是重复目标
                    && TargetTechnoMutator.GetKeepAliveAbility(techno) > 0 // 有价值
                    && TargetCellMutator.IsOutOfSafeZone(techno)) // 不在安全区
                {
                    var currentDist = pThis.Ref.BaseAbstract.DistanceFrom(techno.Convert<AbstractClass>());
                    if (currentDist < bestDist) // 找最近的
                    {
                        bestDist = currentDist;
                        target = techno;
                    }
                }
            }

            if (target.IsNotNull)
            {
                SetTargetToAttack(target);
                return true;
            }

            return false;
        }

        // 不同净化光束不会追击相同的目标
        // 净化光束不会追击安全区内的单位，追击过程即将进入安全区内时也会改变目标
        // 没有追击目标时，净化光束会朝玩家1出生点靠近，距离出生点80范围内时则会开始随机向非安全区位置移动
        private void PurifyBeamUpdate()
        {
            var pThis = Owner.OwnerObject;
            // 雷达事件
            if (!AlertTimer.InProgress())
            {
                AlertTimer.Start(Mutator.TimeToFrame(0, 10));
                RadarEventClass.Create(RadarEventType.Combat, pThis.Ref.BaseAbstract.GetMapCrd());
            }
            // 更新目标
            if (counter % Mutator.TimeToFrame(0, updateDelay != 0 ? updateDelay : 2) == 0)
            {
                // 4-6秒更新一次
                updateDelay = ScenarioClass.Instance.Random.RandomRanged(4, 6);

                // 已经进入安全区了，那就放弃目标，不再索敌，并设定目标去一个随机非安全区位置
                if (TargetCellMutator.IsInSafeZone(pThis))
                {
                    var cell = TargetCellMutator.SelectRandomCellOutOfSafeZone();
                    ResetTargetAndLeave(cell);
                }
                else
                {
                    // 只要不在安全区内，就索敌一次
                    if (!SelectPurifyTarget()) // 索敌一次也没有有效目标
                    {
                        List<Pointer<CellClass>> enemyBases = new List<Pointer<CellClass>>();
                        foreach(var house in HouseClass.Array)
                        {
                            if (myMutator != null && myMutator.IsOnTheirSide(house))
                            {
                                var cell = MapClass.Instance.GetCellAt(CellClass.Cell2Coord(house.Ref.GetBaseCenter()));
                                if (MapClass.Instance.IsWithinUsableArea(cell, true))
                                    enemyBases.Add(cell);
                            }
                        }
                        if (enemyBases.Count > 0)
                        {
                            var cell = enemyBases[ScenarioClass.Instance.Random.RandomRanged(0, enemyBases.Count - 1)];
                            ResetTargetAndLeave(cell);
                        }
                    }
                }
            }

            // 造成伤害
            if (counter % Mutator.TimeToFrame(0, 0.25) == 0)
            {
                PurifyWeapon.Ref.DetonateAtSelf(pThis);
            }

            // 更新激光位置
            var crd = pThis.Ref.BaseAbstract.GetCoords();
            var startCrd = crd;
            startCrd.Z += 2560;
            myLaser.Ref.Source = startCrd;
            myLaser.Ref.Target = crd;
            myLaser.Ref.Progress.Value = 0;
            counter++;
        }

        public override void OnUpdate()
        {
            // 初始化
            if (!mutatorFound)
            {
                // 记录因子
                mutatorFound = true;
                foreach (var mutator in Mutator.Array)
                {
                    if (mutator is PurifierBeam)
                    {
                        var beamMutator = mutator as PurifierBeam;
                        foreach (var beamRef in beamMutator.Beams)
                        {
                            TechnoExt beamExt;
                            if (beamRef.TryGet(out beamExt) && beamExt.OwnerObject == Owner.OwnerObject)
                            {
                                myMutator = mutator as PurifierBeam;
                                break;
                            }
                        }
                        if (myMutator != null)
                            break;
                    }
                }
                // 创建动画
                var pThis = Owner.OwnerObject;
                var crd = pThis.Ref.BaseAbstract.GetCoords();
                var anim = YRMemory.Create<AnimClass>(PurifyBeamAnim, crd);
                anim.Ref.SetOwnerObject(pThis.Convert<ObjectClass>());
                // 创建激光
                var laserStartCrd = crd;
                laserStartCrd.Z += 2560;
                Pointer<LaserDrawClass> pLaser = YRMemory.Create<LaserDrawClass>(laserStartCrd, crd, LaserInnerColor, LaserOuterColor, LaserOuterSpread, 2);
                pLaser.Ref.Thickness = 40;
                pLaser.Ref.IsHouseColor = true;
                pLaser.Ref.Fades = false;
                pLaser.Ref.Duration = 2;
                pLaser.Ref.Progress.Value = 0;
                myLaser = new SwizzleablePointer<LaserDrawClass>(pLaser);
            }
            PurifyBeamUpdate();
        }
    }
}
