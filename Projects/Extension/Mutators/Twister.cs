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
    public class Twister : TargetCellMutator
    {
        public override string UIName => "龙卷风暴";
        public override string Description => "多股龙卷风在地图上移动，对位于其行进路线上的玩家单位造成伤害并将其击退。";
        public override bool IsAvailableInRPG => true;
        public override int Score => 2;

        public Twister(Pointer<HouseClass> owner) : base(owner) { }

        private List<ExtensionReference<TechnoExt>> ActiveTwisters = new List<ExtensionReference<TechnoExt>>();
        private DecoratorId scenarioDecoratorId;

        // 静态缓存武器类型指针，避免每次查找
        private static Pointer<WeaponTypeClass> MutTwisterWeapon => WeaponTypeClass.ABSTRACTTYPE_ARRAY.Find("MutTwisterWeapon");
        private static Pointer<UnitTypeClass> TwisterUnitType => UnitTypeClass.ABSTRACTTYPE_ARRAY.Find("MUTTWISTER");

        public override void Init(bool isInitial = true)
        {
            base.Init(isInitial);

            // 计算生成数量： (地图宽度 + 高度) / 19
            var mapRect = MapClass.Instance.MapRect;
            int count = Math.Max(1, (mapRect.Width + mapRect.Height) / 19);

            // 从可用的安全区外格子中随机选择位置并生成
            for (int i = 0; i < count; i++)
            {
                var cellPtr = SelectRandomCellOutOfSafeZone();
                SpawnTwister(cellPtr.Ref.MapCoords);
            }

            // 创建渲染/清理装饰器
            scenarioDecoratorId = ScenarioExt.Global().FetchScenarioDecoratorID;
            ScenarioExt.Global().CreateDecorator<TwisterRangeDecorator>(scenarioDecoratorId, "TwisterRangeDecorator", this);
        }

        public override void Uninit()
        {
            // 直接删除所有龙卷风对象
            foreach (var twRef in ActiveTwisters)
            {
                if (twRef.TryGet(out var ext))
                {
                    var pThis = ext.OwnerObject;
                    if (pThis.IsNotNull && pThis.Ref.Base.IsAlive)
                    {
                        // 请求对象自行清理
                        pThis.Ref.Base.UnInit();
                    }
                }
            }
            ActiveTwisters.Clear();

            // 直接移除装饰器（如果存在）
            try
            {
                ScenarioExt.Global().Remove(scenarioDecoratorId);
            }
            catch
            {
                // 忽略：若装饰器已被移除或场景销毁则无动作
            }

            base.Uninit();
        }

        private void SpawnTwister(CellStruct startCrd)
        {
            var unitType = TwisterUnitType;
            if (unitType.IsNull)
                return;

            var owner = GetRandomHouseOnOurSide();
            if (owner.IsNull)
                return;

            var twisterUnit = TechnoExt.CreateObjectWithDecorator<MutatorTwister>(
                unitType.Convert<ObjectTypeClass>(),
                owner,
                MutatorTwister.ID,
                this,
                startCrd
            ).Convert<TechnoClass>();

            if (twisterUnit.IsNull)
                return;

            ++Game.IKnowWhatImDoing;
            twisterUnit.Ref.Base.Put(MapClass.Instance.GetCellAt(startCrd).Ref.Base.GetCoords(), DirType.North);
            --Game.IKnowWhatImDoing;

            var fallback = TargetCellMutator.SelectRandomCellOutOfSafeZone();
            if (fallback.IsNotNull)
            {
                twisterUnit.Ref.SetDestination(fallback);
                twisterUnit.Ref.BaseMission.QueueMission(Mission.Move, true);
            }

            var ext = TechnoExt.ExtMap.Find(twisterUnit);
            if (ext != null)
            {
                ActiveTwisters.Add(new ExtensionReference<TechnoExt>(ext));
            }
        }

        [Serializable]
        public class MutatorTwister : MutatorEventDecorator
        {
            public static new DecoratorId ID => new DecoratorId((int)Mutator.TechnoDecoratorIDs.UniqueDecorator);

            private CellStruct initialSpawn;
            private CellStruct targetCell;
            private int explodeCounter = 0;
            private int moveCounter = 0;
            private int moveInterval = 0; // 帧数，随机 4-6 秒

            // 增加：上次强制退出安全区触发帧 与 冷却帧数（用于每1秒最多触发一次）
            private int lastSafeZoneExitFrame = int.MinValue;
            private int safeZoneExitCooldownFrames = 0;

            // 常量：排斥距离（格）
            private const double RepelDistanceCells = 5.0;
            // 每次移动偏移距离（格）
            private const int MoveDistanceMin = 3;
            private const int MoveDistanceMax = 5;

            public MutatorTwister(Mutator mutator, CellStruct spawn) : base(mutator)
            {
                initialSpawn = spawn;
                targetCell = spawn;
                ResetMoveInterval();
                // 在构造时计算 1 秒对应的帧数
                safeZoneExitCooldownFrames = Mutator.TimeToFrame(0, 1);
            }

            private void ResetMoveInterval()
            {
                var rnd = ScenarioClass.Instance.Random;
                int sec = rnd.RandomRanged(4, 6);
                moveInterval = Mutator.TimeToFrame(0, sec);
                moveInterval += rnd.RandomRanged(0, Mutator.TimeToFrame(0, 1) / 4);
                moveCounter = 0;
            }

            public override void OnUpdate()
            {
                var pThis = (Decorative as TechnoExt).OwnerObject;
                if (pThis.IsNull || !pThis.Ref.Base.IsAlive) return;

                var currentCrd = pThis.Ref.BaseAbstract.GetCoords();
                var currentCell = MapClass.Instance.GetCellAt(currentCrd);

                // 如果当前在安全区内，立刻选择一个安全区外的目标并朝外移动（龙卷风可以改变方向以退出安全区）
                // 增加节流：每 1 秒最多触发一次
                if (TargetCellMutator.IsInSafeZone(currentCell))
                {
                    if (Game.CurrentFrame - lastSafeZoneExitFrame >= safeZoneExitCooldownFrames)
                    {
                        var fallback = TargetCellMutator.SelectRandomCellOutOfSafeZone();
                        if (fallback.IsNotNull)
                        {
                            targetCell = fallback.Ref.MapCoords;
                            var targetAbstract = MapClass.Instance.GetCellAt(targetCell).Convert<AbstractClass>();
                            pThis.Ref.SetDestination(targetAbstract);
                            pThis.Ref.BaseMission.QueueMission(Mission.Move, true);

                            // 记录触发帧，开始冷却
                            lastSafeZoneExitFrame = Game.CurrentFrame;
                        }
                    }
                }

                // 到达目标时挑选新目标（避免停滞）
                if (CellClass.Coord2Cell(currentCrd).DistanceFrom(targetCell) <= 1.0)
                {
                    ResetMoveInterval();
                    ChooseNewTarget(pThis, currentCrd);
                }

                // 引爆武器（每0.25秒），龙卷风持续引爆（即使穿过或短暂进入安全区也会继续）
                explodeCounter++;
                if (explodeCounter >= Mutator.TimeToFrame(0, 0.25))
                {
                    explodeCounter = 0;
                    DetonateWeapon(pThis);
                }

                // 周期性移动
                moveCounter++;
                if (moveCounter >= moveInterval)
                {
                    moveCounter = 0;
                    ResetMoveInterval();
                    ChooseNewTarget(pThis, currentCrd);
                }

                // 若目标被篡改（外部），确保执行移动mission
                var dest = pThis.Convert<FootClass>().Ref.Destination;
                if (dest.IsNotNull && dest.Ref.GetMapCrd() != targetCell)
                {
                    var targetAbstract = MapClass.Instance.GetCellAt(targetCell).Convert<AbstractClass>();
                    pThis.Ref.SetDestination(targetAbstract);
                    pThis.Ref.BaseMission.QueueMission(Mission.Move, true);
                }
            }

            private void ChooseNewTarget(Pointer<TechnoClass> pThis, CoordStruct currentCrd)
            {
                var parent = (Mutator)myMutator;
                var twisterOwner = parent as Twister;

                var currentCell = CellClass.Coord2Cell(currentCrd);
                var rnd = ScenarioClass.Instance.Random;

                var primaryCandidates = new List<CellStruct>();   // 满足所有条件：范围、可用、非安全区、远离其他龙卷风
                var secondaryCandidates = new List<CellStruct>(); // 退化条件：范围内且可用（允许在安全区内），仍避免靠近其它龙卷风

                // 遍历半径内的偏移
                var spreadEnum = new CellSpreadEnumerator(MoveDistanceMax);
                foreach (var offset in spreadEnum)
                {
                    var candidate = currentCell + offset;

                    if (TargetCellMutator.IsOutOfSafeZone(candidate))
                    {
                        // 记录 secondary 候选（安全区外即可）
                        secondaryCandidates.Add(candidate);
                    }
                    else
                        continue;

                    double offsetDist = offset.Magnitude();
                    if (offsetDist <= MoveDistanceMin || offsetDist > MoveDistanceMax)
                        continue;

                    // 排除与其他龙卷风过近的位置
                    bool nearOther = false;
                    if (twisterOwner != null)
                    {
                        foreach (var otherRef in twisterOwner.ActiveTwisters)
                        {
                            if (!otherRef.TryGet(out var otherExt))
                                continue;
                            var otherObj = otherExt.OwnerObject;
                            if (otherObj.IsNull)
                                continue;
                            if (otherObj == pThis)
                                continue;

                            var otherCell = otherObj.Ref.BaseAbstract.GetMapCrd();
                            double cellDistToOther = candidate.DistanceFrom(otherCell);
                            if (cellDistToOther <= RepelDistanceCells)
                            {
                                nearOther = true;
                                break;
                            }
                        }
                    }
                    if (nearOther)
                        continue;

                    primaryCandidates.Add(candidate);
                }

                CellStruct chosenCell = CellStruct.Empty;
                if (primaryCandidates.Count > 0)
                {
                    chosenCell = ScenarioClass.GetRandomInList(primaryCandidates);
                }
                else if (secondaryCandidates.Count > 0)
                {
                    // 退化：范围内允许安全区格子
                    chosenCell = ScenarioClass.GetRandomInList(secondaryCandidates);
                }
                else
                {
                    // 最后退化：全图范围内寻找安全区外的格子
                    var fallbackCell = TargetCellMutator.SelectRandomCellOutOfSafeZone();
                    if (fallbackCell.IsNotNull)
                        chosenCell = fallbackCell.Ref.MapCoords;
                    else
                    {
                        chosenCell = currentCell; // 最糟情况下保留当前位置
                        Logger.Log("Twister at {0} failed to find a new target cell, staying in place.", currentCrd);
                    }
                }

                targetCell = chosenCell;
                var targetAbstract = MapClass.Instance.GetCellAt(targetCell).Convert<AbstractClass>();
                pThis.Ref.SetDestination(targetAbstract);
                pThis.Ref.BaseMission.QueueMission(Mission.Move, true);
            }

            private void DetonateWeapon(Pointer<TechnoClass> techno)
            {
                var weapon = MutTwisterWeapon;
                if (weapon.IsNull) return;
                weapon.Ref.DetonateAtSelf(techno);
            }
        }

        [Serializable]
        public class TwisterRangeDecorator : EventRenderDecorator
        {
            private Twister myOwnerMutator;

            public TwisterRangeDecorator(Twister ownerMutator)
            {
                myOwnerMutator = ownerMutator;
            }

            public override void OnUpdate()
            {
                // 定期清理失效引用并在没有活跃龙卷风时移除自身
                if (Game.CurrentFrame % 15 == 0 && myOwnerMutator != null)
                {
                    myOwnerMutator.ActiveTwisters.RemoveAll(t => t.ReferenceCollected);
                }
            }

            public override void OnRender()
            {
                if (myOwnerMutator == null)
                    return;

                foreach (var twRef in myOwnerMutator.ActiveTwisters)
                {
                    if (!twRef.TryGet(out var ext))
                        continue;
                    var pThis = ext.OwnerObject;

                    var script = ext.Get(MutatorTwister.ID) as MutatorTwister;
                    if (script == null) continue;

                    var centerCrd = pThis.Ref.BaseAbstract.GetCoords();
                    centerCrd.Z -= pThis.Ref.Base.GetHeight();
                    float radius = 1.3f - 0.5f;

                    ColorStruct color = new ColorStruct(255, 0, 0);
                    TacticalClass.DrawRadialIndicator(false, false, centerCrd, color, radius);
                }
            }
        }
    }
}
