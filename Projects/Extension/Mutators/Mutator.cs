
using PatcherYRpp;
using Extension.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DynamicPatcher;
using Extension.Ext;
using System.Runtime.CompilerServices;
using Extension.Script;
using Extension.Decorators;

namespace Extension.Mutators
{
    public enum KeepAliveAbility : int
    {
        ConYard = 4,
        Factory = 3,
        Building = 2,
        Foot = 1,
        Insignificant = 0
    }

    [Serializable]
    public abstract class Mutator
    {
        protected enum TechnoDecoratorIDs : int
        {
            UniqueDecorator = 0, // 唯一装饰器，类似Script，仅会附加给某些Type的单位
            BlackDeathBuff = 1,
            FearfulBuff = 2,
            FearBuff = 3,
            TransmutationBuff = 4,
            LeaveCorpseBuff = 5,
            EvasiveManeuverBuff = 6,
            EminentDomainBuff = 7,
            PolarityBuff = 8,
            LifeLeechBuff = 9,
            DiffusionBuff = 10,
            FatalAttractionBuff = 11,
            ShortsightedBuff = 12,
        }

        public static Type MutatorToTest = false ? typeof(ChaosStudios) : null;

        public static List<Mutator> Array => MutatorCacheManager.Instance.MutatorArray;
        public static List<Mutator> ToBeAddedArray => MutatorCacheManager.Instance.PreMutatorArray;
        public static List<Mutator> ToBeRemovedArray => MutatorCacheManager.Instance.PostMutatorArray;
        private static List<List<List<string>>> TechnoLevelTable => MutatorCacheManager.Instance.TechnoLevelTable;
        // TODO : 完善
        private static List<string> Heros = new List<string> 
        {
            // 常规英雄
            "TANY", "SIEG", "ARMR", 
            "MORALES", "AIMORA", "VOLKOV", "CHITZ", "YUNRU", 
            "ASSN", "UNDER", "LIBRA", 
            "SIBFIN", "SICALI", "EUREKA", "URAGAN", 
            "CYCOM", 
            // 战役英雄
            "REZNOV", "KRUKOV", "CBRIS", "RHAD", "STLN", "ARND", "CLNT", "YURIX", 
            // 史诗
            "CNTR", 
            "GOTTER", 
            "MAD", "MADAI", "BOID", "FABOID", 
            // 战役史诗
            "STARDUST", "STARDUSTB", "ICBM", "ICBMWO", "PERUN", "DHANDL", "DHANDR", "HEPH", "STHOR", "SMAMM",
            // 自己加的
            "STHOR1", "KRUKOVX1", "REZNOVX1", "SMAMMX", "STARDUSTHWMJ"
            // 突变因子（风暴英雄，软，红衣）
        };
        // TODO : 用getter的不需要swizzle, 细化脚本
        public static Pointer<TeamTypeClass> MutatorSpawnTeam => TeamTypeClass.ABSTRACTTYPE_ARRAY.Find("MutatorSpawnTeam");
        public static Mutator CreateMutator(Type mutatorType, Pointer<HouseClass> owner)
        {
            if (mutatorType == null || !typeof(Mutator).IsAssignableFrom(mutatorType))
            {
                throw new ArgumentException("无效的 Mutator 类型");
            }

            var constructor = mutatorType.GetConstructor(new[] { typeof(Pointer<HouseClass>) });
            return (Mutator)constructor.Invoke(new object[] { owner });
        }
        public static void DestroyMutator(Type mutatorType, Pointer<HouseClass> owner)
        {
            if (mutatorType == null || !typeof(Mutator).IsAssignableFrom(mutatorType))
            {
                throw new ArgumentException("无效的 Mutator 类型");
            }
            foreach (var mutator in Array)
            {
                if (mutator.GetType() == mutatorType && mutator.Owner.Pointer == owner)
                {
                    mutator.Uninit();
                    break;
                }
            }
        }
        public static void DestroyMutator(Mutator mutator)
        {
            if (mutator == null)
                return;
            mutator.Uninit();
        }
        // 可以创建建筑，teamType可以为空
        public static Pointer<TeamClass> CreateTeamAtCrd(Pointer<TeamTypeClass> teamType, List<Pointer<TechnoTypeClass>> technoTypes, Pointer<HouseClass> pHouse, CoordStruct location, bool forcePlace = false, bool forceCenter = false)
        {
            // 创建单位
            var cell = CellClass.Coord2Cell(location);
            List<Pointer<FootClass>> foots = new List<Pointer<FootClass>>();
            foreach (var type in technoTypes)
            {
                // 放置
                var isBuilding = type.Ref.Base.Base.Base.WhatAmI() == AbstractType.BuildingType;
                var width = isBuilding ? type.Convert<BuildingTypeClass>().Ref.GetFoundationWidth() : 1;
                var height = isBuilding ? type.Convert<BuildingTypeClass>().Ref.GetFoundationHeight(true) : 1;
                var putLocation = forceCenter ? cell : MapClass.Instance.Pathfinding_Find(ref cell, type.Ref.SpeedType, type.Ref.MovementZone, width, height, isBuilding);
                    //(isBuilding ?
                    //pHouse.Ref.FindPlaceForBuilding(type.Convert<BuildingTypeClass>(), -1)
                    //: MapClass.Instance.Pathfinding_Find(ref cell, type.Ref.SpeedType, type.Ref.MovementZone, width, height, isBuilding));
                var dir = isBuilding ? DirType.North : (DirType)(ScenarioClass.Instance.Random.RandomRanged(0, 7) * 32);
                if (putLocation == CellStruct.Empty)
                {
                    Logger.Log("No place to unlimbo.");
                    continue;
                }
                // 创建单位
                var techno = type.Ref.Base.CreateObject(pHouse).Convert<TechnoClass>();
                if (forcePlace)
                    Game.IKnowWhatImDoing++;
                var success = techno.Ref.Base.Put(MapClass.Instance.GetCellAt(CellClass.Cell2Coord(putLocation)).Ref.Base.GetCoords(), dir);
                if (forcePlace)
                    Game.IKnowWhatImDoing--;
                if (!success)
                {
                    Logger.Log("Unlimbo failed at {0}.", putLocation);
                    Game.IKnowWhatImDoing++;
                    success = techno.Ref.Base.Put(MapClass.Instance.GetCellAt(CellClass.Cell2Coord(putLocation)).Ref.Base.GetCoords(), dir);
                    Game.IKnowWhatImDoing--;
                    if (!success)
                    {
                        Logger.Log("Force unlimbo failed.");
                        techno.Ref.BaseAbstract.DTOR();
                        continue;
                    }
                }
                // 记录可移动的单位
                if (!isBuilding)
                    foots.Add(techno.Convert<FootClass>());
            }

            // 必须有可移动单位且有小队类型才能创建小队
            if (foots.Count == 0 || teamType.IsNull)
                return Pointer<TeamClass>.Zero;

            // 创建小队
            var team = YRMemory.Create<TeamClass>(teamType, pHouse, 0);
            foreach (var techno in foots)
            {
                team.Ref.Addmember(techno, true);
            }
            team.Ref.IsForcedActive = true;
            team.Ref.IsUnderStrength = false;
            return team;
        }
        // 可以创建建筑，teamType可以为空
        public static Pointer<TeamClass> CreateTeamAtCrd(Pointer<TeamTypeClass> teamType, List<Pointer<TechnoClass>> technos, CoordStruct location, bool forcePlace = false, bool forceCenter = false)
        {
            // 创建单位
            var cell = CellClass.Coord2Cell(location);
            List<Pointer<FootClass>> foots = new List<Pointer<FootClass>>();
            foreach (var techno in technos)
            {
                var type = techno.Ref.GetTechnoType();
                // 放置
                var isBuilding = type.Ref.Base.Base.Base.WhatAmI() == AbstractType.BuildingType;
                var width = isBuilding ? type.Convert<BuildingTypeClass>().Ref.GetFoundationWidth() : 1;
                var height = isBuilding ? type.Convert<BuildingTypeClass>().Ref.GetFoundationHeight(true) : 1;
                var putLocation = forceCenter ? cell : MapClass.Instance.Pathfinding_Find(ref cell, type.Ref.SpeedType, type.Ref.MovementZone, width, height, isBuilding);
                //(isBuilding ?
                //pHouse.Ref.FindPlaceForBuilding(type.Convert<BuildingTypeClass>(), -1)
                //: MapClass.Instance.Pathfinding_Find(ref cell, type.Ref.SpeedType, type.Ref.MovementZone, width, height, isBuilding));
                var dir = isBuilding ? DirType.North : (DirType)(ScenarioClass.Instance.Random.RandomRanged(0, 7) * 32);
                if (putLocation == CellStruct.Empty)
                {
                    Logger.Log("No place to unlimbo.");
                    continue;
                }
                if (forcePlace)
                    Game.IKnowWhatImDoing++;
                var success = techno.Ref.Base.Put(MapClass.Instance.GetCellAt(CellClass.Cell2Coord(putLocation)).Ref.Base.GetCoords(), dir);
                if (forcePlace)
                    Game.IKnowWhatImDoing--;
                if (!success)
                {
                    Logger.Log("Unlimbo failed at {0}.", putLocation);
                    Game.IKnowWhatImDoing++;
                    success = techno.Ref.Base.Put(MapClass.Instance.GetCellAt(CellClass.Cell2Coord(putLocation)).Ref.Base.GetCoords(), dir);
                    Game.IKnowWhatImDoing--;
                    if (!success)
                    {
                        Logger.Log("Force unlimbo failed.");
                        techno.Ref.BaseAbstract.DTOR();
                        continue;
                    }
                }
                // 记录可移动的单位
                if (!isBuilding)
                    foots.Add(techno.Convert<FootClass>());
            }

            // 必须有可移动单位且有小队类型才能创建小队
            if (foots.Count == 0 || teamType.IsNull)
                return Pointer<TeamClass>.Zero;

            // 创建小队
            var team = YRMemory.Create<TeamClass>(teamType, foots[0].Ref.BaseAbstract.GetOwningHouse(), 0);
            foreach (var techno in foots)
            {
                team.Ref.Addmember(techno, true);
            }
            team.Ref.IsForcedActive = true;
            team.Ref.IsUnderStrength = false;
            return team;
        }
        public static int TimeToFrame(double min, double sec, int frame = 0)
        {
            return ScenarioExt.TimeToFrame(min, sec, frame);
        }
        public static CellStruct GetGeoCenterMapCrd(Pointer<HouseClass> pHouse)
        {
            return CellClass.Coord2Cell(GetGeoCenterCrd(pHouse));
        }
        public static CoordStruct GetGeoCenterCrd(Pointer<HouseClass> pHouse)
        {
            if (pHouse.Ref.Buildings.Count <= 0)
                return CoordStruct.Empty;
            var center = new CoordStruct(0, 0, 0);
            var count = 0;
            foreach (var building in pHouse.Ref.Buildings)
            {
                var type = building.Ref.Base.GetTechnoType();
                var keepAliveAbility = TargetTechnoMutator.GetKeepAliveAbility(type);
                if (keepAliveAbility <= 0)
                    continue;
                center += building.Ref.BaseAbstract.GetCoords() * keepAliveAbility;
                count += keepAliveAbility;
            }
            if (count > 0)
                return center / count;
            return CoordStruct.Empty;
        }
        public static bool IsBuildable(Pointer<TechnoTypeClass> type)
        {
            return type.Ref.OwnerFlags != 0 && type.Ref.TechLevel >= 0 && type.Ref.TechLevel <= 10;
        }
        public static uint GetLevel(Pointer<TechnoTypeClass> type) 
        {
            if (IsHero(type))//最多0-9级，共10级，第11级留给英雄
                return 10;
            return (uint)Math.Min(Math.Max(type.Ref.Cost / 200, 0), 9);
        }
        public static bool IsHero(Pointer<TechnoTypeClass> type)
        {
            return Heros.Contains(type.Ref.BaseAbstractType.ID);
        }
        public static int GetRttiIdx(Pointer<TechnoTypeClass> type)
        {
            var rtti = type.Ref.BaseAbstractType.Base.WhatAmI();
            if (rtti == AbstractType.AircraftType)
                return 0;
            else if (rtti == AbstractType.InfantryType)
                return 1;
            else if (rtti == AbstractType.UnitType)
                return 2;
            else
                return 3;
        }


