using System;
using System.Collections.Generic;
using System.Linq;
using Extension.Utilities;
using Extension.AttackWave;
using PatcherYRpp;
using DynamicPatcher;

namespace Extension.Mutators
{
    [Serializable]
    public class AggressiveDeployment : TargetTechnoMutator
    {
        public override string UIName => "进攻部署";
        public override string Description => "周期性地将额外的敌方单位部署到战场上。";
        public override bool IsAvailableInRPG => false;
        public override int Score => 3;

        public AggressiveDeployment(Pointer<HouseClass> owner) : base(owner) { }

        private int deployedCount = 0;
        private int deploymentTimer = 0;
        private static readonly int DEPLOYMENT_INTERVAL = TimeToFrame(2, 8); // 部署间隔：2分8秒
        private static readonly int FIRST_DEPLOYMENT_TIME = TimeToFrame(4, 50); // 首次部署时间：4分50秒

        public override bool Update()
        {
            if (!base.Update())
                return false;

            // 递增计时器
            deploymentTimer++;

            // 在首次部署时间前不执行
            if (Game.CurrentFrame < FIRST_DEPLOYMENT_TIME)
                return true;

            // 检查是否应该执行部署
            if (deploymentTimer >= DEPLOYMENT_INTERVAL)
            {
                // 执行部署
                ExecuteDeployment();
                deployedCount++;
                deploymentTimer = 0; // 重置计时器
            }

            return true;
        }

        private void ExecuteDeployment()
        {
            // 遍历所有敌对玩家
            foreach (var playerHouse in HouseClass.Array)
            {
                // 排除自己人
                if (!IsOnTheirSide(playerHouse))
                    continue;

                // 检查该玩家是否被击败
                if (playerHouse.Ref.Defeated)
                    continue;

                // 检查该玩家是否有单位或建筑
                if (!playerHouse.Ref.OwningTechno())
                    continue;

                // 检查该玩家是否有保持存活的建筑（即值得部署针对的）
                bool hasKeepAlive = false;
                foreach (var building in playerHouse.Ref.Buildings)
                {
                    if (TargetTechnoMutator.GetKeepAliveAbility(building.Convert<TechnoClass>()) > 0)
                    {
                        hasKeepAlive = true;
                        break;
                    }
                }
                if (!hasKeepAlive)
                    continue;

                // 对该玩家执行一次部署
                DeployForPlayer(playerHouse);
            }
        }

        private void DeployForPlayer(Pointer<HouseClass> targetPlayer)
        {
            // 获取部署的脚本
            var script = AttackWave.AttackWave.AttackWaveScriptNode.DefaultScript();

            // 获取部署的单位
            var spawner = GetRandomHouseOnOurSide();
            if (spawner.IsNull)
                return;

            // 确定部署位置和单位强度
            CoordStruct deployLocation = CoordStruct.Empty;
            int techLevel = GetTechLevel();
            int amountLevel = GetAmountLevel();

            // 搜索符合条件的部署目标单位
            var validTargetUnits = FindValidTargetUnits(targetPlayer);

            if (validTargetUnits.Count > 0)
            {
                // 选择一个目标单位，并尝试在其周围7格部署
                List<CoordStruct> validDeployLocations = new List<CoordStruct>();
                
                // 遍历所有有效目标单位，收集所有合适的部署位置
                foreach (var targetUnit in validTargetUnits)
                {
                    var location = FindDeployLocationNearTarget(targetUnit, targetPlayer);
                    
                    // 找到合适的部署位置则记录
                    if (location != CoordStruct.Empty)
                        validDeployLocations.Add(location);
                }

                deployLocation = ScenarioClass.GetRandomInList(validDeployLocations);
            }

            if (deployLocation == CoordStruct.Empty)
            {
                // 无符合条件的目标单位，在出生点附近部署
                deployLocation = GetFallbackDeployLocation(targetPlayer);
            }

            // 创建攻击波次
            if (deployLocation != CoordStruct.Empty)
            {
                var type = AttackWaveManager.Instance.CurrentType;
                if (type != null)
                {
                    type.SpawnAt(spawner, script, 1, 1.0, deployLocation, techLevel, amountLevel);
                }
            }
            else
            {
                Logger.Log("无法为玩家 {0} 部署攻击波次，因为没有合适的部署位置。", targetPlayer.Ref.ArrayIndex);
            }
        }

