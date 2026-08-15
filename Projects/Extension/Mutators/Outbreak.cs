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
    public class Outbreak : TargetTechnoMutator
    {
        private static SwizzleablePointer<TechnoTypeClass> ZombieLv1 => new SwizzleablePointer<TechnoTypeClass>(TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find("ZOMBIE_M"));
        private static SwizzleablePointer<TechnoTypeClass> ZombieLv2 => new SwizzleablePointer<TechnoTypeClass>(TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find("HARP"));
        private static SwizzleablePointer<TechnoTypeClass> ZombieLv3 => new SwizzleablePointer<TechnoTypeClass>(TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find("BRUTE"));
        private static SwizzleablePointer<TechnoTypeClass> ZombieLv4 => new SwizzleablePointer<TechnoTypeClass>(TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find("STALKER"));
        public override string UIName => "丧尸大战";
        public override string Description => "敌方被感染的人类会不断地出现在地图上。";
        public override bool IsAvailableInRPG => false;
        public override int Score => 3;
        private int counter = 0;
        public Outbreak(Pointer<HouseClass> owner) : base(owner) { }
        public override bool Update()
        {
            if (!base.Update())
                return false;

            if (Game.CurrentFrame >= TimeToFrame(2, 0))
            {
                if (counter == 0)
                {
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
                        var spawner1 = availableBuildings[ScenarioClass.Instance.Random.RandomRanged(0, availableBuildings.Count - 1)];
                        var spawner2 = availableBuildings[ScenarioClass.Instance.Random.RandomRanged(0, availableBuildings.Count - 1)];
                        var list = GetTypesToSpawn();

                        Pointer<TeamClass> createTeam(Pointer<BuildingClass> spawner) => CreateTeamAtCrd(
                            MutatorSpawnTeam,
                            list,
                            spawner.Ref.Base.BaseAbstract.GetOwningHouse(),
                            spawner.Ref.Base.BaseAbstract.GetCoords());

                        createTeam(spawner1);
                        createTeam(spawner2);
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

                foreach(var building in house.Ref.Buildings)
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
            if (frame < TimeToFrame(6, 40))
            {
                list.Add(ZombieLv1);
                list.Add(ZombieLv1);
                list.Add(ZombieLv1);
            }
            else if (frame < TimeToFrame(13, 20))
            {
                list.Add(ZombieLv2);
                list.Add(ZombieLv2);
                list.Add(ZombieLv3);
            }
            else if (frame < TimeToFrame(20, 0))
            {
                list.Add(ZombieLv2);
                list.Add(ZombieLv2);
                list.Add(ZombieLv2);
                list.Add(ZombieLv3);
                list.Add(ZombieLv3);
                list.Add(ZombieLv3);
            }
            else
            {
                list.Add(ZombieLv2);
                list.Add(ZombieLv2);
                list.Add(ZombieLv3);
                list.Add(ZombieLv3);
                list.Add(ZombieLv4);
                list.Add(ZombieLv4);
            }
            return list;
        }
        private int GetSpawnDelay ()
        {
            var delay = 0;
            var frame = Game.CurrentFrame;
            delay = TimeToFrame(0, 25);
            return delay;
        }
    }
}