        public bool IsOnTheirSide(Pointer<HouseClass> owner)
        {
            if (owner.IsNull)
                return false;
            // 中立不站任何一边
            if (owner.Ref.Type.Ref.MultiplayPassive)
                return false;
            // 已经战败的作战方
            if (owner.Ref.Defeated)
                return false;
            // 没有单位的不考虑
            if (!owner.Ref.OwningTechno())
                return false;
            if (Owner.IsNull)
            {
                foreach (var house in HouseClass.Array)
                {
                    // 和玩家双向结盟才算玩家方
                    if (house.Ref.ControlledByHuman() && owner.Ref.IsAlliedWith(house) && house.Ref.IsAlliedWith(owner))
                        return true;
                }
                return false;
            }
            else
            {
                return !Owner.Ref.IsAlliedWith(owner);
            }
        }
        public bool IsOnOurSide(Pointer<HouseClass> owner)
        {
            if (owner.IsNull)
                return false;
            // 中立不站任何一边
            if (owner.Ref.Type.Ref.MultiplayPassive)
                return false;
            // 已经战败的作战方
            if (owner.Ref.Defeated)
                return false;
            // 没有单位的不考虑
            if (!owner.Ref.OwningTechno())
                return false;
            if (Owner.IsNull)
            {
                foreach (var house in HouseClass.Array)
                {
                    // 和其中一个玩家双向敌对就算我方
                    if (house.Ref.ControlledByHuman() && !owner.Ref.IsAlliedWith(house) && !house.Ref.IsAlliedWith(owner))
                        return true;
                }
                return false;
            }
            else
            {
                return Owner.Ref.IsAlliedWith(owner);
            }

        }
        public Pointer<HouseClass> GetRandomHouseOnOurSide()
        {
            var list = new List<Pointer<HouseClass>>();
            foreach (var house in HouseClass.Array)
            {
                if (IsOnOurSide(house))
                    list.Add(house);
            }
            if (list.Count > 0)
                return list[ScenarioClass.Instance.Random.RandomRanged(0, list.Count - 1)];
            else
            {
                Logger.Log("No available house on our side.");
                return Pointer<HouseClass>.Zero;
            }
        }

