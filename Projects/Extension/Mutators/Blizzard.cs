using DynamicPatcher;
using Extension.Decorators;
using Extension.Ext;
using Extension.Utilities;
using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Extension.Mutators
{
    [Serializable]
    public class Blizzard : Mutator
    {
        public override string UIName => "暴风雪";
        public override string Description => "风暴雷云在地图上飘荡，对位于其行进路线上的玩家单位造成伤害并将其冻结。";
        public override bool IsAvailableInRPG => true;
        public override int Score => 4;

        public Blizzard(Pointer<HouseClass> owner) : base(owner) { }

        private int Counter = 0;
        private int SpawnInterval = 900;
        private int DeltaLeptonsPerInterval = 1;

        // 记录暴风雪当前的预估 X、Y 坐标与出生帧（用于到期清理）
        private List<(CoordStruct CurrentCrd, int SpawnFrame)> RecentSpawns = new List<(CoordStruct CurrentCrd, int)>();

        // 渲染装饰器ID
        private DecoratorId scenarioDecoratorId;

        // 标记因子本身是否已被卸载
        public bool IsUninited { get; private set; } = false;

        // 追踪该因子实例的所有暴风雪单位的Ext
        private List<ExtensionReference<TechnoExt>> ActiveBlizzards = new List<ExtensionReference<TechnoExt>>();

        public override void Init(bool isInitial = true)
        {
            base.Init(isInitial);
            IsUninited = false;

            var leftEdgeCells = MutatorCacheManager.Instance.LeftEdgeCells;
            int edgeLength = leftEdgeCells.Count;

            if (edgeLength > 0)
            {
                SpawnInterval = Math.Max(1, (int)(TimeToFrame(1, 0) / (edgeLength / 8.0)));
                DeltaLeptonsPerInterval = (int)((0.7 * 256.0) / TimeToFrame(0, 1) * SpawnInterval / Math.Sqrt(2));
            }

            scenarioDecoratorId = ScenarioExt.Global().FetchScenarioDecoratorID;
            ScenarioExt.Global().CreateDecorator<BlizzardRangeDecorator>(scenarioDecoratorId, "BlizzardRangeDecorator", this);
        }

        public override void Uninit()
        {
            IsUninited = true;
            base.Uninit();
        }

        public override bool Update()
        {
            if (!base.Update())
                return false;

            Counter++;

            // 清理超过15秒的记录
            int fifteenSecondsFrames = TimeToFrame(0, 15);
            RecentSpawns.RemoveAll(x => (Counter - x.SpawnFrame) > fifteenSecondsFrames);

            if (Counter % SpawnInterval == 0)
            {
                var leftEdgeCells = MutatorCacheManager.Instance.LeftEdgeCells;
                if (leftEdgeCells.Count == 0) return true;

                // 更新列表中已有暴风雪的当前位置坐标
                for (int i = 0; i < RecentSpawns.Count; i++)
                {
                    var rec = RecentSpawns[i];
                    RecentSpawns[i] = (new CoordStruct(rec.CurrentCrd.X + DeltaLeptonsPerInterval, rec.CurrentCrd.Y - DeltaLeptonsPerInterval, rec.CurrentCrd.Z), rec.SpawnFrame);
                }

                CellStruct spawnCrd = CellStruct.Empty;
                bool found = false;

                for (int i = 0; i < 10; i++)
                {
                    var candidate = ScenarioClass.GetRandomInList(leftEdgeCells).Ref.MapCoords;

                    // 基于推算出来的 Lepton坐标判断距离 (10格 = 2560 Lepton)
                    bool tooClose = RecentSpawns.Any(r => CellClass.Cell2Coord(candidate).DistanceFrom(r.CurrentCrd) < 2560.0);

                    if (!tooClose)
                    {
                        spawnCrd = candidate;
                        found = true;
                        break;
                    }
                }

                if (!found && spawnCrd == CellStruct.Empty)
                {
                    spawnCrd = ScenarioClass.GetRandomInList(leftEdgeCells).Ref.MapCoords;
                }

                // 添加自己作为最新的推算记录
                RecentSpawns.Add((CellClass.Cell2Coord(spawnCrd), Counter));
                SpawnBlizzard(spawnCrd);
            }

            return true;
        }

        private void SpawnBlizzard(CellStruct startCrd)
        {
            var unitType = UnitTypeClass.ABSTRACTTYPE_ARRAY.Find("MUTBLIZZ");
            if (unitType.IsNull) return;

            var rightEdgeCells = MutatorCacheManager.Instance.RightEdgeCells;
            CellStruct targetCrd = startCrd;
            if (rightEdgeCells.Count > 0)
            {
                var targetCell = rightEdgeCells.OrderBy(c => Math.Abs(c.Ref.MapCoords.DistanceFrom(startCrd))).FirstOrDefault();
                targetCrd = targetCell.Ref.MapCoords;
            }

            // 使用项目的原生存装器扩展创建单位附加逻辑
            var owner = GetRandomHouseOnOurSide();
            if (owner.IsNull) return;

            var blizzardUnit = TechnoExt.CreateObjectWithDecorator<MutatorBlizzard>(
                unitType.Convert<ObjectTypeClass>(),
                owner,
                MutatorBlizzard.ID,
                this,
                targetCrd
            ).Convert<TechnoClass>();

            if (blizzardUnit.IsNull) return;

            ++Game.IKnowWhatImDoing;
            blizzardUnit.Ref.Base.Put(MapClass.Instance.GetCellAt(startCrd).Ref.Base.GetCoords(), DirType.NorthEast);
            --Game.IKnowWhatImDoing;

            blizzardUnit.Ref.SetDestination(MapClass.Instance.GetCellAt(targetCrd));
            blizzardUnit.Ref.BaseMission.QueueMission(Mission.Move, true);

            // 将新暴风雪添加到追踪列表
            var ext = TechnoExt.ExtMap.Find(blizzardUnit);
            if (ext != null)
            {
                ActiveBlizzards.Add(new ExtensionReference<TechnoExt>(ext));
            }
        }

        [Serializable]
        public class MutatorBlizzard : MutatorEventDecorator
        {
            public static new DecoratorId ID => new DecoratorId((int)Mutator.TechnoDecoratorIDs.UniqueDecorator);

            private CellStruct TargetCell;
            private bool IsActive = true;
            private int ExplodeCounter = 0;

            public bool Active => IsActive;

            public MutatorBlizzard(Mutator mutator, CellStruct targetCell) : base(mutator)
            {
                TargetCell = targetCell;
            }

            public override void OnUpdate()
            {
                var pThis = (Decorative as TechnoExt).OwnerObject;
                if (pThis.IsNull || !pThis.Ref.Base.IsAlive) return;

                var currentCrd = pThis.Ref.BaseAbstract.GetCoords();
                var currentCell = MapClass.Instance.GetCellAt(currentCrd);

                // 进入安全区（矿柱区域）时失效，停止引爆
                IsActive = !TargetCellMutator.IsInSafeZone(currentCell);

                // 抵达目标区域时清理
                if (CellClass.Coord2Cell(currentCrd).DistanceFrom(TargetCell) <= 3.0)
                {
                    pThis.Ref.Base.UnInit();
                    return;
                }

                // 如果发现未朝向目标前进，即覆写寻路
                var dest = pThis.Convert<FootClass>().Ref.Destination;
                if (dest.IsNotNull && dest.Ref.GetMapCrd() != TargetCell)
                {
                    var targetAbstract = MapClass.Instance.GetCellAt(TargetCell).Convert<AbstractClass>();
                    pThis.Ref.SetDestination(targetAbstract);
                    pThis.Ref.BaseMission.QueueMission(Mission.Move, true);
                }

                if (IsActive)
                {
                    ExplodeCounter++;
                    // 每0.5秒产生一次范围打击判断
                    if (ExplodeCounter >= Mutator.TimeToFrame(0, 0.5))
                    {
                        ExplodeCounter = 0;
                        DetonateWeapon(pThis);
                    }
                }
            }

            private void DetonateWeapon(Pointer<TechnoClass> techno)
            {
                var weapon = WeaponTypeClass.ABSTRACTTYPE_ARRAY.Find("MutBlizzard");
                if (weapon.IsNull) return;
                weapon.Ref.DetonateAtSelf(techno);
            }
        }

        [Serializable]
        public class BlizzardRangeDecorator : EventRenderDecorator
        {
            private Blizzard myOwnerMutator;

            public BlizzardRangeDecorator(Blizzard ownerMutator)
            {
                myOwnerMutator = ownerMutator;
            }

            public override void OnUpdate()
            {
                if (Game.CurrentFrame % 15 == 0 && myOwnerMutator != null && myOwnerMutator.IsUninited)
                {
                    // 清理已失效的引用，检查是否还有活跃的暴风雪
                    myOwnerMutator.ActiveBlizzards.RemoveAll(blizzardRef => blizzardRef.ReferenceCollected);

                    if (myOwnerMutator.ActiveBlizzards.Count == 0)
                    {
                        ScenarioExt.Global().Remove(this);
                    }
                }
            }

            public override void OnRender()
            {
                if (myOwnerMutator == null)
                    return;

                // 直接遍历活跃的暴风雪列表进行绘制
                foreach (var blizzardRef in myOwnerMutator.ActiveBlizzards)
                {
                    if (!blizzardRef.TryGet(out var ext))
                    {
                        continue;
                    }

                    var pThis = ext.OwnerObject;
                    if (pThis.IsNull || !pThis.Ref.Base.IsAlive)
                    {
                        continue;
                    }

                    var script = ext.Get(MutatorBlizzard.ID) as MutatorBlizzard;
                    if (script == null) continue;

                    var centerCrd = pThis.Ref.BaseAbstract.GetCoords();
                    float radius = 5.0f;

                    // 活动时画红圈，失效时为灰圈
                    ColorStruct color = script.Active ? new ColorStruct(255, 0, 0) : new ColorStruct(128, 128, 128);
                    TacticalClass.DrawRadialIndicator(false, false, centerCrd, color, radius);
                }
            }
        }
    }
}