using DecoratorHooks;
using DynamicPatcher;
using Extension.Ext;
using Extension.Mutators;
using Extension.Utilities;
using PatcherYRpp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using static Extension.AttackWave.AttackWave;

namespace Extension.AttackWave
{
    [Serializable]
    public class AttackWave
    {
        static Pointer<AnimTypeClass> ChronoAnim => new Pointer<AnimTypeClass>(AnimTypeClass.ABSTRACTTYPE_ARRAY.Find("CHRONOTG"));
        static Pointer<AnimTypeClass> MoonPodAnim => new Pointer<AnimTypeClass>(AnimTypeClass.ABSTRACTTYPE_ARRAY.Find("MEGAPODX"));
        static Pointer<BulletTypeClass> MoonPodProj => new Pointer<BulletTypeClass>(BulletTypeClass.ABSTRACTTYPE_ARRAY.Find("MEGAPODXPROJ"));
        static Pointer<AnimTypeClass> SUPodAnim => new Pointer<AnimTypeClass>(AnimTypeClass.ABSTRACTTYPE_ARRAY.Find("SUPOD"));
        static Pointer<AnimTypeClass> DigAnim => new Pointer<AnimTypeClass>(AnimTypeClass.ABSTRACTTYPE_ARRAY.Find("DIG"));
        static Pointer<AnimTypeClass> DropPodAnim => new Pointer<AnimTypeClass>(AnimTypeClass.ABSTRACTTYPE_ARRAY.Find("DROPPODX"));
        static Pointer<BulletTypeClass> DropPodProj => new Pointer<BulletTypeClass>(BulletTypeClass.ABSTRACTTYPE_ARRAY.Find("DROPPODXPROJ"));
        static Pointer<AnimTypeClass> FFPodAnim => new Pointer<AnimTypeClass>(AnimTypeClass.ABSTRACTTYPE_ARRAY.Find("FFPOD"));
        static int StdPathStepCount = 12; // 每此向前行进这么多距离
        private int AttackMovePathStepCount
        {
            get
            {
                int subOccupationCount = 0;

                foreach (var member in Members)
                {
                    if (member.Ref.IsAirUnit())
                        continue;

                    subOccupationCount += member.Ref.GetTechnoType().Ref.GetSubOccupationCount();
                }

                subOccupationCount = (int)Math.Sqrt((double)subOccupationCount);
                return Math.Min(24, subOccupationCount + StdPathStepCount);
            }
            set
            { }
        }
        private int AttackMoveMissionDelay
        {
            get
            {
                return AttackMovePathStepCount * 2 / 3;
            }
            set
            { }
        }

        public static List<AttackWave> Array => AttackWaveManager.Instance.Array;

        public enum AttackWaveScriptAction
        {
            Custom = 0,
            AttackRandomPlayerBase = 1, // 目标作战方被消灭后自动停止
            AttackNearestPlayerBase = 2, // 目标作战方被消灭后自动停止
            GuardNearestAIBase = 3,
            GoHunting = 4, // 不会自动停止
            GuardCurrentPosition = 5,
            Retreat = 6, // 不会自动停止
            // AttackMissionObject
            // ForcedTargetMissionObject
            // GuardMissionObject
            // MoveToMissionObject
        }

        [Serializable]
        public struct AttackWaveScriptNode
        {
            public AttackWaveScriptAction Action;
            public int Data;

            public AttackWaveScriptNode(AttackWaveScriptAction action, int data = -1)
            {
                Action = action;
                Data = data;
            }

            public static List<AttackWaveScriptNode> DefaultScript()
            {
                return new List<AttackWaveScriptNode>()
                    {
                        new AttackWaveScriptNode(AttackWaveScriptAction.AttackRandomPlayerBase),
                        new AttackWaveScriptNode(AttackWaveScriptAction.GoHunting)
                    };
            }
        }