        // 抽象属性
        public abstract string UIName { get; }
        public abstract string Description { get; }
        public abstract bool IsAvailableInRPG { get; }
        public abstract int Score { get; }

        // 非抽象属性
        public SwizzleablePointer<HouseClass> Owner { get; private set; }

        // CTOR
        public Mutator(Pointer<HouseClass> owner)
        {
            Owner = new SwizzleablePointer<HouseClass>(owner.IsNotNull ? owner : Pointer<HouseClass>.Zero);
        }

        // 虚函数
        public virtual void Init(bool isInitial = true) 
        {
            ToBeAddedArray.Add(this);
            // 文字提示
            string hint = "激活突变因子:" + UIName;
            MessageListClass.Instance.PrintMessage(hint, (ColorSchemeIndex)(HouseClass.Player.Ref.ColorSchemeIndex));
            // 在超武栏显示
            string swName = this.GetType().Name + "Icon";
            var swType = SuperWeaponTypeClass.ABSTRACTTYPE_ARRAY.Find(swName);
            if (swType.IsNull)
            {
                Logger.Log(swName + " don't exist!");
            }
            else
            {
                foreach (var house in HouseClass.Array)
                {
                    if (house.Ref.ControlledByHuman())
                    {
                        var sw = house.Ref.FindSuperWeapon(swType);
                        sw.Ref.Grant(true, false, false);
                        if (house == HouseClass.Player)
                        {
                            if (SidebarClass.Instance.AddCameo(AbstractType.Special, swType.Ref.ArrayIndex))
                                SidebarClass.Instance.RepaintSidebar();
                        }
                    }
                }
            }
        }
        public virtual void Uninit() 
        {
            ToBeRemovedArray.Add(this);
            // 文字提示
            string hint = "突变因子失效:" + UIName;
            MessageListClass.Instance.PrintMessage(hint, (ColorSchemeIndex)(HouseClass.Player.Ref.ColorSchemeIndex));
            // 在超武栏显示
            string swName = this.GetType().Name + "Icon";
            var swType = SuperWeaponTypeClass.ABSTRACTTYPE_ARRAY.Find(swName);
            if (swType.IsNull)
            {
                Logger.Log(swName + " don't exist!");
            }
            else
            {
                foreach (var house in HouseClass.Array)
                {
                    if (house.Ref.ControlledByHuman())
                    {
                        var sw = house.Ref.FindSuperWeapon(swType);
                        sw.Ref.Lose();
                        if (house == HouseClass.Player)
                        {
                            if (DisplayClass.Instance.CurrentSWTypeIndex == swType.Ref.ArrayIndex)
                                DisplayClass.Instance.CurrentSWTypeIndex = -1;
                            SidebarClass.Instance.RepaintSidebar();
                            house.Ref.RecheckTechTree = true;
                        }
                    }
                }
            }
        }
        public virtual bool Update()
        {
            // 具有所有者的因子，在所有者战败后失效
            if (!Owner.IsNull && Owner.Ref.Defeated)
            {
                Uninit();
                return false;
            }

            return IsActive();
        }
        public bool IsActive()
        {
            return Array.Contains(this) && !ToBeRemovedArray.Contains(this);
        }