        private List<Pointer<TechnoClass>> FindValidTargetUnits(Pointer<HouseClass> targetPlayer)
        {
            var validUnits = new List<Pointer<TechnoClass>>();

            foreach (var techno in TechnoClass.Array)
            {
                if (techno.Ref.BaseAbstract.GetOwningHouse() != targetPlayer)
                    continue;

                if (!IsTechnoValid(techno))
                    continue;

                validUnits.Add(techno);
            }

            return validUnits;
        }

        protected override bool IsTechnoValid(Pointer<TechnoClass> techno)
        {
            // 不能是建筑
            if (techno.Ref.Base.Base.WhatAmI() == AbstractType.Building)
                return false;

            // 不能在Limbo或不在可见范围
            if (techno.Ref.Base.InLimbo || !techno.Ref.IsInPlayfield)
                return false;

            // 必须有武器
            if (!techno.Ref.IsArmed())
                return false;

            // 不能在出生点15码内
            var ownerHouse = techno.Ref.BaseAbstract.GetOwningHouse();
            var baseCenter = ownerHouse.Ref.GetBaseCenter();

            if (baseCenter != CellStruct.Empty)
            {
                var spawnLocation = CellClass.Cell2Coord(baseCenter);
                var technoLocation = techno.Ref.BaseAbstract.GetCoords();

                if (spawnLocation.DistanceFrom(technoLocation) <= 15 * 256) // 15格 = 15*256 Leptons
                    return false;
            }

            return true;
        }

        private CoordStruct FindDeployLocationNearTarget(Pointer<TechnoClass> targetUnit, Pointer<HouseClass> targetPlayer)
        {
            var targetCrd = targetUnit.Ref.BaseAbstract.GetCoords();
            var targetMapCrd = CellClass.Coord2Cell(targetCrd);

            // 尝试在目标7格范围内找到符合连通条件的位置
            var baseCenterCell = targetPlayer.Ref.GetBaseCenter();

            // 使用CellSpreadEnumerator遍历目标周围的格子
            var enumerator = new CellSpreadEnumerator(7);
            
            foreach (var offset in enumerator)
            {
                var candidateMapCrd = offset + targetMapCrd;

                // 检查该位置是否在地图范围内
                if (!MapClass.Instance.IsWithinUsableArea(candidateMapCrd, true))
                    continue;

                var candidateCell = MapClass.Instance.GetCellAt(candidateMapCrd);
                var candidateCrd = candidateCell.Ref.Base.GetCoords();

                // 检查地面连通性
                if (baseCenterCell != CellStruct.Empty
                    && !MapClass.IsInSameZone(baseCenterCell, candidateMapCrd, MovementZone.Infantry, false, false, false))
                    continue;

                // 找到合适位置
                return candidateCrd;
            }

            return CoordStruct.Empty;
        }

        private CoordStruct GetFallbackDeployLocation(Pointer<HouseClass> targetPlayer)
        {
            // 在目标玩家出生点25-40码范围内随机部署
            var baseCenterCell = targetPlayer.Ref.GetBaseCenter();
            var spawnCrd = CellClass.Cell2Coord(baseCenterCell);

            // 生成随机距离 25-40格 = 6400-10240 Leptons
            var randomDistance = ScenarioClass.Instance.Random.RandomRanged(6400, 10240);
            
            // 生成随机方向和位置，尝试40次找到有效位置
            const int MAX_ATTEMPTS = 40;
            for (int attempt = 0; attempt < MAX_ATTEMPTS; attempt++)
            {
                var randomAngle = ScenarioClass.Instance.Random.RandomRanged(0, 255);
                var radians = (randomAngle / 256.0) * Math.PI * 2;
                
                var offsetX = (int)(randomDistance * Math.Cos(radians));
                var offsetY = (int)(randomDistance * Math.Sin(radians));

                var deployLocation = new CoordStruct(
                    spawnCrd.X + offsetX,
                    spawnCrd.Y + offsetY,
                    spawnCrd.Z
                );

                // 检查是否在地图范围内，如果是则返回
                if (MapClass.Instance.IsWithinUsableArea(deployLocation))
                    return deployLocation;
            }

            // 40次尝试都失败，返回空
            return CoordStruct.Empty;
        }

        private int GetTechLevel()
        {
            // 根据部署次数返回科技等级
            // 部署次数越多，科技等级越高
            return Math.Min(1 + deployedCount, 7);
        }

        private int GetAmountLevel()
        {
            // 根据部署次数返回数量等级
            // 部署次数越多，数量等级越高
            return Math.Min(1 + deployedCount, 7);
        }
    }
}
