using DynamicPatcher;
using Extension.Decorators;
using Extension.Ext;
using Extension.Utilities;
using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Mutators
{
    [Serializable]
    public class OrbitalStrike : TargetCellMutator
    {
        // Mutator
        public override string UIName => "轨道轰炸";
        public override string Description => "敌人会在地图上周期性地施放轨道轰炸。";
        public override bool IsAvailableInRPG => true;
        public override int Score => 1;

        public OrbitalStrike(Pointer<HouseClass> owner) : base(owner) { }

        // -------------------------------------------------------
        // 准备阶段
        // -------------------------------------------------------
        private int prepareCounter = 0;
        private static int PrepareDelay => TimeToFrame(1, 20); // 80秒

        // -------------------------------------------------------
        // 判定阶段
        // -------------------------------------------------------
        private bool isReady = true;
        private int checkCounter = 0;
        private static int CheckDelay => TimeToFrame(0, 5); // 5秒

        // -------------------------------------------------------
        // 瞄准阶段状态
        // -------------------------------------------------------
        [Serializable]
        private struct TargetRecord
        {
            public ExtensionReference<TechnoExt> TechnoRef;

            public TargetRecord(TechnoExt ext)
            {
                TechnoRef = new ExtensionReference<TechnoExt>(ext);
            }
        }

        private enum AimingPhase { None, FirstRound, SecondRound }

        private AimingPhase aimingPhase = AimingPhase.None;
        private List<TargetRecord> aimingTargets = new List<TargetRecord>();
        private int aimingTargetCount = 0;
        private int aimingCounter = 0;
        private int aimingTimer = 0;

        private static int AimRoundInterval => TimeToFrame(0, 4);   // 两轮之间间隔4秒
        private static int ShotInterval => TimeToFrame(0, 0.1);      // 每次取目标间隔0.1秒
        private static int ShotDelay_FirstRound => TimeToFrame(0, 8); // 第一轮shot延迟8秒
        private static int ShotDelay_SecondRound => TimeToFrame(0, 4); // 第二轮shot延迟4秒
        private static int MinTargetCount => 5;

        private int GetMaxTargetCount()
        {
            var frame = Game.CurrentFrame;
            if (frame < TimeToFrame(5, 0))
                return 2;
            else if (frame < TimeToFrame(8, 20))
                return 3;
            else if (frame < TimeToFrame(11, 40))
                return 4;
            else if (frame < TimeToFrame(16, 40))
                return 8;
            else
                return 10;
        }

        public override bool Update()
        {
            if (!base.Update())
                return false;

            if (aimingPhase != AimingPhase.None)
            {
                UpdateAiming();
                return true;
            }

            if (!isReady)
            {
                prepareCounter++;
                if (prepareCounter >= PrepareDelay)
                {
                    prepareCounter = 0;
                    isReady = true;
                    checkCounter = 0;
                }
            }
            else
            {
                checkCounter++;
                if (checkCounter >= CheckDelay)
                {
                    checkCounter = 0;
                    TryBeginStrike();
                }
            }

            return true;
        }

        private void TryBeginStrike()
        {
            var candidates = new List<Pointer<TechnoClass>>();
            foreach (var techno in TechnoClass.Array)
            {
                if (techno.Ref.Base.InLimbo || !techno.Ref.IsInPlayfield)
                    continue;

                var owner = techno.Ref.BaseAbstract.GetOwningHouse();
                if (!IsOnTheirSide(owner))
                    continue;

                if (!IsOutOfSafeZone(techno))
                    continue;

                candidates.Add(techno);
            }

            if (candidates.Count < MinTargetCount)
                return;

            int x = GetMaxTargetCount();
            int selectCount = Math.Min(x * 2, candidates.Count);

            var rng = ScenarioClass.Instance.Random;
            for (int i = 0; i < selectCount; i++)
            {
                int j = rng.RandomRanged(i, candidates.Count - 1);
                var tmp = candidates[i];
                candidates[i] = candidates[j];
                candidates[j] = tmp;
            }

            aimingTargets.Clear();
            for (int i = 0; i < selectCount; i++)
            {
                var technoExt = TechnoExt.ExtMap.Find(candidates[i]);
                aimingTargets.Add(new TargetRecord(technoExt));
            }

            aimingPhase = AimingPhase.FirstRound;
            aimingTargetCount = x;
            aimingCounter = 0;
            aimingTimer = 0;

            isReady = false;
            prepareCounter = 0;
        }

        private void UpdateAiming()
        {
            if (aimingPhase == AimingPhase.FirstRound)
            {
                if (aimingCounter < aimingTargetCount)
                {
                    if (aimingTimer % ShotInterval == 0)
                    {
                        SpawnShotsForTarget(aimingTargets[aimingCounter], ShotDelay_FirstRound);
                        aimingCounter++;
                    }
                }
                else if (aimingTimer >= AimRoundInterval)
                {
                    aimingTargets.RemoveRange(0, aimingTargetCount);
                    aimingPhase = AimingPhase.SecondRound;
                    aimingCounter = 0;
                    aimingTimer = 0;
                    return;
                }

                aimingTimer++;
            }
            else if (aimingPhase == AimingPhase.SecondRound)
            {
                if (aimingCounter < aimingTargetCount)
                {
                    if (aimingTimer % ShotInterval == 0)
                    {
                        SpawnShotsForTarget(aimingTargets[aimingCounter], ShotDelay_SecondRound);
                        aimingCounter++;
                    }
                }
                else
                {
                    aimingPhase = AimingPhase.None;
                    aimingTargets.Clear();
                    return;
                }

                aimingTimer++;
            }
        }

        private void SpawnShotsForTarget(TargetRecord record, int shotDelay)
        {
            CoordStruct targetCrd;

            if (record.TechnoRef.TryGet(out var ext))
            {
                targetCrd = ext.OwnerObject.Ref.BaseAbstract.GetCoords();
            }
            else
            {
                var candidates = new List<Pointer<TechnoClass>>();
                foreach (var techno in TechnoClass.Array)
                {
                    if (techno.Ref.Base.InLimbo || !techno.Ref.IsInPlayfield)
                        continue;
                    var owner = techno.Ref.BaseAbstract.GetOwningHouse();
                    if (!IsOnTheirSide(owner))
                        continue;
                    if (!IsOutOfSafeZone(techno))
                        continue;
                    candidates.Add(techno);
                }

                if (candidates.Count == 0)
                    return;

                targetCrd = ScenarioClass.GetRandomInList(candidates).Ref.BaseAbstract.GetCoords();
            }

            var firer = GetRandomHouseOnOurSide();
            if (firer.IsNull)
                return;

            // 在目标位置播放瞄准提示音
            int vocIdx = VocClass.FindIndex("TargetPainterStart");
            if (vocIdx != -1)
                VocClass.PlayAt(vocIdx, targetCrd);

            var targetMapCrd = CellClass.Coord2Cell(targetCrd);
            var extraCells = GetRandomCellsNear(targetMapCrd, 7, 2);
            var scenarioExt = ScenarioExt.Global();

            scenarioExt.CreateDecorator<OrbitalStrikeShot>(
                scenarioExt.FetchScenarioDecoratorID,
                "OrbitalStrikeShot",
                targetMapCrd,
                firer,
                shotDelay);

            scenarioExt.CreateDecorator<OrbitalStrikeShot>(
                scenarioExt.FetchScenarioDecoratorID,
                "OrbitalStrikeShot",
                extraCells[0],
                firer,
                shotDelay);

            scenarioExt.CreateDecorator<OrbitalStrikeShot>(
                scenarioExt.FetchScenarioDecoratorID,
                "OrbitalStrikeShot",
                extraCells[1],
                firer,
                shotDelay);
        }

        /// <summary>
        /// 在指定格子周围 maxRadius 格内，随机取 count 个在地图内的格子（可能重复）
        /// </summary>
        private static List<CellStruct> GetRandomCellsNear(CellStruct center, uint maxRadius, int count)
        {
            var pool = new List<CellStruct>();
            var enumerator = new CellSpreadEnumerator(maxRadius, 1); // 从1开始跳过中心格
            foreach (var offset in enumerator)
            {
                var candidate = new CellStruct((short)(center.X + offset.X), (short)(center.Y + offset.Y));
                if (MapClass.Instance.IsWithinUsableArea(candidate, true))
                    pool.Add(candidate);
            }

            var result = new List<CellStruct>(count);
            if (pool.Count == 0)
            {
                for (int i = 0; i < count; i++)
                    result.Add(center);
                return result;
            }

            for (int i = 0; i < count; i++)
                result.Add(ScenarioClass.GetRandomInList(pool));

            return result;
        }

        // -------------------------------------------------------
        // 单次轨道打击指示器 + 发射装饰器
        // -------------------------------------------------------
        [Serializable]
        private class OrbitalStrikeShot : EventRenderDecorator
        {
            // 指示器参数
            private static readonly float OuterRadius = 42f;        // 最外圈半径，始终显示
            private static readonly float InnerRadiusStep = 7f;     // 每圈缩小量
            private static int InnerRingInterval => Mutator.TimeToFrame(0, 0.25); // 每0.25秒增加一圈
            private static int InnerRingStartBefore => Mutator.TimeToFrame(0, 3); // 距发射不足3秒时开始
            private static readonly ColorStruct IndicatorColor = new ColorStruct(255, 140, 0);

            private static readonly int SpawnHeight = 15 * 256;

            private static Pointer<WeaponTypeClass> OrbitalWeapon =>
                WeaponTypeClass.ABSTRACTTYPE_ARRAY.Find("MutOrbital");

            private CellStruct targetCell;
            private Pointer<HouseClass> firer;
            /// <summary>外部延迟帧数，延迟结束后才开始指示器倒计时</summary>
            private int launchDelay;
            private int timer = 0;          // 从装饰器创建起计的总帧数
            private int localTimer = -1;    // 延迟结束后的本地帧数，-1表示尚未激活
            private bool launched = false;

            private static int IndicatorDuration => Mutator.TimeToFrame(0, 4); // 指示器总显示时长4秒

            public OrbitalStrikeShot(CellStruct targetCell, Pointer<HouseClass> firer, int launchDelay)
            {
                this.targetCell = targetCell;
                this.firer = firer;
                this.launchDelay = launchDelay;
            }

            public override void OnUpdate()
            {
                // 等待外部延迟
                if (timer < launchDelay)
                {
                    timer++;
                    return;
                }

                // 激活本地计时
                if (localTimer < 0)
                    localTimer = 0;

                // 到达发射时机，发射后立即移除
                if (!launched && localTimer >= IndicatorDuration)
                {
                    Launch();
                    launched = true;
                    Decorative?.Remove(this);
                    return;
                }

                localTimer++;
                timer++;
            }

            public override void OnRender()
            {
                var targetCoord = MapClass.Instance.GetCellAt(targetCell).Ref.Base.GetCoords();

                // 始终绘制最外圈
                TacticalClass.DrawRadialIndicator(false, false, targetCoord, IndicatorColor, OuterRadius, true);

                if (localTimer < 0)
                    return;

                // 距发射剩余帧数
                int framesUntilLaunch = IndicatorDuration - localTimer;

                // 不足3秒时开始向内增加内圈
                if (framesUntilLaunch < InnerRingStartBefore)
                {
                    int innerElapsed = InnerRingStartBefore - framesUntilLaunch;
                    int innerRingCount = innerElapsed / InnerRingInterval;

                    for (int i = 0; i < innerRingCount; i++)
                    {
                        float radius = OuterRadius - InnerRadiusStep * (i + 1);
                        if (radius <= 0.0f)
                            break;

                        TacticalClass.DrawRadialIndicator(false, false, targetCoord, IndicatorColor, radius, true);
                    }
                }
            }

            private void Launch()
            {
                var weapon = OrbitalWeapon;
                if (weapon.IsNull)
                    return;

                if (firer.IsNull)
                    return;

                var pTargetCell = MapClass.Instance.GetCellAt(targetCell);
                var targetCoord = pTargetCell.Ref.Base.GetCoords();
                var spawnCoord = new CoordStruct(targetCoord.X, targetCoord.Y, targetCoord.Z + SpawnHeight);

                WeaponTypeExt.SimpleFire(
                    weapon,
                    pTargetCell.Convert<AbstractClass>(),
                    Pointer<TechnoClass>.Zero,
                    firer,
                    spawnCoord);
            }
        }
    }
}