        [Serializable]
        public class DelayedGlobalEvent : EventDecorator
        {
            /// <summary>
            /// 延迟执行的回调信息
            /// </summary>
            [Serializable]
            private class DelayedAction
            {
                public int DelayFrames { get; set; }
                public int ElapsedFrames { get; set; }
                public System.Action Callback { get; set; }

                public DelayedAction(int delayFrames, System.Action callback)
                {
                    DelayFrames = delayFrames;
                    ElapsedFrames = 0;
                    Callback = callback;
                }

                public bool Update()
                {
                    ElapsedFrames++;
                    if (ElapsedFrames >= DelayFrames)
                    {
                        Callback?.Invoke();
                        return true; // 已完成
                    }
                    return false; // 继续等待
                }
            }

            private List<DelayedAction> pendingActions = new List<DelayedAction>();

            public DelayedGlobalEvent() : base()
            {
            }

            /// <summary>
            /// 添加一个延迟执行的动作
            /// </summary>
            /// <param name="delayFrames">延迟帧数</param>
            /// <param name="callback">执行的回调函数</param>
            public void AddDelayedAction(int delayFrames, System.Action callback)
            {
                if (delayFrames < 0 || callback == null)
                    return;

                pendingActions.Add(new DelayedAction(delayFrames, callback));
            }

