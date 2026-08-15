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

namespace Extension.Mutators
{
    [Serializable]
    public class LavaBurst : TargetCellMutator
    {
        // Mutator
        public override string UIName => "岩浆爆发";
        public override string Description => "岩浆会周期性地在随机位置从地下喷发，并对玩家的空中和地面单位造成伤害。";
        public override bool IsAvailableInRPG => true;
        public override int Score => 3;
        public LavaBurst(Pointer<HouseClass> owner) : base(owner) { }

        public override bool Update()
        {
            if (!base.Update())
                return false;

            counter++;
            counter %= TimeToFrame(0, 0.5);
            if (counter == 0)
            {
                for (int i = 0; i != 5; i++)
                {
                    var cell = TargetCellMutator.SelectRandomCellOutOfSafeZone();
                    // 不刷在建筑中心1格范围内
                    var centerBuilding = cell.Ref.GetBuilding();
                    if (centerBuilding.IsNotNull && IsOnTheirSide(centerBuilding.Ref.BaseAbstract.GetOwningHouse()) && centerBuilding.Ref.BaseAbstract.DistanceFrom(cell.Convert<AbstractClass>()) <= 256)
                        continue;
                    bool hasBuildingNearBy = false;
                    for (int j = 0; j != 8; j++)
                    {
                        var neighbour = cell.Ref.GetNeighbourCell((Direction)j);
                        var building = neighbour.Ref.GetBuilding();
                        if (building.IsNotNull && IsOnTheirSide(building.Ref.BaseAbstract.GetOwningHouse()) && building.Ref.BaseAbstract.DistanceFrom(cell.Convert<AbstractClass>()) <= 256)
                        {
                            hasBuildingNearBy = true;
                            break;
                        }
                    }
                    if (!hasBuildingNearBy)
                    {
                        var scenarioExt = ScenarioExt.Global();
                        scenarioExt.CreateDecorator<LavaBurstShot>(
                            scenarioExt.FetchScenarioDecoratorID,
                            "LavaBurstShot",
                            cell.Ref.MapCoords,
                            this);
                        break;
                    }
                }
            }
            return true;
        }

        // LavaBurst
        private int counter = 0;

        [Serializable]
        private class LavaBurstShot : EventRenderDecorator
        {
            // 常量
            private static int LaunchDelay => Mutator.TimeToFrame(0, 4);      // 准备时间：4秒
            private static int RemoveDelay => Mutator.TimeToFrame(0, 20);     // 总持续时间：20秒
            private static int FireInterval => Mutator.TimeToFrame(0, 0.125); // 射击间隔：0.125秒
            private static int AnimInterval => Mutator.TimeToFrame(0, 0.7);   // 动画间隔
            private static int RingInterval => Mutator.TimeToFrame(0, 1);     // 增加内圈间隔：1秒
            private static readonly float MaxRadius = 30f;                    // 最大半径（最外圈）
            private static readonly float RadiusStep = 6f;                    // 圈间距
            private static readonly int GeyserHeight = 600;                   // 喷发动画高度（leptons）
            private static readonly ColorStruct IndicatorColor = new ColorStruct(255, 165, 0);
            private const string EruptSoundName = "BlastBurn";                // 喷发音效名

            private static Pointer<WeaponTypeClass> LavaWeapon =>
                WeaponTypeClass.ABSTRACTTYPE_ARRAY.Find("MutLava");
            private static Pointer<AnimTypeClass> GeyserAnimType =>
                AnimTypeClass.ABSTRACTTYPE_ARRAY.Find("MutLavaAnim");
            private static Pointer<AnimTypeClass> GroundAnimType =>
                AnimTypeClass.ABSTRACTTYPE_ARRAY.Find("PBURNEXP");

            private CellStruct targetCell;
            private LavaBurst myMutator;
            private int timer = 0;
            private int fireCounter = 0;
            private int animCounter = 0;

            public LavaBurstShot(CellStruct targetCell, LavaBurst myMutator)
            {
                this.targetCell = targetCell;
                this.myMutator = myMutator;
            }

            public override void OnUpdate()
            {
                // 准备时间结束、进入喷发阶段的第一帧：播放音效
                if (timer == LaunchDelay)
                {
                    var targetCoord = MapClass.Instance.GetCellAt(targetCell).Ref.Base.GetCoords();
                    int soundIndex = VocClass.FindIndex(EruptSoundName);
                    if (soundIndex != -1)
                        VocClass.PlayAt(soundIndex, targetCoord);
                }

                // 持续射击阶段
                if (timer >= LaunchDelay)
                {
                    // 每 FireInterval 帧引爆一次
                    fireCounter++;
                    if (fireCounter >= FireInterval)
                    {
                        fireCounter = 0;
                        FireLava();
                    }

                    // 每 AnimInterval 帧创建一次动画
                    animCounter++;
                    if (animCounter >= AnimInterval)
                    {
                        animCounter = 0;
                        SpawnAnims();
                    }
                }

                // 总持续时间结束后移除
                if (timer >= RemoveDelay)
                {
                    Decorative?.Remove(this);
                    return;
                }

                timer++;
            }

            public override void OnRender()
            {
                var targetCoord = MapClass.Instance.GetCellAt(targetCell).Ref.Base.GetCoords();

                if (timer < LaunchDelay)
                {
                    // 准备阶段：从最外圈开始，随时间推移不断向内增加圈
                    int ringCount = RingInterval > 0 ? (timer / RingInterval + 1) : 1;
                    for (int i = 0; i < ringCount; i++)
                    {
                        float radius = MaxRadius - i * RadiusStep;
                        if (radius <= 0.0f)
                            break;
                        TacticalClass.DrawRadialIndicator(false, false, targetCoord, IndicatorColor, radius, true);
                    }
                }
                //else
                //{
                //    // 持续射击阶段：始终绘制所有圈
                //    for (float radius = MaxRadius; radius > 0.0f; radius -= RadiusStep)
                //    {
                //        TacticalClass.DrawRadialIndicator(false, false, targetCoord, IndicatorColor, radius, true);
                //    }
                //}
            }

            private void SpawnAnims()
            {
                var targetCoord = MapClass.Instance.GetCellAt(targetCell).Ref.Base.GetCoords();

                // 空中动画：目标格子上方 GeyserHeight 高度
                var geyserAnimType = GeyserAnimType;
                if (geyserAnimType.IsNotNull)
                {
                    var airCoord = new CoordStruct(targetCoord.X, targetCoord.Y, targetCoord.Z + GeyserHeight);
                    YRMemory.Create<AnimClass>(geyserAnimType, airCoord);
                }

                // 地面动画：目标格子地面
                var groundAnimType = GroundAnimType;
                if (groundAnimType.IsNotNull)
                {
                    YRMemory.Create<AnimClass>(groundAnimType, targetCoord);
                }
            }

            private void FireLava()
            {
                var weapon = LavaWeapon;
                if (weapon.IsNull)
                {
                    Logger.Log("LavaBurstShot: MutLava weapon not found!");
                    return;
                }

                var firer = myMutator.GetRandomHouseOnOurSide();
                if (firer.IsNull)
                {
                    Logger.Log("LavaBurstShot: No valid house to fire!");
                    return;
                }

                var pTargetCell = MapClass.Instance.GetCellAt(targetCell);
                var targetCoord = pTargetCell.Ref.Base.GetCoords();
                WeaponTypeExt.Detonate(weapon, targetCoord, pTargetCell.Convert<AbstractClass>(), Pointer<TechnoClass>.Zero, firer);
            }
        }
    }
}
