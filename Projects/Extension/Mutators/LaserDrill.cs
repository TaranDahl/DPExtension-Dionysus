using DynamicPatcher;
using Extension.Decorators;
using Extension.Ext;
using Extension.Script;
using Extension.Utilities;
using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InteropUtils;

namespace Extension.Mutators
{
    [Serializable]
    public class LaserDrill : TargetCellMutator
    {
        // Mutator
        public LaserDrill(Pointer<HouseClass> owner) : base(owner) { }
        public override string UIName => "激光钻机";
        public override string Description => "一台敌方激光钻机会不停地攻击位于敌人视野范围内的玩家单位。";
        public override bool IsAvailableInRPG => false;
        public override int Score => 2;
        public override void Init(bool isInitial = true)
        {
            base.Init(isInitial);
            TryPlaceDrill();
        }
        public override void Uninit()
        {
            foreach (var house in HouseClass.Array)
            {
                if (!IsOnOurSide(house))
                    continue;

                foreach (var building in house.Ref.Buildings)
                {
                    var type = building.Ref.Type;
                    if (type != driller && type != drillerWreckage)
                        continue;

                    building.Ref.BaseObject.Vanish(Pointer<TechnoClass>.Zero);
                }
            }
            base.Uninit();
        }
        public override bool Update()
        {
            if (!base.Update())
                return false;

            bool hasDriller = MyDrillerExt.TryGet(out var ext);

            if (HasDrillerLastFrame && !hasDriller)
                DrillerIsRebuilding = true;

            if (!hasDriller)
            {
                if (DrillerIsRebuilding)
                {
                    if (RebuildCounter >= RebuildTime)
                    {
                        DrillerIsRebuilding = false;
                        RebuildCounter = 0;
                        TryPlaceDrill();
                    }
                    else
                    {
                        RebuildCounter++;
                    }
                }
                else
                {
                    if (RetryCounter == 0)
                        TryPlaceDrill();

                    ++RetryCounter;
                    RetryCounter %= RetryTime;
                }
            }

            HasDrillerLastFrame = MyDrillerExt.TryGet(out ext);
            return true;
        }

