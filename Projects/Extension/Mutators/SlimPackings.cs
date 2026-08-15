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
    public class SlimPackings : BuffMutator
    {
        // 数值调整：
        // 资源总量按星际版本*汇率（3）*估计经济比率（4）
        // 并且后续会增加捡钱时盟友也获取等量的钱
        // 汇率是考虑单位价格，大概是3倍
        // 经济比率是考虑星际2一分钟只有1640经济，而MO后期1分钟2、3W经济都很正常，所以乘个4
        // Mutator
        public override string UIName => "小捞油水";
        public override string Description => "玩家的工人单位采集资源的效率降低，但是地图上会生成可以拾取的资源。";
        public override bool IsAvailableInRPG => false;
        public override int Score => 5;

        protected override bool IsBuffEnemy => true;

        public SlimPackings(Pointer<HouseClass> owner) : base(owner) { }
        public override bool Update()
        {
            if (!base.Update())
                return false;

            if (SpawnCrateCounter == 0)
            {
                // 移除超时的箱子
                foreach (var crate in MyCrates)
                {
                    if (crate.CrateTimer.Completed())
                        crate.Remove();
                }
                MyCrates.RemoveAll(crate => crate.CrateTimer.Completed());

                // 刷新箱子
                if (Game.CurrentFrame >= TimeToFrame(3, 0))
                {
                    TrySpawnCrate();
                    TrySpawnCrate(true);
                }
                else
                {
                    TrySpawnCrate();
                    TrySpawnCrate();
                }
            }
            SpawnCrateCounter++;
            SpawnCrateCounter %= GetSpawnDelay();
            return true;
        }
        public override void OnTechnoCTOR(Pointer<TechnoClass> techno)
        {
            if (!IsTechnoOwnerValid(techno.Ref.BaseAbstract.GetOwningHouse()))
                return;

            var type = techno.Ref.GetTechnoType();
            var rtti = techno.Ref.BaseAbstract.WhatAmI();
            if (rtti == AbstractType.Unit)
            {
                if (type.Convert<UnitTypeClass>().Ref.Harvester)
                    BuffTechno(techno);
            }
            else if (rtti == AbstractType.Infantry)
            {
                // TODO：步兵在这里没法判断奴隶
            }
        }
        public override void OnTechnoChangeOwner(Pointer<TechnoClass> techno, Pointer<HouseClass> toHouse)
        {
            if (!IsTechnoOwnerValid(toHouse))
                UnbuffTechno(techno);
            else
                BuffTechno(techno);
        }

        // BuffMutator
        protected override void BuffTechno(Pointer<TechnoClass> techno)
        {
            var ext = TechnoExt.ExtMap.Find(techno);
            if (ext.Get(SlimPackingsMinerBuff.ID) == null)
                ext.CreateDecorator<SlimPackingsMinerBuff>(SlimPackingsMinerBuff.ID, "SlimPackingsMinerBuff", this);
        }
        protected override void UnbuffTechno(Pointer<TechnoClass> techno)
        {
            var ext = TechnoExt.ExtMap.Find(techno);
            ext.Remove(SlimPackingsMinerBuff.ID);
        }

        // SlimPackings
        private const int StdMoney = 600;
        private int SpawnCrateCounter = 0;
        private List<Crate> MyCrates = new List<Crate>();
        private static int GetSpawnDelay()
        {
            var stdDelay = Game.CurrentFrame >= TimeToFrame(3, 0) ? TimeToFrame(0, 7) : Mutator.TimeToFrame(0, 5.4);
            var moneyPerCrate = RulesClass.Instance.Ref.SoloCrateMoney;
            return (int)(((double)moneyPerCrate / StdMoney) * stdDelay);
        }
        private void TrySpawnCrate(bool closeToPlayer = false)
        {
            var newCrate = new Crate();
            if (closeToPlayer)
            {
                // 要求在玩家建筑附近
                for (var i = 0; i != 200; ++i)
                {
                    var cell = ScenarioClass.GetRandomInList(TargetCellMutator.UsableCells);
                    bool nearPlayer = false;
                    foreach (var house in HouseClass.Array)
                    {
                        if (nearPlayer)
                            break;
                        if (!IsOnTheirSide(house))
                            continue;
                        foreach (var building in house.Ref.Buildings)
                        {
                            if (TargetTechnoMutator.GetKeepAliveAbility(building.Convert<TechnoClass>()) <= 0)
                                continue;
                            if (CellClass.Coord2Cell(building.Ref.BaseAbstract.GetCoords()).DistanceFrom(cell.Ref.MapCoords) <= 30)
                            {
                                nearPlayer = true;
                                break;
                            }
                        }
                    }
                    if (!nearPlayer)
                        continue;
                    var success = newCrate.TryPlaceOnMapCrd(cell.Ref.MapCoords);
                    if (success)
                    {
                        newCrate.CrateTimer.Start(TimeToFrame(5, 0));
                        cell.Ref.Powerup = 0;
                        MyCrates.Add(newCrate);
                        break;
                    }
                }
            }
            else
            {
                // 全场随机
                for (var i = 0; i != 100; ++i)
                {
                    var cell = ScenarioClass.GetRandomInList(TargetCellMutator.UsableCells);
                    var success = newCrate.TryPlaceOnMapCrd(cell.Ref.MapCoords);
                    if (success)
                    {
                        newCrate.CrateTimer.Start(TimeToFrame(5, 0));
                        cell.Ref.Powerup = 0;
                        MyCrates.Add(newCrate);
                        break;
                    }
                }
            }
        }
        public static void OnCrateCollected(CellStruct mapCrd)
        {
            foreach (var mutator in Mutator.Array)
            {
                var slimPacking = mutator as SlimPackings;
                if (slimPacking != null)
                {
                    for (int i = 0; i < slimPacking.MyCrates.Count; i++)
                    {
                        if (slimPacking.MyCrates[i].Location == mapCrd)
                        {
                            slimPacking.MyCrates[i].Remove();
                            slimPacking.MyCrates.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
        }


        [Serializable]
        private class SlimPackingsMinerBuff : MutatorEventDecorator
        {
            // Decorator
            // TODO : Fix me
            public static DecoratorId ID => new DecoratorId((int)TechnoDecoratorIDs.UniqueDecorator);

            public SlimPackingsMinerBuff(Mutator mutator) : base(mutator) { }

            // EventDecorator
            public override unsafe void OnUpdate()
            {
                var ext = Decorative as TechnoExt;
                // 因子失效后移除自己
                if (!myMutator.IsActive())
                {
                    Decorative.Remove(this);
                    return;
                }
                // 如果在采矿，那就消除脚下的矿
                var pThis = ext.OwnerObject;
                var rtti = pThis.Ref.BaseAbstract.WhatAmI();
                if (rtti == AbstractType.Unit)
                {
                    if (pThis.Ref.BaseMission.CurrentMission == Mission.Harvest && pThis.Ref.BaseMission.MissionStatus == 1 && pThis.Ref.Animtaion.Value >= 9)
                        ReduceTiberiumBeneath(pThis);
                }
                else if (rtti == AbstractType.Infantry)
                {
                    if (pThis.Ref.BaseMission.CurrentMission == Mission.Harvest && pThis.Ref.BaseMission.UpdateTimer.Completed())
                        ReduceTiberiumBeneath(pThis);
                }
            }
            private void ReduceTiberiumBeneath(Pointer<TechnoClass> pTechno)
            {
                var cell = MapClass.Instance.GetCellAt(pTechno.Convert<AbstractClass>());
                cell.Ref.ReduceTiberium(4);
            }
        }
    }
}
