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
using static Extension.Mutators.MissileCommand;

namespace Extension.Mutators
{
    [Serializable]
    public class VoidReanimators : BuffMutator
    {
        [Serializable]
        private class Corpse
        {
            public SwizzleablePointer<TechnoTypeClass> type;
            public SwizzleablePointer<HouseClass> owner;
            public CellStruct mapCrd;

            public Corpse()
            {
                type = new SwizzleablePointer<TechnoTypeClass>(Pointer<TechnoTypeClass>.Zero);
                owner = new SwizzleablePointer<HouseClass>(Pointer<HouseClass>.Zero);
                mapCrd = CellStruct.Empty;
            }
            public Corpse(SwizzleablePointer<TechnoTypeClass> type,
                SwizzleablePointer<HouseClass> owner,
                CellStruct mapCrd)
            {
                this.type = type;
                this.owner = owner;
                this.mapCrd = mapCrd;
            }
        }
        private static Pointer<TechnoTypeClass> VoidReanimator => TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find("VOIDREANIMATOR");
        private static string SpawnSound => "EVA_UnitsInCombat";
        public override string UIName => "虚空重生者";
        public override string Description => "虚空重生者游荡在战场上，不断地复活你的敌人。";
        public override bool IsAvailableInRPG => false;
        public override int Score => 5;

        protected override bool IsBuffEnemy => false;

        private int CheckCounter = TimeToFrame(0, 10);
        private bool LastCheckResult = false;
        private int SpawnCounter = TimeToFrame(0, 62);
        private int ClearCounter = 0;
        private Dictionary<int, (Corpse corpse, bool isReanimating)> Corpses
            = new Dictionary<int, (Corpse corpse, bool isReanimating)>();
        private List<int> LastRoundReanimatingCorpseIDs = new List<int>();
        public VoidReanimators(Pointer<HouseClass> owner) : base(owner) { }
        public override bool Update()
        {
            if (!base.Update())
                return false;

            // 10秒后开始生效
            if (Game.CurrentFrame >= TimeToFrame(0, 10)
                // 62秒刷一个红衣
                && SpawnCounter++ >= TimeToFrame(0, 62))
            {
                // 每10秒检查一次
                if (CheckCounter++ % TimeToFrame(0, 10) == 0)
                    LastCheckResult = CheckCanSpawn();

                if (LastCheckResult)
                {
                    SpawnCounter = 0;
                    VoxClass.Play(SpawnSound);
                    List<Pointer<BuildingClass>> availableBuildings = new List<Pointer<BuildingClass>>();
                    foreach (var house in HouseClass.Array)
                    {
                        if (!IsOnOurSide(house))
                            continue;

                        foreach (var building in house.Ref.Buildings)
                        {
                            if (CanSpawnReanimator(building.Convert<TechnoClass>()))
                                availableBuildings.Add(building);
                        }
                    }
                    if (availableBuildings.Count > 0)
                    {
                        var spawner = availableBuildings[ScenarioClass.Instance.Random.RandomRanged(0, availableBuildings.Count - 1)];
                        var list = new List<Pointer<TechnoClass>>();
                        var reanimator = TechnoExt.CreateObjectWithDecorator<VoidReanimatorScript>
                            (VoidReanimator.Convert<ObjectTypeClass>(), spawner.Ref.Base.BaseAbstract.GetOwningHouse(), VoidReanimatorScript.ID, this).Convert<TechnoClass>();
                        list.Add(reanimator);
                        CreateTeamAtCrd(Pointer<TeamTypeClass>.Zero, list, spawner.Ref.Base.BaseAbstract.GetCoords());
                    }
                }
            }

            // 每10秒清理一次
            if (ClearCounter++ % TimeToFrame(0, 10) == 0)
            {
                // 如果有尸体经过10秒仍未被清除则标记为没有正在复活
                foreach (var idx in LastRoundReanimatingCorpseIDs)
                {
                    if (Corpses.TryGetValue(idx, out var corpse))
                        Corpses[idx] = (Corpses[idx].corpse, false);
                }
                // 记录本轮正在复活的
                LastRoundReanimatingCorpseIDs.Clear();
                foreach (var corpse in Corpses)
                {
                    if (corpse.Value.isReanimating)
                        LastRoundReanimatingCorpseIDs.Add(corpse.Key);
                }
            }
            return true;
        }
        public override void Init(bool isInitial = true)
        {
            base.Init();
            foreach (var techno in TechnoClass.Array)
            {
                if (IsTechnoValid(techno))
                    BuffTechno(techno);
            }
        }
        public override void Uninit()
        {
            foreach (var techno in TechnoClass.Array)
            {
                if (IsTechnoValid(techno))
                    UnbuffTechno(techno);
            }
            base.Uninit();
        }