        private bool SpawnMember(Pointer<TechnoClass> member, CoordStruct crd)
        {
            var cell = MapClass.Instance.GetCellAt(crd);
            bool isInAir =
                crd.Z >= cell.Ref.GetFloorHeight(new Point2D(0,0)) + 208
                && member.Ref.IsAirUnit();

            ++Game.IKnowWhatImDoing;
            var result =  member.Ref.Base.Put(crd, ScenarioClass.GetRandomInEnum<DirType>());
            --Game.IKnowWhatImDoing;

            if (!result)
            {
                Logger.Log("Attack wave spawn member failed.");
                return false;
            }

            // 空中单位需要调整Locomotor状态
            if (isInAir)
            {
                member.Ref.SetDestination(cell);
                member.Ref.BaseMission.QueueMission(Mission.Move, true);
                var loco = member.Convert<FootClass>().Ref.Locomotor;

                var jjloco = loco.ToLocomotionClass<JumpjetLocomotionClass>();
                if (jjloco.IsNotNull)
                {
                    jjloco.Ref.LocoState = JumpjetLocomotionClass.State.Cruising;
                }

                //var flyloco = loco.ToLocomotionClass<FlyLocomotionClass>();
                //if (flyloco.IsNotNull)
                //{
                //}
            }

            cell.Ref.ScatterContent(CoordStruct.Empty, true, true, cell.Ref.ContainsBridge());

            return true;
        }
        private enum MovementRestrictionLevel
        {
            None = 4,
            Water = 3,
            Ground = 2,
            Amphibious = 1,
            Fly = 0
        }
        private MovementRestrictionLevel GetMovementRestrictionLevel(MovementZone mz)
        {
            switch (mz)
            {
                case MovementZone.None:
                    return MovementRestrictionLevel.None;
                case MovementZone.Water:
                case MovementZone.WaterBeach:
                    return MovementRestrictionLevel.Water;
                case MovementZone.Normal:
                case MovementZone.Crusher:
                case MovementZone.CrusherAll:
                case MovementZone.Destroyer:
                case MovementZone.Infantry:
                case MovementZone.InfantryDestroyer:
                    return MovementRestrictionLevel.Ground;
                case MovementZone.Amphibious:
                case MovementZone.AmphibiousCrusher:
                case MovementZone.AmphibiousDestroyer:
                    return MovementRestrictionLevel.Amphibious;
                case MovementZone.Fly:
                case MovementZone.Subterrannean:
                    return MovementRestrictionLevel.Fly;
                default:
                    return MovementRestrictionLevel.Fly;
            }
        }
        private (MovementZone mz, SpeedType st) GetStandardMoveArgs(MovementRestrictionLevel level)
        {
            switch (level)
            {
                case MovementRestrictionLevel.None:
                    return (MovementZone.None, SpeedType.None);
                case MovementRestrictionLevel.Water:
                    return (MovementZone.Water, SpeedType.Float);
                case MovementRestrictionLevel.Ground:
                    return (MovementZone.Normal, SpeedType.Track);
                case MovementRestrictionLevel.Amphibious:
                    return (MovementZone.Amphibious, SpeedType.Amphibious);
                case MovementRestrictionLevel.Fly:
                    return (MovementZone.Fly, SpeedType.Winged);
                default:
                    return (MovementZone.Fly, SpeedType.Winged);
            }
        }
        private void MissionConstruction()
        {
            int ProcessConstructionStart(int sideIdx)
            {
                // 确定怎么投送
                List<Pointer<TechnoClass>> airUnits = new List<Pointer<TechnoClass>>();
                List<Pointer<TechnoClass>> groundUnits = new List<Pointer<TechnoClass>>();
                List<Pointer<TechnoClass>> groundInfs = new List<Pointer<TechnoClass>>();
                bool spawnAirUnits = sideIdx != 0; // 盟军把空军超时空到地上再起飞，其它阵营都是从天上降下来
                int subOccupationCount = 0; // 需要占多少位置
                MovementRestrictionLevel movementRestrictionLevel = MovementRestrictionLevel.Fly; // 需要遵守什么寻路

                foreach (var member in Members)
                {
                    var memberType = member.Ref.GetTechnoType();

                    if (member.Ref.IsAirUnit() && spawnAirUnits)
                    {
                        airUnits.Add(member);
                    }
                    else
                    {
                        if (member.Ref.BaseAbstract.WhatAmI() == AbstractType.Infantry)
                            groundInfs.Add(member);
                        else
                            groundUnits.Add(member);
                        subOccupationCount += memberType.Ref.GetSubOccupationCount();
                    }

                    var memberMovementRestrictionLevel = GetMovementRestrictionLevel(memberType.Ref.MovementZone);

                    if (memberMovementRestrictionLevel == MovementRestrictionLevel.Ground && movementRestrictionLevel == MovementRestrictionLevel.Water
                        || memberMovementRestrictionLevel == MovementRestrictionLevel.Water && movementRestrictionLevel == MovementRestrictionLevel.Ground)
                    {
                        movementRestrictionLevel = MovementRestrictionLevel.None;
                        Logger.Log("Found both techno with ground and water movementzone in single attack wave, could cause pathing failure.");
                    }

                    if (memberMovementRestrictionLevel > movementRestrictionLevel)
                        movementRestrictionLevel = memberMovementRestrictionLevel;
                }

                // 每9个格子刷一个空投舱
                var spawnPodCount = (int)Math.Ceiling(subOccupationCount / 27.0);

                // 刷出空军，生成空投舱
                var moveArgs = GetStandardMoveArgs(movementRestrictionLevel);
                var enumerator = new CellSpreadEnumerator(20);
                var startMapCrd = CellClass.Coord2Cell(SpawnCrd);
                List<CellStruct> usedMapCrds = new List<CellStruct>();

                bool isMapCrdValid(CellStruct mapCrd)
                {
                    // 不能已被选择
                    if (usedMapCrds.Contains(mapCrd))
                        return false;

                    var cell = MapClass.Instance.GetCellAt(mapCrd);

                    // 必须可通行
                    if (!cell.Ref.IsClearToMove(moveArgs.st, moveArgs.mz, true, true, cell.Ref.GetLevel()))
                        return false;

                    if (cell.Ref.GetBuilding().IsNotNull)
                        return false;

                    // 有效位置
                    if (!MapClass.Instance.IsWithinUsableArea(mapCrd, true))
                        return false;

                    // 必须和SpawnCrd寻路距离40格以内
                    if (AStarClass.AttemptPath(startMapCrd, mapCrd, moveArgs.mz) > 40)
                        return false;

                    return true;
                }

                bool isInf = false;
                void putGround(CoordStruct crd)
                {
                    // 将这些位置标记为已使用
                    usedMapCrds.Add(CellClass.Coord2Cell(crd));

                    isInf = !isInf && groundInfs.Count > 0;

                    if (isInf)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            if (groundInfs.Count > 0)
                            {
                                var inf = groundInfs[0];
                                groundInfs.RemoveAt(0);
                                inf.Ref.Base.SetLocation(crd);
                            }
                        }
                    }
                    else
                    {
                        if (groundUnits.Count > 0)
                        {
                            var unit = groundUnits[0];
                            groundUnits.RemoveAt(0);
                            unit.Ref.Base.SetLocation(crd);
                        }
                    }
                }

