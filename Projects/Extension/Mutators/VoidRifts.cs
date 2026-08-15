using DynamicPatcher;
using Extension.Decorators;
using Extension.Ext;
using Extension.Utilities;
using PatcherYRpp;
using Scripts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Extension.Mutators.Magnificent;

namespace Extension.Mutators
{
    [Serializable]
    public class VoidRifts : TargetCellMutator
    {
        // 数值修改：
        // 裂隙间隔20降低到10，因为红警2地图太小很容易塞不下
        public override string UIName => "虚空裂隙";
        public override string Description => "虚空裂隙周期性地出现在随机位置，并会不断地生成敌方单位，直至其被摧毁。";
        public override bool IsAvailableInRPG => false;
        public override int Score => 10;
        private static Pointer<TechnoTypeClass> Rift => TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find("VOIDRIFT");
        private static Pointer<AnimTypeClass> RiftSpawnAnim => AnimTypeClass.ABSTRACTTYPE_ARRAY.Find("CHRONOFD");
        private static string SpawnSound => "EVA_UnitsInCombat";
        private static int RiftGapRange = 10;
        private int counter = 0;
        // 检查位置是否可以放置虚空裂隙是件很耗时的工作。
        // 为了性能考虑，我不能把所有工作放在刷新裂隙的那一帧。
        // 我的策略是：
        // 每轮刷新之后，记录非安全区
        // 在下轮开始前，平均到每一帧，检查一部分格子是否有效，若有效则加入列表缓存
        // 当刷新时，仅3个条件：
        // 1.格子是否在地图内；
        // 2.格子是否在本轮已经刷新的裂隙周围；
        // 3.是否格子上有建筑。
        // 如果不符合，则从缓存中移除这个格子。
        // 尝试至多64次或缓存用尽。
        private bool IsProcessingCache = false;
        private bool IsUsingRandomCache = false;
        private bool IsFinished = false;
        private int CurrentCheckingIndex = 0;
        private int CellToCheckPerFrameCount = 0;
        private List<SwizzleablePointer<CellClass>> CurrentRoundAvailableCells = new List<SwizzleablePointer<CellClass>>();
        private List<SwizzleablePointer<CellClass>> CurrentRoundValidCells = new List<SwizzleablePointer<CellClass>>();
        private List<Pointer<CellClass>> SelectedCells = new List<Pointer<CellClass>>();
        public VoidRifts(Pointer<HouseClass> owner) : base(owner) { }
        public override bool Update()
        {
            if (!base.Update())
                return false;

            // 0分50时开始检查缓存
            if (Game.CurrentFrame >= TimeToFrame(0, 50) && !IsProcessingCache && !IsFinished)
                ResetCached();

            // 处理缓存
            if (IsProcessingCache && !IsFinished)
                ProcessCacheMission();

            // 2分钟20时开始生效
            if (Game.CurrentFrame >= TimeToFrame(2, 20))
            {
                if (counter == 0)
                {
                    // 找位置
                    var count = Game.CurrentFrame >= TimeToFrame(8, 20) ? 4 : 2;
                    for (var i = 0; i < 64 && CurrentRoundValidCells.Count > 0 && SelectedCells.Count < count; i++)
                        TrySelectCellToSpawn();
                    // 刷裂隙
                    VoxClass.Play(SpawnSound);
                    var list = new List<Pointer<TechnoTypeClass>>();
                    list.Add(Rift);
                    foreach (var cell in SelectedCells)
                    {
                        var house = GetRandomHouseOnOurSide();
                        var rift = TechnoExt.CreateObjectWithDecorator<VoidRiftScript>(Rift.Convert<ObjectTypeClass>(), house, VoidRiftScript.ID).Convert<TechnoClass>();
                        ++Game.IKnowWhatImDoing;
                        rift.Ref.Base.Put(cell.Ref.Base.GetCoords(), DirType.North);
                        --Game.IKnowWhatImDoing;
                        YRMemory.Create<AnimClass>(RiftSpawnAnim, cell.Ref.Base.GetCoords());
                    }
                    // 重置缓存
                    ResetCached();
                }

                counter++;
                counter %= TimeToFrame(0, 90);
            }
            return true;
        }
        private bool TrySelectCellToSpawn()
        {
            if (CurrentRoundValidCells.Count <= 0)
                return false;

            var idx = ScenarioClass.Instance.Random.RandomRanged(0, CurrentRoundValidCells.Count - 1);
            var cell = CurrentRoundValidCells[idx];

            if (!MapClass.Instance.IsWithinUsableArea(cell, true))
            {
                CurrentRoundValidCells.RemoveAt(idx);
                return false;
            }

            if (cell.Ref.GetBuilding().IsNotNull)
            {
                CurrentRoundValidCells.RemoveAt(idx);
                return false;
            }

            foreach (var selected in SelectedCells)
            {
                if (cell.Ref.Base.DistanceFrom(selected.Convert<AbstractClass>()) <= RiftGapRange * 256)
                {
                    CurrentRoundValidCells.RemoveAt(idx);
                    return false;
                }
            }
            SelectedCells.Add(cell);
            return true;
        }
        private void ProcessCacheMission()
        {
            if (IsUsingRandomCache)
            {
                for (var i = 0; i < CellToCheckPerFrameCount; i++)
                {
                    var cell = ScenarioClass.GetRandomInList(CurrentRoundAvailableCells);
                    if (IsCellValid(cell.Pointer))
                    {
                        CurrentRoundValidCells.Add(cell);
                    }
                }
                IsFinished = false;
            }
            else
            {
                for (var i = 0; i < CellToCheckPerFrameCount && CurrentCheckingIndex < CurrentRoundAvailableCells.Count; CurrentCheckingIndex++, i++)
                {
                    var cell = CurrentRoundAvailableCells[CurrentCheckingIndex];
                    if (IsCellValid(cell.Pointer))
                    {
                        CurrentRoundValidCells.Add(cell);
                    }
                }
                IsFinished = CurrentCheckingIndex >= CurrentRoundAvailableCells.Count;
            }
        }
        private void ResetCached()
        {
            CurrentCheckingIndex = 0;
            CurrentRoundAvailableCells.Clear();
            CurrentRoundValidCells.Clear();
            SelectedCells.Clear();
            CurrentRoundAvailableCells = NonSafeZoneCells.ToList();
            // 最多90秒-5帧，最少1帧
            var timeToNextWave = Math.Max(1, (Game.CurrentFrame - counter - 5 + TimeToFrame(0, 90) - TimeToFrame(0, 50)) % TimeToFrame(0, 90));
            var perFrameCount = CurrentRoundAvailableCells.Count / timeToNextWave + 1;
            // 1帧最多检查1格。每帧工作量超过1，那就使用随机策略
            IsUsingRandomCache = perFrameCount > 1;
            CellToCheckPerFrameCount = Math.Min(1, perFrameCount);
            Logger.Log("CellToCheckPerFrameCount {0}", CellToCheckPerFrameCount);
            IsProcessingCache = CurrentCheckingIndex < CurrentRoundAvailableCells.Count;
            IsFinished = CurrentCheckingIndex >= CurrentRoundAvailableCells.Count;
        }
        private bool IsCellValid(Pointer<CellClass> cell)
        {
            if (cell.IsNull)
                return false;

            // 必须在安全区外
            if (!TargetCellMutator.IsOutOfSafeZone(cell))
                return false;

            // 必须不在水里
            var landType = cell.Ref.LandType;
            if (landType == LandType.Water || landType == LandType.Beach)
                return false;

            // 防止建筑重叠
            if (cell.Ref.GetBuilding().IsNotNull)
                return false;

            // 检查周围的建筑
            // 不能有虚空裂隙，也不能有超过3个友方单位
            var centerMapCrd = cell.Ref.MapCoords;
            var cellEnum = new CellSpreadEnumerator((uint)RiftGapRange);
            var alliedTechnoList = new List<Pointer<TechnoClass>>();
            foreach (var offset in cellEnum)
            {
                var curMapCrd = centerMapCrd + offset;
                var curCell = MapClass.Instance.GetCellAt(curMapCrd);
                for (var obj = curCell.Ref.FirstObject; obj.IsNotNull; obj = obj.Ref.NextObject)
                {
                    var techno = obj.Convert<TechnoClass>();
                    if (techno.IsNotNull && techno.Ref.GetTechnoType() == Rift)
                        return false;
                    if (IsOnOurSide(obj.Ref.Base.GetOwningHouse()) && !alliedTechnoList.Contains(techno))
                    {
                        alliedTechnoList.Add(techno);
                        if (alliedTechnoList.Count > 3)
                            return false;
                    }
                }
            }

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
                    if (TargetTechnoMutator.GetKeepAliveAbility(building.Convert<TechnoClass>()) == 0)
                        continue;

                    // 目标必须联通
                    if (MapClass.IsInSameZone(cell.Ref.MapCoords, buildingCell.Ref.MapCoords))
                        return true;
                }
            }

