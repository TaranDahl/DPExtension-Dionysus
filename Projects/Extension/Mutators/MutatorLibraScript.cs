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
    public class MutatorLibra : TechnoScriptable
    {
        private enum LibraSkillStatus
        {
            Idle = 0,
            Aiming = 1,
            Cooldown = 2,
        }

        public MutatorLibra(TechnoExt owner) : base(owner) { }

        private LibraSkillStatus skillStatus = LibraSkillStatus.Idle;
        private int checkCounter = 0;
        private int aimingCounter = 0;
        private int cooldownCounter = 0;
        private int checkInterval = 0;
        private int aimingDuration = 0;
        private int cooldownDuration = 0;
        private SwizzleablePointer<CellClass> targetCell = new SwizzleablePointer<CellClass>(Pointer<CellClass>.Zero);
        private static Pointer<WeaponTypeClass> LibraNuke => WeaponTypeClass.ABSTRACTTYPE_ARRAY.Find("MutLibraNuke");

        public override void OnUpdate()
        {
            var pThis = Owner.OwnerObject;

            // 初始化时间参数（只在第一次调用时执行）
            if (checkInterval == 0)
            {
                checkInterval = Mutator.TimeToFrame(0, 3);      // 每3秒检查一次
                aimingDuration = Mutator.TimeToFrame(0, 4);     // 瞄准4秒
                cooldownDuration = Mutator.TimeToFrame(1, 0);   // 60秒冷却
            }

            switch (skillStatus)
            {
                case LibraSkillStatus.Idle:
                    UpdateIdleStatus(pThis);
                    break;
                case LibraSkillStatus.Aiming:
                    UpdateAimingStatus(pThis);
                    break;
                case LibraSkillStatus.Cooldown:
                    UpdateCooldownStatus();
                    break;
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

            // 检查周围7范围内的所有敌方目标
            var cellEnum = new CellSpreadEnumerator(7);
            List<Pointer<CellClass>> validTargetCells = new List<Pointer<CellClass>>();

            foreach (var offset in cellEnum)
            {
                var checkMapCrd = myMapCrd + offset;

                // 检查这个格子周围3格内的敌方单位
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

                    // 检查距离是否小于等于3格
                    if (technoMapCrd.DistanceFrom(checkMapCrd) > 3)
                        continue;

                    enemyCount++;
                    totalEnemyHealth += techno.Ref.Base.Health;

                    // 必须目标格子上有敌方单位，周围敌方单位数超过4个且总血量超过600
                    if (hasEnemyOnTargetCell && enemyCount > 4 && totalEnemyHealth > 600)
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

            // 计时瞄准时长
            aimingCounter++;
            pThis.Ref.ROFTimer.Start(2);

            if (aimingCounter % Mutator.TimeToFrame(0, 0.25) == 0)
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
                skillStatus = LibraSkillStatus.Idle;
                cooldownCounter = 0;
                checkCounter = 0;
            }
        }

        private void EnterAimingStatus(Pointer<TechnoClass> pThis)
        {
            skillStatus = LibraSkillStatus.Aiming;
            aimingCounter = 0;

            // 立即设置目标
            var targetCellAbs = targetCell.Pointer.Convert<AbstractClass>();
            pThis.Ref.BaseMission.OverrideMission(Mission.Attack, targetCellAbs, Pointer<AbstractClass>.Zero);
        }

        private void EnterCooldownStatus()
        {
            skillStatus = LibraSkillStatus.Cooldown;
            cooldownCounter = 0;
        }

        private void ReleaseNuke(Pointer<TechnoClass> pThis)
        {
            var pHouse = pThis.Ref.BaseAbstract.GetOwningHouse();
            var targetMapCrd = targetCell.Ref.MapCoords;

            // 释放超级武器
            if (LibraNuke.IsNotNull)
            {
                LibraNuke.Ref.DetonateAtTarget(pThis, targetCell.Pointer.Convert<AbstractClass>());
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
