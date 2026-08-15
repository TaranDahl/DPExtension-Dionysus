using DynamicPatcher;
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
    public class Propagators : TargetTechnoMutator
    {
        private static SwizzleablePointer<TechnoTypeClass> Propagator => new SwizzleablePointer<TechnoTypeClass>(TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find("MACJOHN"));
        private static string SpawnSound => "EVA_UnitsInCombat";
        public override string UIName => "同化体";
        public override string Description => "无形的麻酱缓慢爬向你的基地，被其接触到的任何单位和建筑都将变成和它们一样的复制体。";
        public override bool IsAvailableInRPG => false;
        public override int Score => 8;
        private int counter = 0;
        public Propagators(Pointer<HouseClass> owner) : base(owner) { }
        public override bool Update()
        {
            if (!base.Update())
                return false;

            // 3分钟25时开始生效
            if (Game.CurrentFrame >= TimeToFrame(3, 25))
            {
                if (counter == 0)
                {
                    VoxClass.Play(SpawnSound);
                    List<Pointer<BuildingClass>> availableBuildings = new List<Pointer<BuildingClass>>();
                    foreach (var house in HouseClass.Array)
                    {
                        if (!IsOnOurSide(house))
                            continue;

                        foreach (var building in house.Ref.Buildings)
                        {
                            if (IsTechnoValid(building.Convert<TechnoClass>()))
                                availableBuildings.Add(building);
                        }
                    }
                    if (availableBuildings.Count > 0)
                    {
                        var wholeList = GetTypesToSpawn();
                        foreach(var type in wholeList)
                        {
                            var list = new List<Pointer<TechnoTypeClass>>();
                            list.Add(type);
                            var spawner = availableBuildings[ScenarioClass.Instance.Random.RandomRanged(0, availableBuildings.Count - 1)];
                            CreateTeamAtCrd(MutatorSpawnTeam, list, spawner.Ref.Base.BaseAbstract.GetOwningHouse(), spawner.Ref.Base.BaseAbstract.GetCoords());
                        }
                    }
                }

                counter++;
                counter %= GetSpawnDelay();
            }
            return true;
        }

        protected override unsafe bool IsTechnoValid(Pointer<TechnoClass> techno)
        {
            // 必须是建筑
            if (techno.Ref.Base.Base.WhatAmI() != AbstractType.Building)
                return false;

            // 必须是友军
            var technoOwner = techno.Ref.Base.Base.GetOwningHouse();
            if (!IsOnOurSide(technoOwner))
                return false;

            // 必须在地图内
            var technoCell = techno.Ref.Base.GetCell();
            if (!MapClass.Instance.IsWithinUsableArea(ref technoCell.Ref.MapCoords, true))
                return false;

            // 必须不在水里
            var landType = technoCell.Ref.LandType;
            if (landType == LandType.Water || landType == LandType.Beach)
                return false;

            // 必须和至少一个敌人基地位置陆地联通
            foreach (var house in HouseClass.Array)
            {
                // 目标必须是敌人
                if (!IsOnTheirSide(house))
                    continue;

                foreach (var building in house.Ref.Buildings)
                {
                    // 目标必须不在水里
                    var buildingCell = building.Ref.Base.Base.GetCell();
                    var targetLandType = buildingCell.Ref.LandType;
                    if (targetLandType == LandType.Water || targetLandType == LandType.Beach)
                        continue;

                    // 目标必须是保持存活的建筑
                    if (GetKeepAliveAbility(building.Convert<TechnoClass>()) == 0)
                        continue;

                    // 目标必须联通
                    if (MapClass.Instance.IsInSameZone(ref technoCell.Ref.MapCoords, ref buildingCell.Ref.MapCoords, MovementZone.Infantry, false, false, false))
                        return true;
                }
            }

            return false;
        }
        private List<Pointer<TechnoTypeClass>> GetTypesToSpawn()
        {
            var list = new List<Pointer<TechnoTypeClass>>();
            var frame = Game.CurrentFrame;
            if (frame < TimeToFrame(11, 40))
            {
                list.Add(Propagator);
            }
            else if (frame < TimeToFrame(18, 20))
            {
                list.Add(Propagator);
                list.Add(Propagator);
            }
            else if (frame < TimeToFrame(23, 20))
            {
                list.Add(Propagator);
                list.Add(Propagator);
                list.Add(Propagator);
                list.Add(Propagator);
            }
            else
            {
                list.Add(Propagator);
                list.Add(Propagator);
                list.Add(Propagator);
                list.Add(Propagator);
                list.Add(Propagator);
            }
            return list;
        }
        private int GetSpawnDelay()
        {
            var delay = 0;
            var frame = Game.CurrentFrame;
            if (frame < TimeToFrame(8, 20))
                delay = TimeToFrame(0, 90);
            else if (frame < TimeToFrame(20, 0))
                delay = TimeToFrame(0, 60);
            else
                delay = TimeToFrame(0, 50);
            return delay;
        }
    }
}