            public override void OnUpdate()
            {
                // 更新所有待执行的动作，已完成的移除
                List<DelayedAction> completedActions = new List<DelayedAction>();
                foreach (var action in pendingActions)
                {
                    if (action.Update())
                        completedActions.Add(action);
                }

                // 移除已完成的动作
                foreach (var action in completedActions)
                    pendingActions.Remove(action);

                // 如果所有动作都完成了，移除这个Decorator
                if (pendingActions.Count == 0 && Decorative != null)
                {
                    Decorative.Remove(this);
                }
            }
        }

        [Serializable]
        protected abstract class ShotWithPreImpactAnim
        {
            protected abstract SwizzleablePointer<AnimTypeClass> PreImpactAnimType { get; }
            protected abstract SwizzleablePointer<SuperWeaponTypeClass> ShotSWType { get; }
            protected TargetCellMutator MyMutator;
            protected int ImpactDelay;
            protected int timer = 0;
            protected CoordStruct Location;
            public ShotWithPreImpactAnim(int impactDelay, CoordStruct location, TargetCellMutator myMutator)
            {
                ImpactDelay = impactDelay;
                Location = location;
                MyMutator = myMutator;
            }

            public bool Update()
            {
                // 创建提示动画
                if (timer == 0 && PreImpactAnimType.Pointer.IsNotNull)
                    YRMemory.Create<AnimClass>(PreImpactAnimType.Pointer, Location);

                // 发射shot
                if (timer >= ImpactDelay)
                {
                    List<Pointer<HouseClass>> ourSideHouses = new List<Pointer<HouseClass>>();
                    foreach (var house in HouseClass.Array)
                    {
                        if (MyMutator.IsOnOurSide(house))
                            ourSideHouses.Add(house);
                    }
                    if (ourSideHouses.Count > 0)
                    {
                        var max = ourSideHouses.Count;
                        var house = ourSideHouses.ElementAt(ScenarioClass.Instance.Random.RandomRanged(0, max - 1));
                        SuperWeaponTypeExt.LaunchFakeSW(ShotSWType, house, CellClass.Coord2Cell(Location));
                    }
                }

                // 返回是否生命周期结束
                timer++;
                return timer <= ImpactDelay;
            }
        }
    }

