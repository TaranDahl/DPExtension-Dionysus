using DynamicPatcher;
using Extension.Decorators;
using Extension.Ext;
using Extension.Mutators;
using Extension.Utilities;
using PatcherYRpp;
using Scripts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.Mutators
{
    [Serializable]
    public class MissileCommand : TargetCellMutator
    {
        // Mutator
        public MissileCommand(Pointer<HouseClass> owner) : base(owner) { }
        public override string UIName => "飞弹大战";
        public override string Description => "你的建筑会不停地遭受飞弹轰炸的袭击，你必须将它们击落。";
        public override bool IsAvailableInRPG => false;
        public override int Score => 3;
        public override bool Update()
        {
            // 检查因子是否可用
            if (!base.Update())
                return false;

            if (spawnCounter == 0)
            {
                bool hasTargetToAttack = false;
                foreach (var house in HouseClass.Array)
                {
                    if (hasTargetToAttack)
                        break;

                    if (!IsOnTheirSide(house))
                        continue;

                    foreach (var building in house.Ref.Buildings)
                    {
                        if (TargetTechnoMutator.GetKeepAliveAbility(building.Convert<TechnoClass>()) > 0)
                            hasTargetToAttack = true; break;
                    }
                }
                if (hasTargetToAttack)
                {
                    var list = GetTypesToSpawn();
                    var subList = new List<Pointer<TechnoTypeClass>>();
                    foreach (var type in list)
                    {
                        subList.Add(type);
                        if (type != missileGuard)
                        {
                            var owner = GetRandomHouseOnOurSide();
                            var cell = SelectRandomCellOnTheEdge();
                            CreateMissileAtMapCrd(subList, owner, cell.Ref.Base.GetCoords());
                            cell = SelectRandomCellOnTheEdge(); // 生成两次
                            CreateMissileAtMapCrd(subList, owner, cell.Ref.Base.GetCoords());
                        }
                    }
                }
            }

            nukeCounter++;
            spawnCounter++;
            spawnCounter %= GetSpawnDelay();
            return true;
        }

        // TargetCellMutator

        // MissileCommand
        public enum MissileGuardSlot : int
        {
            None = -1,
            Middle = 0,
            LeftInner = 1,
            RightInner = 2,
            LeftOuter = 3,
            RightOuter = 4,
            SlotCount = 5
        }
        public static Pointer<TechnoTypeClass> miniMissile => TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find("CMDMSL1");
        public static Pointer<TechnoTypeClass> bigMissile => TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find("CMDMSL2");
        public static Pointer<TechnoTypeClass> nukeMissile => TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find("CMDMSL3");
        public static Pointer<TechnoTypeClass> missileGuard => TechnoTypeClass.ABSTRACTTYPE_ARRAY.Find("MSLGRD");

        private int nukeCounter = 0;
        private int spawnCounter = 0;
        private void CreateMissileAtMapCrd(List<Pointer<TechnoTypeClass>> technoTypes, Pointer<HouseClass> pHouse, CoordStruct crd)
        {
            var cell = CellClass.Coord2Cell(crd);
            // 创建单位
            List<Pointer<TechnoClass>> guards = new List<Pointer<TechnoClass>>();
            List<MissileGuardScript> guardScripts = new List<MissileGuardScript>();
            Pointer<TechnoClass> missile = Pointer<TechnoClass>.Zero;
            MissileScript missileScript = null;
            foreach (var type in technoTypes)
            {
                // 放置
                var dir = (DirType)0;
                var edge = MapClass.Instance.WhichEdgeImOn(cell);
                switch (edge)
                {
                    case InGameEdge.Left:
                        dir = DirType.NE; break;
                    case InGameEdge.Up:
                        dir = DirType.NW; break;
                    case InGameEdge.Right:
                        dir = DirType.SW; break;
                    case InGameEdge.Down:
                        dir = DirType.SE; break;
                    default:
                        dir = (DirType)(ScenarioClass.Instance.Random.RandomRanged(0, 7) * 32); break;
                }
                // 创建单位
                var techno = type.Ref.Base.CreateObject(pHouse).Convert<TechnoClass>();
                Game.IKnowWhatImDoing++;
                var success = techno.Ref.Base.Put(crd, dir);
                Game.IKnowWhatImDoing--;
                if (!success)
                {
                    Logger.Log("Force unlimbo failed.");
                    techno.Ref.BaseAbstract.DTOR();
                    continue;
                }
                var ext = TechnoExt.ExtMap.Find(techno);
                // 记录
                if (type == missileGuard)
                {
                    guardScripts.Add(ext.CreateDecorator<MissileGuardScript>(MissileGuardScript.ID, "MissileGuard Script", this));
                    guards.Add(techno);
                }
                else // 一次最多包含一发导弹
                {
                    missileScript = ext.CreateDecorator<MissileScript>(MissileScript.ID, "Missile Script", this);
                    missile = techno;
                }
            }
            // 建立防御关系
            for (int i = 0; i < guardScripts.Count; i++)
            {
                var guard = guards[i];
                var guardScript = guardScripts[i];
                missileScript.TryReceiveGuard(guard);
                guardScript.FindMissileToGuard(missile);
            }
        }
        private List<Pointer<TechnoTypeClass>> GetTypesToSpawn()
        {
            var list = new List<Pointer<TechnoTypeClass>>();
            var frame = Game.CurrentFrame;
            if (frame < TimeToFrame(5, 0))
            {
                list.Add(miniMissile);
            }
            else if (frame < TimeToFrame(10, 0))
            {
                var rand = ScenarioClass.Instance.Random.RandomRanged(0, 99);
                if (rand < 10)
                {
                    list.Add(missileGuard);
                    list.Add(miniMissile);
                }
                else
                {
                    list.Add(miniMissile);
                }
            }
            else if (frame < TimeToFrame(15, 0))
            {
                var rand = ScenarioClass.Instance.Random.RandomRanged(0, 99);
                if (rand < 20)
                {
                    list.Add(missileGuard);
                    list.Add(miniMissile);
                }
                else if (rand < 40)
                {
                    list.Add(bigMissile);
                }
                else
                {
                    list.Add(miniMissile);
                }
            }
            else
            {
                var rand = ScenarioClass.Instance.Random.RandomRanged(0, 99);
                if (rand < 20)
                {
                    list.Add(missileGuard);
                    list.Add(miniMissile);
                }
                else if (rand < 50)
                {
                    list.Add(bigMissile);
                }
                else if (rand < 90)
                {
                    list.Add(miniMissile);
                }
                else
                {
                    if (nukeCounter >= TimeToFrame(1, 0))
                    {
                        nukeCounter = 0;
                        list.Add(missileGuard);
                        list.Add(missileGuard);
                        list.Add(missileGuard);
                        list.Add(missileGuard);
                        list.Add(missileGuard);
                        list.Add(nukeMissile);
                    }
                    else
                    {
                        list.Add(miniMissile);
                    }
                }
            }
            return list;
        }
        private int GetSpawnDelay()
        {
            var delay = 0;
            var frame = Game.CurrentFrame;
            if (frame < TimeToFrame(5, 0))
                delay = TimeToFrame(0, 15);
            else if (frame < TimeToFrame(10, 0))
                delay = TimeToFrame(0, 5);
            else if (frame < TimeToFrame(15, 0))
                delay = TimeToFrame(0, 2);
            else
                delay = TimeToFrame(0, 1);
            return delay;
        }
        public static CoordStruct GetGuardFLH(MissileCommand.MissileGuardSlot slot)
        {
            switch (slot)
            {
                case MissileCommand.MissileGuardSlot.Middle:
                    return new CoordStruct(512, 0, 0);
                case MissileCommand.MissileGuardSlot.LeftInner:
                    return new CoordStruct(256, 256, 0);
                case MissileCommand.MissileGuardSlot.RightInner:
                    return new CoordStruct(256, -256, 0);
                case MissileCommand.MissileGuardSlot.LeftOuter:
                    return new CoordStruct(0, 512, 0);
                case MissileCommand.MissileGuardSlot.RightOuter:
                    return new CoordStruct(0, -512, 0);
                default:
                    return new CoordStruct(512, 0, 0);
            }
        }

        [Serializable]
        public class MissileGuardScript : MutatorEventDecorator
        {
            // Decorator
            public static DecoratorId ID => new DecoratorId((int)TechnoDecoratorIDs.UniqueDecorator);

            // MissileGuardScript
            private ExtensionReference<TechnoExt> GuardTarget;
            public MissileCommand.MissileGuardSlot slot = MissileCommand.MissileGuardSlot.Middle;
            private int Energy = 200;
            private int UpdateCounter = 0;
            private bool Initiated = false;
            private ColorStruct interceptLaserInnerColor = new ColorStruct(127, 127, 0);
            private ColorStruct interceptLaserOuterColor = new ColorStruct(127, 127, 0);
            private ColorStruct interceptLaserOuterSpread = new ColorStruct(0, 0, 0);

            public MissileGuardScript(Mutator mutator) : base(mutator) { }

            public override void OnUpdate()
            {
                // 更新守卫目标
                UpdateGuardTarget();
                // 更新位置
                UpdateDest();
                // 拦截抛射体
                UpdateInterceptor();
            }

            public bool FindMissileToGuard(Pointer<TechnoClass> missile)
            {
                if (missile.IsNotNull) // 如果给了目标，那就直接去守卫那个目标
                {
                    GuardTarget.Set(TechnoExt.ExtMap.Find(missile));
                    return true;
                }
                else // 如果没给目标，那就自己找一个目标
                {
                    var pThis = (Decorative as TechnoExt).OwnerObject;
                    var bestTarget = Pointer<TechnoClass>.Zero;
                    var bestDist = int.MaxValue;
                    foreach (var ac in AircraftClass.Array)
                    {
                        var acOwner = ac.Ref.BaseAbstract.GetCoords();
                        var acType = ac.Ref.Type.Convert<TechnoTypeClass>();
                        // 是导弹
                        if ((acType == MissileCommand.miniMissile || acType == MissileCommand.bigMissile || acType == MissileCommand.nukeMissile)
                            && myMutator.IsOnOurSide(ac.Ref.BaseAbstract.GetOwningHouse())) // 是友军
                        {
                            var acExt = TechnoExt.ExtMap.Find(ac.Convert<TechnoClass>());
                            var acScriptable = acExt.Get(MissileScript.ID) as MissileScript;
                            var dist = pThis.Ref.BaseAbstract.DistanceFrom(ac.Convert<AbstractClass>());
                            if (acScriptable.CanReceiveGuard() // 有空位
                            && dist < bestDist) // 距离近
                            {
                                bestTarget = ac.Convert<TechnoClass>();
                                bestDist = dist;
                            }
                        }
                    }
                    if (bestTarget.IsNotNull)
                    {
                        var ext = TechnoExt.ExtMap.Find(bestTarget);
                        var missileScriptable = ext.Get(MissileScript.ID) as MissileScript;
                        var newSlot = missileScriptable.TryReceiveGuard(pThis);
                        if (newSlot == MissileCommand.MissileGuardSlot.None)
                        {
                            Logger.Log("Assign guard target failed!");
                            return false;
                        }
                        GuardTarget.Set(ext);
                        slot = newSlot;
                        return true;
                    }
                    return false;
                }
            }
            private void UpdateGuardTarget()
            {
                GuardTarget.TryGet(out TechnoExt targetExt);
                if (targetExt != null)
                    return;
                // 找个新目标
                FindMissileToGuard(Pointer<TechnoClass>.Zero);
            }
            private void UpdateDest()
            {
                GuardTarget.TryGet(out TechnoExt targetExt);
                CoordStruct crd;
                if (targetExt == null)
                    crd = (Decorative as TechnoExt).OwnerObject.Ref.BaseAbstract.GetCoords();
                else
                {
                    var target = targetExt.OwnerObject;
                    crd = target.Ref.Base.GetFLH(-6, MissileCommand.GetGuardFLH(slot));
                }
                var pThis = (Decorative as TechnoExt).OwnerObject.Convert<FootClass>();
                if (!Initiated)
                {
                    pThis.Ref.Base.SetDestination(MapClass.Instance.GetCellAt(crd).Convert<AbstractClass>());
                }
                else
                {
                    pThis.Ref.Destination = MapClass.Instance.GetCellAt(crd).Convert<AbstractClass>();
                    pThis.Ref.MoveTo(crd);
                }
            }
            private void UpdateInterceptor()
            {
                if (UpdateCounter == 0 && Energy >= 10)
                {
                    var pThis = (Decorative as TechnoExt).OwnerObject;
                    foreach (var bullet in BulletClass.Array)
                    {
                        if (pThis.Ref.BaseAbstract.DistanceFrom(bullet.Convert<AbstractClass>()) <= 2048 // 距离8格
                            && bullet.Ref.Owner.IsNotNull && myMutator.IsOnTheirSide(bullet.Ref.Owner.Ref.BaseAbstract.GetOwningHouse()) // 只拦敌人的
                            && !bullet.Ref.Type.Ref.Inviso) // 不拦Inviso
                        {
                            var source = pThis.Ref.BaseAbstract.GetCoords();
                            var target = bullet.Ref.Base.Base.GetCoords();
                            YRMemory.Create<LaserDrawClass>(source, target, interceptLaserInnerColor, interceptLaserOuterColor, interceptLaserOuterSpread, 5);
                            Energy -= 10;
                            bullet.Ref.Explode();
                            bullet.Ref.Base.UnInit();
                            if (Energy < 10)
                                break;
                        }
                    }
                }
                UpdateCounter++;
                UpdateCounter %= 3;
            }
        }

        [Serializable]
        public class MissileScript : MutatorEventDecorator
        {
            // Decorator
            public static DecoratorId ID => new DecoratorId((int)TechnoDecoratorIDs.UniqueDecorator);

            // MutatorEventDecorator
            public MissileScript(Mutator mutator) : base(mutator) 
            {
                for (var slot = MissileGuardSlot.Middle; slot != MissileGuardSlot.SlotCount; slot++)
                {
                    Guards.Add(slot, new ExtensionReference<TechnoExt>());
                }
            }

            // MissileScript
            private ExtensionReference<TechnoExt> Target;
            private bool Initiated = false;
            private Dictionary<MissileCommand.MissileGuardSlot, ExtensionReference<TechnoExt>> Guards = new Dictionary<MissileCommand.MissileGuardSlot, ExtensionReference<TechnoExt>>();
            private TimerStruct AlertTimer = new TimerStruct();


            public override void OnUpdate()
            {
                // 核弹雷达事件
                var pThis = (Decorative as TechnoExt).OwnerObject;
                if (pThis.Ref.GetTechnoType() == nukeMissile)
                {
                    if (!AlertTimer.InProgress())
                    {
                        AlertTimer.Start(Mutator.TimeToFrame(0, 10));
                        RadarEventClass.Create(RadarEventType.Combat, pThis.Ref.BaseAbstract.GetMapCrd());
                    }
                }
                // 没有目标，找个新目标
                if (!Target.TryGet(out var ext))
                {
                    var list = new List<Pointer<TechnoClass>>();
                    foreach (var house in HouseClass.Array)
                    {
                        if (!myMutator.IsOnTheirSide(house))
                            continue;

                        foreach (var building in house.Ref.Buildings)
                        {
                            if (TargetTechnoMutator.GetKeepAliveAbility(building.Convert<TechnoClass>()) > 0)
                                list.Add(building.Convert<TechnoClass>());
                        }
                    }
                    if (list.Count > 0)
                    {
                        // 设置目标
                        var target = ScenarioClass.GetRandomInList(list);
                        Target.Set(TechnoExt.ExtMap.Find(target));
                        var crd = target.Ref.BaseAbstract.GetCoords();
                        var foot = (Decorative as TechnoExt).OwnerObject.Convert<FootClass>();
                        var loco = foot.Ref.Locomotor;
                        Pointer<RocketLocomotionClass> pLoco = loco.ToLocomotionClass<RocketLocomotionClass>();
                        // 飞过去
                        if (!Initiated)
                        {
                            // 起飞
                            foot.Ref.Base.SetDestination(target.Convert<AbstractClass>());
                            foot.Ref.BaseMission.QueueMission(Mission.Move, true);
                            Initiated = true;
                        }
                        else
                        {
                            // 改变方向
                            pLoco.Ref.Destination = crd;
                        }
                    }
                    else
                    {
                        (Decorative as TechnoExt).OwnerObject.Ref.Base.Vanish(Pointer<TechnoClass>.Zero);
                    }
                }
            }
            public override DamageState AfterReceiveDamage(Pointer<int> pDamage, int DistanceFromEpicenter, Pointer<WarheadTypeClass> pWH, Pointer<ObjectClass> pAttacker, bool IgnoreDefenses, bool PreventPassengerEscape, Pointer<HouseClass> pAttackingHouse, DamageState result, int damageTaken)
            {
                var ext = (Decorative as TechnoExt);
                var pThis = ext.OwnerObject;
                // 大导弹死亡时生俩小导弹
                if (result == DamageState.NowDead && pThis.Ref.GetTechnoType() == bigMissile)
                {
                    List<Pointer<TechnoTypeClass>> list = new List<Pointer<TechnoTypeClass>>();
                    list.Add(miniMissile);
                    var owner = pThis.Ref.BaseAbstract.GetOwningHouse();
                    var crd = pThis.Ref.BaseAbstract.GetCoords();
                    (myMutator as MissileCommand).CreateMissileAtMapCrd(list, owner, crd);
                    (myMutator as MissileCommand).CreateMissileAtMapCrd(list, owner, crd);
                }
                return base.AfterReceiveDamage(pDamage, DistanceFromEpicenter, pWH, pAttacker, IgnoreDefenses, PreventPassengerEscape, pAttackingHouse, result, damageTaken);
            }
            public MissileCommand.MissileGuardSlot TryReceiveGuard(Pointer<TechnoClass> guard, MissileCommand.MissileGuardSlot slot = MissileCommand.MissileGuardSlot.None)
            {
                if (slot != MissileCommand.MissileGuardSlot.None)
                {
                    // 指定了slot，那就尝试slot
                    TechnoExt oldGuard;
                    Guards.TryGetValue(slot, out var refExt);
                    refExt.TryGet(out oldGuard);
                    if (oldGuard == null)
                    {
                        Guards[slot].Set(TechnoExt.ExtMap.Find(guard));
                        return slot;
                    }
                    else
                    {
                        return MissileCommand.MissileGuardSlot.None;
                    }
                }
                else
                {
                    // 没指定slot，那就指定挨个slot尝试
                    var result = MissileCommand.MissileGuardSlot.None;
                    for (var i = MissileCommand.MissileGuardSlot.Middle; i != MissileCommand.MissileGuardSlot.SlotCount; i++)
                    {
                        result = TryReceiveGuard(guard, i);
                        if (result != MissileCommand.MissileGuardSlot.None)
                            break;
                    }
                    return result;
                }
            }
            public bool CanReceiveGuard()
            {
                for (var i = MissileCommand.MissileGuardSlot.Middle; i != MissileCommand.MissileGuardSlot.SlotCount; i++)
                {
                    Guards[i].TryGet(out TechnoExt oldGuard);
                    if (oldGuard == null)
                        return true;
                }
                return false;
            }
        }
    }
}