                foreach (var offset in enumerator)
                {
                    var currentCenter = offset + startMapCrd;

                    if (airUnits.Count <= 0 && groundInfs.Count <= 0 && groundUnits.Count <= 0)
                        break;

                    var centerCell = MapClass.Instance.GetCellAt(currentCenter);

                    // 空军
                    if (airUnits.Count > 0)
                    {
                        var airUnit = airUnits[0];
                        airUnits.RemoveAt(0);
                        var crd = centerCell.Ref.Base.GetCoords();
                        crd.Z += 2560;

                        // 不成功就再塞回去
                        if (!SpawnMember(airUnit, crd))
                            airUnits.Add(airUnit);
                    }

                    // 空投舱
                    if (groundInfs.Count > 0 || groundUnits.Count > 0)
                    {
                        bool valid = true;

                        if (!isMapCrdValid(currentCenter))
                            continue;

                        foreach (var neighbourOffset in CellClass.Neighbours)
                        {
                            var currentMapCrd = neighbourOffset + currentCenter;

                            if (!isMapCrdValid(currentMapCrd))
                            {
                                valid = false;
                                break;
                            }
                        }

                        if (valid)
                        {
                            var centerCrd = centerCell.Ref.Base.GetCoords();
                            // 在中间位置播动画
                            switch (sideIdx)
                            {
                                case 0:
                                    YRMemory.Create<AnimClass>(ChronoAnim, centerCrd);
                                    break;
                                case 1:
                                    YRMemory.Create<AnimClass>(SUPodAnim, centerCrd);
                                    //MoonPodProj.Ref.CreateBullet(centerCell.Convert<AbstractClass>(), Pointer<TechnoClass>.Zero, Pointer<WeaponTypeClass>.Zero);
                                    break;
                                case 2:
                                    YRMemory.Create<AnimClass>(DigAnim, centerCrd);
                                    break;
                                case 3:
                                    YRMemory.Create<AnimClass>(FFPodAnim, centerCrd);
                                    //DropPodProj.Ref.CreateBullet(centerCell.Convert<AbstractClass>(), Pointer<TechnoClass>.Zero, Pointer<WeaponTypeClass>.Zero);
                                    break;
                                default:
                                    break;
                            }
                            // 取出数个单位将其放置在相应位置，但不Put，任务结束时才Put。
                            putGround(centerCrd);
                            foreach (var neighbourOffset in CellClass.Neighbours)
                            {
                                if (groundInfs.Count <= 0 && groundUnits.Count <= 0)
                                    break;
                                var currentMapCrd = neighbourOffset + currentCenter;
                                var currentCrd = MapClass.Instance.GetCellAt(currentMapCrd).Ref.Base.GetCoords();
                                putGround(currentCrd);
                            }
                        }
                    }
                }

                // 结束时间
                switch (sideIdx)
                {
                    case 0:
                        return ScenarioExt.TimeToFrame(0, 2);
                    case 1:
                        return ScenarioExt.TimeToFrame(0, 0, 25);
                    case 2:
                        return ScenarioExt.TimeToFrame(0, 2);
                    case 3:
                        return ScenarioExt.TimeToFrame(0, 0, 25);
                    default:
                        return 0;
                }
            }
            void ProcessConstructionEnd()
            {
                foreach (var member in Members)
                {
                    SpawnMember(member, member.Ref.BaseAbstract.GetCoords());
                }

                // 报警
                int infCostCount = 0;
                int vehCostCount = 0;
                int airCostCount = 0;
                int navCostCount = 0;
                foreach (var member in Members)
                {
                    var type = member.Ref.GetTechnoType();
                    var cost = type.Ref.Cost;
                    if (member.Ref.IsAirUnit())
                    {
                        airCostCount += cost;
                    }
                    else if (type.Ref.Naval)
                    {
                        navCostCount += cost;
                    }
                    else if (member.Ref.BaseAbstract.WhatAmI() == AbstractType.Unit)
                    {
                        vehCostCount += cost;
                    }
                    else if (member.Ref.BaseAbstract.WhatAmI() == AbstractType.Infantry)
                    {
                        infCostCount += cost;
                    }
                }
                var max = new[] { infCostCount, vehCostCount, airCostCount, navCostCount }.Max();
                if (max <= 0)
                    return;
                var voxName = "";
                if (max == infCostCount)
                    voxName = "EVA_EnemyInfantryBattalionDetected";
                else if (max == vehCostCount)
                    voxName = "EVA_ArmorBattallianDetected";
                else if (max == airCostCount)
                    voxName = "EVA_EnemyAirArmadaDetected";
                else if (max == navCostCount)
                    voxName = "EVA_EnemyFleetDetected";

                VoxClass.Speak(voxName, VoxType.INTERRUPT, VoxPriorityType.CRITICAL);
                RadarEventClass.Create(RadarEventType.EnemySensed, CellClass.Coord2Cell(SpawnCrd));

                // 播放风暴英雄语音特效（如果波次中有英雄）
                if (HeroesFromTheStormIdx.Count > 0)
                {
                    HeroesFromTheStorm.AddVoiceEffects(this);
                }
            }
            if (MissionTimer.StartTime == -1)
            {
                var delay = ProcessConstructionStart(SideIdx);
                MissionTimer.Start(delay);
            }