        // LaserDrill
        public static Pointer<BuildingTypeClass> driller => new Pointer<BuildingTypeClass>(BuildingTypeClass.ABSTRACTTYPE_ARRAY.Find("LSRDRL"));
        public static Pointer<BuildingTypeClass> drillerWreckage => new Pointer<BuildingTypeClass>(BuildingTypeClass.ABSTRACTTYPE_ARRAY.Find("LSRDRLWKG"));
        public static Pointer<WarheadTypeClass> repairFinishWH => new Pointer<WarheadTypeClass>(WarheadTypeClass.ABSTRACTTYPE_ARRAY.Find("DrillRepairFinishWH"));
        public static int RebuildTime => TimeToFrame(2, 0);
        public static int RetryTime => TimeToFrame(0, 10);
        private static double FindRange = 20.0;
        private bool HasDrillerLastFrame = false;
        private ExtensionReference<TechnoExt> MyDrillerExt;
        private CellStruct MyDrillerMapCrd = CellStruct.Empty;
        private SwizzleablePointer<HouseClass> MyDrillerOwner = new SwizzleablePointer<HouseClass>();
        private int RetryCounter = 0;
        private bool DrillerIsRebuilding = false;
        private int RebuildCounter = 0;
        private static CellStruct FindPlaceForDrill(CellStruct baseCenter)
        {
            var offsetToCenter = new CellStruct(2, 2);
            var realCenterMapCrd = baseCenter - offsetToCenter;
            var cellEnum = new CellSpreadEnumerator((uint)FindRange);
            var validCells = new List<CellStruct>();
            foreach (var offset in cellEnum)
            {
                var mapCrd = offset + realCenterMapCrd;
                if (CanPlaceDrill(mapCrd))
                    validCells.Add(mapCrd);
            }
            if (validCells.Count > 0)
                return ScenarioClass.GetRandomInList(validCells);
            return CellStruct.Empty;
        }
        private static bool CanPlaceDrill(CellStruct mapCrd)
        {
            var height = MapClass.Instance.GetCellAt(mapCrd).Ref.GetFloorHeight(new Point2D(0, 0));
            foreach (var foundation in driller.Ref.BaseObjectType.GetFoundationList())
            {
                var curCell = MapClass.Instance.GetCellAt(foundation + mapCrd);
                // 没建筑重叠
                if (curCell.Ref.GetBuilding().IsNotNull)
                    return false;
                // 在地图内
                if (!MapClass.Instance.IsWithinUsableArea(foundation + mapCrd, true))
                    return false;
                // 位于同一高度（影响伤害查找）
                if (curCell.Ref.GetFloorHeight(new Point2D(0, 0)) != height)
                    return false;
            }
            return true;
        }
        private static void ClearPlaceForDrill(CellStruct mapCrd)
        {
            foreach (var foundation in driller.Ref.BaseObjectType.GetFoundationList())
            {
                var curCell = MapClass.Instance.GetCellAt(foundation + mapCrd);
                // 删掉重叠的建筑
                var building = curCell.Ref.GetBuilding();
                if (building.IsNotNull)
                    building.Ref.BaseObject.Vanish(Pointer<TechnoClass>.Zero);
            }
        }
        private bool TryPlaceDrill()
        {
            var center = CellStruct.Empty;
            var owner = Pointer<HouseClass>.Zero;

            // 先检查已经记录的格子
            if (MyDrillerMapCrd != CellStruct.Empty)
            {
                var cell = MapClass.Instance.GetCellAt(MyDrillerMapCrd);
                var building = cell.Ref.GetBuilding();
                if (building.IsNotNull)
                {
                    // 有钻机
                    if (building.Ref.Type == driller || building.Ref.Type == drillerWreckage)
                        return true;
                }
                // 原本的格子已经在地图外了
                if (!MapClass.Instance.IsWithinUsableArea(MyDrillerMapCrd, true))
                {
                    MyDrillerMapCrd = CellStruct.Empty;
                    MyDrillerOwner = new SwizzleablePointer<HouseClass>(Pointer<HouseClass>.Zero);
                }
                else
                {
                    // 如果放不了，那就先把上面的建筑清掉
                    if (!CanPlaceDrill(MyDrillerMapCrd))
                        ClearPlaceForDrill(MyDrillerMapCrd);
                    center = MyDrillerMapCrd;
                    owner = MyDrillerOwner;
                }
            }
            
            // 如果记录的格子无效或没记录过，那就重新找
            if (MyDrillerMapCrd == CellStruct.Empty)
            {
                var validCells = new List<CellStruct>();
                var validOwner = new List<Pointer<HouseClass>>();
                foreach (var house in HouseClass.Array)
                {
                    if (!IsOnOurSide(house))
                        continue;
                    var curCenter = GetGeoCenterMapCrd(house);
                    if (curCenter == CellStruct.Empty)
                        curCenter = house.Ref.GetBaseCenter();
                    if (curCenter != CellStruct.Empty && MapClass.Instance.IsWithinUsableArea(curCenter, true))
                    {
                        var placeForDrill = FindPlaceForDrill(curCenter);
                        if (placeForDrill != CellStruct.Empty)
                        {
                            validCells.Add(placeForDrill);
                            validOwner.Add(house);
                        }
                    }
                }
                if (validCells.Count <= 0)
                {
                    Logger.Log("Find place for laser drill failed!");
                    return false;
                }
                var idx = ScenarioClass.Instance.Random.RandomRanged(0, validCells.Count - 1);
                center = validCells[idx];
                owner = validOwner[idx];
                MyDrillerMapCrd = center;
                MyDrillerOwner = new SwizzleablePointer<HouseClass>(owner);
            }
            List<Pointer<TechnoClass>> list = new List<Pointer<TechnoClass>>();
            var drill = TechnoExt.CreateObjectWithDecorator<LaserDrillScript>(driller.Convert<ObjectTypeClass>(), owner, LaserDrillScript.ID, this).Convert<TechnoClass>();
            list.Add(drill);
            MyDrillerExt.Set(TechnoExt.ExtMap.Find(drill));
            CreateTeamAtCrd(Pointer<TeamTypeClass>.Zero, list, CellClass.Cell2Coord(center), true, true);
            return true;
        }


        [Serializable]
        public class LaserDrillScript : MutatorEventDecorator
        {
            public static DecoratorId ID => new DecoratorId((int)TechnoDecoratorIDs.UniqueDecorator);
            private static int targetingDelay => Mutator.TimeToFrame(0, 1);
            private int rebuildCounter = 0;
            private int targetingCounter = 0;
            private bool mutatorFound = false;

            public LaserDrillScript(Mutator mutator) : base(mutator) { }