    // 需要选择单位作为目标的因子
    [Serializable]
    public abstract class TargetTechnoMutator : Mutator
    {
        protected TargetTechnoMutator(Pointer<HouseClass> owner) : base(owner) { }
        protected abstract bool IsTechnoValid(Pointer<TechnoClass> techno);
        public static int GetKeepAliveAbility(Pointer<TechnoClass> techno)
        {
            var type = techno.Ref.GetTechnoType();
            return GetKeepAliveAbility(type);
        }
        public static int GetKeepAliveAbility(Pointer<TechnoTypeClass> type)
        {
            if (type.Ref.BaseAbstractType.Base.WhatAmI() == AbstractType.BuildingType)
            {
                var buildingType = type.Convert<BuildingTypeClass>();
                if (buildingType.Ref.ConstructionYard)
                    return 4;

                if (buildingType.Ref.Factory != AbstractType.None)
                    return 3;
                // TODO : Nullable
                return type.Ref.Base.Insignificant || type.Ref.DontScore ? 0 : 2;
            }
            var typeExt = TechnoTypeExt.ExtMap.Find(type);
            if (typeExt.KeepAlive)
                return 2;
            return type.Ref.Base.Insignificant || type.Ref.DontScore ? 0 : 1;
        }
    }

    // 需要选择格子作为目标的因子
    [Serializable]
    public abstract class TargetCellMutator : Mutator
    {
        // 数值修改：
        // 安全区中心为每个矿柱
        // 安全区范围20格
        // Mutator
        public override bool Update()
        {
            // 检查因子是否可用
            if (!base.Update())
                return false;

            // 更新shots
            List<ShotWithPreImpactAnim> expiredShots = new List<ShotWithPreImpactAnim>();
            foreach (var shot in ActivatedShots)
            {
                if (!shot.Update())
                    expiredShots.Add(shot);
            }
            foreach (var shot in expiredShots)
                ActivatedShots.Remove(shot);

            return true;
        }
        public override void Uninit()
        {
            base.Uninit();
            ActivatedShots.Clear();
        }
        