            return false;
        }

        [Serializable]
        public class VoidRiftScript : EventDecorator
        {
            // 数值修改：
            // 小波次价值改为350左右。考虑到步兵没人权的暂时先这样给数值。
            // 大波次价值改为900，1800，3150。
            // Decorator
            public static DecoratorId ID => new DecoratorId((int)TechnoDecoratorIDs.UniqueDecorator);

            // VoidRiftScript
            private static Pointer<AnimTypeClass> WaveSpawnAnim => AnimTypeClass.ABSTRACTTYPE_ARRAY.Find("CHRONOFD");
            private static List<string> Tier1Names = new List<string>
            {
                "GGI",
                "E2",
                "FLAMER",
                "INIT",
                "HARP",
                "KAOS",
                "COVE",
                "KNIGHT",
                "MTNK",
                "HTNK",
                "JTNK",
                "SCAR",
                "AMC",
                "YTNK",
                "CYCL"
            };
            private static List<string> Tier2Names = new List<string>
            {
                "STALKER",
                "MARA",
                "SHADOW",
                "JUMPJET",
                "COMA",
                "ENFO",
                "SHOCK",
                "GYRO",
                "BOREK",
                "SCHP",
                "RAIL",
                "DRACO",
                "RHAD"
            };
            private static List<string> Tier3Names = new List<string>
            {
                "BASI",
                "DEVO",
                "CRYO",
                "THOR",
                "ABRM",
                "APOC",
                "EMPR",
                "BANE",
                "WISP",
                "GHTNK",
                "COND",
                "KRUKOV"
            };
            private int MiniWaveCounter = 1; // 初始冷却
            private bool LastMiniWaveIsFoehn = false;
            private int BigWaveCounter = 1; // 初始冷却
            private bool IsFirstBigWave = true;
            private TimerStruct AlertTimer = new TimerStruct();

            public override void OnUpdate()
            {
                var pThis = (Decorative as TechnoExt).OwnerObject;
                // 雷达事件
                if (!AlertTimer.InProgress())
                {
                    AlertTimer.Start(Mutator.TimeToFrame(0, 10));
                    RadarEventClass.Create(RadarEventType.EnemySensed, pThis.Ref.BaseAbstract.GetMapCrd());
                }

                // 更新小波次
                if (MiniWaveCounter == 0)
                {
                    var types = GetTypesMini(out LastMiniWaveIsFoehn);
                    SpawnWave(types);
                }
                MiniWaveCounter++;
                MiniWaveCounter %= GetMiniWaveDelay();

                // 更新大波次
                if (BigWaveCounter == 0)
                {
                    var types = GetTypesBig();
                    SpawnWave(types);
                }
                BigWaveCounter++;
                BigWaveCounter %= GetBigWaveDelay();
            }
            private void SpawnWave(List<Pointer<TechnoTypeClass>> list)
            {
                var ext = Decorative as TechnoExt;
                var pThis = ext.OwnerObject;
                var crd = pThis.Ref.BaseAbstract.GetCoords();
                var owner = pThis.Ref.BaseAbstract.GetOwningHouse();
                YRMemory.Create<AnimClass>(WaveSpawnAnim, crd);
                Mutator.CreateTeamAtCrd(Mutator.MutatorSpawnTeam, list, owner, crd);
            }
            private List<Pointer<TechnoTypeClass>> GetTypesMini(out bool isFoehn)
            {
                var list = new List<Pointer<TechnoTypeClass>>();
                var rand = ScenarioClass.Instance.Random.RandomRanged(0, 3);
                isFoehn = false;
                if (rand == 0)
                {
                    var type1 = TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find("E1");
                    var type2 = TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find("GGI");
                    list.Add(type1);
                    list.Add(type2);
                }
                else if (rand == 1)
                {
                    var type1 = TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find("E2");
                    var type2 = TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find("FLAKT");
                    list.Add(type1);
                    list.Add(type2);
                    list.Add(type1);
                    list.Add(type2);
                }
                else if (rand == 2)
                {
                    var type1 = TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find("INIT");
                    var type2 = TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find("HARP");
                    list.Add(type1);
                    list.Add(type2);
                }
                else
                {
                    var type1 = TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find("KNIGHT");
                    var type2 = TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find("COVE");
                    list.Add(type1);
                    list.Add(type2);
                    isFoehn = true;
                }
                return list;
            }
            private List<Pointer<TechnoTypeClass>> GetTypesBig()
            {
                var list = new List<Pointer<TechnoTypeClass>>();
                var frame = Game.CurrentFrame;
                // 决定总预算
                int budget = 0;
                if (frame <= Mutator.TimeToFrame(10, 0))
                    budget = ScenarioExt.ExchangeCurrency(300);
                else if (frame <= Mutator.TimeToFrame(8, 20))
                    budget = ScenarioExt.ExchangeCurrency(600);
                else
                    budget = ScenarioExt.ExchangeCurrency(1050);
                // 决定列表
                var nameList = new List<string>();
                nameList.AddRange(Tier1Names);
                if (frame > Mutator.TimeToFrame(10, 0))
                    nameList.AddRange(Tier2Names);
                if (frame > Mutator.TimeToFrame(16, 40))
                    nameList.AddRange(Tier3Names);
                // 从列表中抽取单位，直到用尽预算
                for (; budget > 0;)
                {
                    var name = ScenarioClass.GetRandomInList(nameList);
                    var type = TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find(name);
                    list.Add(type);
                    budget -= type.Ref.Cost;
                }
                return list;
            }
            private int GetMiniWaveDelay()
            {
                return LastMiniWaveIsFoehn ? Mutator.TimeToFrame(0, 24) : Mutator.TimeToFrame(0, 12);
            }
            private int GetBigWaveDelay()
            {
                return IsFirstBigWave ? Mutator.TimeToFrame(0, 30) : Mutator.TimeToFrame(0, 90);
            }
        }
    }
}
