using System;
using System.Collections.Generic;
using DynamicPatcher;
using PatcherYRpp;
using Extension.Ext;
using Extension.Script;
using Extension.Utilities;
using System.Linq;
using Extension.Mutators;

namespace Scripts
{
    [Serializable]
    public class MutatorReznov : TechnoScriptable
    {
        private enum ReznovSkillStatus
        {
            Idle = 0,
            Aiming = 1,
            Cooldown = 2,
        }

        public MutatorReznov(TechnoExt owner) : base(owner) { }

        private ReznovSkillStatus skillStatus = ReznovSkillStatus.Idle;
        private int checkCounter = 0;
        private int aimingCounter = 0;
        private int cooldownCounter = 0;
        private int checkInterval = 0;
        private int aimingDuration = 0;
        private int cooldownDuration = 0;
        private SwizzleablePointer<CellClass> targetCell = new SwizzleablePointer<CellClass>(Pointer<CellClass>.Zero);
        private static Pointer<WeaponTypeClass> ReznovNuke => WeaponTypeClass.ABSTRACTTYPE_ARRAY.Find("MutRezNukeMain");
        private static Pointer<AnimTypeClass> AimingAnim => AnimTypeClass.ABSTRACTTYPE_ARRAY.Find("TARCTARG");
        private static ColorStruct laserColor = new ColorStruct(255, 0, 0);
        private static ColorStruct laserSpread = new ColorStruct(0, 0, 0);

        public override void OnUpdate()
        {
            var pThis = Owner.OwnerObject;

            // 初始化时间参数（只在第一次调用时执行）
            if (checkInterval == 0)
            {
                checkInterval = Mutator.TimeToFrame(0, 3);      // 每3秒检查一次
                aimingDuration = Mutator.TimeToFrame(0, 5);     // 瞄准5秒
                cooldownDuration = Mutator.TimeToFrame(1, 0);   // 60秒冷却
            }

            switch (skillStatus)
            {
                case ReznovSkillStatus.Idle:
                    UpdateIdleStatus(pThis);
                    break;
                case ReznovSkillStatus.Aiming:
                    UpdateAimingStatus(pThis);
                    break;
                case ReznovSkillStatus.Cooldown:
                    UpdateCooldownStatus();
                    break;
            }
        }

        public override void OnFire(Pointer<AbstractClass> pTarget, int weaponIndex)
        {
            if (weaponIndex == 1 && pTarget.Ref.WhatAmI()==AbstractType.Cell)
            {
                var pThis = Owner.OwnerObject;
                var crd = pTarget.Ref.GetCoords();
                // 空袭激光
                Pointer<LaserDrawClass> pLaser = YRMemory.Create<LaserDrawClass>(pThis.Ref.Base.GetFLH(0, CoordStruct.Empty), crd, laserColor, laserColor, laserSpread, 3);
                // 弹头动画
                var anim = YRMemory.Create<AnimClass>(AimingAnim, crd);
            }
        }