        // TargetCellMutator
        protected TargetCellMutator(Pointer<HouseClass> owner) : base(owner) { }
        protected static List<SwizzleablePointer<CellClass>> TiberTreeCells => MutatorCacheManager.Instance.TiberTreeCells;
        public static List<SwizzleablePointer<CellClass>> UsableCells => MutatorCacheManager.Instance.UsableCells;
        public static List<SwizzleablePointer<CellClass>> NonSafeZoneCells => MutatorCacheManager.Instance.NonSafeZoneCells;
        public static List<SwizzleablePointer<CellClass>> EdgeCells => MutatorCacheManager.Instance.EdgeCells;
        public static List<SwizzleablePointer<CellClass>> LeftEdgeCells => MutatorCacheManager.Instance.LeftEdgeCells;
        public static List<SwizzleablePointer<CellClass>> UpEdgeCells => MutatorCacheManager.Instance.UpEdgeCells;
        public static List<SwizzleablePointer<CellClass>> RightEdgeCells => MutatorCacheManager.Instance.RightEdgeCells;
        public static List<SwizzleablePointer<CellClass>> DownEdgeCells => MutatorCacheManager.Instance.DownEdgeCells;
        private static double SafeZoneRange = 20.0;
        private static void RecheckCellCaches()
        {
            if (TiberTreeCells.Count == 0) // 只执行一次，因为假设矿柱不会变少，即使在地图外也不会少
            {
                // 找到所有矿柱
                foreach (var terrain in TerrainClass.Array)
                {
                    var type = terrain.Ref.Type;
                    if (!type.Ref.SpawnsTiberium)
                        continue;

                    var cell = MapClass.Instance.GetCellAt(terrain.Ref.Base.Base.GetCoords());
                    TiberTreeCells.Add(new SwizzleablePointer<CellClass>(cell));
                }
            }
            // 重绘安全区和边缘区
            NonSafeZoneCells.Clear();
            EdgeCells.Clear();
            var pMap = MapClass.Instance;
            pMap.CellIteratorReset();
            for (var pCell = pMap.CellIteratorNext(); pCell.IsNotNull; pCell = pMap.CellIteratorNext())
            {
                if (MapClass.Instance.IsWithinUsableArea(pCell, true))
                    UsableCells.Add(new SwizzleablePointer<CellClass>(pCell));
                if (IsOutOfSafeZone(pCell))
                    NonSafeZoneCells.Add(new SwizzleablePointer<CellClass>(pCell));
                if (MapClass.Instance.IsOnMapEdge(pCell.Ref.MapCoords))
                {
                    EdgeCells.Add(new SwizzleablePointer<CellClass>(pCell));
                    if (MapClass.Instance.IsOnMapEdge(pCell.Ref.MapCoords,InGameEdge.Left))
                        LeftEdgeCells.Add(new SwizzleablePointer<CellClass>(pCell));
                    else if (MapClass.Instance.IsOnMapEdge(pCell.Ref.MapCoords, InGameEdge.Up))
                        UpEdgeCells.Add(new SwizzleablePointer<CellClass>(pCell));
                    else if (MapClass.Instance.IsOnMapEdge(pCell.Ref.MapCoords, InGameEdge.Right))
                        RightEdgeCells.Add(new SwizzleablePointer<CellClass>(pCell));
                    else
                        DownEdgeCells.Add(new SwizzleablePointer<CellClass>(pCell));
                }
            }
        }
        public static void OnUsableAreaChange()
        {
            RecheckCellCaches();
        }
        protected List<ShotWithPreImpactAnim> ActivatedShots = new List<ShotWithPreImpactAnim>();