            public override void OnUpdate()
            {
                if (!mutatorFound)
                {
                    mutatorFound = true;
                    foreach (var mutator in Mutator.Array)
                    {
                        if (mutator is LaserDrill)
                        {
                            myMutator = mutator;
                            break;
                        }
                    }
                }
                var pThis = (Decorative as TechnoExt).OwnerObject;
                var building = pThis.Convert<BuildingClass>();
                // 钻机更新
                if (building.Ref.Type == LaserDrill.driller)
                {
                    var target = building.Ref.Base.Target;
                    if (target.IsNotNull)
                    {
                        // 不攻击安全区内的单位
                        if (TargetCellMutator.IsInSafeZone(target))
                        {
                            building.Ref.Base.SetTarget(Pointer<AbstractClass>.Zero);
                            building.Ref.Base.BaseMission.QueueMission(Mission.Guard, true);
                        }
                    }

                    if (target.IsNull)
                    {
                        targetingCounter++;
                        targetingCounter %= targetingDelay;
                        if (targetingCounter == 0)
                        {
                            // 索敌
                            Pointer<TechnoClass> bestTarget = Pointer<TechnoClass>.Zero;
                            int bestDist = int.MaxValue;
                            foreach (var techno in TechnoClass.Array)
                            {
                                if (myMutator.IsOnTheirSide(techno.Ref.BaseAbstract.GetOwningHouse())
                                    && TargetTechnoMutator.GetKeepAliveAbility(techno) > 0
                                    && !TargetCellMutator.IsInSafeZone(techno)
                                    && pThis.Ref.GetFireError(techno.Convert<AbstractClass>(), -1, false) != FireError.ILLEGAL)
                                {
                                    var currentDist = pThis.Ref.BaseAbstract.DistanceFrom(techno.Convert<AbstractClass>());
                                    if (currentDist < bestDist) // 找最近的
                                    {
                                        bestDist = currentDist;
                                        bestTarget = techno;
                                    }
                                }
                            }
                            if (bestTarget.IsNotNull)
                            {
                                building.Ref.Base.SetTarget(bestTarget.Convert<AbstractClass>());
                                building.Ref.Base.BaseMission.QueueMission(Mission.Attack, true);
                            }
                        }
                    }
                }
                else // 残骸更新
                {
                    rebuildCounter++;
                    // 维修进度
                    var newHealth = (int)((double)building.Ref.Type.Ref.BaseObjectType.Strength * ((double)rebuildCounter / (double)LaserDrill.RebuildTime));
                    newHealth = newHealth > 1 ? newHealth : 1;
                    var damage = building.Ref.BaseObject.Health - newHealth;
                    building.Ref.BaseObject.TakeDamage(damage, false);
                    // 重建完毕
                    if (rebuildCounter >= LaserDrill.RebuildTime)
                    {
                        rebuildCounter = 0;
                        PhobosTechnoExt.ConvertToType(building.Convert<FootClass>(), driller.Convert<TechnoTypeClass>());
                        building.Ref.BaseObject.Mark(MarkType.CHANGE_REDRAW);
                    }
                    else
                    {
                        // 残骸不能攻击
                        if (pThis.Ref.Target.IsNotNull)
                            pThis.Ref.SetTarget(Pointer<AbstractClass>.Zero);
                    }
                }
            }
            public override void OnReceiveDamage(Pointer<int> pDamage, int DistanceFromEpicenter, Pointer<WarheadTypeClass> pWH, Pointer<ObjectClass> pAttacker, bool IgnoreDefenses, bool PreventPassengerEscape, Pointer<HouseClass> pAttackingHouse)
            {
                if (IgnoreDefenses)
                    return;
                var pThis = (Decorative as TechnoExt).OwnerObject;
                var building = pThis.Convert<BuildingClass>();
                if (building.Ref.Type == LaserDrill.drillerWreckage)  // 钻机残骸
                    pDamage.Ref = 0; // 免疫伤害
            }
            public override DamageState AfterReceiveDamage(Pointer<int> pDamage, int DistanceFromEpicenter, Pointer<WarheadTypeClass> pWH, Pointer<ObjectClass> pAttacker, bool IgnoreDefenses, bool PreventPassengerEscape, Pointer<HouseClass> pAttackingHouse, DamageState result, int damageTaken)
            {
                if (IgnoreDefenses || result != DamageState.NowDead)
                    return result;
                var pThis = (Decorative as TechnoExt).OwnerObject;
                var building = pThis.Convert<BuildingClass>();
                if (building.Ref.Type == LaserDrill.driller) // 钻机
                {
                    result = DamageState.Unaffected;
                    rebuildCounter = 0;
                    InteropUtils.PhobosTechnoExt.ConvertToType(building.Convert<FootClass>(), drillerWreckage.Convert<TechnoTypeClass>());
                    building.Ref.BaseObject.Mark(MarkType.CHANGE_REDRAW);
                    pThis.Ref.Base.Health = damageTaken; // 免死
                    pThis.Ref.Base.IsAlive = true;
                }
                else
                {
                    // 死之前变回去，避免各种计数问题
                    InteropUtils.PhobosTechnoExt.ConvertToType(building.Convert<FootClass>(), driller.Convert<TechnoTypeClass>());
                }
                return result;
            }
        }
    }
}
