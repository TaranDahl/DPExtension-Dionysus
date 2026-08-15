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
    public class KillBots : TargetTechnoMutator
    {
        private static Pointer<UnitTypeClass> MurderBot => new Pointer<UnitTypeClass>(UnitTypeClass.ABSTRACTTYPE_ARRAY.Find("MURDERBOT"));
        private static Pointer<UnitTypeClass> DeathBot => new Pointer<UnitTypeClass>(UnitTypeClass.ABSTRACTTYPE_ARRAY.Find("DEATHBOT"));
        private static Pointer<UnitTypeClass> KillBot => new Pointer<UnitTypeClass>(UnitTypeClass.ABSTRACTTYPE_ARRAY.Find("KILLBOT"));
        private static string SpawnSound => "EVA_UnitsInCombat";
        public override string UIName => "杀戮机器人";
        public override string Description => "来源不明的进攻性机器人已被释放到了科普卢星区，意图制造毁灭。经过用心险恶的工程改造后，它们在达到预先设定的击杀数量之前都是无敌的存在。只有在那之后，它们才能被阻止。不过，你能撑到最后吗？";
        public override bool IsAvailableInRPG => false;
        public override int Score => 6;
        private int counter = 0;
        public KillBots(Pointer<HouseClass> owner) : base(owner) { }
        public override bool Update()
        {
            if (!base.Update())
                return false;

            // 3分钟35时开始生效
            if (Game.CurrentFrame >= TimeToFrame(3, 35))
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
                        foreach (var type in wholeList)
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
            if (frame < TimeToFrame(6, 40))
            {
                list.Add(MurderBot.Convert<TechnoTypeClass>());
            }
            else if (frame < TimeToFrame(10, 0))
            {
                list.Add(MurderBot.Convert<TechnoTypeClass>());
                list.Add(MurderBot.Convert<TechnoTypeClass>());
            }
            else if (frame < TimeToFrame(14, 10))
            {
                list.Add(DeathBot.Convert<TechnoTypeClass>());
            }
            else if (frame < TimeToFrame(19, 10))
            {
                list.Add(DeathBot.Convert<TechnoTypeClass>());
                list.Add(DeathBot.Convert<TechnoTypeClass>());
            }
            else if (frame < TimeToFrame(21, 40))
            {
                list.Add(KillBot.Convert<TechnoTypeClass>());
                list.Add(KillBot.Convert<TechnoTypeClass>());
            }
            else if (frame < TimeToFrame(25, 0))
            {
                list.Add(KillBot.Convert<TechnoTypeClass>());
                list.Add(KillBot.Convert<TechnoTypeClass>());
                list.Add(KillBot.Convert<TechnoTypeClass>());
            }
            else
            {
                list.Add(KillBot.Convert<TechnoTypeClass>());
                list.Add(KillBot.Convert<TechnoTypeClass>());
                list.Add(KillBot.Convert<TechnoTypeClass>());
                list.Add(KillBot.Convert<TechnoTypeClass>());
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
                delay = TimeToFrame(0, 70);
            else
                delay = TimeToFrame(0, 60);
            return delay;
        }
    }
}