        public static bool IsInSafeZone(Pointer<TechnoClass> techno)
        {
            var cell = MapClass.Instance.GetCellAt(techno.Ref.BaseAbstract.GetCoords());
            return IsInSafeZone(cell);
        }
        public static bool IsInSafeZone(Pointer<AbstractClass> abs)
        {
            var cell = MapClass.Instance.GetCellAt(abs.Ref.GetCoords());
            return IsInSafeZone(cell);
        }
        public static bool IsInSafeZone(CellStruct mapcrd)
        {
            var cell = MapClass.Instance.GetCellAt(CellClass.Cell2Coord(mapcrd));
            return IsInSafeZone(cell);
        }
        public static bool IsInSafeZone(Pointer<CellClass> cell)
        {
            foreach (var tiberTreeCell in TiberTreeCells)
            {
                if (cell.Ref.MapCoords.DistanceFrom(tiberTreeCell.Ref.MapCoords) <= SafeZoneRange)
                    return true;
            }
            return false;
        }
        public static bool IsOutOfSafeZone(Pointer<TechnoClass> techno)
        {
            var cell = MapClass.Instance.GetCellAt(techno.Ref.BaseAbstract.GetCoords());
            return MapClass.Instance.IsWithinUsableArea(cell, true) && !IsInSafeZone(cell);
        }
        public static bool IsOutOfSafeZone(Pointer<AbstractClass> abs)
        {
            var cell = MapClass.Instance.GetCellAt(abs.Ref.GetCoords());
            return MapClass.Instance.IsWithinUsableArea(cell, true) && !IsInSafeZone(cell);
        }
        public static bool IsOutOfSafeZone(CellStruct mapcrd)
        {
            var cell = MapClass.Instance.GetCellAt(CellClass.Cell2Coord(mapcrd));
            return MapClass.Instance.IsWithinUsableArea(cell, true) && !IsInSafeZone(cell);
        }
        public static bool IsOutOfSafeZone(Pointer<CellClass> cell)
        {
            return MapClass.Instance.IsWithinUsableArea(cell, true) && !IsInSafeZone(cell);
        }
        public static Pointer<CellClass> SelectRandomCellOutOfSafeZone()
        {
            var max = NonSafeZoneCells.Count;
            if (max <= 0)
            {
                Logger.Log("No safe zone cell!");
                return Pointer<CellClass>.Zero;
            }
            var cell = NonSafeZoneCells[ScenarioClass.Instance.Random.RandomRanged(0, max - 1)];
            return cell.Pointer;
        }
        public static Pointer<CellClass> SelectRandomCellOnTheEdge(InGameEdge edge = InGameEdge.None)
        {
            List<SwizzleablePointer<CellClass>> list;
            switch (edge)
            {
                case InGameEdge.Left:
                    list = LeftEdgeCells; break;
                case InGameEdge.Up:
                    list = UpEdgeCells; break;
                case InGameEdge.Right:
                    list = RightEdgeCells; break;
                case InGameEdge.Down: 
                    list = DownEdgeCells; break;
                default:
                    list = EdgeCells; break;
            }
            return list[ScenarioClass.Instance.Random.RandomRanged(0, list.Count - 1)].Pointer;
        }

    }

    // 贴Buff的因子
    [Serializable]
    public abstract class BuffMutator : Mutator
    {
        protected BuffMutator(Pointer<HouseClass> owner) : base(owner) { }
        public List<TechnoTypeClass> blackList = new List<TechnoTypeClass>();
        protected abstract bool IsBuffEnemy { get; }
        protected bool IsTechnoOwnerValid(Pointer<HouseClass> technoOwner)
        {
            return IsBuffEnemy ? IsOnTheirSide(technoOwner) : IsOnOurSide(technoOwner);
        }
        public virtual void OnTechnoCTOR(Pointer<TechnoClass> techno) { }
        public virtual void OnTechnoChangeOwner(Pointer<TechnoClass> techno, Pointer<HouseClass> toHouse) { }
        protected abstract void BuffTechno(Pointer<TechnoClass> techno);
        protected abstract void UnbuffTechno(Pointer<TechnoClass> techno);
    }

    [Serializable]
    public class MutatorTechnoScriptable : TechnoScriptable
    {
        public Mutator myMutator = null;
        public MutatorTechnoScriptable(TechnoExt owner) : base(owner) { }
    }
    [Serializable]
    public class MutatorEventDecorator : EventDecorator
    {
        public Mutator myMutator = null;
        public MutatorEventDecorator(Mutator mutator) 
        {
            myMutator = mutator;
        }
    }
}