            if (MissionTimer.Completed())
            {
                ProcessConstructionEnd();
                IsReadyForNextScript = true;
            }
        }
        private void MissionRetreat()
        {
            foreach(var member in Members)
            {
                bool shouldVanish = true;
                if (member.Ref.BaseAbstract.WhatAmI() != AbstractType.Building)
                {
                    var memberFoot = member.Convert<FootClass>();
                    if (memberFoot.Ref.Locomotor.Is_Moving_Now())
                    {
                        memberFoot.Ref.Locomotor.Stop_Moving();
                        shouldVanish = false;
                    }
                }

                if (shouldVanish)
                {
                    member.Ref.Base.Vanish(Pointer<TechnoClass>.Zero);
                    // TODO: 播放动画，盟军超时空，EP钻地，苏军烟雾弹，焚风增值爆炸
                }
            }
        }
        private void MissionAttackMove()
        {
            if (!MissionTimer.Expired())
                return;

            MissionTimer.Start(ScenarioExt.TimeToFrame(0, AttackMoveMissionDelay));

            var focus = ScriptFocus;

            if (focus.IsNull)
            {
                Logger.Log("Enter mission attack move with no focus.");
                return;
            }

            var scriptFocusCrd = focus.Ref.GetCoords();
            var scriptFocusCell = MapClass.Instance.GetCellAt(scriptFocusCrd);
            var currentCrd = CurrentCrd;
            var currentMapCrd = CellClass.Coord2Cell(currentCrd);
            var scriptFocusMapCrd = scriptFocusCell.Ref.MapCoords;
            var stray = Stray;
            var members = Members;

            void membersAttackMoveTo(Pointer<AbstractClass> dest, Pointer<AbstractClass> target)
            {
                // 让空闲的单位去攻击目标
                foreach (var member in members)
                {
                    var memberMission = member.Ref.BaseMission.CurrentMission;
                    var canAttackMove = member.Ref.CanAttackOnTheMove();

                    if (memberMission != Mission.Attack) // 检查任务
                    {
                        var memberFoot = member.Convert<FootClass>();
                        var missionMeet = memberFoot.Ref.MegaMission == Mission.AttackMove;
                        var targetMeet = memberFoot.Ref.MegaDestination == target || memberFoot.Ref.MegaTarget == target;
                        if (canAttackMove && (!missionMeet || !targetMeet)) // 检查MegaMission
                        {
                            member.Convert<FootClass>().Ref.TryAttackMove(target);
                        }
                        else if (!canAttackMove)
                        {
                            member.Ref.BaseMission.QueueMission(Mission.Move, false);
                            member.Ref.SetDestination(dest);
                        }
                    }
                }
            }

            var pathFinder = PathFinder;
            var pathFinderMZ = PathFinder.IsNotNull ? PathFinder.Ref.Base.GetTechnoType().Ref.MovementZone : MovementZone.None;
            if (AStarClass.AttemptPath(currentMapCrd, scriptFocusMapCrd, pathFinderMZ) > stray) // 还没到脚本设置的目的地
            {
                var dest = Destination;
                var destMapCrd = dest.IsNotNull ? dest.Ref.MapCoords : CellStruct.Empty;

                if (dest.IsNull // 没有目的地
                    || currentCrd.DistanceFrom(dest.Ref.Base.GetCoords()) <= stray * 256) // 已经抵达目的地
                {
                    // 查找新的目的地
                    if (pathFinder.IsNotNull) // 和小队不同，攻击波次并不排除建筑，所以可能没有PathFinder但仍有成员。
                    {
                        var data = pathFinder.Ref.FindPath(scriptFocusCell.Ref.MapCoords);
                        var currentCell = pathFinder.Ref.BaseObject.GetCell();

                        if (data.IsNotNull && data.Ref.TotalDistance > 0)
                        {
                            var pathStepCount = AttackMovePathStepCount;
                            for (var i = 0; i < data.Ref.PathLength && (i < pathStepCount || pathFinder.Ref.BaseObject.IsCellOccupied(currentCell, 0xFFFFFFFF, -1, Pointer<CellClass>.Zero, true) >= 7); i++)
                            {
                                if (!data.Ref.GetDir(i, out var dir))
                                    break;

                                currentCell = currentCell.Ref.GetNeighbourCell(dir);
                            }
                            Logger.Log("New path finded, currentMapCrd " + currentMapCrd + ", new dest " + currentCell.Ref.MapCoords);
                        }

                        Destination = currentCell;
                        destMapCrd = dest.IsNotNull ? dest.Ref.MapCoords : CellStruct.Empty;
                    }

                    dest = Destination;
                }

                if (dest.IsNotNull)
                {
                    var target = Target;
                    var destCrd = dest.Ref.Base.GetCoords();

                    if (currentCrd.DistanceFrom(destCrd) > stray * 256) // 还没到寻路目的地
                    {

                        if (target.IsNull) // 没有敌人要打
                        {
                            // 检查所有成员，如果有目标则设置为攻击波次目标
                            List<Pointer<AbstractClass>> targets = new List<Pointer<AbstractClass>>();
                            foreach (var member in members)
                            {
                                var memberTarget = member.Ref.Target;

                                if (memberTarget.IsNull)
                                    continue;

                                // 目标必须是单位
                                if ((memberTarget.Ref.AbstractFlags & AbstractFlags.Techno) == AbstractFlags.None)
                                    continue;

                                // 不能是友军目标
                                if (member.Ref.BaseAbstract.GetOwningHouse().Ref.IsAlliedWith(memberTarget.Ref.GetOwningHouse()))
                                    continue;

                                // 目标不能太远
                                if (member.Ref.BaseAbstract.DistanceFrom(memberTarget) > 30 * 256)
                                    continue;

                                targets.Add(member.Ref.Target);
                            }
                            Target = ScenarioClass.GetRandomInList(targets).Convert<TechnoClass>();
                            target = Target;
                        }
                    }
                    else
                    {
                        Logger.Log("New dest still in stray area, currentMapCrd" + currentMapCrd + ", destMapCrd " + destMapCrd);
                    }

                    if (target.IsNotNull) // 有敌人要打
                    {
                        // AttackMove到目标
                        membersAttackMoveTo(target.Ref.Base.GetCell().Convert<AbstractClass>(), target.Convert<AbstractClass>());
                    }
                    else
                    {
                        // 前往目的地附近
                        membersAttackMoveTo(dest.Convert<AbstractClass>(), dest.Convert<AbstractClass>());
                    }
                }
                else
                {
                    Logger.Log("Can not find path to MegaFocus, currentMapCrd " + currentMapCrd + ", megaDestMapCrd " + scriptFocusMapCrd + ", destMapCrd " + destMapCrd);
                    ScriptFocus = Pointer<AbstractClass>.Zero; // 清除目标，下一帧脚本会施加新的目标}
                }
            }
            else
            {
                if (focus.Ref.WhatAmI() != AbstractType.Cell) // 脚本目的地是要杀的单位（而且还活着）
                {
                    // AttackMove到目标
                    membersAttackMoveTo(focus, focus);
                }
                else
                {
                    ScriptFocus = Pointer<AbstractClass>.Zero; // 清除目标，下一帧脚本会施加新的目标
                }
            }
        }
        private void UpdateMemberList()
        {
            var owner = OwnerHouse;
            for (int i = 0; i < _Members.Count; i++)
            {
                if (!_Members[i].TryGet(out var memberExt) // 单位死了
                    || memberExt.OwnerObject.Ref.BaseAbstract.GetOwningHouse() != OwnerHouse // 单位被控了
                    || memberExt.OwnerObject.Ref.Base.InLimbo && Mission != Mission.Construction) // 单位limbo了
                {
                    _Members.RemoveAt(i);
                    i--;
                }
            }
            if (_Members.Count == 0)
                IsAlive = false;
        }
        private void UpdateMission()
        {
            switch (Mission)
            {
                case Mission.Construction:
                    {
                        MissionConstruction();
                        break;
                    }
                case Mission.Retreat:
                    {
                        MissionRetreat();
                        break;
                    }
                case Mission.Attack:
                    {
                        break;
                    }
                case Mission.Move:
                    {
                        break;
                    }
                case Mission.Guard:
                    {
                        break;
                    }
                case Mission.AttackMove:
                    {
                        MissionAttackMove();
                        break;
                    }
                case Mission.Hunt:
                    {
                        break;
                    }
                default:
                    break;
            }
        }

        public delegate bool TechnoValidator(Pointer<TechnoClass> techno);
        public delegate bool AbstractValidator<T>(Pointer<T> abs);
        public static (KeepAliveAbility level, Pointer<TechnoClass> techno) GetMostImporantantTarget(Pointer<HouseClass> house, CoordStruct currentCrd, bool nearest)
        {
            Pointer<TechnoClass> findNearestInDVC(DynamicVectorClass<Pointer<TechnoClass>> dvc, TechnoValidator validator)
            {
                var nearest = Pointer<TechnoClass>.Zero;
                double nearestDist = double.MaxValue;

                foreach (var techno in dvc)
                {
                    if (validator != null ? !validator(techno) : false)
                        continue;

                    var dist = techno.Ref.BaseAbstract.GetCoords().DistanceFrom(currentCrd);
                    if (dist < nearestDist)
                    {
                        nearest = techno.Convert<TechnoClass>();
                        nearestDist = dist;
                    }
                }
                return nearest;
            }
            Pointer<TechnoClass> selectRandomInDVC(DynamicVectorClass<Pointer<TechnoClass>> dvc, TechnoValidator validator)
            {
                if (validator != null)
                {
                    List<Pointer<TechnoClass>> validTechnos = new List<Pointer<TechnoClass>>();
                    foreach (var techno in dvc)
                    {
                        if (validator(techno))
                            validTechnos.Add(techno);
                    }
                    return ScenarioClass.GetRandomInList(validTechnos);
                }
                else
                {
                    return ScenarioClass.GetRandomInDVC(dvc);
                }
            }

            if (house.Ref.ConYards.Count > 0)
            {
                var dvc = Pointer<byte>.AsPointer(ref house.Ref.conyards).Convert<DynamicVectorClass<Pointer<TechnoClass>>>().Ref;
                if (!nearest)
                    return (KeepAliveAbility.ConYard, selectRandomInDVC(dvc, null));
                else
                    return (KeepAliveAbility.ConYard, findNearestInDVC(dvc, null));
            }

            if (house.Ref.NumAirpads + house.Ref.NumBarracks + house.Ref.NumShipyards + house.Ref.NumWarFactories + house.Ref.NumConYards > 0)
            {
                bool factoryValidator(Pointer<TechnoClass> techno)
                {
                    var factoryType = techno.Convert<BuildingClass>().Ref.Type.Ref.Factory;
                    if (factoryType == AbstractType.None)
                        return false;

                    if (factoryType == AbstractType.BuildingType
                    || factoryType == AbstractType.AircraftType
                    || factoryType == AbstractType.UnitType
                    || factoryType == AbstractType.InfantryType)
                    {
                        return true;
                    }

                    return false;
                }
                var dvc = Pointer<byte>.AsPointer(ref house.Ref.buildings).Convert<DynamicVectorClass<Pointer<TechnoClass>>>().Ref;
                if (!nearest)
                    return (KeepAliveAbility.Factory, selectRandomInDVC(dvc, factoryValidator));
                else
                    return (KeepAliveAbility.Factory, findNearestInDVC(dvc, factoryValidator));
            }

            if (house.Ref.OwningBuilding())
            {
                bool buildingValidator(Pointer<TechnoClass> techno)
                {
                    var type = techno.Ref.GetTechnoType();
                    return !type.Ref.Base.Insignificant && !type.Ref.DontScore;
                }
                var dvc = Pointer<byte>.AsPointer(ref house.Ref.buildings).Convert<DynamicVectorClass<Pointer<TechnoClass>>>().Ref;
                if (!nearest)
                    return (KeepAliveAbility.Building, selectRandomInDVC(dvc, buildingValidator));
                else
                    return (KeepAliveAbility.Building, findNearestInDVC(dvc, buildingValidator));
            }

            if (house.Ref.OwningTechno())
            {
                bool keepAliveTechnoValidator(Pointer<TechnoClass> techno)
                {
                    if (techno.Ref.BaseAbstract.GetOwningHouse() != house)
                        return false;

                    if (TechnoTypeExt.ExtMap.Find(techno.Ref.GetTechnoType()).KeepAlive)
                        return true;

                    return false;
                }
                var dvc = DynamicVectorClass<Pointer<TechnoClass>>.GetDynamicVector(FootClass.ArrayPointer);
                var result = nearest ? findNearestInDVC(dvc, keepAliveTechnoValidator) : selectRandomInDVC(dvc, keepAliveTechnoValidator);
                if (result.IsNotNull)
                    return (KeepAliveAbility.Building, result);

                bool simpleTechnoValidator(Pointer<TechnoClass> techno)
                {
                    if (techno.Ref.BaseAbstract.GetOwningHouse() != house)
                        return false;

                    var type = techno.Ref.GetTechnoType();
                    if (type.Ref.DontScore || type.Ref.Base.Insignificant)
                        return false;

                    return true;
                }
                result = nearest ? findNearestInDVC(dvc, simpleTechnoValidator) : selectRandomInDVC(dvc, simpleTechnoValidator);
                if (result.IsNotNull)
                    return (KeepAliveAbility.Foot, result);
            }

            return (KeepAliveAbility.Insignificant, Pointer<TechnoClass>.Zero);
        }
        private void ProcessScript(AttackWaveScriptNode node)
        {
            var currentCrd = CurrentCrd;

            switch (node.Action)
            {
                case AttackWaveScriptAction.AttackRandomPlayerBase:
                    {
                        // 没有目标作战方，说明脚本刚开始执行（若执行完毕会同时切换脚本）
                        if (TargetHouse.IsNull) 
                        {
                            // 找个作战方作为目标
                            List<Pointer<HouseClass>> players = new List<Pointer<HouseClass>>();
                            foreach (var house in HouseClass.Array)
                            {
                                if (house.Ref.ControlledByPlayer() && !house.Ref.Defeated)
                                    players.Add(house);
                            }
                            var targetHouse = ScenarioClass.GetRandomInList(players);
                            TargetHouse = new SwizzleablePointer<HouseClass>(targetHouse);
                        }

                        // 有目标作战方，则更新目标
                        if (!TargetHouse.IsNull)
                        {
                            if (ScriptFocus.IsNull) // 没有目标，则重选目标
                            {
                                var target = GetMostImporantantTarget(TargetHouse, currentCrd, false);
                                if (target.level > KeepAliveAbility.Foot && target.techno.IsNotNull) // 有新目标，则设置目标并结束
                                {
                                    ScriptFocus = target.techno.Convert<AbstractClass>();
                                }
                                else // 目标无效，结束脚本
                                {
                                    IsReadyForNextScript = true;
                                }
                            }
                        }
                        else // 作战方无效，结束脚本
                        {
                            IsReadyForNextScript = true;
                        }

                        if (!IsReadyForNextScript && Mission != Mission.AttackMove)
                            Mission = Mission.AttackMove;
                        break;
                    }
                case AttackWaveScriptAction.AttackNearestPlayerBase:
                    {
                        // 没有目标作战方，说明脚本刚开始执行（若执行完毕会同时切换脚本）
                        if (TargetHouse.IsNull)
                        {
                            // 找个作战方作为目标
                            static IComparable calculateDist(Pointer<HouseClass> house, CoordStruct crd)
                            {
                                var baseCenter = house.Ref.GetBaseCenter();
                                return baseCenter.DistanceFrom(CellClass.Coord2Cell(crd));
                            }
                            static bool houseValidator(Pointer<HouseClass> house)
                            {
                                return house.Ref.ControlledByHuman() && !house.Ref.Defeated;
                            }
                            var nearest = MiscHelpers.FindNearest<Pointer<HouseClass>, CoordStruct>(HouseClass.Array, currentCrd, calculateDist, 1, houseValidator);
                            TargetHouse = new SwizzleablePointer<HouseClass>(nearest.FirstOrDefault());
                        }

                        // 有目标作战方，则更新目标
                        if (!TargetHouse.IsNull)
                        {
                            if (ScriptFocus.IsNull) // 没有目标，则重选目标
                            {
                                var target = GetMostImporantantTarget(TargetHouse, currentCrd, false);
                                if (target.level > KeepAliveAbility.Foot && target.techno.IsNotNull) // 有新目标，则设置目标并结束
                                {
                                    ScriptFocus = target.techno.Convert<AbstractClass>();
                                }
                                else // 目标无效，结束脚本
                                {
                                    IsReadyForNextScript = true;
                                }
                            }
                        }
                        else // 作战方无效，结束脚本
                        {
                            IsReadyForNextScript = true;
                        }

                        if (!IsReadyForNextScript && Mission != Mission.AttackMove)
                            Mission = Mission.AttackMove;
                        break;
                    }
                case AttackWaveScriptAction.GuardNearestAIBase:
                    {
                        break;
                    }
                case AttackWaveScriptAction.GoHunting:
                    {
                        if (Mission != Mission.Hunt)
                        {
                            Mission = Mission.Hunt;
                            ScriptFocus = Pointer<AbstractClass>.Zero;
                        }
                        break;
                    }
                case AttackWaveScriptAction.GuardCurrentPosition:
                    {
                        break;
                    }
                case AttackWaveScriptAction.Retreat:
                    {
                        if (Mission != Mission.Retreat)
                        {
                            Mission = Mission.Retreat;
                            MissionFocus = Pointer<AbstractClass>.Zero;
                            ScriptFocus = Pointer<AbstractClass>.Zero;
                        }
                        break;
                    }
                default:
                    break;
            }
        }
        private void UpdateScript()
        {
            if (IsReadyForNextScript)
            {
                CurrentScriptIdx++;
                ScriptFocus = Pointer<AbstractClass>.Zero;
                IsReadyForNextScript = false;
            }

            if (CurrentScriptIdx >= 0 && CurrentScriptIdx < Script.Count)
            {
                ProcessScript(Script[CurrentScriptIdx]);
            }
        }
        // 周期更新，不是每帧
        public void Update()
        {
            // 检查成员存活状态
            UpdateMemberList();
            
            if (IsAlive)
            {
                // 更新脚本
                UpdateScript();

                // 更新任务
                UpdateMission();
            }
        }

        public bool ShouldDeleteNow()
        {
            return !IsAlive;
        }
        public void AddMembers(List<Pointer<TechnoClass>> members)
        {
            foreach (var member in members)
                _Members.Add(TechnoExt.ExtMap.Find(member));
        }

        // 出生
        CoordStruct SpawnCrd;
        int SideIdx = -1;

        // 当前行动，由脚本设置，由脚本检查单位状态清除，清除则表示当前阶段执行完毕
        ExtensionReference<TechnoExt> _MegaTarget; // 阶段性目标，由脚本推导出
        SwizzleablePointer<CellClass> _MegaDestination; // 阶段性目的地
        Pointer<AbstractClass> ScriptFocus
        {
            get
            {
                if (_MegaTarget.TryGet(out var megaTargetExt))
                {
                    var megaTarget = megaTargetExt.OwnerObject;
                    return megaTarget.Convert<AbstractClass>();
                }

                if (!_MegaDestination.IsNull)
                    return _MegaDestination.Pointer.Convert<AbstractClass>();

                return Pointer<AbstractClass>.Zero;
            }
            set
            {
                if (value.IsNull)
                {
                    _MegaTarget = new ExtensionReference<TechnoExt>();
                    _MegaDestination = new SwizzleablePointer<CellClass>(Pointer<CellClass>.Zero);
                }
                else
                {
                    switch (value.Ref.WhatAmI())
                    {
                        case AbstractType.Cell:
                            _MegaTarget = new ExtensionReference<TechnoExt>();
                            _MegaDestination = new SwizzleablePointer<CellClass>(value.Convert<CellClass>());
                            return;
                        case AbstractType.Building:
                        case AbstractType.Infantry:
                        case AbstractType.Unit:
                        case AbstractType.Aircraft:
                            _MegaTarget.Set(TechnoExt.ExtMap.Find(value.Convert<TechnoClass>()));
                            _MegaDestination = new SwizzleablePointer<CellClass>(Pointer<CellClass>.Zero);
                            return;
                        default:
                            _MegaTarget = new ExtensionReference<TechnoExt>();
                            _MegaDestination = new SwizzleablePointer<CellClass>(Pointer<CellClass>.Zero);
                            Logger.Log("AttackWave set invalid target type: " + value.Ref.WhatAmI().ToString());
                            return;
                    }
                }
            }
        }
        int _TargetHouseIdx = -1;
        Pointer<HouseClass> TargetHouse
        {
            get
            {
                foreach (var house in HouseClass.Array)
                {
                    if (house.Ref.ArrayIndex == _TargetHouseIdx)
                        return house;
                }

                return Pointer<HouseClass>.Zero;
            }
            set
            {
                if (value.IsNotNull)
                    _TargetHouseIdx = value.Ref.ArrayIndex;
                else
                    _TargetHouseIdx = -1;
            }
        }
        CoordStruct CurrentCrd
        {
            get
            {
                var currentCrd = CoordStruct.Empty;
                foreach (var member in Members)
                {
                    currentCrd += member.Ref.BaseAbstract.GetCoords();
                }
                currentCrd /= _Members.Count;

                var pathFinder = PathFinder;

                var bestLocaion = CoordStruct.Empty;

                if (pathFinder.IsNotNull)
                {
                    var pathFinderMZLV = GetMovementRestrictionLevel(pathFinder.Ref.Base.GetTechnoType().Ref.MovementZone);
                    var bestDist = double.MaxValue;
                    foreach (var member in Members)
                    {
                        if (GetMovementRestrictionLevel(member.Ref.GetTechnoType().Ref.MovementZone) != pathFinderMZLV)
                            continue;

                        var currentLocation = member.Ref.BaseAbstract.GetCoords();
                        var currentDist = currentLocation.DistanceFrom(currentCrd);

                        if (currentDist < bestDist)
                        {
                            bestDist = currentDist;
                            bestLocaion = currentLocation;
                        }
                    }
                }

                return bestLocaion;
            }
        }
        int Stray
        {
            get
            {
                // stray = 5
                // 成员数量/9是估计的由于寻路所以单位无法靠近目标点的距离
                return (_Members.Count / 9) + 5;
            }
        }
        List<AttackWaveScriptNode> Script;
        bool IsReadyForNextScript; // 只有脚本和MissionConstruction设置
        int CurrentScriptIdx;

        // 当前阶段性行动，由任务设置，由脚本清除，清除则表示当前任务执行完毕
        ExtensionReference<TechnoExt> _Target; // 当前目标
        SwizzleablePointer<CellClass> _Destination; // 当前目的地
        Pointer<TechnoClass> Target
        {
            get
            {
                if (_Target.TryGet(out var targetExt))
                    return targetExt.OwnerObject;

                return Pointer<TechnoClass>.Zero;
            }
            set
            {
                _Target.Set(TechnoExt.ExtMap.Find(value));
            }
        }
        Pointer<CellClass> Destination
        {
            get { return _Destination.Pointer; }
            set { _Destination.Pointer = value; }
        }
        Pointer<AbstractClass> MissionFocus
        {
            get
            {
                if (_Target.TryGet(out var megaTargetExt))
                {
                    var target = megaTargetExt.OwnerObject;
                    return target.Convert<AbstractClass>();
                }

                if (!_Destination.IsNull)
                    return _Destination.Pointer.Convert<AbstractClass>();

                return Pointer<AbstractClass>.Zero;
            }
            set
            {
                if (value.IsNull)
                {
                    _Target.Set(null);
                    _Destination = new SwizzleablePointer<CellClass>(Pointer<CellClass>.Zero);
                }
                else
                {
                    switch (value.Ref.WhatAmI())
                    {
                        case AbstractType.Cell:
                            _Target.Set(null);
                            _Destination = new SwizzleablePointer<CellClass>(value.Convert<CellClass>());
                            return;
                        case AbstractType.Building:
                        case AbstractType.Infantry:
                        case AbstractType.Unit:
                        case AbstractType.Aircraft:
                            _Target.Set(TechnoExt.ExtMap.Find(value.Convert<TechnoClass>()));
                            _Destination = new SwizzleablePointer<CellClass>(Pointer<CellClass>.Zero);
                            return;
                        default:
                            _Target.Set(null);
                            _Destination = new SwizzleablePointer<CellClass>(Pointer<CellClass>.Zero);
                            Logger.Log("AttackWave set invalid focus type: " + value.Ref.WhatAmI().ToString());
                            return;
                    }
                }
            }
        }
        Mission _Mission = Mission.None;
        Mission Mission
        {
            get
            { 
                return _Mission;
            }
            set
            {
                switch (value)
                {
                    case Mission.None: // 无任务
                    case Mission.Construction: // 正在刷出
                    case Mission.Retreat: // 执行完毕，自刎归天
                    case Mission.Attack: // 进攻指定目标，目标则回到警戒状态
                    case Mission.Move: // 移动，移动但不索敌
                    case Mission.Guard: // 警戒
                    case Mission.AttackMove: // 边前进边索敌，遇敌会临时切换目标
                    case Mission.Hunt: // 进攻任意目标，直到自身死亡
                        _Mission = value;
                        break;
                    default:
                        Logger.Log("AttackWave set invalid mission: " + value.ToString());
                        _Mission = Mission.None;
                        break;
                }
            }
        }
        TimerStruct MissionTimer;
        Pointer<FootClass> PathFinder
        {
            get
            {
                var mostRestrictedTechnos = new List<Pointer<TechnoClass>>();
                var mostRestrictedLevel = MovementRestrictionLevel.Fly;

                foreach (var member in Members)
                {
                    var rtti = member.Ref.BaseAbstract.WhatAmI();

                    // 建筑不能寻路
                    if (rtti == AbstractType.Building)
                        continue;

                    var blocedMax = 0;
                    switch (rtti)
                    {
                        case AbstractType.Unit:
                            blocedMax = 5; break;
                        case AbstractType.Infantry:
                            blocedMax = 15; break;
                        default:
                            blocedMax = int.MaxValue; break;
                    }

                    // 周围人太多了，单位很可能被卡住寻不了路
                    if (member.Ref.Base.GetCell().Ref.BlockedNeighbours > blocedMax)
                        continue;

                    var level = GetMovementRestrictionLevel(member.Ref.GetTechnoType().Ref.MovementZone);

                    if (level > mostRestrictedLevel)
                    {
                        mostRestrictedTechnos = new List<Pointer<TechnoClass>>();
                        mostRestrictedLevel = level;
                    }

                    if (level == mostRestrictedLevel)
                    {
                        mostRestrictedTechnos.Add(member);
                    }
                }

                return ScenarioClass.GetRandomInList(mostRestrictedTechnos).Convert<FootClass>();
            }
        }
        
        // 生存
        List<ExtensionReference<TechnoExt>> _Members = new List<ExtensionReference<TechnoExt>>();
        List<Pointer<TechnoClass>> Members
        {
            get
            {
                var result = new List<Pointer<TechnoClass>>();
                foreach (var memberRef in _Members)
                {
                    if (memberRef.TryGet(out var memberExt))
                        result.Add(memberExt.OwnerObject);
                }
                return result;
            }
            set
            {
                _Members = new List<ExtensionReference<TechnoExt>>();
                foreach (var member in value)
                    _Members.Add(TechnoExt.ExtMap.Find(member));
            }
        }
        int _OwnerHouseIdx = -1;
        public Pointer<HouseClass> OwnerHouse
        {
            get
            {
                foreach (var house in HouseClass.Array)
                {
                    if (house.Ref.ArrayIndex == _OwnerHouseIdx)
                        return house;
                }

                return Pointer<HouseClass>.Zero;
            }
            set
            {
                if (value.IsNotNull)
                    _OwnerHouseIdx = value.Ref.ArrayIndex;
                else
                    _OwnerHouseIdx = -1;
            }
        }

        bool IsAlive = true; // 没有可作战单位时就设为false
        public List<int> HeroesFromTheStormIdx = new List<int>();

        public AttackWave(CoordStruct spawnCrd, List<AttackWaveScriptNode> script, List<Pointer<TechnoClass>> members, int sideIdx, bool needsConstruction = true)
        {
            SpawnCrd = spawnCrd;
            ScriptFocus = Pointer<AbstractClass>.Zero;
            MissionFocus = Pointer<AbstractClass>.Zero;
            Mission = needsConstruction ? Mission.Construction : Mission.None;
            MissionTimer.Stop();
            Script = script;
            CurrentScriptIdx = needsConstruction ? -1 : 0;
            IsReadyForNextScript = !needsConstruction;
            SideIdx = sideIdx;
            Members = members;
            OwnerHouse = members.FirstOrDefault().Ref.BaseAbstract.GetOwningHouse();
            Array.Add(this);
        }
    }
}