        private void UpdateIdleStatus(Pointer<TechnoClass> pThis)
        {
            // 每checkInterval帧检查一次
            if (checkCounter++ % checkInterval != 0)
                return;

            checkCounter = 0;
            targetCell = new SwizzleablePointer<CellClass>(Pointer<CellClass>.Zero);

            var pMyHouse = pThis.Ref.BaseAbstract.GetOwningHouse();
            var myMapCrd = pThis.Ref.BaseAbstract.GetMapCrd();

            // 检查周围10范围内的所有敌方目标
            var cellEnum = new CellSpreadEnumerator(10);
            List<Pointer<CellClass>> validTargetCells = new List<Pointer<CellClass>>();

            foreach (var offset in cellEnum)
            {
                var checkMapCrd = myMapCrd + offset;
                
                // 检查这个格子周围6格内的敌方单位
                int enemyCount = 0;
                int totalEnemyHealth = 0;
                bool hasEnemyOnTargetCell = false;

                foreach (var techno in TechnoClass.Array)
                {
                    var technoMapCrd = techno.Ref.BaseAbstract.GetMapCrd();

                    // 跳过不在地图上的单位
                    if (techno.Ref.Base.InLimbo || !techno.Ref.IsInPlayfield)
                        continue;

                    var owner = techno.Ref.BaseAbstract.GetOwningHouse();
                    // 检查是否是敌方单位
                    if (pMyHouse.Ref.Type.Ref.MultiplayPassive || pMyHouse.Ref.IsAlliedWith(owner))
                        continue;

                    // 检查是否在目标格子上
                    if (technoMapCrd == checkMapCrd)
                        hasEnemyOnTargetCell = true;

                    // 检查距离是否小于等于6格
                    if (technoMapCrd.DistanceFrom(checkMapCrd) > 6)
                        continue;

                    enemyCount++;
                    totalEnemyHealth += techno.Ref.Base.Health;

                    // 必须目标格子上有敌方单位，周围敌方单位数超过4个且总血量超过1200
                    if (hasEnemyOnTargetCell && enemyCount > 4 && totalEnemyHealth > 1200)
                    {
                        validTargetCells.Add(MapClass.Instance.GetCellAt(checkMapCrd));
                        break;
                    }
                }
            }

            // 如果有有效目标，随机选择一个并进入技能瞄准状态
            if (validTargetCells.Count > 0)
            {
                targetCell = new SwizzleablePointer<CellClass>(ScenarioClass.GetRandomInList(validTargetCells));
                EnterAimingStatus(pThis);
            }
        }

        private void UpdateAimingStatus(Pointer<TechnoClass> pThis)
        {
            // 在技能瞄准状态时，确保目标和任务正确
            var targetCellAbs = targetCell.Pointer.Convert<AbstractClass>();
            if (pThis.Ref.Target != targetCellAbs || pThis.Ref.BaseMission.CurrentMission != Mission.Attack)
            {
                pThis.Ref.BaseMission.OverrideMission(Mission.Attack, targetCellAbs, Pointer<AbstractClass>.Zero);
            }

            // 举枪
            pThis.Convert<InfantryClass>().Ref.PlayAnim((int)SequenceAnimType.SecondaryFire, false, 0);
            pThis.Ref.Animtaion.Value = 10;

            // 计时瞄准时长
            aimingCounter++;
            pThis.Ref.ROFTimer.Start(2);

            if (aimingCounter % Mutator.TimeToFrame(0, 0, 2) == 0)
                pThis.Ref.Fire(targetCellAbs, 1);

            if (aimingCounter >= aimingDuration)
            {
                // 技能瞄准状态结束，释放超级武器
                ReleaseNuke(pThis);
                ResetTargetAndMission(pThis);
                EnterCooldownStatus();
            }
        }

        private void UpdateCooldownStatus()
        {
            // 计时冷却时长
            cooldownCounter++;
            if (cooldownCounter >= cooldownDuration)
            {
                skillStatus = ReznovSkillStatus.Idle;
                cooldownCounter = 0;
                checkCounter = 0;
            }
        }

        private void EnterAimingStatus(Pointer<TechnoClass> pThis)
        {
            skillStatus = ReznovSkillStatus.Aiming;
            aimingCounter = 0;
            
            // 立即设置目标
            var targetCellAbs = targetCell.Pointer.Convert<AbstractClass>();
            pThis.Ref.BaseMission.OverrideMission(Mission.Attack, targetCellAbs, Pointer<AbstractClass>.Zero);
        }

        private void EnterCooldownStatus()
        {
            skillStatus = ReznovSkillStatus.Cooldown;
            cooldownCounter = 0;
        }

        private void ReleaseNuke(Pointer<TechnoClass> pThis)
        {
            var pHouse = pThis.Ref.BaseAbstract.GetOwningHouse();
            var targetMapCrd = targetCell.Ref.MapCoords;

            // 释放
            if (ReznovNuke.IsNotNull)
            {
                ReznovNuke.Ref.DetonateAtTarget(pThis, targetCell.Pointer.Convert<AbstractClass>());
            }
        }

        private void ResetTargetAndMission(Pointer<TechnoClass> pThis)
        {
            // 重设目标和任务
            pThis.Ref.SetTarget(Pointer<AbstractClass>.Zero);
            targetCell = new SwizzleablePointer<CellClass>(Pointer<CellClass>.Zero);
        }
    }
}