        private bool CanSpawnReanimator(Pointer<TechnoClass> techno)
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
                    if (TargetTechnoMutator.GetKeepAliveAbility(building.Convert<TechnoClass>()) == 0)
                        continue;

                    // 目标必须联通
                    if (MapClass.Instance.IsInSameZone(ref technoCell.Ref.MapCoords, ref buildingCell.Ref.MapCoords, MovementZone.Infantry, false, false, false))
                        return true;
                }
            }

            return false;
        }
        private bool CheckCanSpawn()
        {
            // 检查是否有超过4个红衣
            int reanimatorCount = 0;
            foreach (var infantry in InfantryClass.Array)
            {
                if (infantry.Ref.Type.Convert<TechnoTypeClass>() == VoidReanimator // 是红衣
                    && IsOnOurSide(infantry.Ref.BaseAbstract.GetOwningHouse())) // 是我家的
                {
                    reanimatorCount++;
                }
            }
            if (reanimatorCount >= 4)
                return false;
            // 检查是否有尸体
            if (Corpses.Count <= 0)
                return false;
            return true;
        }

        protected override void BuffTechno(Pointer<TechnoClass> techno)
        {
            var ext = TechnoExt.ExtMap.Find(techno);
            if (ext.Get(LeaveCorpseBuff.ID) == null)
                ext.CreateDecorator<LeaveCorpseBuff>(LeaveCorpseBuff.ID, "LeaveCorpseBuff", this);
        }

        protected override void UnbuffTechno(Pointer<TechnoClass> techno)
        {
            var ext = TechnoExt.ExtMap.Find(techno);
            ext.Remove(LeaveCorpseBuff.ID);
        }
        public override void OnTechnoCTOR(Pointer<TechnoClass> techno)
        {
            if (IsTechnoValid(techno))
                BuffTechno(techno);
        }
        public override void OnTechnoChangeOwner(Pointer<TechnoClass> techno, Pointer<HouseClass> toHouse)
        {
            if (!IsTechnoOwnerValid(toHouse))
                UnbuffTechno(techno);
            else
                BuffTechno(techno);
        }
        private bool IsTechnoValid(Pointer<TechnoClass> techno)
        {
            if (!IsTechnoOwnerValid(techno.Ref.BaseAbstract.GetOwningHouse()))
                return false;

            // 建筑不产生尸体
            if ((techno.Ref.BaseAbstract.AbstractFlags & AbstractFlags.Foot) == AbstractFlags.None)
                return false;
            return true;
        }

        [Serializable]
        public class VoidReanimatorScript : MutatorEventDecorator
        {
            private enum ReanimatorScriptStatus
            {
                Idle = 0,
                GoToCorpse = 1,
                ReanimateCorpse = 2,
            }

            // Decorator
            public static new DecoratorId ID => new DecoratorId((int)TechnoDecoratorIDs.UniqueDecorator);

            // MutatorEventDecorator
            public VoidReanimatorScript(Mutator mutator) : base(mutator) { }

            // VoidReanimatorScript
            private static Pointer<AnimTypeClass> ReanimateAnim => AnimTypeClass.ABSTRACTTYPE_ARRAY.Find("DIG");
            private ReanimatorScriptStatus Status;
            private int FindCounter = 0;
            private int CheckMissionCounter = 0;
            private int ReanimationCounter = -1;
            private int TargetCorpseKey = -1;

            public override void OnUpdate()
            {
                var pThis = (Decorative as TechnoExt).OwnerObject;
                var mutator = myMutator as VoidReanimators;
                switch (Status)
                {
                    case ReanimatorScriptStatus.Idle:
                        {
                            if (FindCounter++ % TimeToFrame(0, 3) == 0)
                            {
                                double bestDist = int.MaxValue;
                                int bestCorpseKey = -1;
                                var myMapCrd = pThis.Ref.BaseAbstract.GetMapCrd();
                                // 找最近的尸体
                                foreach (var tuple in mutator.Corpses)
                                {
                                    var corpse = tuple.Value;

                                    if (corpse.isReanimating)
                                        continue;

                                    var dist = myMapCrd.DistanceFrom(corpse.corpse.mapCrd);
                                    if (dist < bestDist)
                                    {
                                        bestDist = dist;
                                        bestCorpseKey = tuple.Key;
                                    }
                                }
                                // 记录尸体并前往
                                if (bestDist != int.MaxValue)
                                {
                                    TargetCorpseKey = bestCorpseKey;
                                    Status = ReanimatorScriptStatus.GoToCorpse;
                                    GoToCorpse();
                                }
                            }
                            break;
                        }
                    case ReanimatorScriptStatus.GoToCorpse:
                        {
                            // 被别的红衣抢先了
                            if (!IsTargetCorpseValid(out var corpse))
                            {
                                EnterIdleStatus();
                            }
                            else
                            {
                                if (CheckMissionCounter++ % TimeToFrame(0,3) == 0)
                                    GoToCorpse();
                            }
                            break;
                        }
                    case ReanimatorScriptStatus.ReanimateCorpse:
                        {
                            if (--ReanimationCounter <= 0)
                            {
                                if (IsTargetCorpseValid(out var corpse, false))
                                {
                                    ReanimateCorpse(corpse);
                                    mutator.Corpses.Remove(TargetCorpseKey);
                                }
                                FindCounter = 0;
                                EnterIdleStatus();
                            }
                            break;
                        }
                }
            }

            public override void OnFire(Pointer<AbstractClass> pTarget, int weaponIndex)
            {
                var pThis = (Decorative as TechnoExt).OwnerObject;
                var mutator = myMutator as VoidReanimators;
                if (ReanimationCounter == -1)
                {
                    if (pTarget.Ref.WhatAmI() == AbstractType.Cell
                        && IsTargetCorpseValid(out var corpse)
                        && pTarget.Convert<CellClass>().Ref.MapCoords == corpse.mapCrd)
                    {
                        StartReanimateCorpse(corpse);
                        mutator.Corpses[TargetCorpseKey] = (mutator.Corpses[TargetCorpseKey].corpse, true);
                    }
                    else
                    {
                        EnterIdleStatus();
                    }
                }
            }

            private void GoToCorpse()
            {
                var pThis = (Decorative as TechnoExt).OwnerObject;
                var mutator = myMutator as VoidReanimators;
                mutator.Corpses.TryGetValue(TargetCorpseKey, out var targetCorpse);
                var targetCell = MapClass.Instance.GetCellAt(targetCorpse.corpse.mapCrd).Convert<AbstractClass>();
                if (pThis.Ref.Target != targetCell || pThis.Ref.BaseMission.CurrentMission != Mission.Attack)
                {
                    pThis.Ref.SetTarget(targetCell);
                    pThis.Ref.BaseMission.QueueMission(Mission.Attack, true);
                }
            }
            private static void ReanimateCorpse(Corpse corpse)
            {
                List<Pointer<TechnoTypeClass>> types = new List<Pointer<TechnoTypeClass>>();
                var crd = MapClass.Instance.GetCellAt(corpse.mapCrd).Ref.Base.GetCoords();
                types.Add(corpse.type);
                // 复活单位
                CreateTeamAtCrd(MutatorSpawnTeam, types, corpse.owner, crd, true, true);
                // 播放动画
                YRMemory.Create<AnimClass>(ReanimateAnim, crd);
            }
            private void EnterIdleStatus()
            {
                TargetCorpseKey = -1;
                ReanimationCounter = -1;
                Status = ReanimatorScriptStatus.Idle;
                var pThis = (Decorative as TechnoExt).OwnerObject;
                pThis.Ref.SetTarget(Pointer<AbstractClass>.Zero);
                pThis.Ref.SetDestination(Pointer<AbstractClass>.Zero);
                pThis.Ref.BaseMission.QueueMission(Mission.Guard, true);
            }
            private void StartReanimateCorpse(Corpse corpse)
            {
                var pThis = (Decorative as TechnoExt).OwnerObject;
                var mutator = myMutator as VoidReanimators;
                // 停下
                pThis.Convert<FootClass>().Ref.StopMoving();
                var type = corpse.type;
                var level = GetLevel(type);
                // 切状态
                Status = ReanimatorScriptStatus.ReanimateCorpse;
                // 开始计时
                int time = 0;
                if (level <= 3)
                    time = TimeToFrame(0, 1.5);
                else if (level <= 7)
                    time = TimeToFrame(0, 4);
                else
                    time = TimeToFrame(0, 8);
                ReanimationCounter = time;
            }
            private bool IsTargetCorpseValid(out Corpse corpse, bool notReanimating = true)
            {
                var mutator = myMutator as VoidReanimators;
                var result = mutator.Corpses.TryGetValue(TargetCorpseKey, out var tuple)
                    && (!notReanimating || !tuple.isReanimating);
                if (result)
                    corpse = tuple.corpse;
                else
                    corpse = null;
                return result;
            }
        }

        [Serializable]
        public class LeaveCorpseBuff : MutatorEventDecorator
        {
            // Decorator
            public static new DecoratorId ID => new DecoratorId((int)TechnoDecoratorIDs.LeaveCorpseBuff);
            public LeaveCorpseBuff(Mutator mutator) : base(mutator) { }
            public override DamageState AfterReceiveDamage(Pointer<int> pDamage, int DistanceFromEpicenter, Pointer<WarheadTypeClass> pWH, Pointer<ObjectClass> pAttacker, bool IgnoreDefenses, bool PreventPassengerEscape, Pointer<HouseClass> pAttackingHouse, DamageState result, int damageTaken)
            {
                if (result == DamageState.NowDead)
                {
                    var pThis = (Decorative as TechnoExt).OwnerObject;
                    var mutator = myMutator as VoidReanimators;
                    do
                    {
                        // 红衣不留尸体
                        if (pThis.Ref.GetTechnoType() == VoidReanimator)
                            break;
                        // 被自己人或不知名伤害打死不留尸体
                        var pAttackerHouse = pAttacker.IsNotNull ? pAttacker.Ref.Base.GetOwningHouse() : pAttackingHouse;
                        if (pAttackerHouse.IsNull || pThis.Ref.BaseAbstract.GetOwningHouse().Ref.IsAlliedWith(pAttackerHouse))
                            break;
                        RecordCorpse();
                    }
                    while (false);
                }
                return result;
            }

            private void RecordCorpse()
            {
                var pThis = (Decorative as TechnoExt).OwnerObject;
                var mutator = myMutator as VoidReanimators;
                var type = pThis.Ref.GetTechnoType();
                // 最多存500
                if (mutator.Corpses.Count >= 500)
                {
                    // 低级则不存
                    if (GetLevel(type) <= 3)
                        return;

                    // 高级则踢掉一个随机的已有的
                    List<int> kickableKey = new List<int>();
                    foreach (var tuple in mutator.Corpses)
                    {
                        if (!tuple.Value.isReanimating)
                            kickableKey.Add(tuple.Key);
                    }
                    if (kickableKey.Count > 0)
                    {
                        var toKick = ScenarioClass.GetRandomInList(kickableKey);
                        mutator.Corpses.Remove(toKick);
                    }
                    else
                    {
                        Logger.Log("Corpse dict full but unavailable!");
                    }
                }
                var owner = pThis.Ref.BaseAbstract.GetOwningHouse();
                var mapCrd = pThis.Ref.BaseAbstract.GetMapCrd();
                var corpse = new Corpse(new SwizzleablePointer<TechnoTypeClass>(type), new SwizzleablePointer<HouseClass>(owner), mapCrd);
                mutator.Corpses.Add(pThis.Ref.BaseAbstract.UniqueID, (corpse, false));
            }
        }
    }


}
